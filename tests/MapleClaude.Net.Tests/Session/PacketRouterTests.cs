using FluentAssertions;
using MapleClaude.Net.Packet;
using MapleClaude.Net.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MapleClaude.Net.Tests.Session;

public class PacketRouterTests
{
    private static InPacket BuildPacket(OutHeader op, params byte[] body)
    {
        var p = OutPacket.Of((short)op);
        p.WriteBytes(body);
        return new InPacket(p.ToArray());
    }

    [Fact]
    public void Dispatch_InvokesRegisteredHandler()
    {
        var router = new PacketRouter(NullLogger<PacketRouter>.Instance);
        var captured = (short)0;
        router.Register(OutHeader.CheckPasswordResult, (pkt, _) =>
        {
            captured = pkt.ReadByte();
        });

        router.Dispatch(BuildPacket(OutHeader.CheckPasswordResult, 0x07), session: null!);
        captured.Should().Be(0x07);
    }

    [Fact]
    public void Dispatch_UnknownOpcode_DoesNotThrow()
    {
        var router = new PacketRouter(NullLogger<PacketRouter>.Instance);
        var act = () => router.Dispatch(BuildPacket((OutHeader)9999), session: null!);
        act.Should().NotThrow();
    }

    [Fact]
    public void Unregister_RemovesHandler()
    {
        var router = new PacketRouter(NullLogger<PacketRouter>.Instance);
        var fired = false;
        router.Register(OutHeader.AliveReq, (_, _) => fired = true);
        router.Unregister(OutHeader.AliveReq);
        router.Dispatch(BuildPacket(OutHeader.AliveReq), session: null!);
        fired.Should().BeFalse();
    }
}
