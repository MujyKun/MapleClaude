using FluentAssertions;
using MapleClaude.Net.Packet;
using MapleClaude.Net.Session;
using Xunit;

namespace MapleClaude.Net.Tests.Session;

public class HandshakeReaderTests
{
    [Fact]
    public void ReadsConnectPacketFromKinoko()
    {
        // Build the same wire shape the server sends in LoginPacket.connect():
        // [length:short(LE)|version=95|patch="1"|sendIv(4)|recvIv(4)|locale=8]
        var body = OutPacket.Raw();
        body.WriteShort((short)95);
        body.WriteString("1");
        body.WriteBytes(new byte[] { 0x46, 0x72, 0x00, 0x52 });
        body.WriteBytes(new byte[] { 0xC8, 0x05, 0x05, 0x53 });
        body.WriteByte((byte)8);
        var bodyBytes = body.ToArray();

        var wire = OutPacket.Raw();
        wire.WriteShort((short)bodyBytes.Length);
        wire.WriteBytes(bodyBytes);
        var bytes = wire.ToArray();

        HandshakeReader.TryRead(bytes, out var info, out var consumed).Should().BeTrue();
        consumed.Should().Be(bytes.Length);
        info.Version.Should().Be((short)95);
        info.Patch.Should().Be("1");
        info.SendIv.Should().Equal(new byte[] { 0x46, 0x72, 0x00, 0x52 });
        info.RecvIv.Should().Equal(new byte[] { 0xC8, 0x05, 0x05, 0x53 });
        info.Locale.Should().Be((byte)8);
    }

    [Fact]
    public void NotEnoughBytes_ReturnsFalse()
    {
        HandshakeReader.TryRead(new byte[] { 0x05 }, out _, out var consumed).Should().BeFalse();
        consumed.Should().Be(0);
    }

    [Fact]
    public void WrongVersion_Throws()
    {
        var body = OutPacket.Raw();
        body.WriteShort((short)83);
        body.WriteString("1");
        body.WriteBytes(new byte[4]);
        body.WriteBytes(new byte[4]);
        body.WriteByte((byte)8);
        var bodyBytes = body.ToArray();

        var wire = OutPacket.Raw();
        wire.WriteShort((short)bodyBytes.Length);
        wire.WriteBytes(bodyBytes);
        var bytes = wire.ToArray();

        var act = () => HandshakeReader.TryRead(bytes, out _, out _);
        act.Should().Throw<InvalidDataException>();
    }
}
