using MapleClaude.Render;
using MapleClaude.UI;
using MapleClaude.Wz;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MapleClaude.UI.Game;

/// <summary>
/// Bottom status bar. Always visible. Shows HP/MP/EXP gauges and menu buttons.
/// WZ: <c>StatusBar3.img/mainBar/</c>
/// Buttons open their respective panels via callbacks.
/// </summary>
public sealed class StatusBar : GamePanel
{
    private readonly WzSprite? _barSprite;
    private readonly WzSprite? _hpGauge;
    private readonly WzSprite? _mpGauge;
    private readonly WzSprite? _expBar;
    private readonly BuiltInFont? _font;

    private readonly Button? _btMenu;
    private readonly Button? _btCharacter;
    private readonly Button? _btCommunity;
    private readonly Button? _btEvent;
    private readonly Button? _btCashShop;
    private readonly Button? _btQuit;
    private readonly List<Button> _allButtons = new();

    private float _hpPercent = 0f;
    private float _mpPercent = 0f;
    private float _expPercent = 0f;

    public Action? OnMenu { get; set; }
    public Action? OnCharacter { get; set; }
    public Action? OnCommunity { get; set; }
    public Action? OnCashShop { get; set; }
    public Action? OnQuit { get; set; }

    // Placeholder stats — will be set by packet handlers once wired
    public int Level { get; set; } = 1;
    public string CharName { get; set; } = string.Empty;
    public int Hp { get; set; } = 50;
    public int MaxHp { get; set; } = 50;
    public int Mp { get; set; } = 50;
    public int MaxMp { get; set; } = 50;
    public long Exp { get; set; } = 0;
    public long NextExp { get; set; } = 1;

    public StatusBar(WzTextureLoader loader, WzPackage? ui, BuiltInFont? font)
    {
        _font = font;
        IsVisible = true;
        Position = new Vector2(0, 480);

        var bar = ui?.GetItem("StatusBar3.img/mainBar") as WzProperty;
        var status = bar?.Get("status") ?? bar?.Get("status800");
        _barSprite = status is WzCanvas bc ? loader.Load(bc) : null;

        var gauge = ui?.GetItem("StatusBar3.img/gauge") as WzProperty;
        _hpGauge = LoadFromProp(loader, gauge, "hp/layer:0");
        _mpGauge = LoadFromProp(loader, gauge, "mp/layer:0");
        _expBar = LoadFromProp(loader, bar, "EXPBar/0");

        _btMenu = MakeButton(loader, ui, "StatusBar3.img/mainBar/menu/BT_MENU",
            () => OnMenu?.Invoke());
        _btCharacter = MakeButton(loader, ui, "StatusBar3.img/mainBar/menu/BT_CHARACTER",
            () => OnCharacter?.Invoke());
        _btCommunity = MakeButton(loader, ui, "StatusBar3.img/mainBar/menu/BT_COMMUNITY",
            () => OnCommunity?.Invoke());
        _btCashShop = MakeButton(loader, ui, "StatusBar3.img/mainBar/BT_CASHSHOP",
            () => OnCashShop?.Invoke());
        _btQuit = MakeButton(loader, ui, "StatusBar3.img/mainBar/setting/BT_QUIT",
            () => OnQuit?.Invoke());

        PositionButtons();
    }

    private void PositionButtons()
    {
        if (_btMenu != null) _btMenu.Position = new Vector2(520, 492);
        if (_btCharacter != null) _btCharacter.Position = new Vector2(560, 492);
        if (_btCommunity != null) _btCommunity.Position = new Vector2(600, 492);
        if (_btCashShop != null) _btCashShop.Position = new Vector2(700, 492);
        if (_btQuit != null) _btQuit.Position = new Vector2(760, 492);
    }

    public override void Update(GameTime gameTime)
    {
        if (MaxHp > 0) _hpPercent = (float)Hp / MaxHp;
        if (MaxMp > 0) _mpPercent = (float)Mp / MaxMp;
        if (NextExp > 0) _expPercent = (float)Exp / NextExp;
    }

    public override void Draw(SpriteBatch sb, Texture2D white)
    {
        if (!IsVisible) return;

        if (_barSprite != null)
        {
            _barSprite.Draw(sb, Position + new Vector2(400, 10));
        }
        else
        {
            // Fallback drawn bar
            sb.Draw(white, new Rectangle(0, 480, 800, 40), new Color(20, 20, 30));
            sb.Draw(white, new Rectangle(0, 479, 800, 1), new Color(80, 80, 100));
        }

        DrawGauge(sb, white, new Rectangle(128, 490, 140, 10), _hpPercent, new Color(200, 50, 50));
        DrawGauge(sb, white, new Rectangle(128, 504, 140, 10), _mpPercent, new Color(50, 80, 200));
        DrawGauge(sb, white, new Rectangle(4, 478, 400, 4), _expPercent, new Color(220, 180, 40));

        _font?.Draw(sb, $"Lv.{Level} {CharName}", new Vector2(6, 483), new Color(220, 200, 150));
        _font?.Draw(sb, $"{Hp}/{MaxHp}", new Vector2(130, 487), Color.White);
        _font?.Draw(sb, $"{Mp}/{MaxMp}", new Vector2(130, 501), new Color(150, 180, 255));

        foreach (var b in _allButtons) b.Draw(sb);
    }

    public override bool HandleMouseButton(int x, int y, bool down)
    {
        if (!IsVisible) return false;
        foreach (var b in _allButtons)
            if (b.HandleMouseButton(x, y, down)) return true;
        return false;
    }

    private static void DrawGauge(SpriteBatch sb, Texture2D white, Rectangle bounds, float pct, Color color)
    {
        sb.Draw(white, bounds, new Color(0, 0, 0, 120));
        if (pct > 0)
        {
            var filled = new Rectangle(bounds.X, bounds.Y, (int)(bounds.Width * Math.Clamp(pct, 0f, 1f)), bounds.Height);
            sb.Draw(white, filled, color);
        }
    }

    private static WzSprite? LoadFromProp(WzTextureLoader loader, WzProperty? root, string path)
    {
        if (root is null) return null;
        var parts = path.Split('/');
        WzNode? node = root;
        foreach (var p in parts)
        {
            if (node is WzProperty pr) node = pr.Get(p);
            else return null;
            if (node is null) return null;
        }
        return node is WzCanvas c ? loader.Load(c) : null;
    }

    private Button? MakeButton(WzTextureLoader loader, WzPackage? ui, string path, Action onClick)
    {
        try
        {
            var root = ui?.GetItem(path) as WzProperty;
            if (root is null) return null;
            var b = new Button(loader, root) { OnClick = onClick };
            _allButtons.Add(b);
            return b;
        }
        catch { return null; }
    }
}
