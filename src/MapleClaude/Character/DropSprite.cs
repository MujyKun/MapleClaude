using MapleClaude.Render;
using MapleClaude.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MapleClaude.Character;

/// <summary>
/// A dropped item or meso bag on the ground. The drop-in motion is a faithful port of the v95
/// <c>CDropPool</c> animation (<c>OnDropEnterField</c> + <c>Update</c>):
///   • state 1 — parabolic toss from the source (mob/player) toward the landing point:
///       y = sourceY − Vy·t + 400·t²  (Vy=400 → a ~100px pop), x eases source→mid→landing;
///   • state 2 — if the source was above the landing, a straight fall the rest of the way;
///   • state 3 — at rest, a gentle ±3px sine bob.
/// Non-animated (ON_THE_FOOTHOLD) drops skip straight to the resting state. The real item icon is
/// rendered (money shows a colour-coded coin). Picked up by <c>DropLeaveField</c>.
/// </summary>
public sealed class DropSprite
{
    public int     DropId          { get; }
    public bool    IsMoney         { get; }
    public int     ItemIdOrAmount  { get; }
    public Vector2 Position        { get; private set; }

    /// <summary>Render layer (0..7), the WZ layer of the foothold this drop lands on. Set once at spawn from
    /// <c>FieldScene.LayerAt</c> using the landing point. Drops are then ground-fixed, so this never
    /// changes.</summary>
    public int     Layer           { get; set; } = 7;

    private const float Vy = 400f;   // initial toss velocity (CDropPool uses 400; 720 for special own-types)

    private readonly Vector2 _source;   // pt1 — toss origin
    private readonly Vector2 _ground;   // pt2 — landing / resting point
    private readonly int     _tEnd;     // parabolic-motion duration (ms)
    private readonly WzSprite? _icon;
    private readonly BuiltInFont? _font;

    private int   _state;     // 1 toss, 2 fall, 3 rest
    private float _tick;      // ms elapsed in the current state
    private float _angle;     // resting-bob phase

    // Pick-up "absorb" animation (CDropPool RegisterAbsorbItemAnimation): fly into the picker + fade.
    private bool    _absorbing;
    private Vector2 _absorbFrom;
    private Func<Vector2>? _absorbTarget;   // LIVE picker position — re-read each frame so it homes to the moving body
    private float   _absorbT;
    private float   _alpha = 1f;
    private const float AbsorbDur = 0.4f;

    /// <summary>True once the pick-up absorb animation has finished — GameStage then removes the drop.</summary>
    public bool Finished { get; private set; }

    /// <summary>Begin the pick-up animation: the item flies into the picker (a live position delegate, so it
    /// tracks the body as it moves) while fading out, accelerating in.</summary>
    public void StartAbsorb(Func<Vector2> target)
    {
        _absorbing = true; _absorbFrom = Position; _absorbTarget = target; _absorbT = 0f;
    }

    // Meso amounts colour-coded.
    private static readonly (int min, Color color)[] MesoColors =
    [
        (1,       new Color(220, 200, 100)),   // bronze coin
        (1000,    new Color(200, 200, 200)),   // silver coin
        (10000,   new Color(255, 215, 0)),     // gold coin
        (100000,  new Color(255, 100, 100)),   // red coin
    ];

    public DropSprite(int dropId, bool isMoney, int itemIdOrAmount,
        Vector2 source, Vector2 ground, bool animated, WzSprite? icon, BuiltInFont? font)
    {
        DropId         = dropId;
        IsMoney        = isMoney;
        ItemIdOrAmount = itemIdOrAmount;
        _ground        = ground;
        _source        = animated ? source : ground;
        _icon          = icon;
        _font          = font;
        _tEnd          = ParabolicDuration((int)_source.Y, (int)_ground.Y);
        _state         = animated ? 1 : 3;   // ON_THE_FOOTHOLD → appear at rest
        Position       = _source;
    }

    // CDropPool::calculate_parbolic_motion_duration (non-explosive path).
    private static int ParabolicDuration(int y1, int y2)
    {
        if (y1 <= y2) return 1000;
        var v6 = 30 * ((int)Math.Sqrt(1000.0 * (2 * (100 + y2 - y1)) / 800.0) + 1) + 500;
        return (int)Math.Min(1000.0, v6);
    }

    public void Update(float dt)
    {
        if (_absorbing)
        {
            _absorbT += dt;
            var at = Math.Min(1f, _absorbT / AbsorbDur);
            var tgt = _absorbTarget?.Invoke() ?? _absorbFrom;            // live target → tracks the moving body
            Position = Vector2.Lerp(_absorbFrom, tgt, at * at);          // accelerating velocity into the picker
            _alpha = 1f - at;
            if (at >= 1f) Finished = true;
            return;
        }

        var dtMs = dt * 1000f;
        switch (_state)
        {
            case 1:   // parabolic toss
            {
                _tick += dtMs;
                var dx = _ground.X - _source.X;
                var t  = _tick / 1000f;
                // X eases to the midpoint over the first 500ms, then on to the landing over the remainder.
                var xf = Math.Min(1f, _tick / 500f)
                       + (_tick > 500f ? Math.Min(1f, (_tick - 500f) / Math.Max(1f, _tEnd - 500f)) : 0f);
                var x = _source.X + xf * dx * 0.5f;
                var y = _source.Y - Vy * t + 400f * t * t;
                Position = new Vector2(x, y);
                if (_tick >= _tEnd)
                {
                    if (_source.Y < _ground.Y) { _state = 2; _tick = 0; }
                    else { _state = 3; _tick = 0; Position = _ground; }
                }
                break;
            }
            case 2:   // straight fall the rest of the way to the ground
            {
                _tick += dtMs;
                var y = _source.Y + (_tick / 1000f) * Vy;
                if (y >= _ground.Y) { _state = 3; Position = _ground; }
                else Position = new Vector2(_ground.X, y);
                break;
            }
            default:  // 3 — resting: gentle sine bob
                _angle += MathF.PI * dt;   // ~0.5 Hz
                Position = new Vector2(_ground.X, _ground.Y + MathF.Sin(_angle) * 3f);
                break;
        }
    }

    public void Draw(SpriteBatch sb, Texture2D white, Vector2 screenPos)
    {
        var a = (byte)(_alpha * 255f);   // fade during the pick-up absorb
        if (!IsMoney && _icon is not null)
        {
            // Real item icon, anchored by its bottom-centre to the drop point so it sits ON the foothold
            // (CDropPool offsets the layer up by height/2 to the same effect).
            sb.Draw(_icon.Texture, screenPos, null, new Color((byte)255, (byte)255, (byte)255, a), 0f,
                new Vector2(_icon.Width / 2f, _icon.Height), 1f, SpriteEffects.None, 0f);
            return;
        }

        if (IsMoney)
        {
            var coinColor = MesoColors[0].color;
            foreach (var (min, col) in MesoColors)
                if (ItemIdOrAmount >= min) coinColor = col;
            DrawCoin(sb, white, (int)screenPos.X, (int)screenPos.Y, new Color(coinColor.R, coinColor.G, coinColor.B, a));
            return;
        }

        // Item with no resolved icon → small neutral placeholder.
        sb.Draw(white, new Rectangle((int)screenPos.X - 8, (int)screenPos.Y - 8, 16, 16),
            new Color((byte)150, (byte)150, (byte)150, (byte)(_alpha * 210f)));
    }

    private static void DrawCoin(SpriteBatch sb, Texture2D white, int cx, int cy, Color color)
    {
        const int r = 7;
        for (var dy = -r; dy <= r; dy++)
        {
            var dx = (int)Math.Sqrt(Math.Max(0, r * r - dy * dy));
            sb.Draw(white, new Rectangle(cx - dx, cy + dy, 2 * dx + 1, 1), color);
        }
        sb.Draw(white, new Rectangle(cx - 2, cy - 3, 3, 2), new Color(255, 255, 255, 160));
    }
}
