using FluentAssertions;
using MapleClaude.Net.Packet;
using Xunit;

namespace MapleClaude.Net.Tests.Packet;

public class MovePathEncoderTests
{
    [Fact]
    public void NormalElement_EncodesExpectedShape()
    {
        var elems = new[]
        {
            new MoveElement
            {
                Attr = 0,
                X = 100, Y = 200,
                Vx = 140, Vy = 0,
                Fh = 5,
                XOffset = 0, YOffset = 0,
                MoveAction = 0,
                Elapse = 100,
            },
        };
        var blob = MovePathEncoder.Encode(80, 200, 140, 0, elems);

        var r = new InPacket(blob);
        r.ReadShort().Should().Be(80);
        r.ReadShort().Should().Be(200);
        r.ReadShort().Should().Be(140);
        r.ReadShort().Should().Be(0);
        r.ReadByte().Should().Be(1);
        r.ReadByte().Should().Be(0);       // attr
        r.ReadShort().Should().Be(100);    // x
        r.ReadShort().Should().Be(200);    // y
        r.ReadShort().Should().Be(140);    // vx
        r.ReadShort().Should().Be(0);      // vy
        r.ReadShort().Should().Be(5);      // fh
        r.ReadShort().Should().Be(0);      // xOff
        r.ReadShort().Should().Be(0);      // yOff
        r.ReadByte().Should().Be(0);       // moveAction
        r.ReadShort().Should().Be(100);    // elapse
        r.Remaining.Should().Be(0);
    }

    [Fact]
    public void JumpElement_OnlyEncodesVelocity()
    {
        var elems = new[]
        {
            new MoveElement { Attr = 1, Vx = 50, Vy = -200, MoveAction = 0, Elapse = 50 },
        };
        var blob = MovePathEncoder.Encode(0, 0, 0, 0, elems);
        var r = new InPacket(blob);
        r.Skip(8 + 1 + 1);
        r.ReadShort().Should().Be(50);
        r.ReadShort().Should().Be(-200);
        r.ReadByte().Should().Be(0);
        r.ReadShort().Should().Be(50);
        r.Remaining.Should().Be(0);
    }

    [Fact]
    public void StatChangeElement_SkipsMoveActionAndElapse()
    {
        var elems = new[]
        {
            new MoveElement { Attr = 9, Stat = 7 },
        };
        var blob = MovePathEncoder.Encode(0, 0, 0, 0, elems);
        var r = new InPacket(blob);
        r.Skip(8 + 1);
        r.ReadByte().Should().Be(9);
        r.ReadByte().Should().Be(7);
        r.Remaining.Should().Be(0);
    }

    [Fact]
    public void TeleportElement_EncodesXYFh()
    {
        var elems = new[]
        {
            new MoveElement { Attr = 3, X = 50, Y = 100, Fh = 9, MoveAction = 2, Elapse = 30 },
        };
        var blob = MovePathEncoder.Encode(0, 0, 0, 0, elems);
        var r = new InPacket(blob);
        r.Skip(8 + 1 + 1);
        r.ReadShort().Should().Be(50);
        r.ReadShort().Should().Be(100);
        r.ReadShort().Should().Be(9);
        r.ReadByte().Should().Be(2);
        r.ReadShort().Should().Be(30);
        r.Remaining.Should().Be(0);
    }
}
