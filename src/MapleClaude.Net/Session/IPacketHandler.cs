using MapleClaude.Net.Packet;

namespace MapleClaude.Net.Session;

/// <summary>Per-opcode handler invoked by <see cref="PacketRouter"/>.</summary>
public interface IPacketHandler
{
    void Handle(InPacket packet, ClientSession session);
}

/// <summary>Inline-lambda helper for stages that just want to subscribe.</summary>
public sealed class DelegatePacketHandler : IPacketHandler
{
    private readonly Action<InPacket, ClientSession> _handler;
    public DelegatePacketHandler(Action<InPacket, ClientSession> handler)
    {
        _handler = handler;
    }
    public void Handle(InPacket packet, ClientSession session) => _handler(packet, session);
}
