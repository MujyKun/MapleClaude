namespace MapleClaude.Domain;

/// <summary>One channel entry inside a <see cref="WorldInfo"/>.</summary>
public sealed class ChannelInfo
{
    public string Name { get; set; } = string.Empty;
    public int UserCount { get; set; }
    public byte WorldId { get; set; }
    public byte ChannelId { get; set; }
    public bool Adult { get; set; }
}
