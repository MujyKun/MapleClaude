using MapleClaude.Domain;
using MapleClaude.Net.Packet;

namespace MapleClaude.Net.Handlers;

/// <summary>
/// Decoders for <c>CharacterStat</c> and <c>AvatarLook</c> wire blobs (used
/// inside <c>SelectWorldResult(11)</c> and <c>CreateNewCharacterResult(14)</c>).
/// Mirrors upstream Kinoko's <c>CharacterStat.encode</c> + <c>AvatarLook.encode</c>.
/// </summary>
public static class AvatarCodec
{
    /// <summary>List of v95 job IDs that use the extended-SP wire format.</summary>
    private static readonly HashSet<int> ExtendSpJobIds = new()
    {
        // Evan
        2001, 2200, 2210, 2211, 2212, 2213, 2214, 2215, 2216, 2217, 2218,
        // Resistance Battle Mage / Wild Hunter / Mechanic
        3300, 3310, 3311, 3312,
        3500, 3510, 3511, 3512,
        3200, 3210, 3211, 3212,
    };

    public static CharacterStat DecodeCharacterStat(InPacket p)
    {
        var s = new CharacterStat
        {
            CharacterId = p.ReadInt(),
            Name = p.ReadString(13),
            Gender = p.ReadByte(),
            Skin = p.ReadByte(),
            Face = p.ReadInt(),
            Hair = p.ReadInt(),
            PetSn1 = p.ReadLong(),
            PetSn2 = p.ReadLong(),
            PetSn3 = p.ReadLong(),
            Level = p.ReadByte(),
            Job = p.ReadShort(),
            Str = p.ReadShort(),
            Dex = p.ReadShort(),
            Int = p.ReadShort(),
            Luk = p.ReadShort(),
            Hp = p.ReadInt(),
            MaxHp = p.ReadInt(),
            Mp = p.ReadInt(),
            MaxMp = p.ReadInt(),
            Ap = p.ReadShort(),
        };
        if (ExtendSpJobIds.Contains(s.Job))
        {
            var count = p.ReadByte();
            // Each entry is {byte jobLevel, byte sp}.
            s.SpRaw = new byte[1 + count * 2];
            s.SpRaw[0] = count;
            var sub = p.ReadBytes(count * 2);
            Array.Copy(sub, 0, s.SpRaw, 1, sub.Length);
        }
        else
        {
            // Non-extend: single short SP.
            var spLow = p.ReadByte();
            var spHigh = p.ReadByte();
            s.SpRaw = new[] { spLow, spHigh };
        }
        s.Exp = p.ReadInt();
        s.Pop = p.ReadShort();
        s.TempExp = p.ReadInt();
        s.PosMap = p.ReadInt();
        s.Portal = p.ReadByte();
        s.PlayTime = p.ReadInt();
        s.SubJob = p.ReadShort();
        return s;
    }

    public static AvatarLook DecodeAvatarLook(InPacket p)
    {
        var look = new AvatarLook
        {
            Gender = p.ReadByte(),
            Skin = p.ReadByte(),
            Face = p.ReadInt(),
        };
        p.ReadByte(); // 0
        look.Hair = p.ReadInt();
        var bp = p.ReadByte();
        while (bp != 0xFF)
        {
            var itemId = p.ReadInt();
            look.HairEquip[bp] = itemId;
            bp = p.ReadByte();
        }
        bp = p.ReadByte();
        while (bp != 0xFF)
        {
            var itemId = p.ReadInt();
            look.UnseenEquip[bp] = itemId;
            bp = p.ReadByte();
        }
        look.WeaponStickerId = p.ReadInt();
        for (var i = 0; i < 3; i++)
        {
            look.PetIds[i] = p.ReadInt();
        }
        return look;
    }

    public static void EncodeAvatarLook(OutPacket p, AvatarLook look)
    {
        p.WriteByte(look.Gender);
        p.WriteByte(look.Skin);
        p.WriteInt(look.Face);
        p.WriteByte((byte)0);
        p.WriteInt(look.Hair);
        foreach (var kv in look.HairEquip)
        {
            p.WriteByte((byte)kv.Key);
            p.WriteInt(kv.Value);
        }
        p.WriteByte(unchecked((byte)-1));
        foreach (var kv in look.UnseenEquip)
        {
            p.WriteByte((byte)kv.Key);
            p.WriteInt(kv.Value);
        }
        p.WriteByte(unchecked((byte)-1));
        p.WriteInt(look.WeaponStickerId);
        for (var i = 0; i < 3; i++)
        {
            p.WriteInt(look.PetIds[i]);
        }
    }
}
