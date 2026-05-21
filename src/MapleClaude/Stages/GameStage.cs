using MapleClaude.App;
using MapleClaude.Render;
using MapleClaude.UI;
using MapleClaude.UI.Game;
using MapleClaude.Wz;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MapleClaude.Stages;

/// <summary>
/// In-game stage. Placeholder map background until the map packet pipeline is wired.
/// Hosts all game UI panels: StatusBar, MiniMap, ChatBar, BuffList, and the toggle
/// panels (Equip/Item/Skill/Stats/Quest/KeyConfig/Option/CharInfo/NpcTalk/Shop/Notice).
/// Key bindings:
///   E = EquipInventory, I = ItemInventory, K = SkillBook, S = StatsInfo, Q = QuestLog
///   M = MiniMap toggle
/// </summary>
public sealed class GameStage : Stage
{
    private readonly ILogger<GameStage> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly WzPackage? _ui;
    private readonly WzPackage? _map;
    private readonly WzPackage? _sound;

    private WzTextureLoader? _loader;

    // Always-visible panels
    private StatusBar? _statusBar;
    private ChatBar? _chatBar;
    private MiniMap? _miniMap;
    private BuffList? _buffList;

    // Toggle panels
    private EquipInventory? _equip;
    private ItemInventory? _item;
    private SkillBook? _skill;
    private StatsInfo? _stats;
    private QuestLog? _quest;
    private KeyConfig? _keyConfig;
    private OptionMenu? _optionMenu;
    private CharInfo? _charInfo;

    // Modal panels
    private NpcTalk? _npcTalk;
    private Shop? _shop;
    private Notice? _notice;
    private QuitConfirmOverlay? _quitConfirm;

    // Ordered list for input dispatch (modal-first)
    private readonly List<GamePanel> _panels = new();

    public GameStage(
        ILogger<GameStage> logger,
        ILoggerFactory loggerFactory,
        WzPackage? ui,
        WzPackage? map,
        WzPackage? sound)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _ui = ui;
        _map = map;
        _sound = sound;
    }

    public override void OnEnter(MapleClaudeGame game)
    {
        base.OnEnter(game);
        _loader = new WzTextureLoader(GraphicsDevice);

        var font = Game.Font;

        _statusBar = new StatusBar(_loader, _ui, font);
        _chatBar = new ChatBar(_loader, _ui, font);
        _miniMap = new MiniMap(_loader, _ui, font);
        _buffList = new BuffList(_loader, _ui, font);

        _equip = new EquipInventory(_loader, _ui, font);
        _item = new ItemInventory(_loader, _ui, font);
        _skill = new SkillBook(_loader, _ui, font);
        _stats = new StatsInfo(_loader, _ui, font);
        _quest = new QuestLog(_loader, _ui, font);
        _keyConfig = new KeyConfig(_loader, _ui, font);
        _optionMenu = new OptionMenu(_loader, _ui, font);
        _charInfo = new CharInfo(_loader, _ui, font);

        _npcTalk = new NpcTalk(_loader, _ui, font);
        _shop = new Shop(_loader, _ui, font);
        _notice = new Notice(_loader, _ui, font);

        _quitConfirm = new QuitConfirmOverlay(_loader, _ui, font, new Vector2(400, 300));
        _quitConfirm.OnYes = () => Game.Exit();
        _quitConfirm.OnNo = () => _quitConfirm.IsVisible = false;

        // StatusBar button callbacks
        _statusBar.OnMenu = () => _optionMenu!.IsVisible = !_optionMenu.IsVisible;
        _statusBar.OnCharacter = () => _stats!.IsVisible = !_stats.IsVisible;
        _statusBar.OnQuit = () => _quitConfirm!.IsVisible = true;

        // Register all toggle panels in draw/input order (modals last so they receive input first)
        _panels.Add(_statusBar);
        _panels.Add(_chatBar);
        _panels.Add(_miniMap);
        _panels.Add(_buffList);
        _panels.Add(_equip);
        _panels.Add(_item);
        _panels.Add(_skill);
        _panels.Add(_stats);
        _panels.Add(_quest);
        _panels.Add(_keyConfig);
        _panels.Add(_optionMenu);
        _panels.Add(_charInfo);
        _panels.Add(_npcTalk);
        _panels.Add(_shop);
        _panels.Add(_notice);

        // Play in-game BGM placeholder (SFX channel leaves login music)
        Game.AudioPlayer.Stop();

        _logger.LogInformation("GameStage entered — {PanelCount} panels", _panels.Count);
    }

    public override void OnExit()
    {
        _loader?.Dispose();
        _loader = null;
        base.OnExit();
    }

    public override void Update(GameTime gameTime)
    {
        foreach (var p in _panels) p.Update(gameTime);
        _quitConfirm?.Update(gameTime);
    }

    public override void Draw(GameTime gameTime, SpriteBatch sb)
    {
        var pp = GraphicsDevice.PresentationParameters;
        var w = pp.BackBufferWidth;
        var h = pp.BackBufferHeight;

        // Placeholder: dark green map tint until map rendering is wired
        sb.Draw(Game.WhitePixel, new Rectangle(0, 0, w, h), new Color(30, 50, 30));

        // Draw panels back-to-front (modals on top)
        foreach (var p in _panels)
            p.Draw(sb, Game.WhitePixel);

        _quitConfirm?.Draw(sb, Game.WhitePixel);
    }

    public override void OnMouseButton(int x, int y, bool down, MouseButton button)
    {
        if (button != MouseButton.Left) return;

        // Quit confirm is topmost
        if (_quitConfirm?.IsVisible == true)
        {
            _quitConfirm.HandleMouseButton(x, y, down);
            return;
        }

        // Modal panels intercept first (notice, npcTalk, shop) — reverse order so top renders last
        for (var i = _panels.Count - 1; i >= 0; i--)
        {
            var p = _panels[i];
            if (p.IsVisible && p.HandleMouseButton(x, y, down))
                return;
        }
    }

    public override void OnTextInput(char ch)
    {
        _chatBar?.OnTextInput(ch);
    }

    public override void OnKeyPress(Keys key)
    {
        if (_quitConfirm?.IsVisible == true)
        {
            _quitConfirm.OnKeyPress(key);
            return;
        }

        // Let visible modal panels handle first
        foreach (var p in _panels)
        {
            if (p.IsVisible && p.OnKeyPress(key))
                return;
        }

        // Global key bindings
        switch (key)
        {
            case Keys.E:
                _equip!.IsVisible = !_equip.IsVisible;
                break;
            case Keys.I:
                _item!.IsVisible = !_item.IsVisible;
                break;
            case Keys.K:
                _skill!.IsVisible = !_skill.IsVisible;
                break;
            case Keys.S:
                _stats!.IsVisible = !_stats.IsVisible;
                break;
            case Keys.Q:
                _quest!.IsVisible = !_quest.IsVisible;
                break;
            case Keys.M:
                _miniMap!.IsVisible = !_miniMap.IsVisible;
                break;
            case Keys.Back:
                // Return to CharSelect (no-op for now — real game wouldn't allow this)
                _logger.LogInformation("GameStage: Back pressed (use quit confirm instead)");
                break;
        }
    }
}
