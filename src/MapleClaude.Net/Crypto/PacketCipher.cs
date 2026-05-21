using System.Buffers.Binary;

namespace MapleClaude.Net.Crypto;

/// <summary>
/// Public API for the MapleStory v95 packet cipher pipeline. Composes
/// <see cref="ShandaCrypto"/>, <see cref="MapleCrypto"/>, and
/// <see cref="IgCipher"/> in the order the server expects per direction,
/// and handles the 4-byte XOR'd packet framing header.
///
/// Reference: <c>kinoko/server/netty/PacketEncoder.java</c> and
/// <c>PacketDecoder.java</c> in the upstream Kinoko server.
/// </summary>
public static class PacketCipher
{
    /// <summary>The MapleStory client version this cipher targets (v95).</summary>
    public const int GameVersion = 95;

    /// <summary>The 4-byte framing header that precedes each encrypted body.</summary>
    public const int HeaderSize = 4;

    /// <summary>
    /// XOR sentinel the client mixes into outgoing header bytes. Matches
    /// upstream Kinoko's server-side <c>RECV_VERSION = GAME_VERSION</c>
    /// check: after the server XORs the header against its own IV[2..3],
    /// the resulting word must equal <see cref="GameVersion"/> or it drops
    /// the connection (<c>PacketDecoder.decode</c>).
    /// </summary>
    private const int SendSentinel = GameVersion;

    /// <summary>
    /// XOR sentinel the client expects in incoming header bytes. The server's
    /// <c>PacketEncoder</c> uses <c>SEND_VERSION = 0xFFFF - GAME_VERSION</c>
    /// when wrapping outgoing packets; XOR'd against the client's recv IV
    /// the result must equal this value.
    /// </summary>
    private const int RecvSentinel = 0xFFFF - GameVersion;

    /// <summary>
    /// Builds the 4-byte little-endian framing header for an outgoing
    /// (client→server) packet. Format: <c>[ rawSeq:short(LE) | dataLen:short(LE) ]</c>
    /// where <c>rawSeq = (iv[2] | iv[3]&lt;&lt;8) ^ GameVersion</c> and
    /// <c>dataLen = payloadLen ^ rawSeq</c>. The server validates by XOR-ing
    /// the header against its mirror IV and expects the result to equal
    /// <see cref="GameVersion"/>.
    /// </summary>
    /// <param name="payloadLen">Length of the encrypted body that will follow the header.</param>
    /// <param name="iv">The current send IV. Read-only here.</param>
    /// <param name="destination">A 4-byte buffer that receives the header.</param>
    public static void BuildHeader(int payloadLen, ReadOnlySpan<byte> iv, Span<byte> destination)
    {
        if (iv.Length < 4)
        {
            throw new ArgumentException("iv must be at least 4 bytes", nameof(iv));
        }
        if (destination.Length < HeaderSize)
        {
            throw new ArgumentException("destination must be at least 4 bytes", nameof(destination));
        }

        var rawSeq = ((iv[2] & 0xFF) | ((iv[3] & 0xFF) << 8)) ^ SendSentinel;
        var dataLen = payloadLen ^ rawSeq;

        BinaryPrimitives.WriteUInt16LittleEndian(destination[..2], (ushort)rawSeq);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..4], (ushort)dataLen);
    }

    /// <summary>
    /// Parses an incoming (server→client) 4-byte header. Validates the
    /// encoded version matches <see cref="RecvSentinel"/> — the value
    /// produced by upstream Kinoko's <c>PacketEncoder</c>; if it doesn't,
    /// <c>Valid</c> is <c>false</c> and the caller should drop the connection.
    /// </summary>
    /// <param name="header">The 4 header bytes the server sent.</param>
    /// <param name="iv">The current recv IV. Read-only here.</param>
    /// <returns>
    /// <c>(true, payloadLen)</c> on success;
    /// <c>(false, 0)</c> if the header is malformed or the version mismatches.
    /// </returns>
    public static (bool Valid, int PayloadLength) ParseHeader(ReadOnlySpan<byte> header, ReadOnlySpan<byte> iv)
    {
        if (header.Length < HeaderSize || iv.Length < 4)
        {
            return (false, 0);
        }

        var version = ((header[0] ^ iv[2]) & 0xFF) | (((header[1] ^ iv[3]) & 0xFF) << 8);
        if (version != RecvSentinel)
        {
            return (false, 0);
        }

        var length = ((header[0] ^ header[2]) & 0xFF) | (((header[1] ^ header[3]) & 0xFF) << 8);
        return (true, length);
    }

    /// <summary>
    /// Encrypts <paramref name="body"/> in place using the send IV, then
    /// rotates <paramref name="iv"/> in place to the next state. Operation
    /// order matches <c>PacketEncoder.encode</c>: Shanda → Maple → InnoHash.
    /// </summary>
    public static void EncryptBody(Span<byte> body, Span<byte> iv)
    {
        ShandaCrypto.Encrypt(body);
        MapleCrypto.Crypt(body, iv);
        IgCipher.InnoHash(iv);
    }

    /// <summary>
    /// Decrypts <paramref name="body"/> in place using the recv IV, then
    /// rotates <paramref name="iv"/> in place to the next state. Operation
    /// order matches <c>PacketDecoder.decode</c>: Maple → Shanda → InnoHash.
    /// </summary>
    public static void DecryptBody(Span<byte> body, Span<byte> iv)
    {
        MapleCrypto.Crypt(body, iv);
        ShandaCrypto.Decrypt(body);
        IgCipher.InnoHash(iv);
    }
}
