namespace MapleClaude.Net.Packet;

/// <summary>
/// Client-side melee damage estimate. The v95 Kinoko server trusts the damage
/// values the client sends (it does not recompute them), so this produces
/// believable numbers that scale with the character rather than a flat random
/// range. It follows the shape of the classic MapleStory physical formula —
/// <c>(primaryStat * multiplier + secondaryStat) * weaponAttack / 100</c> — but
/// is deliberately an <em>estimate</em>: true byte-accuracy would need the
/// equipped weapon's attack and class multiplier (Item.wz) and skill mastery,
/// which aren't wired yet (see the combat-depth note in docs/roadmap.md).
/// </summary>
public static class MeleeDamage
{
    /// <summary>
    /// A [min, max] physical-damage window for one basic melee hit, derived from
    /// the character's job (which stat is primary), level, and stats.
    /// </summary>
    public static (int Min, int Max) Estimate(int jobId, int level, int str, int dex, int @int, int luk)
    {
        var (primary, secondary) = StatsForJob(jobId, str, dex, @int, luk);

        // Weapon attack isn't available from the wire stat block, so estimate it
        // from level (a real equipped-weapon attack would replace this term).
        var weaponAttack = 8.0 + level * 1.3;

        // 1H-weapon-class multiplier as a baseline; real multipliers vary by
        // weapon type (2H sword 4.6, spear/pole-arm 3.0/5.0, …).
        const double multiplier = 4.0;

        var max = (int)((primary * multiplier + secondary) * weaponAttack / 100.0);
        if (max < 1) max = 1;
        // Basic attacks have no mastery, so the spread is wide but floored so the
        // numbers stay readable.
        var min = (int)(max * 0.80);
        if (min < 1) min = 1;
        return (min, max);
    }

    private static (int Primary, int Secondary) StatsForJob(int jobId, int str, int dex, int @int, int luk) =>
        (jobId / 100) switch
        {
            2 => (@int, luk),   // Magician — INT / LUK
            3 => (dex, str),    // Bowman   — DEX / STR
            4 => (luk, dex),    // Thief    — LUK / DEX
            _ => (str, dex),    // Warrior / Pirate / Beginner — STR / DEX
        };
}
