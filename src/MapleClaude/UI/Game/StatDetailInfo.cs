using MapleClaude.Render;
using MapleClaude.Wz;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MapleClaude.UI.Game;

/// <summary>
/// Detailed-stats window — the original client's <c>CUIStatDetail</c>, opened by
/// the main Stat window's "Detailed Info" button. Rebuilt from
/// <c>UI/UIWindow2.img/Stat/detail</c>; the labels are baked into the background
/// art and the derived values (damage range, critical, defenses, accuracy,
/// avoidability, speed, jump) are drawn left-aligned at x=74 over them.
///
/// The original places this window at the main window's top-left + (172, 90);
/// <c>GameStage</c> keeps <see cref="GamePanel.Position"/> pinned there each frame.
/// Derived values come from <see cref="StatDerived"/>; row Y offsets are the
/// client's and may be fine-tuned against the running client via MAPLECLAUDE_DEBUG.
/// </summary>
public sealed class StatDetailInfo : GamePanel
{
    private readonly WzTextureLoader _loader;
    private readonly BuiltInFont? _font;

    private readonly WzSprite? _bg;
    private readonly WzSprite? _bg2;
    private readonly Button? _btHpUp;

    /// <summary>Stat inputs for the derived-value formulas (set by GameStage).</summary>
    public StatInputs Inputs { get; set; } = StatInputs.Default;

    private const int ColValue = 74;
    private const int RowDamage = 15;
    private const int RowCritical = 33;
    private const int RowPdd = 51;
    private const int RowMdd = 69;
    private const int RowAcc = 87;
    private const int RowEva = 105;
    private const int RowSpeed = 177;
    private const int RowJump = 195;

    private int PanelW => _bg?.Width ?? 178;
    private int PanelH => _bg?.Height ?? 247;

    private static readonly Color ValueColor = new(51, 42, 33);

    public StatDetailInfo(WzTextureLoader loader, WzPackage? ui, BuiltInFont? font)
    {
        _loader = loader;
        _font = font;
        IsVisible = false;

        var detail = ui?.GetItem("UIWindow2.img/Stat/detail") as WzProperty;
        _bg  = detail?.Get("backgrnd")  is WzCanvas c1 ? loader.Load(c1) : null;
        _bg2 = detail?.Get("backgrnd2") is WzCanvas c2 ? loader.Load(c2) : null;
        _btHpUp = detail?.Get("BtHpUp") is WzProperty bp ? new Button(loader, bp) : null;
    }

    public override void Update(GameTime gameTime)
    {
        if (!IsVisible) return;
        var m = Mouse.GetState();
        if (_btHpUp != null)
        {
            _btHpUp.Position = Position;
            _btHpUp.Update(m.X, m.Y, m.LeftButton == ButtonState.Pressed);
        }
    }

    public override void Draw(SpriteBatch sb, Texture2D white)
    {
        if (!IsVisible) return;

        _bg?.Draw(sb, Position);
        _bg2?.Draw(sb, Position);

        var d = StatDerived.Compute(Inputs);

        DrawValue(sb, $"{d.MinDamage} ~ {d.MaxDamage}", RowDamage);
        DrawValue(sb, $"{d.CriticalPercent}%", RowCritical);
        DrawValue(sb, d.Pdd.ToString(), RowPdd);
        DrawValue(sb, d.Mdd.ToString(), RowMdd);
        DrawValue(sb, d.Accuracy.ToString(), RowAcc);
        DrawValue(sb, d.Avoidability.ToString(), RowEva);
        DrawValue(sb, d.Speed + "%", RowSpeed);
        DrawValue(sb, d.Jump + "%", RowJump);

        _btHpUp?.Draw(sb);
    }

    private void DrawValue(SpriteBatch sb, string text, int rowY)
        => _font?.Draw(sb, text, new Vector2(Position.X + ColValue, Position.Y + rowY), ValueColor);

    public override bool HandleMouseButton(int x, int y, bool down)
    {
        if (!IsVisible) return false;
        if (_btHpUp?.HandleMouseButton(x, y, down) == true) return true;
        // Swallow clicks on the window body so they don't fall through to the field.
        return new Rectangle((int)Position.X, (int)Position.Y, PanelW, PanelH).Contains(x, y);
    }
}
