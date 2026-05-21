using System.Buffers.Binary;
using MapleClaude.Net.Crypto;

namespace MapleClaude.Net.Session;

/// <summary>
/// Parses the unencrypted server-hello packet that every MapleStory
/// connection (login or channel) begins with. The wire format is
/// <c>[ length:short(LE) | version:short(LE) | patch:LE-shortPrefix+ASCII
/// | sendIv:byte[4] | recvIv:byte[4] | locale:byte ]</c> per upstream
/// Kinoko's <c>LoginPacket.connect</c>.
/// </summary>
public static class HandshakeReader
{
    /// <summary>
    /// Try to consume one server-hello frame from <paramref name="buffer"/>.
    /// </summary>
    /// <param name="buffer">Raw bytes pending on the socket.</param>
    /// <param name="info">Parsed handshake info on success.</param>
    /// <param name="consumed">Number of bytes consumed from the head of the buffer.</param>
    public static bool TryRead(ReadOnlySpan<byte> buffer, out HandshakeInfo info, out int consumed)
    {
        info = default;
        consumed = 0;
        if (buffer.Length < 2)
        {
            return false;
        }
        var bodyLen = BinaryPrimitives.ReadInt16LittleEndian(buffer[..2]);
        if (bodyLen < 7)
        {
            throw new InvalidDataException($"Handshake body length {bodyLen} is too small");
        }
        if (buffer.Length < 2 + bodyLen)
        {
            return false;
        }
        var body = buffer.Slice(2, bodyLen);
        var pos = 0;
        var version = BinaryPrimitives.ReadInt16LittleEndian(body.Slice(pos, 2));
        pos += 2;
        var patchLen = BinaryPrimitives.ReadInt16LittleEndian(body.Slice(pos, 2));
        pos += 2;
        if (patchLen < 0 || pos + patchLen + 4 + 4 + 1 > bodyLen)
        {
            throw new InvalidDataException($"Handshake patch length {patchLen} is malformed");
        }
        var patch = System.Text.Encoding.ASCII.GetString(body.Slice(pos, patchLen));
        pos += patchLen;
        var sendIv = body.Slice(pos, 4).ToArray();
        pos += 4;
        var recvIv = body.Slice(pos, 4).ToArray();
        pos += 4;
        var locale = body[pos];

        if (version != PacketCipher.GameVersion)
        {
            throw new InvalidDataException(
                $"Handshake reports version {version}, expected v95 ({PacketCipher.GameVersion})");
        }
        info = new HandshakeInfo
        {
            Version = version,
            Patch = patch,
            SendIv = sendIv,
            RecvIv = recvIv,
            Locale = locale,
        };
        consumed = 2 + bodyLen;
        return true;
    }
}

/// <summary>Decoded server-hello fields.</summary>
public readonly record struct HandshakeInfo
{
    public short Version { get; init; }
    public string Patch { get; init; }
    public byte[] SendIv { get; init; }
    public byte[] RecvIv { get; init; }
    public byte Locale { get; init; }
}
