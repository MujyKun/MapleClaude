using MapleClaude.Render;
using MapleClaude.UI;
using MapleClaude.Wz;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MapleClaude.UI.Game;

/// <summary>
/// Character stats panel. Toggle with S key.
/// WZ: <c>UIWindow.img/Stat/</c>
/// </summary>
public sealed class StatsInfo : GamePanel
{
    private readonly WzSprite? _background;
    private readonly Button? _btClose;
    private readonly Button? _btDetail;
    private readonly Button? _btAP;
    private readonly BuiltInFont? _font;

    // Placeholder stats — wired to server packets later
    public int Level { get; set; } = 1;
    public string Job { get; set; } = "Beginner";
    public int Str { get; set; } = 4;
    public int Dex { get; set; } = 4;
    public int Int { get; set; } = 4;
    public int Luk { get; set; } = 4;
    public int Hp { get; set; } = 50;
    public int MaxHp { get; set; } = 50;
    public int Mp { get; set; } = 5;
    public int MaxMp { get; set; } = 5;
    public int AP { get; set; } = 0;

    public StatsInfo(WzTextureLoader loader, WzPackage? ui, BuiltInFont? font)
    {
        _font = font;
        IsVisible = false;
        Position = new Vector2(10, 50);

        var stat = ui?.GetItem("UIWindow.img/Stat") as WzProperty;
        _background = stat?.Get("backgrnd") is WzCanvas bc ? loader.Load(bc) : null;

        var closeRoot = stat?.Get("BtClose") as WzProperty;
        if (closeRoot != null)
            _btClose = new Button(loader, closeRoot)
            {
                Position = Position + new Vector2(160, 6),
                OnClick = () => IsVisible = false,
            };

        var detailRoot = stat?.Get("BtDetail") as WzProperty;
        if (detailRoot != null)
            _btDetail = new Button(loader, detailRoot) { Position = Position + new Vector2(120, 300) };

        var apRoot = stat?.Get("BtAP") as WzProperty;
        if (apRoot != null)
            _btAP = new Button(loader, apRoot) { Position = Position + new Vector2(140, 80) };
    }

    public override void Draw(SpriteBatch sb, Texture2D white)
    {
        if (!IsVisible) return;

        if (_background != null)
            _background.Draw(sb, Position + new Vector2(82, 170));
        else
        {
            var rect = new Rectangle((int)Position.X, (int)Position.Y, 172, 335);
            sb.Draw(white, rect, new Color(15, 15, 25, 230));
            DrawBorder(sb, white, rect);
            _font?.Draw(sb, "Character Stats", Position + new Vector2(35, 5), new Color(220, 200, 150));
        }

        if (_font != null)
        {
            var x = Position.X + 8;
            var y = Position.Y + 24;
            DrawStat(sb, "Level", Level.ToString(), x, ref y);
            DrawStat(sb, "Job", Job, x, ref y);
            y += 4;
            DrawStat(sb, "STR", Str.ToString(), x, ref y);
            DrawStat(sb, "DEX", Dex.ToString(), x, ref y);
            DrawStat(sb, "INT", Int.ToString(), x, ref y);
            DrawStat(sb, "LUK", Luk.ToString(), x, ref y);
            y += 4;
            DrawStat(sb, "HP", $"{Hp}/{MaxHp}", x, ref y);
            DrawStat(sb, "MP", $"{Mp}/{MaxMp}", x, ref y);
            y += 4;
            DrawStat(sb, "AP", AP.ToString(), x, ref y);
        }

        _btClose?.Draw(sb);
        _btDetail?.Draw(sb);
        if (AP > 0) _btAP?.Draw(sb);
    }

    private void DrawStat(SpriteBatch sb, string label, string value, float x, ref float y)
    {
        _font!.Draw(sb, label, new Vector2(x, y), new Color(180, 180, 180));
        _font.Draw(sb, value, new Vector2(x + 60, y), Color.White);
        y += 15;
    }

    public override bool HandleMouseButton(int x, int y, bool down)
    {
        if (!IsVisible) return false;
        if (_btClose?.HandleMouseButton(x, y, down) == true) return true;
        if (_btDetail?.HandleMouseButton(x, y, down) == true) return true;
        if (AP > 0 && _btAP?.HandleMouseButton(x, y, down) == true) return true;
        return new Rectangle((int)Position.X, (int)Position.Y, 172, 335).Contains(x, y);
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
