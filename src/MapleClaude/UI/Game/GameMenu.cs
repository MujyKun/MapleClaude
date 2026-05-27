using MapleClaude.Render;
using MapleClaude.Wz;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MapleClaude.UI.Game;

/// <summary>
/// Authentic v95 in-game system menu — <c>CUIGameMenu</c>, opened by <b>ESC</b> (and the status-bar
/// System button). Drawn from <c>UI.wz/UIWindow.img/GameMenu</c>: a 93×167 pre-rendered panel (the
/// "GAME MENU" title bar plus five baked rows) that fades in at the screen's <b>bottom-right</b>,
/// directly above the status bar — exactly where the v95 <c>CFadeWnd</c> (Origin_LB, ~35px above the
/// screen bottom) sits. Each row's <c>mouseOver</c> canvas is overlaid on the baked background row to
/// highlight the hovered/selected item (the authentic <c>ResetButtonState</c> behaviour).
///
/// Items (top→bottom): Change Channel, Change Skin, Game Option, System Option, Quit Game. (Keyboard
/// Setting and JoyPad are <i>not</i> in the v95 ESC menu — verified against the WZ background, which
/// shows exactly these five rows.) Modal: blocks field input while open; ESC / click-outside cancel;
/// Up/Down move the highlight with wraparound; Enter activates; activating an item closes the menu
/// then runs its action.
/// </summary>
public sealed class GameMenu : Overlay
{
    // Geometry measured from UIWindow.img/GameMenu/backgrnd (93×167): dark 1px row separators sit at
    // y = 49/75/101/127/153, so the five 25px rows start at y = 24 with a 26px pitch; each row button
    // is 81px wide, centred in the 93px panel (6px inset each side).
    private const int PanelW = 93, PanelH = 167;
    private const int ItemX = 6, ItemY0 = 24, ItemPitch = 26, ItemW = 81, ItemH = 25;
    // Screen anchor: bottom-right; the panel's bottom edge sits BottomMargin px above the screen
    // bottom (matches the v95 panel bottom at y=565 in 800×600). RightMargin 0 = flush to the right.
    private const int RightMargin = 0, BottomMargin = 35;
    private const float FadeSeconds = 0.18f;

    private static readonly string[] Nodes  = { "BtChannel", "BtSkin", "BtGameOpt", "BtSysOpt", "BtQuit" };
    private static readonly string[] Labels = { "Change Channel", "Change Skin", "Game Option", "System Option", "Quit Game" };

    private readonly WzSprite? _backgrnd;
    private readonly WzSprite?[] _hover;
    private readonly BuiltInFont? _font;

    private int _viewW = 800, _viewH = 600;
    private int _selected = -1;
    private float _alpha;
    private Point _lastMouse = new(-1, -1);

    // Item actions, wired by GameStage.
    public Action? OnChannel      { get; set; }
    public Action? OnSkin         { get; set; }
    public Action? OnGameOption   { get; set; }
    public Action? OnSystemOption { get; set; }
    public Action? OnQuit         { get; set; }

    public GameMenu(WzTextureLoader loader, WzPackage? ui, BuiltInFont? font = null)
    {
        _font = font;
        _hover = new WzSprite?[Nodes.Length];

        var menu = ui?.GetItem("UIWindow.img/GameMenu") as WzProperty;
        _backgrnd = menu?.Get("backgrnd") is WzCanvas bg ? loader.Load(bg) : null;
        for (var i = 0; i < Nodes.Length; i++)
            _hover[i] = LoadState(loader, menu?.Get(Nodes[i]) as WzProperty, "mouseOver");
    }

    private static WzSprite? LoadState(WzTextureLoader loader, WzProperty? root, string state)
        => (root?.Get(state) as WzProperty)?.Get("0") is WzCanvas c ? loader.Load(c) : null;

    private int PanelWidth  => _backgrnd?.Width  ?? PanelW;
    private int PanelHeight => _backgrnd?.Height ?? PanelH;

    /// <summary>Panel top-left in screen space (bottom-right anchored, just above the status bar).</summary>
    private Vector2 TopLeft => new(_viewW - PanelWidth - RightMargin, _viewH - BottomMargin - PanelHeight);

    private Rectangle PanelBounds { get { var p = TopLeft; return new Rectangle((int)p.X, (int)p.Y, PanelWidth, PanelHeight); } }

    private Rectangle ItemRect(int i)
    {
        var p = TopLeft;
        return new Rectangle((int)p.X + ItemX, (int)p.Y + ItemY0 + i * ItemPitch, ItemW, ItemH);
    }

    private int HitTest(int x, int y)
    {
        for (var i = 0; i < Nodes.Length; i++)
            if (ItemRect(i).Contains(x, y)) return i;
        return -1;
    }

    /// <summary>Show the menu, resetting the highlight and restarting the fade-in.</summary>
    public void Open()
    {
        IsVisible = true;
        _selected = -1;
        _alpha = 0f;
        _lastMouse = new Point(-1, -1);
    }

    private void Close()
    {
        IsVisible = false;
        _selected = -1;
    }

    /// <summary>Re-anchor for the active viewport (called at load and on resolution change).</summary>
    public void Relayout(int viewWidth, int viewHeight)
    {
        _viewW = viewWidth;
        _viewH = viewHeight;
    }

    public override void Update(GameTime gameTime)
    {
        if (!IsVisible) { _alpha = 0f; return; }

        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _alpha = Math.Min(1f, _alpha + dt / FadeSeconds);

        // Mouse hover follows the pointer; only override the keyboard highlight when the mouse moves
        // so arrow-key navigation isn't fought by a stationary cursor.
        var ms = Mouse.GetState();
        var pos = new Point(ms.X, ms.Y);
        if (pos != _lastMouse)
        {
            _selected = HitTest(ms.X, ms.Y);
            _lastMouse = pos;
        }
    }

    public override void Draw(SpriteBatch sb, Texture2D white)
    {
        if (!IsVisible) return;
        var p = TopLeft;
        var tint = Color.White * _alpha;

        if (_backgrnd != null)
        {
            _backgrnd.Draw(sb, p, tint);
            // Overlay the highlighted row's mouseOver art on top of the baked background row.
            if (_selected >= 0 && _hover[_selected] != null)
                _hover[_selected]!.Draw(sb, new Vector2(p.X + ItemX, p.Y + ItemY0 + _selected * ItemPitch), tint);
            return;
        }

        // Fallback when the WZ asset is unavailable: a drawn panel + text rows.
        sb.Draw(white, PanelBounds, new Color(20, 38, 56) * _alpha);
        DrawBorder(sb, white, PanelBounds, new Color(120, 170, 210) * _alpha);
        if (_font != null)
        {
            _font.Draw(sb, "GAME MENU", new Vector2(p.X + 8, p.Y + 4), Color.White * _alpha);
            for (var i = 0; i < Labels.Length; i++)
            {
                var r = ItemRect(i);
                if (i == _selected) sb.Draw(white, r, new Color(60, 120, 180) * _alpha);
                _font.Draw(sb, Labels[i], new Vector2(r.X + 4, r.Y + 4), Color.White * _alpha);
            }
        }
    }

    public override bool HandleMouseButton(int x, int y, bool down)
    {
        if (!IsVisible) return false;

        var hit = HitTest(x, y);
        _selected = hit;
        _lastMouse = new Point(x, y);

        if (down)
        {
            // A press anywhere outside the panel cancels (authentic click-outside-closes).
            if (!PanelBounds.Contains(x, y)) Close();
            return true;
        }

        // Release inside an item activates it.
        if (hit >= 0) Activate(hit);
        return true;
    }

    public override bool OnKeyPress(Keys key)
    {
        if (!IsVisible) return false;
        switch (key)
        {
            case Keys.Escape:
                Close();
                return true;
            case Keys.Up:
                _selected = _selected <= 0 ? Nodes.Length - 1 : _selected - 1;
                return true;
            case Keys.Down:
                _selected = _selected < 0 ? 0 : (_selected + 1) % Nodes.Length;
                return true;
            case Keys.Enter:
                if (_selected >= 0) Activate(_selected);
                return true;
        }
        return true; // modal: swallow every key while open
    }

    private void Activate(int i)
    {
        Close();
        switch (i)
        {
            case 0: OnChannel?.Invoke(); break;
            case 1: OnSkin?.Invoke(); break;
            case 2: OnGameOption?.Invoke(); break;
            case 3: OnSystemOption?.Invoke(); break;
            case 4: OnQuit?.Invoke(); break;
        }
    }

    private static void DrawBorder(SpriteBatch sb, Texture2D white, Rectangle r, Color c)
    {
        sb.Draw(white, new Rectangle(r.X, r.Y, r.Width, 1), c);
        sb.Draw(white, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), c);
        sb.Draw(white, new Rectangle(r.X, r.Y, 1, r.Height), c);
        sb.Draw(white, new Rectangle(r.Right - 1, r.Y, 1, r.Height), c);
    }
}
