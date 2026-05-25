using MapleClaude.Domain;
using MapleClaude.Render;
using MapleClaude.Wz;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MapleClaude.UI.Game;

/// <summary>
/// Character ability window — the original client's <c>CUIStat</c>, toggled with
/// the S key. Rebuilt 1:1 from <c>UI/UIWindow2.img/Stat/main</c>: backgrounds and
/// buttons are positioned by their WZ origin (the negative origins encode each
/// child's in-window offset), and the stat values are drawn at the exact
/// window-local coordinates the client uses (left-aligned at x=54).
///
/// All <c>+</c> buttons send an ability-up request to the server and do NOT mutate
/// stats locally — the authoritative value comes back via <c>StatChanged</c>.
/// </summary>
public sealed class StatsInfo : GamePanel
{
    private readonly WzTextureLoader _loader;
    private readonly BuiltInFont? _font;

    // ── Backgrounds ───────────────────────────────────────────────────────────
    private readonly WzSprite? _bg;
    private readonly WzSprite? _bg2;
    private readonly WzSprite? _bg3;
    private readonly WzSprite? _cover0;
    private readonly WzSprite? _cover1;

    // ── Buttons ───────────────────────────────────────────────────────────────
    // m_pBtApUp order from CUIStat::OnCreate: HP, MP, STR, DEX, INT, LUK.
    private readonly Button?[] _apBtns = new Button?[6];
    private readonly Button? _btDetailOpen;
    private readonly Button? _btDetailClose;
    private readonly Button? _btAuto;   // non-pirate auto-assign
    private readonly Button? _btAuto1;  // pirate STR
    private readonly Button? _btAuto2;  // pirate DEX

    // ── Values (set by GameStage from CharStats / StatChanged) ─────────────────
    public string Name { get; set; } = string.Empty;
    public string Job { get; set; } = "Beginner";
    public int JobId { get; set; }
    public string Guild { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public int Exp { get; set; }
    public int Fame { get; set; }
    public int AP { get; set; }
    public int SP { get; set; }
    public int Str { get; set; } = 4;
    public int Dex { get; set; } = 4;
    public int Int { get; set; } = 4;
    public int Luk { get; set; } = 4;
    public int Hp { get; set; } = 50; public int MaxHp { get; set; } = 50;
    public int Mp { get; set; } = 5;  public int MaxMp { get; set; } = 5;

    // Equip bonuses for the "%d (%d%+d)" breakdown (0 until equip data flows).
    public int StrBonus { get; set; }
    public int DexBonus { get; set; }
    public int IntBonus { get; set; }
    public int LukBonus { get; set; }

    // ── Callbacks (wired by GameStage) ────────────────────────────────────────
    public Action? OnHpUp { get; set; }
    public Action? OnMpUp { get; set; }
    public Action? OnStrUp { get; set; }
    public Action? OnDexUp { get; set; }
    public Action? OnIntUp { get; set; }
    public Action? OnLukUp { get; set; }
    /// <summary>Auto-assign: argument is the primary-stat flag to pour all AP into.</summary>
    public Action<int>? OnAutoAssign { get; set; }
    /// <summary>Raised when the detail button toggles; argument is the new open state.</summary>
    public Action<bool>? OnDetailToggled { get; set; }

    public bool DetailOpen { get; private set; }

    // ── Layout (window-local pixel offsets from CUIStat::Draw) ─────────────────
    private const int RowName = 32;
    private const int RowJob = 50;
    private const int RowLevel = 68;
    private const int RowGuild = 86;
    private const int RowHp = 104;
    private const int RowMp = 122;
    private const int RowExp = 140;
    private const int RowFame = 158;
    private const int RowAp = 200;     // AP value (right-aligned at x=85)
    private const int RowStr = 227;
    private const int RowDex = 245;
    private const int RowInt = 263;
    private const int RowLuk = 281;
    private const int ColValue = 54;
    private const int ApRightEdge = 85;

    private int PanelW => _bg?.Width ?? 172;
    private int PanelH => _bg?.Height ?? 337;

    private static readonly Color ValueColor = new(51, 42, 33);

    private bool _dragging;
    private Vector2 _dragOff;

    public StatsInfo(WzTextureLoader loader, WzPackage? ui, BuiltInFont? font)
    {
        _loader = loader;
        _font = font;
        IsVisible = false;
        Position = new Vector2(540, 100);

        var main = ui?.GetItem("UIWindow2.img/Stat/main") as WzProperty;

        _bg     = LoadCanvas(main, "backgrnd");
        _bg2    = LoadCanvas(main, "backgrnd2");
        _bg3    = LoadCanvas(main, "backgrnd3");
        _cover0 = LoadCanvas(main, "cover0");
        _cover1 = LoadCanvas(main, "cover1");

        _apBtns[0] = MakeBtn(main, "BtHpUp",  () => OnHpUp?.Invoke());
        _apBtns[1] = MakeBtn(main, "BtMpUp",  () => OnMpUp?.Invoke());
        _apBtns[2] = MakeBtn(main, "BtStrUp", () => OnStrUp?.Invoke());
        _apBtns[3] = MakeBtn(main, "BtDexUp", () => OnDexUp?.Invoke());
        _apBtns[4] = MakeBtn(main, "BtIntUp", () => OnIntUp?.Invoke());
        _apBtns[5] = MakeBtn(main, "BtLukUp", () => OnLukUp?.Invoke());

        _btDetailOpen  = MakeBtn(main, "BtDetailOpen",  () => SetDetail(true));
        _btDetailClose = MakeBtn(main, "BtDetailClose", () => SetDetail(false));

        _btAuto  = MakeBtn(main, "BtAuto",  () => AutoAssign(StatDerived.PrimaryStatFlag(JobId)));
        _btAuto1 = MakeBtn(main, "BtAuto1", () => AutoAssign(0x40));   // pirate → STR
        _btAuto2 = MakeBtn(main, "BtAuto2", () => AutoAssign(0x80));   // pirate → DEX
    }

    // ── Visibility helpers ────────────────────────────────────────────────────

    /// <summary>Beginners (job 0/2001) at level ≤10 see covers over the ability
    /// section; the AP-distribution controls are hidden.</summary>
    private bool BeginnerCovered => (JobId % 1000 == 0 || JobId == 2001) && Level <= 10;

    private bool IsPirate => (JobId / 100) % 10 == 5;

    private void SetDetail(bool open)
    {
        DetailOpen = open;
        OnDetailToggled?.Invoke(open);
    }

    private void AutoAssign(int statFlag)
    {
        if (AP <= 0) return;
        OnAutoAssign?.Invoke(statFlag);
    }

    // ── Update ────────────────────────────────────────────────────────────────
    public override void Update(GameTime gameTime)
    {
        if (!IsVisible)
        {
            if (DetailOpen) SetDetail(false);   // closing the main window closes detail
            return;
        }

        var m = Mouse.GetState();
        if (_dragging)
        {
            if (m.LeftButton == ButtonState.Pressed)
                Position = new Vector2(m.X, m.Y) - _dragOff;
            else
                _dragging = false;
        }

        var apOk = AP > 0 && !BeginnerCovered;
        // STR/DEX/INT/LUK enabled when AP available; HP/MP additionally need Lv ≥ 20.
        for (var i = 0; i < 6; i++)
        {
            if (_apBtns[i] == null) continue;
            _apBtns[i]!.Enabled = i < 2 ? (apOk && Level >= 20) : apOk;
            _apBtns[i]!.Position = Position;
            _apBtns[i]!.Update(m.X, m.Y, m.LeftButton == ButtonState.Pressed);
        }

        foreach (var b in AutoButtons())
        {
            b.Enabled = apOk;
            b.Position = Position;
            b.Update(m.X, m.Y, m.LeftButton == ButtonState.Pressed);
        }

        var detail = DetailOpen ? _btDetailClose : _btDetailOpen;
        if (detail != null)
        {
            detail.Position = Position;
            detail.Update(m.X, m.Y, m.LeftButton == ButtonState.Pressed);
        }
    }

    private IEnumerable<Button> AutoButtons()
    {
        if (BeginnerCovered) yield break;
        if (IsPirate)
        {
            if (_btAuto1 != null) yield return _btAuto1;
            if (_btAuto2 != null) yield return _btAuto2;
        }
        else if (_btAuto != null)
        {
            yield return _btAuto;
        }
    }

    // ── Draw ──────────────────────────────────────────────────────────────────
    public override void Draw(SpriteBatch sb, Texture2D white)
    {
        if (!IsVisible) return;

        _bg?.Draw(sb, Position);
        _bg2?.Draw(sb, Position);
        _bg3?.Draw(sb, Position);

        var covered = BeginnerCovered;
        if (covered)
        {
            _cover0?.Draw(sb, Position);
            _cover1?.Draw(sb, Position);
        }

        // Info section (always shown).
        DrawValue(sb, Name, RowName);
        DrawValue(sb, Job, RowJob);
        DrawValue(sb, Level.ToString(), RowLevel);
        DrawValue(sb, string.IsNullOrEmpty(Guild) ? "-" : Guild, RowGuild);
        DrawValue(sb, $"{Hp} / {MaxHp}", RowHp);
        DrawValue(sb, $"{Mp} / {MaxMp}", RowMp);
        DrawValue(sb, ExpText(), RowExp);
        DrawValue(sb, Fame.ToString(), RowFame);

        if (!covered)
        {
            // AP — right-aligned to x=85.
            var apText = AP.ToString();
            var apW = _font?.Measure(apText).X ?? 0f;
            _font?.Draw(sb, apText, new Vector2(Position.X + ApRightEdge - apW, Position.Y + RowAp), ValueColor);

            DrawValue(sb, StatText(Str, StrBonus), RowStr);
            DrawValue(sb, StatText(Dex, DexBonus), RowDex);
            DrawValue(sb, StatText(Int, IntBonus), RowInt);
            DrawValue(sb, StatText(Luk, LukBonus), RowLuk);

            for (var i = 0; i < 6; i++) _apBtns[i]?.Draw(sb);
            foreach (var b in AutoButtons()) b.Draw(sb);
        }

        (DetailOpen ? _btDetailClose : _btDetailOpen)?.Draw(sb);
    }

    private void DrawValue(SpriteBatch sb, string text, int rowY)
        => _font?.Draw(sb, text, new Vector2(Position.X + ColValue, Position.Y + rowY), ValueColor);

    private string ExpText()
    {
        var next = ExpTable.GetNextLevelExp(Level);
        var pct = next > 0 ? (int)((double)Exp / next * 100.0) : 0;
        return $"{Exp} ({pct}%)";
    }

    private static string StatText(int baseValue, int bonus)
        => bonus != 0 ? $"{baseValue + bonus} ({baseValue}{bonus:+0;-0})" : baseValue.ToString();

    // ── Input ─────────────────────────────────────────────────────────────────
    public override bool HandleMouseButton(int x, int y, bool down)
    {
        if (!IsVisible) return false;

        // Detail toggle.
        var detail = DetailOpen ? _btDetailClose : _btDetailOpen;
        if (detail?.HandleMouseButton(x, y, down) == true) return true;

        if (!BeginnerCovered)
        {
            for (var i = 0; i < 6; i++)
                if (_apBtns[i]?.HandleMouseButton(x, y, down) == true) return true;
            foreach (var b in AutoButtons())
                if (b.HandleMouseButton(x, y, down) == true) return true;
        }

        // Close (the X is baked into the title-bar art at top-right; no WZ node).
        var closeRect = new Rectangle((int)Position.X + PanelW - 20, (int)Position.Y + 5, 15, 14);
        if (down && closeRect.Contains(x, y)) { IsVisible = false; return true; }

        // Drag by the title bar (top strip).
        var titleBar = new Rectangle((int)Position.X, (int)Position.Y, PanelW, 22);
        if (down && titleBar.Contains(x, y))
        {
            _dragging = true;
            _dragOff = new Vector2(x - Position.X, y - Position.Y);
            return true;
        }

        return new Rectangle((int)Position.X, (int)Position.Y, PanelW, PanelH).Contains(x, y);
    }

    public override bool OnKeyPress(Keys key)
    {
        if (key == Keys.Escape && IsVisible) { IsVisible = false; return true; }
        return false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private WzSprite? LoadCanvas(WzProperty? root, string name)
        => root?.Get(name) is WzCanvas c ? _loader.Load(c) : null;

    private Button? MakeBtn(WzProperty? root, string name, Action onClick)
    {
        if (root?.Get(name) is not WzProperty pr) return null;
        return new Button(_loader, pr) { OnClick = onClick };
    }
}
