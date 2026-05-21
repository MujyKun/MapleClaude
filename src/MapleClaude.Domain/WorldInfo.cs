namespace MapleClaude.Domain;

/// <summary>
/// One world entry from a <c>WorldInformation(10)</c> packet. The terminator
/// packet has <see cref="WorldId"/> == -1 and no other fields.
/// </summary>
public sealed class WorldInfo
{
    public sbyte WorldId { get; set; }
    public string Name { get; set; } = string.Empty;
    public byte State { get; set; }
    public string EventDescription { get; set; } = string.Empty;
    public short EventExpRate { get; set; } = 100;
    public short EventDropRate { get; set; } = 100;
    public byte BlockCharCreation { get; set; }
    public List<ChannelInfo> Channels { get; } = new();
    public short BalloonCount { get; set; }
}
