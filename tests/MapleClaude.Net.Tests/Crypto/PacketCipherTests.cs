using FluentAssertions;
using MapleClaude.Net.Crypto;
using Xunit;

namespace MapleClaude.Net.Tests.Crypto;

public class PacketCipherTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(100)]
    [InlineData(1456)]
    [InlineData(2000)]
    public void BuildHeader_xors_payload_length_with_send_sentinel(int payloadLen)
    {
        // The client SEND sentinel is GAME_VERSION (95); the server's receiver
        // XORs the header against its own IV[2..3] and expects the result to
        // equal GAME_VERSION. Round-trip via the server-side decode formula.
        const int gameVersion = 95;
        byte[] iv = [0x11, 0x22, 0x33, 0x44];
        Span<byte> header = stackalloc byte[PacketCipher.HeaderSize];

        PacketCipher.BuildHeader(payloadLen, iv, header);

        var serverVersion = ((header[0] ^ iv[2]) & 0xFF) | (((header[1] ^ iv[3]) & 0xFF) << 8);
        serverVersion.Should().Be(gameVersion,
            "the server's decoder expects the XOR'd header word to equal GAME_VERSION (kinoko PacketDecoder)");

        var serverLen = ((header[0] ^ header[2]) & 0xFF) | (((header[1] ^ header[3]) & 0xFF) << 8);
        serverLen.Should().Be(payloadLen);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(100)]
    [InlineData(1456)]
    [InlineData(2000)]
    public void ParseHeader_decodes_server_built_header(int payloadLen)
    {
        // Synthesize a header the way the server's PacketEncoder does: rawSeq =
        // (iv[2] | iv[3]<<8) ^ (0xFFFF - GAME_VERSION); dataLen = len ^ rawSeq.
        const int sendVersion = 0xFFFF - 95;
        byte[] iv = [0x11, 0x22, 0x33, 0x44];
        var rawSeq = ((iv[2] & 0xFF) | ((iv[3] & 0xFF) << 8)) ^ sendVersion;
        var dataLen = payloadLen ^ rawSeq;
        var header = new byte[PacketCipher.HeaderSize];
        header[0] = (byte)(rawSeq & 0xFF);
        header[1] = (byte)((rawSeq >> 8) & 0xFF);
        header[2] = (byte)(dataLen & 0xFF);
        header[3] = (byte)((dataLen >> 8) & 0xFF);

        var (valid, parsedLen) = PacketCipher.ParseHeader(header, iv);

        valid.Should().BeTrue();
        parsedLen.Should().Be(payloadLen);
    }

    [Fact]
    public void ParseHeader_rejects_mismatched_iv()
    {
        // Build a server-shaped header against one IV, then try to parse against another.
        const int sendVersion = 0xFFFF - 95;
        byte[] iv = [0x11, 0x22, 0x33, 0x44];
        byte[] otherIv = [0x99, 0xAA, 0xBB, 0xCC];
        var rawSeq = ((iv[2] & 0xFF) | ((iv[3] & 0xFF) << 8)) ^ sendVersion;
        var dataLen = 100 ^ rawSeq;
        var header = new byte[]
        {
            (byte)(rawSeq & 0xFF),
            (byte)((rawSeq >> 8) & 0xFF),
            (byte)(dataLen & 0xFF),
            (byte)((dataLen >> 8) & 0xFF),
        };

        var (valid, _) = PacketCipher.ParseHeader(header, otherIv);

        valid.Should().BeFalse(
            "the parse decodes the version from header XOR iv; mismatched IV means wrong version");
    }

    [Fact]
    public void ParseHeader_rejects_short_input()
    {
        byte[] iv = [0x11, 0x22, 0x33, 0x44];
        byte[] shortHeader = [1, 2, 3];

        var (valid, _) = PacketCipher.ParseHeader(shortHeader, iv);
        valid.Should().BeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(100)]
    [InlineData(1500)]
    [InlineData(4096)]
    public void Encrypt_then_Decrypt_round_trips_with_synced_ivs(int length)
    {
        byte[] sendIv = [0xAB, 0xCD, 0xEF, 0x01];
        byte[] recvIv = (byte[])sendIv.Clone();
        var original = NewDeterministicBuffer(length, seed: length);
        var wire = (byte[])original.Clone();

        PacketCipher.EncryptBody(wire, sendIv);
        PacketCipher.DecryptBody(wire, recvIv);

        wire.Should().Equal(original,
            "Encrypt+Decrypt with paired IV state must restore the original bytes");
    }

    [Fact]
    public void EncryptBody_advances_iv()
    {
        byte[] iv = [0xAB, 0xCD, 0xEF, 0x01];
        var ivBefore = (byte[])iv.Clone();
        var data = NewDeterministicBuffer(50, seed: 7);

        PacketCipher.EncryptBody(data, iv);

        iv.Should().NotEqual(ivBefore,
            "EncryptBody must call InnoHash to rotate the IV in place");
    }

    [Fact]
    public void DecryptBody_advances_iv()
    {
        byte[] iv = [0xAB, 0xCD, 0xEF, 0x01];
        var ivBefore = (byte[])iv.Clone();
        var data = NewDeterministicBuffer(50, seed: 7);

        PacketCipher.DecryptBody(data, iv);

        iv.Should().NotEqual(ivBefore);
    }

    [Fact]
    public void Two_sequential_packets_keep_ivs_in_sync()
    {
        byte[] sendIv = [0xAB, 0xCD, 0xEF, 0x01];
        byte[] recvIv = (byte[])sendIv.Clone();
        var packet1 = NewDeterministicBuffer(100, seed: 1);
        var packet2 = NewDeterministicBuffer(200, seed: 2);

        var wire1 = (byte[])packet1.Clone();
        var wire2 = (byte[])packet2.Clone();

        PacketCipher.EncryptBody(wire1, sendIv);
        PacketCipher.EncryptBody(wire2, sendIv);

        PacketCipher.DecryptBody(wire1, recvIv);
        PacketCipher.DecryptBody(wire2, recvIv);

        wire1.Should().Equal(packet1, "first packet survives the round-trip");
        wire2.Should().Equal(packet2, "second packet survives — proves IV rotation stays in sync");
        sendIv.Should().Equal(recvIv, "IVs converge to the same state after equal traffic");
    }

    private static byte[] NewDeterministicBuffer(int length, int seed)
    {
        var rng = new Random(seed);
        var buf = new byte[length];
        rng.NextBytes(buf);
        return buf;
    }
}
