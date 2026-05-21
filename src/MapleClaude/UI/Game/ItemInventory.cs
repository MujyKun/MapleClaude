using MapleClaude.Render;
using MapleClaude.UI;
using MapleClaude.Wz;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MapleClaude.UI.Game;

/// <summary>
/// Item inventory panel. Toggle with I key.
/// 5 tabs: Equip / Use / Setup / Etc / Cash.
/// WZ: <c>UIWindow.img/Item/</c>
/// </summary>
public sealed class ItemInventory : GamePanel
{
    private readonly WzSprite? _background;
    private readonly Button? _btClose;
    private readonly Button?[] _tabs = new Button?[5];
    private readonly BuiltInFont? _font;
    private readonly List<Button> _allButtons = new();
    private int _activeTab = 0;

    private static readonly string[] TabNames = ["Equip", "Use", "Setup", "Etc", "Cash"];

    public ItemInventory(WzTextureLoader loader, WzPackage? ui, BuiltInFont? font)
    {
        _font = font;
        IsVisible = false;
        Position = new Vector2(370, 50);

        var item = ui?.GetItem("UIWindow.img/Item") as WzProperty;
        _background = item?.Get("backgrnd") is WzCanvas bc ? loader.Load(bc) : null;

        var closeRoot = item?.Get("BtClose") as WzProperty;
        if (closeRoot != null)
        {
            _btClose = new Button(loader, closeRoot) { OnClick = () => IsVisible = false };
            _allButtons.Add(_btClose);
        }

        for (var i = 0; i < 5; i++)
        {
            var idx = i;
            var tabRoot = item?.Get($"Tab/enabled/{i}") as WzProperty;
            if (tabRoot != null)
            {
                _tabs[i] = new Button(loader, tabRoot) { OnClick = () => _activeTab = idx };
                _allButtons.Add(_tabs[i]!);
            }
        }

        ApplyLayout();
    }

    private void ApplyLayout()
    {
        if (_btClose != null) _btClose.Position = Position + new Vector2(160, 6);
        for (var i = 0; i < 5; i++)
            if (_tabs[i] != null) _tabs[i]!.Position = Position + new Vector2(6 + i * 32, 22);
    }

    public override void Update(GameTime gameTime) => ApplyLayout();

    public override void Draw(SpriteBatch sb, Texture2D white)
    {
        if (!IsVisible) return;

        if (_background != null)
            _background.Draw(sb, Position + new Vector2(82, 170));
        else
        {
            sb.Draw(white, new Rectangle((int)Position.X, (int)Position.Y, 172, 340), new Color(15, 15, 25, 230));
            DrawBorder(sb, white, new Rectangle((int)Position.X, (int)Position.Y, 172, 340));
            _font?.Draw(sb, "Items - " + TabNames[_activeTab], Position + new Vector2(45, 5),
                new Color(220, 200, 150));
        }

        foreach (var b in _allButtons) b?.Draw(sb);
    }

    public override bool HandleMouseButton(int x, int y, bool down)
    {
        if (!IsVisible) return false;
        foreach (var b in _allButtons)
            if (b?.HandleMouseButton(x, y, down) == true) return true;
        var r = new Rectangle((int)Position.X, (int)Position.Y, 172, 340);
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
