using MapleClaude.Render;
using MapleClaude.Wz;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MapleClaude.Character;

/// <summary>
/// Renders a single field mob by playing its WZ animation frames.
/// WZ path: <c>Mob.wz/{templateId:D7}.img/{state}/{frame}</c>
///
/// States used: stand (idle), move, attack1, hit1, die1.
/// Falls back to a colour-coded placeholder when WZ assets are unavailable.
/// HP bar drawn beneath the sprite when health data is available.
/// </summary>
public sealed class MobLook
{
    // ── Identity ──────────────────────────────────────────────────────────────
    public int  MobId      { get; }
    public int  TemplateId { get; }

    // ── World position ────────────────────────────────────────────────────────
    public Vector2 Position { get; set; }

    /// <summary>Render layer (0..7), the WZ layer of the foothold this mob is standing on. Used by
    /// GameStage's per-layer draw pass so the mob renders behind/in front of the right Map.wz objs.
    /// Updated by <see cref="MobController"/> whenever the mob crosses onto a foothold in a different
    /// layer. Defaults to 7 (the topmost, always-visible layer) for fly mobs / unattached mobs.</summary>
    public int Layer { get; set; } = 7;

    // ── Health (fed by MobDamaged / StatSet packets) ──────────────────────────
    public int Hp    { get; set; } = -1;   // -1 = unknown
    public int MaxHp { get; set; } = -1;

    /// <summary>Mob HP as a percentage 0..100, pushed by MobHPIndicator(298) right after
    /// each hit the local player lands. -1 = never indicated. When set (and HitTagVisible),
    /// the HP bar drawn above the sprite shows this — Hp/MaxHp are used otherwise (e.g.
    /// MobDamaged broadcasts from other players that carry absolute values).</summary>
    public int HpPercent { get; private set; } = -1;

    /// <summary>Display name (resolved from String.wz/Mob.img/&lt;id&gt;/name via NameService).
    /// Set by GameStage at MobLook construction; shown in the "{Name} (Lv. N)" tag below
    /// the mob for ~5 s after each hit (see HitTagVisible).</summary>
    public string Name  { get; set; } = string.Empty;

    /// <summary>Mob level (from MobInfo.Level). Shown alongside the name in the hit tag.</summary>
    public int    Level { get; set; }

    /// <summary>RGB 0xRRGGBB for the over-head HP bar fill (from Mob.wz/info/hpTagColor).
    /// 0 = fall back to the default green/yellow/red gradient by HP %.</summary>
    public int    HpTagColor   { get; set; }

    /// <summary>RGB 0xRRGGBB for the over-head HP bar background (from
    /// Mob.wz/info/hpTagBgcolor). 0 = default dark.</summary>
    public int    HpTagBgColor { get; set; }

    /// <summary>Single shared "I was just hit" timer driving the HP bar AND the name tag
    /// visibility. Reset on every OnHit and SetHpPercent; HitTagVisible is true while &gt; 0.</summary>
    private float _hitFeedbackTimer;
    public bool   HitTagVisible => _hitFeedbackTimer > 0f;
    public string NameTagText   => string.IsNullOrEmpty(Name) ? string.Empty
                                                              : $"{Name} (Lv. {Level})";

    // ── State ─────────────────────────────────────────────────────────────────
    public enum MobState { Stand, Move, Attack, Hit, Die }
    private bool     _facingLeft;
    private bool     _dead;

    // ── Animation ─────────────────────────────────────────────────────────────
    private readonly Dictionary<MobState, List<(WzSprite sprite, int delayMs)>> _anims = new();
    private MobState _curState = MobState.Stand;
    private int   _frame;
    private float _frameTimer;



    private static readonly string[] StateNames = ["stand", "move", "attack1", "hit1", "die1"];
    private bool _loaded;

    private const int PlaceholderW = 40;
    private const int PlaceholderH = 50;

    public MobLook(int mobId, int templateId, Vector2 position)
    {
        MobId      = mobId;
        TemplateId = templateId;
        Position   = position;
    }

    // ── Loading ───────────────────────────────────────────────────────────────

    public void Load(WzTextureLoader loader, WzPackage? mobWz)
    {
        if (mobWz is null) return;
        var strid  = $"{TemplateId:D7}.img";
        if (mobWz.GetItem(strid) is not WzImage img) return;
        var root = img.Root;

        // info/link: many mobs store their animation frames under another template id
        // (e.g. recolours/variants). Resolve it and load frames from the linked image.
        if (root.GetItem("info/link") is string link && int.TryParse(link, out var linkId)
            && mobWz.GetItem($"{linkId:D7}.img") is WzImage linked)
        {
            root = linked.Root;
        }

        var states = new[]
        {
            (MobState.Stand,  "stand"),
            (MobState.Move,   "move"),
            (MobState.Attack, "attack1"),
            (MobState.Hit,    "hit1"),
            (MobState.Die,    "die1"),
        };
        foreach (var (st, name) in states)
        {
            var stateNode = root.Get(name) as WzProperty;
            if (stateNode is null) continue;
            var frames = new List<(WzSprite, int)>();
            var fi     = 0;
            while (true)
            {
                var raw = stateNode.Get($"{fi}");
                if (raw is null) break;
                int delay;
                WzSprite? sprite;
                if (raw is WzCanvas dc)       { delay = 120; sprite = loader.Load(dc); }
                else if (raw is WzProperty fp) { delay = ReadDelay(fp); sprite = LoadFrame(loader, fp); }
                else break;
                if (sprite != null) frames.Add((sprite, delay));
                fi++;
            }
            if (frames.Count > 0) _anims[st] = frames;
        }
        _loaded = _anims.Count > 0;
    }

    // ── State control ─────────────────────────────────────────────────────────

    public void SetState(MobState state)
    {
        if (state == _curState) return;
        _curState   = state;
        _frame      = 0;
        _frameTimer = 0;
    }

    public void OnHit(int damage)
    {
        _hitFeedbackTimer = HitFeedbackDuration;
        SetState(MobState.Hit);
    }

    /// <summary>Push the server's MobHPIndicator(298) percentage. Also resets the
    /// hit-feedback timer so the HP bar + name tag stay visible alongside our local
    /// OnHit feedback.</summary>
    public void SetHpPercent(byte pct)
    {
        HpPercent = pct;
        _hitFeedbackTimer = HitFeedbackDuration;
    }

    private const float HitFeedbackDuration = 5f;   // HP bar + name tag auto-hide after this

    public void OnDie() { _dead = false; SetState(MobState.Die); }

    public bool IsDead => _dead;

    /// <summary>True once <see cref="OnDie"/> has been called and the die animation is
    /// playing. Used by GameStage to dedup the die sound when both MobHPIndicator(0%)
    /// and MobLeaveField(type=2) arrive for the same mob.</summary>
    public bool IsDying => _curState == MobState.Die;

    public void SetFacing(bool facingLeft) => _facingLeft = facingLeft;

    // ── Hit rect ──────────────────────────────────────────────────────────────

    /// <summary>World-space AABB of the current animation frame's hit/body rect,
    /// derived from the canvas's <c>lt</c>/<c>rb</c> vectors and the mob's foot
    /// position. Mirrors <c>CMob::GetBodyRect</c> in the v95 client: the precomputed
    /// foot-relative rect (and its horizontally-mirrored variant) is translated by the
    /// mob's foot point. Returns <see langword="null"/> when the current frame doesn't
    /// carry lt/rb (e.g. placeholder render) — callers should fall back to a
    /// foot-anchored default rect.</summary>
    public Rectangle? GetWorldHitRect()
    {
        if (!_anims.TryGetValue(_curState, out var frames) || frames.Count == 0) return null;
        var (sprite, _) = frames[Math.Min(_frame, frames.Count - 1)];
        if (sprite.Lt is not Vector2 lt || sprite.Rb is not Vector2 rb) return null;

        // MobLook draws unflipped when facing left (sprite native facing). Facing
        // right horizontally mirrors the sprite around the foot, so the body rect
        // mirrors too: flipped_lt.x = -rb.x, flipped_rb.x = -lt.x.
        int x = (int)Position.X;
        int y = (int)Position.Y;
        int left, right;
        if (_facingLeft)
        {
            left  = x + (int)lt.X;
            right = x + (int)rb.X;
        }
        else
        {
            left  = x - (int)rb.X;
            right = x - (int)lt.X;
        }
        int top    = y + (int)lt.Y;
        int bottom = y + (int)rb.Y;
        if (right < left)  (left, right)  = (right, left);
        if (bottom < top)  (top, bottom)  = (bottom, top);
        return new Rectangle(left, top, right - left, bottom - top);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public void Update(float dt)
    {
        if (_hitFeedbackTimer > 0) _hitFeedbackTimer = Math.Max(0, _hitFeedbackTimer - dt);

        if (!_anims.TryGetValue(_curState, out var frames) || frames.Count == 0) return;

        var delayMs = frames[_frame].delayMs;
        if (delayMs <= 0) delayMs = 150;
        _frameTimer += dt * 1000f;
        if (_frameTimer >= delayMs)
        {
            _frameTimer -= delayMs;
            _frame++;
            if (_frame >= frames.Count)
            {
                if (_curState == MobState.Die) { _dead = true; return; }
                if (_curState == MobState.Hit) SetState(MobState.Stand);
                else _frame = 0;
            }
        }
    }

    // ── Draw ──────────────────────────────────────────────────────────────────

    public void Draw(SpriteBatch sb, Texture2D white, Vector2 screenPos)
    {
        var flip = _facingLeft ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
        var tint = Color.White;

        if (_loaded && _anims.TryGetValue(_curState, out var frames) && frames.Count > 0)
        {
            var (sprite, _) = frames[Math.Min(_frame, frames.Count - 1)];
            sprite.Draw(sb, screenPos, flip, tint);
        }
        else
        {
            // Placeholder coloured box — colour varies by templateId range (gives visual variety)
            var hue = (TemplateId / 100) % 6;
            Color fill = hue switch
            {
                0 => new Color(180, 60,  60,  200),
                1 => new Color(60,  180, 60,  200),
                2 => new Color(60,  60,  200, 200),
                3 => new Color(180, 140, 40,  200),
                4 => new Color(140, 40,  180, 200),
                _ => new Color(40,  160, 180, 200),
            };
            sb.Draw(white, new Rectangle(
                (int)(screenPos.X - PlaceholderW / 2f),
                (int)(screenPos.Y - PlaceholderH),
                PlaceholderW, PlaceholderH), fill);
        }

        DrawHpBar(sb, white, screenPos);
    }

    private void DrawHpBar(SpriteBatch sb, Texture2D white, Vector2 screenPos)
    {
        // MobHPIndicator(298) only fires for hits WE land, so the server's HpPercent push is
        // the most authoritative source. Fall back to absolute Hp/MaxHp from MobDamaged
        // (which we receive for other players' attacks). HitTagVisible gates auto-hide.
        float pct;
        if (HpPercent >= 0 && HitTagVisible) pct = HpPercent / 100f;
        else if (Hp >= 0 && MaxHp > 0)       pct = (float)Hp / MaxHp;
        else return;
        pct = Math.Clamp(pct, 0f, 1f);

        const int BarW = 50;
        const int BarH = 5;
        var bx = (int)(screenPos.X - BarW / 2f);
        var by = (int)(screenPos.Y - PlaceholderH - 12);

        // 1 px dark outline so the bar reads on any background.
        sb.Draw(white, new Rectangle(bx - 1, by - 1, BarW + 2, BarH + 2), new Color(0, 0, 0, 220));

        // Background: hpTagBgcolor when set (the v95 client uses the per-mob tag color so
        // e.g. boss bars use their authored palette), otherwise a darkened ink.
        var bgColor = HpTagBgColor != 0 ? FromRgb(HpTagBgColor, 200) : new Color(20, 20, 20, 220);
        sb.Draw(white, new Rectangle(bx, by, BarW, BarH), bgColor);

        // Fill: hpTagColor when set; otherwise the green/yellow/red gradient by HP %.
        var fillColor = HpTagColor != 0
            ? FromRgb(HpTagColor, 255)
            : pct > 0.5f  ? new Color(80,  200, 80)
            : pct > 0.25f ? new Color(220, 180, 40)
                          : new Color(220, 60,  60);
        var fillW = (int)(BarW * pct);
        if (fillW > 0) sb.Draw(white, new Rectangle(bx, by, fillW, BarH), fillColor);
    }

    private static Color FromRgb(int rgb, byte alpha)
        => new Color((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF, (int)alpha);

    private static WzSprite? LoadFrame(WzTextureLoader loader, WzProperty frameNode)
    {
        foreach (var (_, v) in frameNode.Items)
            if (v is WzCanvas c) return loader.Load(c);
        return null;
    }

    private static int ReadDelay(WzProperty node) =>
        node.Get("delay") switch { int i => i, short s => s, long l => (int)l, _ => 150 };
}
