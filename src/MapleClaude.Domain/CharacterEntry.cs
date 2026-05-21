namespace MapleClaude.Domain;

/// <summary>
/// One row in the character-select list. Mirrors a single <c>AvatarData</c>
/// entry plus the per-character family/rank trailer added by
/// <c>SelectWorldResult(11)</c>.
/// </summary>
public sealed class CharacterEntry
{
    public required CharacterStat Stat { get; init; }
    public required AvatarLook Look { get; init; }
    public bool OnFamily { get; set; }
    public CharacterRank? Rank { get; set; }
}

/// <summary>4 short ranking values present when the character has a server-side rank.</summary>
public sealed class CharacterRank
{
    public int WorldRank { get; set; }
    public int WorldRankMove { get; set; }
    public int JobRank { get; set; }
    public int JobRankMove { get; set; }
}
