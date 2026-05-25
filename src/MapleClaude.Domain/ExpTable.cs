namespace MapleClaude.Domain;

/// <summary>
/// Per-level required-EXP table (the client-side <c>NEXTLEVEL</c> data). The
/// server only sends the character's current EXP, so the Stat window computes
/// the "exp (pct%)" line against this table locally — exactly like the original
/// v95 client's <c>get_next_level_exp(level)</c>.
///
/// Generated from the same piecewise growth the Kinoko server uses
/// (<c>GameConstants.initializeExpTable</c>): hand-set values for levels 1..9,
/// plateaus at 9/29/69/119, then ×1.2 / ×1.08 / ×1.07 / ×1.06 bands up to 199.
/// Level 200 (and beyond) is the cap → 0.
/// </summary>
public static class ExpTable
{
    public const int LevelMax = 200;

    private static readonly int[] Next = Build();

    /// <summary>
    /// EXP required to advance from <paramref name="level"/> to the next level.
    /// Returns 0 at or above the level cap (and for out-of-range input).
    /// </summary>
    public static int GetNextLevelExp(int level)
    {
        if (level < 1 || level > LevelMax)
        {
            return 0;
        }
        return Next[level];
    }

    private static int[] Build()
    {
        // Mirrors kinoko/world/GameConstants.initializeExpTable() verbatim.
        var n = new int[LevelMax + 1];
        n[1] = 15;
        n[2] = 34;
        n[3] = 57;
        n[4] = 92;
        n[5] = 135;
        n[6] = 372;
        n[7] = 560;
        n[8] = 840;
        n[9] = 1242;
        n[10] = n[9];
        n[11] = n[9];
        n[12] = n[9];
        n[13] = n[9];
        n[14] = n[9];
        for (var i = 15; i <= 29; i++)
        {
            n[i] = (int)(n[i - 1] * 1.2 + 0.5);
        }
        n[30] = n[29];
        n[31] = n[29];
        n[32] = n[29];
        n[33] = n[29];
        n[34] = n[29];
        for (var i = 35; i <= 39; i++)
        {
            n[i] = (int)(n[i - 1] * 1.2 + 0.5);
        }
        for (var i = 40; i <= 69; i++)
        {
            n[i] = (int)(n[i - 1] * 1.08 + 0.5);
        }
        n[70] = n[69];
        n[71] = n[69];
        n[72] = n[69];
        n[73] = n[69];
        n[74] = n[69];
        for (var i = 75; i <= 119; i++)
        {
            n[i] = (int)(n[i - 1] * 1.07 + 0.5);
        }
        n[120] = n[119];
        n[121] = n[119];
        n[122] = n[119];
        n[123] = n[119];
        n[124] = n[119];
        for (var i = 125; i <= 159; i++)
        {
            n[i] = (int)(n[i - 1] * 1.07 + 0.5);
        }
        for (var i = 160; i <= 199; i++)
        {
            n[i] = (int)(n[i - 1] * 1.06 + 0.5);
        }
        n[200] = 0;
        return n;
    }
}
