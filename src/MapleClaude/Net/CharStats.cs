namespace MapleClaude.Net;

/// <summary>
/// Accumulated character-stat snapshot decoded from server packets
/// (<see cref="GamePacketHandler"/> reads <c>StatChanged(30)</c> bursts and
/// merges them into one of these). Pure POD; UI panels read from it.
///
/// All fields default to zero / <see cref="string.Empty"/>; partial updates
/// preserve the prior value for any stat whose mask bit wasn't set in the
/// inbound packet.
/// </summary>
public sealed class CharStats
{
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public int JobId { get; set; }
    public int Str { get; set; }
    public int Dex { get; set; }
    public int Int { get; set; }
    public int Luk { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Mp { get; set; }
    public int MaxMp { get; set; }
    public int Exp { get; set; }
    public int AP { get; set; }
    public int SP { get; set; }
}
