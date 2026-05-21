using FluentAssertions;
using MapleClaude.Net.Packet;
using Xunit;

namespace MapleClaude.Net.Tests.Packet;

public class InPacketTests
{
    [Fact]
    public void RoundTrip_Primitives()
    {
        var p = OutPacket.Raw();
        p.WriteByte((byte)0xAB);
        p.WriteShort((short)0x1234);
        p.WriteInt(unchecked((int)0xDEADBEEF));
        p.WriteLong(0x0102030405060708L);
        p.WriteFloat(1.5f);
        p.WriteDouble(-2.25);

        var r = new InPacket(p.ToArray());
        r.ReadByte().Should().Be(0xAB);
        r.ReadShort().Should().Be((short)0x1234);
        r.ReadInt().Should().Be(unchecked((int)0xDEADBEEF));
        r.ReadLong().Should().Be(0x0102030405060708L);
        r.ReadFloat().Should().Be(1.5f);
        r.ReadDouble().Should().Be(-2.25);
        r.Remaining.Should().Be(0);
    }

    [Fact]
    public void RoundTrip_String()
    {
        var p = OutPacket.Raw();
        p.WriteString("admin");
        p.WriteString("");
        p.WriteString("aaa", 5);
        var r = new InPacket(p.ToArray());
        r.ReadString().Should().Be("admin");
        r.ReadString().Should().Be("");
        r.ReadString(5).Should().Be("aaa");
    }

    [Fact]
    public void RoundTrip_StringWithExactLengthAtCapacity()
    {
        var p = OutPacket.Raw();
        p.WriteString("ABCDE", 5);
        var r = new InPacket(p.ToArray());
        r.ReadString(5).Should().Be("ABCDE");
    }

    [Fact]
    public void RoundTrip_BoolAndBytes()
    {
        var p = OutPacket.Raw();
        p.WriteByte(true);
        p.WriteByte(false);
        p.WriteBytes(new byte[] { 0x01, 0x02, 0x03, 0x04 });
        var r = new InPacket(p.ToArray());
        r.ReadBool().Should().BeTrue();
        r.ReadBool().Should().BeFalse();
        r.ReadBytes(4).Should().Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 });
    }

    [Fact]
    public void Peek_DoesNotAdvanceCursor()
    {
        var p = OutPacket.Of(InHeader.CheckPassword);
        var r = new InPacket(p.ToArray());
        r.PeekShort().Should().Be((short)InHeader.CheckPassword);
        r.PeekShort().Should().Be((short)InHeader.CheckPassword);
        r.Position.Should().Be(0);
        r.ReadShort().Should().Be((short)InHeader.CheckPassword);
    }

    [Fact]
    public void Read_PastEnd_Throws()
    {
        var r = new InPacket(new byte[] { 0x01 });
        r.ReadByte().Should().Be(0x01);
        var act = () => r.ReadByte();
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Skip_AdvancesCursor()
    {
        var p = OutPacket.Raw();
        p.WriteBytes(new byte[] { 0x01, 0x02, 0x03, 0x04 });
        var r = new InPacket(p.ToArray());
        r.Skip(2);
        r.ReadByte().Should().Be(0x03);
    }

    [Fact]
    public void ReadString_FixedLength_StopsAtFirstNul()
    {
        var bytes = new byte[] { (byte)'A', (byte)'B', 0x00, (byte)'X', (byte)'X' };
        var r = new InPacket(bytes);
        r.ReadString(5).Should().Be("AB");
    }
}
