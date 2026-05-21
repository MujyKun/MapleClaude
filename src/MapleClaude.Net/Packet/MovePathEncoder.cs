namespace MapleClaude.Net.Packet;

/// <summary>
/// Encodes a list of <see cref="MoveElement"/> into the v95 wire format the
/// channel server expects in <c>UserMove(44)</c>. Mirrors upstream Kinoko's
/// <c>kinoko/world/field/life/MovePath.encode</c> byte-for-byte.
/// </summary>
public static class MovePathEncoder
{
    public static byte[] Encode(short originX, short originY, short originVx, short originVy, IReadOnlyList<MoveElement> elements)
    {
        var p = OutPacket.Raw();
        p.WriteShort(originX);
        p.WriteShort(originY);
        p.WriteShort(originVx);
        p.WriteShort(originVy);
        p.WriteByte((byte)elements.Count);
        foreach (var e in elements)
        {
            p.WriteByte(e.Attr);
            switch (Category(e.Attr))
            {
                case MoveCategory.Normal:
                    p.WriteShort(e.X);
                    p.WriteShort(e.Y);
                    p.WriteShort(e.Vx);
                    p.WriteShort(e.Vy);
                    p.WriteShort(e.Fh);
                    if (e.Attr == 12)
                    {
                        p.WriteShort(e.FhFallStart);
                    }
                    p.WriteShort(e.XOffset);
                    p.WriteShort(e.YOffset);
                    break;
                case MoveCategory.Jump:
                    p.WriteShort(e.Vx);
                    p.WriteShort(e.Vy);
                    break;
                case MoveCategory.Teleport:
                    p.WriteShort(e.X);
                    p.WriteShort(e.Y);
                    p.WriteShort(e.Fh);
                    break;
                case MoveCategory.StatChange:
                    p.WriteByte(e.Stat);
                    continue;
                case MoveCategory.StartFallDown:
                    p.WriteShort(e.Vx);
                    p.WriteShort(e.Vy);
                    p.WriteShort(e.FhFallStart);
                    break;
                case MoveCategory.FlyingBlock:
                    p.WriteShort(e.X);
                    p.WriteShort(e.Y);
                    p.WriteShort(e.Vx);
                    p.WriteShort(e.Vy);
                    break;
                case MoveCategory.Action:
                    break;
            }
            p.WriteByte(e.MoveAction);
            p.WriteShort(e.Elapse);
        }
        var bytes = p.ToArray();
        p.Release();
        return bytes;
    }

    private enum MoveCategory { Normal, Jump, Teleport, StatChange, StartFallDown, FlyingBlock, Action }

    private static MoveCategory Category(byte attr) => attr switch
    {
        0 or 5 or 12 or 14 or 35 or 36 => MoveCategory.Normal,
        1 or 2 or 13 or 16 or 18 or 31 or 32 or 33 or 34 => MoveCategory.Jump,
        3 or 4 or 6 or 7 or 8 or 10 => MoveCategory.Teleport,
        9 => MoveCategory.StatChange,
        11 => MoveCategory.StartFallDown,
        17 => MoveCategory.FlyingBlock,
        _ => MoveCategory.Action,
    };
}

/// <summary>One move-path sub-action.</summary>
public sealed class MoveElement
{
    public byte Attr { get; init; }
    public short X { get; init; }
    public short Y { get; init; }
    public short Vx { get; init; }
    public short Vy { get; init; }
    public short Fh { get; init; }
    public short FhFallStart { get; init; }
    public short XOffset { get; init; }
    public short YOffset { get; init; }
    public byte Stat { get; init; }
    public byte MoveAction { get; init; }
    public short Elapse { get; init; }
}
