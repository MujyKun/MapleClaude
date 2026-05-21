using MapleClaude.Net.Packet;
using Microsoft.Extensions.Logging;

namespace MapleClaude.Net.Session;

/// <summary>
/// Dispatches a decoded <see cref="InPacket"/> to the handler registered for
/// its leading opcode. Unknown opcodes are logged and skipped — the
/// connection survives so we don't die on every Kinoko-side opcode we
/// haven't ported yet.
/// </summary>
public sealed class PacketRouter
{
    private readonly ILogger<PacketRouter> _logger;
    private readonly Dictionary<short, IPacketHandler> _handlers = new();
    private readonly object _lock = new();

    public PacketRouter(ILogger<PacketRouter> logger)
    {
        _logger = logger;
    }

    public void Register(OutHeader header, IPacketHandler handler)
        => Register((short)header, handler);

    public void Register(OutHeader header, Action<InPacket, ClientSession> handler)
        => Register((short)header, new DelegatePacketHandler(handler));

    public void Register(short opcode, IPacketHandler handler)
    {
        lock (_lock)
        {
            _handlers[opcode] = handler;
        }
    }

    public void Unregister(OutHeader header)
    {
        lock (_lock)
        {
            _handlers.Remove((short)header);
        }
    }

    public void Dispatch(InPacket packet, ClientSession session)
    {
        var op = packet.ReadShort();
        IPacketHandler? handler;
        lock (_lock)
        {
            _handlers.TryGetValue(op, out handler);
        }
        if (handler is null)
        {
            var name = Enum.IsDefined(typeof(OutHeader), op) ? ((OutHeader)op).ToString() : "unknown";
            _logger.LogDebug("Unhandled opcode {Op} ({Name}) len={Len}", op, name, packet.Length);
            return;
        }
        try
        {
            handler.Handle(packet, session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Handler for opcode {Op} threw", op);
        }
    }
}
