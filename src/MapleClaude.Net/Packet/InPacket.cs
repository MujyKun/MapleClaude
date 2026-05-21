using System.Buffers.Binary;
using System.Text;

namespace MapleClaude.Net.Packet;

/// <summary>
/// Cursor-style reader for an already-decrypted MapleStory packet body.
/// All multi-byte primitives are little-endian to match the v95 wire
/// protocol. Strings are length-prefixed (LE-short) + US-ASCII per
/// upstream Kinoko's <c>NioBufferInPacket</c>.
/// </summary>
public sealed class InPacket
{
    private readonly byte[] _data;
    private int _position;

    public InPacket(byte[] data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public InPacket(ReadOnlySpan<byte> data)
    {
        _data = data.ToArray();
    }

    /// <summary>Bytes the reader currently sits in front of (NOT consumed).</summary>
    public int Position => _position;

    /// <summary>Bytes left to read.</summary>
    public int Remaining => _data.Length - _position;

    /// <summary>Total length of the underlying buffer.</summary>
    public int Length => _data.Length;

    /// <summary>Raw access to the decrypted body. Useful for fixtures.</summary>
    public ReadOnlySpan<byte> Data => _data;

    /// <summary>Read the next byte without advancing the cursor.</summary>
    public byte PeekByte()
    {
        EnsureRemaining(1);
        return _data[_position];
    }

    /// <summary>Read the next 2 bytes as a little-endian short without advancing.</summary>
    public short PeekShort()
    {
        EnsureRemaining(2);
        return BinaryPrimitives.ReadInt16LittleEndian(_data.AsSpan(_position, 2));
    }

    public byte ReadByte()
    {
        EnsureRemaining(1);
        return _data[_position++];
    }

    public sbyte ReadSByte() => (sbyte)ReadByte();

    public bool ReadBool() => ReadByte() != 0;

    public short ReadShort()
    {
        EnsureRemaining(2);
        var v = BinaryPrimitives.ReadInt16LittleEndian(_data.AsSpan(_position, 2));
        _position += 2;
        return v;
    }

    public ushort ReadUShort() => (ushort)ReadShort();

    public int ReadInt()
    {
        EnsureRemaining(4);
        var v = BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(_position, 4));
        _position += 4;
        return v;
    }

    public uint ReadUInt() => (uint)ReadInt();

    public long ReadLong()
    {
        EnsureRemaining(8);
        var v = BinaryPrimitives.ReadInt64LittleEndian(_data.AsSpan(_position, 8));
        _position += 8;
        return v;
    }

    public float ReadFloat()
    {
        EnsureRemaining(4);
        var v = BinaryPrimitives.ReadSingleLittleEndian(_data.AsSpan(_position, 4));
        _position += 4;
        return v;
    }

    public double ReadDouble()
    {
        EnsureRemaining(8);
        var v = BinaryPrimitives.ReadDoubleLittleEndian(_data.AsSpan(_position, 8));
        _position += 8;
        return v;
    }

    /// <summary>Read exactly <paramref name="count"/> bytes and return a new array.</summary>
    public byte[] ReadBytes(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        EnsureRemaining(count);
        var copy = new byte[count];
        Array.Copy(_data, _position, copy, 0, count);
        _position += count;
        return copy;
    }

    /// <summary>Read a fixed-length ASCII string (NULs preserved as written, then trimmed at the first NUL).</summary>
    public string ReadString(int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        EnsureRemaining(length);
        var slice = _data.AsSpan(_position, length);
        _position += length;
        // Match the server's encodeString(value, length): fixed buffer, zero-padded.
        var nul = slice.IndexOf((byte)0);
        if (nul >= 0)
        {
            slice = slice[..nul];
        }
        return Encoding.ASCII.GetString(slice);
    }

    /// <summary>Read a length-prefixed (LE-short) ASCII string.</summary>
    public string ReadString()
    {
        var length = ReadShort();
        if (length < 0)
        {
            throw new InvalidDataException($"InPacket.ReadString: negative length {length}");
        }
        EnsureRemaining(length);
        var s = Encoding.ASCII.GetString(_data, _position, length);
        _position += length;
        return s;
    }

    public void Skip(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        EnsureRemaining(count);
        _position += count;
    }

    /// <summary>Reset the cursor to the start; useful for parsing the same body twice.</summary>
    public void Rewind() => _position = 0;

    private void EnsureRemaining(int count)
    {
        if (Remaining < count)
        {
            throw new InvalidDataException(
                $"InPacket: tried to read {count} bytes but only {Remaining} remain (pos={_position}, len={_data.Length}).");
        }
    }
}
