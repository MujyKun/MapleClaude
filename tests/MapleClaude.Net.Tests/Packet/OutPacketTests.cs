using FluentAssertions;
using MapleClaude.Net.Packet;
using Xunit;

namespace MapleClaude.Net.Tests.Packet;

public class OutPacketTests
{
    [Fact]
    public void OfHeader_WritesOpcodeLittleEndian()
    {
        var p = OutPacket.Of(InHeader.CheckPassword);
        var bytes = p.ToArray();
        bytes.Should().HaveCount(2);
        bytes[0].Should().Be(0x01);
        bytes[1].Should().Be(0x00);
    }

    [Fact]
    public void Primitives_WriteLittleEndian()
    {
        var p = OutPacket.Raw();
        p.WriteByte((byte)0xAB);
        p.WriteShort((short)0x1234);
        p.WriteInt(unchecked((int)0xDEADBEEF));
        p.WriteLong(0x0102030405060708L);
        var bytes = p.ToArray();
        bytes.Should().Equal(new byte[]
        {
            0xAB,
            0x34, 0x12,
            0xEF, 0xBE, 0xAD, 0xDE,
            0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01,
        });
    }

    [Fact]
    public void WriteString_LengthPrefixedAscii()
    {
        var p = OutPacket.Raw();
        p.WriteString("admin");
        var bytes = p.ToArray();
        bytes.Should().Equal(new byte[] { 0x05, 0x00, (byte)'a', (byte)'d', (byte)'m', (byte)'i', (byte)'n' });
    }

    [Fact]
    public void WriteString_Fixed_NullPads()
    {
        var p = OutPacket.Raw();
        p.WriteString("Hi", 5);
        var bytes = p.ToArray();
        bytes.Should().Equal(new byte[] { (byte)'H', (byte)'i', 0x00, 0x00, 0x00 });
    }

    [Fact]
    public void WriteString_Fixed_Truncates()
    {
        var p = OutPacket.Raw();
        p.WriteString("HelloWorld", 5);
        var bytes = p.ToArray();
        bytes.Should().Equal(new byte[] { (byte)'H', (byte)'e', (byte)'l', (byte)'l', (byte)'o' });
    }

    [Fact]
    public void WriteString_Null_TreatedAsEmpty()
    {
        var p = OutPacket.Raw();
        p.WriteString(null);
        var bytes = p.ToArray();
        bytes.Should().Equal(new byte[] { 0x00, 0x00 });
    }

    [Fact]
    public void Grow_HandlesLargePayloads()
    {
        var p = OutPacket.Raw(8);
        for (var i = 0; i < 1024; i++)
        {
            p.WriteInt(i);
        }
        p.Size.Should().Be(4096);
        var bytes = p.ToArray();
        bytes.Should().HaveCount(4096);
        bytes[0].Should().Be(0);
        bytes[4].Should().Be(1);
        bytes[4092].Should().Be(0xFF);
        bytes[4093].Should().Be(0x03);
    }
}
