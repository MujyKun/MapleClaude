using MapleClaude.Render;
using MapleClaude.Wz;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MapleClaude.UI;

/// <summary>
/// Quit-confirmation modal. YES exits the app; NO dismisses.
/// WZ: <c>Login.img/QuitConfirm/</c> — falls back to drawn box if missing.
/// </summary>
public sealed class QuitConfirmOverlay : Overlay
{
    private readonly WzSprite? _background;
    private readonly Button? _btYes;
    private readonly Button? _btNo;
    private readonly BuiltInFont? _font;
    private Vector2 _center;
    private int _viewW = 800;
    private int _viewH = 600;

    public Action? OnYes { get; set; }
    public Action? OnNo { get; set; }

    public QuitConfirmOverlay(WzTextureLoader loader, WzPackage? ui, BuiltInFont? font, Vector2 center)
    {
        _font = font;
        _center = center;

        var quit = ui?.GetItem("Login.img/QuitConfirm") as WzProperty;
        _background = quit?.Get("backgrnd") is WzCanvas bg ? loader.Load(bg) : null;

        var yesRoot = quit?.Get("BtYes") as WzProperty;
        if (yesRoot != null)
        {
            _btYes = new Button(loader, yesRoot)
            {
                Position = center + new Vector2(-40, 36),
                OnClick = () => { IsVisible = false; OnYes?.Invoke(); },
            };
        }
        var noRoot = quit?.Get("BtNo") as WzProperty;
        if (noRoot != null)
        {
            _btNo = new Button(loader, noRoot)
            {
                Position = center + new Vector2(40, 36),
                OnClick = () => { IsVisible = false; OnNo?.Invoke(); },
            };
        }
    }

    /// <summary>
    /// Recentre the dialog (and resize the dim layer) for the active viewport.
    /// Called when the in-game window resolution changes.
    /// </summary>
    public void Relayout(int viewWidth, int viewHeight)
    {
        _viewW = viewWidth;
        _viewH = viewHeight;
        _center = new Vector2(viewWidth / 2f, viewHeight / 2f);
        if (_btYes != null) _btYes.Position = _center + new Vector2(-40, 36);
        if (_btNo  != null) _btNo.Position  = _center + new Vector2(40, 36);
    }

    public override void Draw(SpriteBatch sb, Texture2D white)
    {
        if (!IsVisible) return;

        // Dim the whole window.
        sb.Draw(white, new Rectangle(0, 0, _viewW, _viewH), new Color(0, 0, 0, 100));

        if (_background != null)
        {
            _background.Draw(sb, _center);
        }
        else
        {
            var rect = new Rectangle((int)_center.X - 140, (int)_center.Y - 55, 280, 110);
            sb.Draw(white, rect, new Color(20, 20, 30, 230));
            DrawBorder(sb, white, rect, new Color(100, 100, 140));
        }

        if (_font != null)
        {
            var msg = "Are you sure you want to quit?";
            var sz = _font.Measure(msg);
            _font.Draw(sb, msg, _center + new Vector2(-sz.X / 2f, -12), Color.White);
        }
        _btYes?.Draw(sb);
        _btNo?.Draw(sb);
    }

    public override bool HandleMouseButton(int x, int y, bool down)
    {
        if (!IsVisible) return false;
        if (_btYes?.HandleMouseButton(x, y, down) == true) return true;
        if (_btNo?.HandleMouseButton(x, y, down) == true) return true;
        return true;
    }

    public override bool OnKeyPress(Keys key)
    {
        if (!IsVisible) return false;
        if (key == Keys.Escape) { IsVisible = false; OnNo?.Invoke(); return true; }
        if (key == Keys.Enter) { IsVisible = false; OnYes?.Invoke(); return true; }
        return true;
    }

    private static void DrawBorder(SpriteBatch sb, Texture2D white, Rectangle rect, Color color)
    {
        sb.Draw(white, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
        sb.Draw(white, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color);
        sb.Draw(white, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
        sb.Draw(white, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color);
    }
}
