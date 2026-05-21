using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace MapleClaude.Net.Packet;

/// <summary>
/// Pooled writer for an outgoing MapleStory packet body. The first 2 bytes
/// are the opcode (LE-short); the rest is the packet payload. The 4-byte
/// cipher header is added separately by the network session layer.
///
/// Primitives mirror upstream Kinoko's <c>NioBufferOutPacket</c> byte-for-byte:
/// all multi-byte values are little-endian; strings are length-prefixed
/// (LE-short) + US-ASCII.
/// </summary>
public sealed class OutPacket
{
    private byte[] _buffer;
    private int _position;

    private OutPacket(int initialCapacity)
    {
        _buffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
        _position = 0;
    }

    /// <summary>Construct an empty packet with no opcode prepended.</summary>
    public static OutPacket Raw(int capacity = 32) => new(capacity);

    /// <summary>Construct a packet pre-loaded with the given opcode (LE-short).</summary>
    public static OutPacket Of(InHeader header)
    {
        var p = new OutPacket(32);
        p.WriteShort((short)header);
        return p;
    }

    /// <summary>Construct a packet pre-loaded with a raw opcode word.</summary>
    public static OutPacket Of(short opcode)
    {
        var p = new OutPacket(32);
        p.WriteShort(opcode);
        return p;
    }

    public int Size => _position;

    /// <summary>The opcode at offset 0 of the body, if at least 2 bytes have been written.</summary>
    public short Header => _position >= 2 ? BinaryPrimitives.ReadInt16LittleEndian(_buffer) : (short)0;

    public void WriteByte(byte value)
    {
        EnsureCapacity(1);
        _buffer[_position++] = value;
    }

    public void WriteByte(int value) => WriteByte((byte)value);
    public void WriteByte(short value) => WriteByte((byte)value);
    public void WriteByte(bool value) => WriteByte((byte)(value ? 1 : 0));
    public void WriteSByte(sbyte value) => WriteByte((byte)value);

    public void WriteShort(short value)
    {
        EnsureCapacity(2);
        BinaryPrimitives.WriteInt16LittleEndian(_buffer.AsSpan(_position, 2), value);
        _position += 2;
    }

    public void WriteShort(int value) => WriteShort((short)value);

    public void WriteUShort(ushort value)
    {
        EnsureCapacity(2);
        BinaryPrimitives.WriteUInt16LittleEndian(_buffer.AsSpan(_position, 2), value);
        _position += 2;
    }

    public void WriteInt(int value)
    {
        EnsureCapacity(4);
        BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(_position, 4), value);
        _position += 4;
    }

    public void WriteInt(bool value) => WriteInt(value ? 1 : 0);
    public void WriteInt(long value) => WriteInt((int)value);

    public void WriteUInt(uint value)
    {
        EnsureCapacity(4);
        BinaryPrimitives.WriteUInt32LittleEndian(_buffer.AsSpan(_position, 4), value);
        _position += 4;
    }

    public void WriteLong(long value)
    {
        EnsureCapacity(8);
        BinaryPrimitives.WriteInt64LittleEndian(_buffer.AsSpan(_position, 8), value);
        _position += 8;
    }

    public void WriteFloat(float value)
    {
        EnsureCapacity(4);
        BinaryPrimitives.WriteSingleLittleEndian(_buffer.AsSpan(_position, 4), value);
        _position += 4;
    }

    public void WriteDouble(double value)
    {
        EnsureCapacity(8);
        BinaryPrimitives.WriteDoubleLittleEndian(_buffer.AsSpan(_position, 8), value);
        _position += 8;
    }

    public void WriteBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
        {
            return;
        }
        EnsureCapacity(bytes.Length);
        bytes.CopyTo(_buffer.AsSpan(_position, bytes.Length));
        _position += bytes.Length;
    }

    public void WriteBytes(byte[] bytes) => WriteBytes(bytes.AsSpan());

    /// <summary>Write a length-prefixed (LE-short) US-ASCII string.</summary>
    public void WriteString(string? value)
    {
        value ??= string.Empty;
        var bytes = Encoding.ASCII.GetBytes(value);
        if (bytes.Length > short.MaxValue)
        {
            throw new ArgumentException("string too long for length-prefixed encoding", nameof(value));
        }
        EnsureCapacity(2 + bytes.Length);
        WriteShort((short)bytes.Length);
        WriteBytes(bytes);
    }

    /// <summary>Write a fixed-length US-ASCII string padded with NUL up to <paramref name="length"/>.</summary>
    public void WriteString(string? value, int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        value ??= string.Empty;
        EnsureCapacity(length);
        var bytes = Encoding.ASCII.GetBytes(value);
        var copy = Math.Min(bytes.Length, length);
        Array.Copy(bytes, 0, _buffer, _position, copy);
        // Zero-pad the rest (the rented array is uninitialised on rent).
        for (var i = copy; i < length; i++)
        {
            _buffer[_position + i] = 0;
        }
        _position += length;
    }

    /// <summary>Return a fresh copy of the encoded body. The pool buffer is released afterwards.</summary>
    public byte[] ToArray()
    {
        var copy = new byte[_position];
        Array.Copy(_buffer, copy, _position);
        return copy;
    }

    /// <summary>Direct view of the encoded body (valid until the next Write/Release call).</summary>
    public ReadOnlySpan<byte> AsSpan() => _buffer.AsSpan(0, _position);

    /// <summary>Release the underlying pooled buffer.</summary>
    public void Release()
    {
        if (_buffer.Length > 0)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = [];
            _position = 0;
        }
    }

    private void EnsureCapacity(int needed)
    {
        var required = _position + needed;
        if (required <= _buffer.Length)
        {
            return;
        }
        var grown = ArrayPool<byte>.Shared.Rent(Math.Max(required, _buffer.Length * 2));
        Array.Copy(_buffer, grown, _position);
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = grown;
    }
}
