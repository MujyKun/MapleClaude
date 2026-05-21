namespace MapleClaude.Domain;

/// <summary>
/// Decoded character stats matching upstream Kinoko's
/// <c>CharacterStat.encode</c>. Note the <c>SP</c> field is encoded as
/// either a single short (non-extend jobs) or an extended (count + array of
/// {jobLevel, sp}) blob — for the client side we just preserve the raw byte
/// blob plus a single fallback short for non-extend jobs.
/// </summary>
public sealed class CharacterStat
{
    public int CharacterId { get; set; }
    /// <summary>13-byte ASCII string (null-terminated within the fixed buffer).</summary>
    public string Name { get; set; } = string.Empty;
    public byte Gender { get; set; }
    public byte Skin { get; set; }
    public int Face { get; set; }
    public int Hair { get; set; }
    public long PetSn1 { get; set; }
    public long PetSn2 { get; set; }
    public long PetSn3 { get; set; }
    public byte Level { get; set; }
    public short Job { get; set; }
    public short Str { get; set; }
    public short Dex { get; set; }
    public short Int { get; set; }
    public short Luk { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Mp { get; set; }
    public int MaxMp { get; set; }
    public short Ap { get; set; }
    /// <summary>For non-extend jobs: a single short. For extend-SP jobs: a {byte count, {byte jobLevel, byte sp}[count]} blob.</summary>
    public byte[] SpRaw { get; set; } = Array.Empty<byte>();
    public int Exp { get; set; }
    public short Pop { get; set; }
    public int TempExp { get; set; }
    public int PosMap { get; set; }
    public byte Portal { get; set; }
    public int PlayTime { get; set; }
    public short SubJob { get; set; }
}
