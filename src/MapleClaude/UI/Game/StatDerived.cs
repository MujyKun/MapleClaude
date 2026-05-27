namespace MapleClaude.UI.Game;

/// <summary>
/// Inputs for the derived-stat calculator. All stat fields are <em>totals</em>
/// (base + equip + buff). Today the client only tracks base ability stats, so
/// the optional fields below default to neutral (no weapon, no mastery, no
/// bonuses); the formulas are correct and will tighten automatically as equip /
/// skill data starts flowing in later phases.
/// </summary>
public struct StatInputs
{
    public int JobId;
    public int Str;
    public int Dex;
    public int Int;
    public int Luk;
    public int MaxHp;
    public int MaxMp;

    /// <summary>v95 weapon-type id (see <see cref="StatDerived"/>). 0 = unarmed.</summary>
    public int WeaponType;
    public int Watk;
    public int Matk;
    /// <summary>Weapon mastery, 0..0.95.</summary>
    public float Mastery;

    public int Speed;
    public int Jump;

    // Equip/buff bonuses applied on top of the base-derived values (0 today).
    public int AccBonus;
    public int EvaBonus;
    public int PddBonus;
    public int MddBonus;

    public static StatInputs Default => new() { Speed = 100, Jump = 100 };
}

/// <summary>Output of <see cref="StatDerived.Compute"/> — the values shown in the
/// detailed-stats window (<c>CUIStatDetail</c>).</summary>
public readonly record struct DerivedStats(
    int MinDamage,
    int MaxDamage,
    int Accuracy,
    int Avoidability,
    int Pdd,
    int Mdd,
    int CriticalPercent,
    int Speed,
    int Jump);

/// <summary>
/// Client-side derived-stat formulas for the character detail window. Mirrors the
/// v95 structure: damage range from primary/secondary stat × weapon multiplier ×
/// total attack × mastery; accuracy from DEX/LUK. Physical and magic branches are
/// chosen by job. Defense comes entirely from equipment, so base values are 0.
///
/// Coefficients for damage and accuracy are the concrete reference-client values;
/// avoidability uses the standard GMS display formula (refine against the original
/// client's <c>CUIStatDetail::Draw</c> when finer fidelity is needed).
/// </summary>
public static class StatDerived
{
    public const int DamageMax = 999_999;

    public static DerivedStats Compute(in StatInputs s)
    {
        var (primary, secondary, magic) = ResolvePrimarySecondary(s);

        var attack = magic ? s.Matk : s.Watk;
        var mult = WeaponMultiplier(s.WeaponType);
        var mastery = Math.Clamp(s.Mastery, 0f, 0.95f);

        // v83/v95 display formula: damageMultiplier = attack / 100 (no %dmg buff yet).
        var dmgMult = attack / 100f;
        var max = (int)((mult * primary + secondary) * dmgMult);
        var min = (int)((mult * primary * 0.9f * mastery + secondary) * dmgMult);
        max = Math.Clamp(max, 0, DamageMax);
        min = Math.Clamp(Math.Min(min, max), 0, DamageMax);

        var acc = (int)(s.Dex * 0.8f + s.Luk * 0.5f) + s.AccBonus;
        var eva = (int)(s.Dex * 0.25f + s.Luk * 0.25f) + s.EvaBonus;

        var speed = s.Speed <= 0 ? 100 : s.Speed;
        var jump = s.Jump <= 0 ? 100 : s.Jump;

        return new DerivedStats(
            MinDamage: min,
            MaxDamage: max,
            Accuracy: acc,
            Avoidability: eva,
            Pdd: s.PddBonus,
            Mdd: s.MddBonus,
            CriticalPercent: 5,
            Speed: speed,
            Jump: jump);
    }

    /// <summary>Primary/secondary ability stat and physical-vs-magic branch for a
    /// job. Category = the hundreds digit (0 beginner, 1 warrior, 2 magician,
    /// 3 bowman, 4 thief, 5 pirate).</summary>
    public static (int primary, int secondary, bool magic) ResolvePrimarySecondary(in StatInputs s)
    {
        var category = (s.JobId / 100) % 10;
        return category switch
        {
            1 => (s.Str, s.Dex, false),  // warrior
            2 => (s.Int, s.Luk, true),   // magician
            3 => (s.Dex, s.Str, false),  // bowman
            4 => (s.Luk, s.Dex, false),  // thief
            5 => (s.Str, s.Dex, false),  // pirate (knuckle; gun → DEX, refine later)
            _ => (s.Str, s.Dex, false),  // beginner
        };
    }

    /// <summary>Primary ability stat flag for auto-assign (matches
    /// <c>GameSender.MapleStat</c>).</summary>
    public static int PrimaryStatFlag(int jobId) => ((jobId / 100) % 10) switch
    {
        1 => 0x40,   // warrior  → STR
        2 => 0x100,  // magician → INT
        3 => 0x80,   // bowman   → DEX
        4 => 0x200,  // thief    → LUK
        5 => 0x40,   // pirate   → STR
        _ => 0x40,   // beginner → STR
    };

    /// <summary>Per-weapon-type damage multiplier (v83 reference values; weapon
    /// types match the standard MapleStory weapon-type ids).</summary>
    private static float WeaponMultiplier(int weaponType) => weaponType switch
    {
        30 => 4.0f,                 // 1H sword
        31 or 32 or 37 or 38 => 4.4f, // 1H axe / 1H mace / wand / staff
        33 or 39 or 47 or 49 => 3.6f, // dagger / claw / crossbow? / gun
        40 => 4.6f,                 // 2H sword
        41 or 42 or 48 => 4.8f,     // 2H axe / 2H mace / knuckle
        43 or 44 => 5.0f,           // spear / polearm
        45 => 3.4f,                 // bow
        46 => 3.6f,                 // crossbow
        _ => 0.0f,                  // unarmed / unknown → no damage shown
    };
}
