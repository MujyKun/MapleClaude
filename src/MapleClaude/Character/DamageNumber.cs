using MapleClaude.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MapleClaude.Character;

/// <summary>
/// Manages a pool of floating damage / heal numbers above the game world.
/// Each entry rises for 0.7 s then fades for 0.3 s.
/// Caller adds entries via <see cref="Add"/>; <see cref="Update"/> and
/// <see cref="Draw"/> are called each frame from <see cref="Stages.GameStage"/>.
/// </summary>
public sealed class DamageNumber
{
    public enum Kind
    {
        DamageNormal,   // white
        DamageCrit,     // yellow
        DamageMiss,     // grey   "MISS"
        HealHp,         // green
        HealMp,         // cyan
        MobDamage,      // red (mob hit by player — shown above mob)
        Exp,            // yellow-green  "+N EXP"
    }

    private static readonly Color[] KindColors =
    [
        Color.White,
        new Color(255, 220, 40),
        new Color(160, 160, 160),
        new Color(80,  220, 80),
        new Color(80,  200, 255),
        new Color(255, 60,  60),
        new Color(120, 255, 80),
    ];

    private sealed class Entry
    {
        public string  Text;
        public Color   Color;
        public Vector2 WorldPos;
        public float   Age;
        public float   Vy;          // upward velocity (world units/s)
        public Entry(string t, Color c, Vector2 p) { Text = t; Color = c; WorldPos = p; Vy = -80f; }
    }

    private const float RiseDuration = 0.7f;
    private const float FadeDuration = 0.3f;
    private const float TotalLife    = RiseDuration + FadeDuration;

    private readonly List<Entry> _entries = new();
    private readonly BuiltInFont? _font;

    public DamageNumber(BuiltInFont? font) => _font = font;

    // ── API ───────────────────────────────────────────────────────────────────

    public void Add(int value, Vector2 worldPos, Kind kind = Kind.MobDamage)
    {
        var text = kind switch
        {
            Kind.DamageMiss  => "MISS",
            Kind.HealHp      => $"+{value:N0}",
            Kind.HealMp      => $"+{value:N0}",
            Kind.Exp         => $"+{value:N0} EXP",
            _                => value.ToString("N0"),
        };
        // Slight random horizontal spread so stacked numbers don't overlap
        var spread = (float)(System.Random.Shared.NextDouble() * 20 - 10);
        _entries.Add(new Entry(text, KindColors[(int)kind], worldPos + new Vector2(spread, 0)));
    }

    public void AddMiss(Vector2 worldPos) => Add(0, worldPos, Kind.DamageMiss);

    // ── Update ────────────────────────────────────────────────────────────────

    public void Update(float dt)
    {
        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            var e = _entries[i];
            e.Age           += dt;
            e.WorldPos      += new Vector2(0, e.Vy * dt);
            e.Vy            = Math.Min(0, e.Vy + 40f * dt);  // decelerate
            if (e.Age >= TotalLife) _entries.RemoveAt(i);
        }
    }

    // ── Draw ──────────────────────────────────────────────────────────────────

    public void Draw(SpriteBatch sb, Texture2D white,
                     Func<Vector2, Vector2> worldToScreen)
    {
        if (_font is null) return;
        foreach (var e in _entries)
        {
            var fade = e.Age >= RiseDuration
                ? 1f - (e.Age - RiseDuration) / FadeDuration
                : 1f;
            var alpha = (byte)(255 * Math.Clamp(fade, 0f, 1f));
            var color = e.Color with { A = alpha };
            var screenPos = worldToScreen(e.WorldPos);
            var sz        = _font.Measure(e.Text);
            _font.Draw(sb, e.Text,
                new Vector2(screenPos.X - sz.X / 2f, screenPos.Y - sz.Y),
                color);
        }
    }
}
