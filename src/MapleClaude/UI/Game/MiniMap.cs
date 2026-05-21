using MapleClaude.Render;
using MapleClaude.UI;
using MapleClaude.Wz;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MapleClaude.UI.Game;

/// <summary>
/// Mini-map panel. Toggle with M. Shows a small map thumbnail and map name.
/// WZ: <c>UIWindow2.img/MiniMap/</c>
/// </summary>
public sealed class MiniMap : GamePanel
{
    private readonly WzSprite? _frame;
    private readonly WzSprite? _simpleFrame;
    private readonly Button? _btMin;
    private readonly Button? _btMax;
    private readonly Button? _btBig;
    private readonly Button? _btNpc;
    private readonly BuiltInFont? _font;
    private readonly List<Button> _allButtons = new();

    private bool _expanded = true;
    private string _mapName = string.Empty;
    private string _streetName = string.Empty;

    public void SetMapInfo(string street, string name)
    {
        _streetName = street;
        _mapName = name;
    }

    public MiniMap(WzTextureLoader loader, WzPackage? ui, BuiltInFont? font)
    {
        _font = font;
        IsVisible = true;
        Position = new Vector2(5, 5);

        var mm = ui?.GetItem("UIWindow2.img/MiniMap") as WzProperty;
        _frame = mm?.Get("MAX") is WzCanvas fc ? loader.Load(fc) : null;
        _simpleFrame = mm?.Get("MIN") is WzCanvas sc ? loader.Load(sc) : null;

        _btMin = MakeButton(loader, mm, "BtMin", () => _expanded = false);
        _btMax = MakeButton(loader, mm, "BtMax", () => _expanded = true);
        _btBig = MakeButton(loader, mm, "BtBig", () => { });
        _btNpc = MakeButton(loader, mm, "BtNpc", () => { });

        ApplyLayout();
    }

    private void ApplyLayout()
    {
        if (_btMin != null) _btMin.Position = Position + new Vector2(195, -6);
        if (_btMax != null) _btMax.Position = Position + new Vector2(209, -6);
        if (_btBig != null) _btBig.Position = Position + new Vector2(223, -6);
        if (_btNpc != null) _btNpc.Position = Position + new Vector2(276, -6);
    }

    public override void Update(GameTime gameTime) => ApplyLayout();

    public override void Draw(SpriteBatch sb, Texture2D white)
    {
        if (!IsVisible) return;

        if (_expanded)
        {
            if (_frame != null)
                _frame.Draw(sb, Position + new Vector2(122, 100));
            else
            {
                sb.Draw(white, new Rectangle((int)Position.X, (int)Position.Y, 260, 200), new Color(0, 0, 0, 180));
                DrawBorder(sb, white, new Rectangle((int)Position.X, (int)Position.Y, 260, 200));
            }
            _font?.Draw(sb, _streetName, Position + new Vector2(5, 4), new Color(200, 200, 160));
            _font?.Draw(sb, _mapName, Position + new Vector2(5, 17), Color.White);
        }
        else
        {
            if (_simpleFrame != null)
                _simpleFrame.Draw(sb, Position + new Vector2(60, 8));
            else
            {
                sb.Draw(white, new Rectangle((int)Position.X, (int)Position.Y, 130, 18), new Color(0, 0, 0, 180));
            }
            _font?.Draw(sb, _mapName, Position + new Vector2(5, 4), Color.White);
        }

        foreach (var b in _allButtons) b.Draw(sb);
    }

    public override bool HandleMouseButton(int x, int y, bool down)
    {
        if (!IsVisible) return false;
        foreach (var b in _allButtons)
            if (b.HandleMouseButton(x, y, down)) return true;
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
        var c = new Color(80, 80, 100);
        sb.Draw(white, new Rectangle(r.X, r.Y, r.Width, 1), c);
        sb.Draw(white, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), c);
        sb.Draw(white, new Rectangle(r.X, r.Y, 1, r.Height), c);
        sb.Draw(white, new Rectangle(r.Right - 1, r.Y, 1, r.Height), c);
    }
}
