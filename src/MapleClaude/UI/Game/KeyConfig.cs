using MapleClaude.Render;
using MapleClaude.UI;
using MapleClaude.Wz;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MapleClaude.UI.Game;

/// <summary>
/// Key-binding configuration panel. Opened from the settings menu button.
/// WZ: <c>UIWindow.img/KeyConfig/</c>
/// </summary>
public sealed class KeyConfig : GamePanel
{
    private readonly WzSprite? _background;
    private readonly Button? _btClose;
    private readonly Button? _btDefault;
    private readonly Button? _btOk;
    private readonly BuiltInFont? _font;
    private readonly List<Button> _allButtons = new();

    public KeyConfig(WzTextureLoader loader, WzPackage? ui, BuiltInFont? font)
    {
        _font = font;
        IsVisible = false;
        Position = new Vector2(100, 50);

        var kc = ui?.GetItem("UIWindow.img/KeyConfig") as WzProperty;
        _background = kc?.Get("backgrnd") is WzCanvas bc ? loader.Load(bc) : null;

        _btClose = MakeButton(loader, kc, "BtClose", () => IsVisible = false);
        _btDefault = MakeButton(loader, kc, "BtDefault", () => { });
        _btOk = MakeButton(loader, kc, "BtOK", () => IsVisible = false);

        ApplyLayout();
    }

    private void ApplyLayout()
    {
        if (_btClose != null) _btClose.Position = Position + new Vector2(500, 6);
        if (_btDefault != null) _btDefault.Position = Position + new Vector2(400, 390);
        if (_btOk != null) _btOk.Position = Position + new Vector2(470, 390);
    }

    public override void Update(GameTime gameTime) => ApplyLayout();

    public override void Draw(SpriteBatch sb, Texture2D white)
    {
        if (!IsVisible) return;

        if (_background != null)
            _background.Draw(sb, Position + new Vector2(252, 195));
        else
        {
            var rect = new Rectangle((int)Position.X, (int)Position.Y, 512, 396);
            sb.Draw(white, rect, new Color(15, 15, 25, 230));
            DrawBorder(sb, white, rect);
            _font?.Draw(sb, "Key Configuration", Position + new Vector2(200, 5), new Color(220, 200, 150));
        }

        foreach (var b in _allButtons) b.Draw(sb);
    }

    public override bool HandleMouseButton(int x, int y, bool down)
    {
        if (!IsVisible) return false;
        foreach (var b in _allButtons)
            if (b.HandleMouseButton(x, y, down)) return true;
        return new Rectangle((int)Position.X, (int)Position.Y, 512, 396).Contains(x, y);
    }

    public override bool OnKeyPress(Keys key)
    {
        if (key == Keys.Escape && IsVisible) { IsVisible = false; return true; }
        return false;
    }

    private Button? MakeButton(WzTextureLoader loader, WzProperty? root, string name, Action onClick)
    {
        var pr = root?.Get(name) as WzProperty;
        if (pr is null) return null;
        var b = new Button(loader, pr) { OnClick = onClick };
        _allButtons.Add(b);
        return b;
    }

    private static void DrawBorder(SpriteBatch sb, Texture2D white, Rectangle r)
    {
        var c = new Color(80, 70, 50);
        sb.Draw(white, new Rectangle(r.X, r.Y, r.Width, 1), c);
        sb.Draw(white, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), c);
        sb.Draw(white, new Rectangle(r.X, r.Y, 1, r.Height), c);
        sb.Draw(white, new Rectangle(r.Right - 1, r.Y, 1, r.Height), c);
    }
}
