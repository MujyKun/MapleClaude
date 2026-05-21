using MapleClaude.Render;
using MapleClaude.UI;
using MapleClaude.Wz;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MapleClaude.UI.Game;

/// <summary>
/// Equipment inventory panel. Toggle with E key.
/// WZ: <c>UIWindow.img/Equip/</c>
/// </summary>
public sealed class EquipInventory : GamePanel
{
    private readonly WzSprite? _background;
    private readonly Button? _btClose;
    private readonly BuiltInFont? _font;

    public EquipInventory(WzTextureLoader loader, WzPackage? ui, BuiltInFont? font)
    {
        _font = font;
        IsVisible = false;
        Position = new Vector2(550, 50);

        var equip = ui?.GetItem("UIWindow.img/Equip") as WzProperty;
        _background = equip?.Get("backgrnd") is WzCanvas bc ? loader.Load(bc) : null;

        var closeRoot = equip?.Get("BtClose") as WzProperty;
        if (closeRoot != null)
            _btClose = new Button(loader, closeRoot) { OnClick = () => IsVisible = false };
        if (_btClose != null) _btClose.Position = Position + new Vector2(150, 6);
    }

    public override void Draw(SpriteBatch sb, Texture2D white)
    {
        if (!IsVisible) return;

        if (_background != null)
            _background.Draw(sb, Position + new Vector2(82, 170));
        else
        {
            sb.Draw(white, new Rectangle((int)Position.X, (int)Position.Y, 170, 340), new Color(15, 15, 25, 230));
            DrawBorder(sb, white, new Rectangle((int)Position.X, (int)Position.Y, 170, 340));
            _font?.Draw(sb, "Equipment", Position + new Vector2(55, 5), new Color(220, 200, 150));
        }

        _btClose?.Draw(sb);
    }

    public override bool HandleMouseButton(int x, int y, bool down)
    {
        if (!IsVisible) return false;
        if (_btClose?.HandleMouseButton(x, y, down) == true) return true;
        var r = new Rectangle((int)Position.X, (int)Position.Y, 170, 340);
        return r.Contains(x, y);
    }

    public override bool OnKeyPress(Keys key)
    {
        if (key == Keys.Escape && IsVisible) { IsVisible = false; return true; }
        return false;
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
