using System.Globalization;
using MapleClaude.Character;
using MapleClaude.Render;
using MapleClaude.Wz;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MapleClaude.Map;

/// <summary>
/// Extended in-game map renderer. Loads a full <c>...img</c> map blob
/// (info, back, layers 0..7, foothold, portal, life) and renders the
/// backdrops + tiles + objs every frame. Supports foothold-based
/// player placement and camera follow.
/// </summary>
public sealed class FieldScene
{
    private readonly ILogger<FieldScene> _logger;
    private readonly WzPackage _mapWz;
    private readonly WzTextureLoader _loader;
    private MapScene? _legacyScene;
    private readonly List<TileInfo> _tiles = new();
    private readonly Dictionary<int, Foothold> _footholds = new();
    private readonly Dictionary<int, Portal> _portals = new();
    private MapInfo _info = new();

    public Camera2D Camera { get; } = new();
    public MapInfo Info => _info;
    public IReadOnlyDictionary<int, Foothold> Footholds => _footholds;
    public IReadOnlyDictionary<int, Portal> Portals => _portals;

    public FieldScene(ILogger<FieldScene> logger, WzPackage mapWz, WzTextureLoader loader)
    {
        _logger = logger;
        _mapWz = mapWz;
        _loader = loader;
    }

    public void Load(int mapId)
    {
        var prefix = mapId / 100_000_000;
        var padded = mapId.ToString("D9", CultureInfo.InvariantCulture);
        var path = $"Map/Map{prefix}/{padded}.img";
        if (_mapWz.GetItem(path) is not WzImage img)
        {
            _logger.LogError("Map {Path} not found in Map.wz", path);
            return;
        }
        var root = img.Root;
        _legacyScene = new MapScene(_logger, _mapWz, _loader);
        try
        {
            _legacyScene.Load(root);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Legacy MapScene backdrop load failed");
        }
        LoadInfo(root);
        LoadFootholds(root);
        LoadPortals(root);
        _logger.LogInformation("FieldScene: map={Id} footholds={Fh} portals={P}",
            mapId, _footholds.Count, _portals.Count);
    }

    private void LoadInfo(WzProperty root)
    {
        if (root.Get("info") is not WzProperty info)
        {
            return;
        }
        var bgm = info.Get("bgm")?.ToString() ?? string.Empty;
        var returnMap = ReadInt(info, "returnMap");
        var forcedReturn = ReadInt(info, "forcedReturn");
        var fieldLimit = ReadInt(info, "fieldLimit");
        var mapDesc = info.Get("mapDesc")?.ToString() ?? string.Empty;
        var town = ReadInt(info, "town");
        _info = new MapInfo
        {
            Bgm = bgm,
            ReturnMap = returnMap,
            ForcedReturn = forcedReturn,
            FieldLimit = fieldLimit,
            MapDesc = mapDesc,
            Town = town,
            VRLeft = ReadInt(info, "VRLeft"),
            VRTop = ReadInt(info, "VRTop"),
            VRRight = ReadInt(info, "VRRight"),
            VRBottom = ReadInt(info, "VRBottom"),
        };
    }

    private void LoadFootholds(WzProperty root)
    {
        if (root.Get("foothold") is not WzProperty fhRoot)
        {
            return;
        }
        foreach (var (layerKey, layerNode) in fhRoot.Items)
        {
            if (layerNode is not WzProperty layer)
            {
                continue;
            }
            var layerIdx = int.TryParse(layerKey, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ln) ? ln : 0;
            foreach (var (groupKey, groupNode) in layer.Items)
            {
                if (groupNode is not WzProperty group)
                {
                    continue;
                }
                var groupIdx = int.TryParse(groupKey, NumberStyles.Integer, CultureInfo.InvariantCulture, out var gn) ? gn : 0;
                foreach (var (idStr, entryNode) in group.Items)
                {
                    if (entryNode is not WzProperty entry)
                    {
                        continue;
                    }
                    var id = int.TryParse(idStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
                    _footholds[id] = new Foothold
                    {
                        Id = id,
                        Layer = layerIdx,
                        Group = groupIdx,
                        X1 = ReadInt(entry, "x1"),
                        Y1 = ReadInt(entry, "y1"),
                        X2 = ReadInt(entry, "x2"),
                        Y2 = ReadInt(entry, "y2"),
                        Prev = ReadInt(entry, "prev"),
                        Next = ReadInt(entry, "next"),
                        Force = ReadInt(entry, "force"),
                        CantThrough = ReadInt(entry, "cantThrough") != 0,
                        ForbidFallDown = ReadInt(entry, "forbidFallDown") != 0,
                    };
                }
            }
        }
    }

    private void LoadPortals(WzProperty root)
    {
        if (root.Get("portal") is not WzProperty portalRoot)
        {
            return;
        }
        foreach (var (idxStr, node) in portalRoot.Items)
        {
            if (node is not WzProperty entry)
            {
                continue;
            }
            var idx = int.TryParse(idxStr, out var v) ? v : 0;
            _portals[idx] = new Portal
            {
                Index = idx,
                Name = entry.Get("pn")?.ToString() ?? string.Empty,
                Type = ReadInt(entry, "pt"),
                X = ReadInt(entry, "x"),
                Y = ReadInt(entry, "y"),
                TargetMap = ReadInt(entry, "tm"),
                TargetPortal = entry.Get("tn")?.ToString() ?? string.Empty,
                Delay = ReadInt(entry, "delay"),
                OnlyOnce = ReadInt(entry, "onlyOnce") != 0,
            };
        }
    }

    public void Draw(SpriteBatch sb, Texture2D whitePixel, int screenWidth, int screenHeight)
    {
        if (_legacyScene is null)
        {
            sb.Draw(whitePixel, new Rectangle(0, 0, screenWidth, screenHeight), new Color(8, 8, 20));
            return;
        }
        _legacyScene.Camera = Camera.Position - new Vector2(screenWidth / 2f, screenHeight / 2f);
        _legacyScene.Draw(sb, whitePixel, screenWidth, screenHeight);
    }

    public void PlacePlayerAtPortal(PlayerController player, byte portalIndex)
    {
        if (!_portals.TryGetValue(portalIndex, out var portal))
        {
            // Default portal index 0 if specific one missing.
            if (!_portals.TryGetValue(0, out portal))
            {
                _logger.LogWarning("No portals — spawning player at (0,0)");
                player.Position = Vector2.Zero;
                return;
            }
        }
        player.Position = portal.Position;
        Camera.Position = portal.Position;
        _logger.LogInformation("Player placed at portal {Idx} ({X},{Y})", portalIndex, portal.X, portal.Y);
    }

    private static int ReadInt(WzProperty p, string key)
    {
        var v = p.Get(key);
        return v switch
        {
            int i => i,
            short s => s,
            byte b => b,
            long l => (int)l,
            string s => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0,
            null => 0,
            _ => 0,
        };
    }
}
