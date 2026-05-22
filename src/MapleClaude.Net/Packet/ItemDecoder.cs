using MapleClaude.Domain;

namespace MapleClaude.Net.Packet;

/// <summary>
/// Decodes the v95 <c>GW_ItemSlotBase</c> wire structure (mirrors upstream
/// Kinoko <c>Item.encode</c> + <c>EquipData.encode</c> + <c>PetData.encode</c>).
/// FileTime is 8 bytes (low int + high int). Item-type discriminator: 1=EQUIP,
/// 2=BUNDLE, 3=PET.
/// </summary>
public static class ItemDecoder
{
    public static InventoryItem Decode(InPacket p)
    {
        var type = (InvItemType)p.ReadByte();   // nType
        var item = new InventoryItem
        {
            Type   = type,
            ItemId = p.ReadInt(),               // nItemID
        };
        var cash = p.ReadByte();
        item.Cash = cash != 0;
        if (item.Cash)
        {
            item.ItemSn = p.ReadLong();         // liCashItemSN
        }
        item.DateExpire = p.ReadLong();         // dateExpire (FileTime: low+high int)

        switch (type)
        {
            case InvItemType.Equip:
                item.Equip = DecodeEquip(p, item);
                if (!item.Cash)
                {
                    item.ItemSn = p.ReadLong(); // liSN
                }
                p.ReadLong();                   // ftEquipped
                p.ReadInt();                    // nPrevBonusExpRate
                break;

            case InvItemType.Pet:
                item.PetName = p.ReadString(13);
                p.ReadByte();                   // nLevel
                p.ReadShort();                  // nTameness
                p.ReadByte();                   // nRepleteness
                p.ReadLong();                   // dateDead (FT)
                p.ReadShort();                  // usPetAttribute
                p.ReadShort();                  // usPetSkill
                p.ReadInt();                    // nRemainLife
                item.Attribute = p.ReadShort(); // nAttribute
                break;

            default: // Bundle
                item.Quantity  = p.ReadShort(); // nNumber
                item.Title     = p.ReadString();// sTitle
                item.Attribute = p.ReadShort(); // nAttribute
                if (IsRechargeable(item.ItemId))
                {
                    item.ItemSn = p.ReadLong();
                }
                break;
        }
        return item;
    }

    // Mirrors EquipData.encode (the 15 stat shorts, then title/attribute on the
    // owning item, then the level/exp/upgrade/option block).
    private static EquipStats DecodeEquip(InPacket p, InventoryItem item)
    {
        var e = new EquipStats
        {
            Ruc   = p.ReadByte(),
            Cuc   = p.ReadByte(),
            Str   = p.ReadShort(),
            Dex   = p.ReadShort(),
            Int   = p.ReadShort(),
            Luk   = p.ReadShort(),
            MaxHp = p.ReadShort(),
            MaxMp = p.ReadShort(),
            Pad   = p.ReadShort(),
            Mad   = p.ReadShort(),
            Pdd   = p.ReadShort(),
            Mdd   = p.ReadShort(),
            Acc   = p.ReadShort(),
            Eva   = p.ReadShort(),
            Craft = p.ReadShort(),
            Speed = p.ReadShort(),
            Jump  = p.ReadShort(),
        };
        item.Title     = p.ReadString();    // sTitle (on the item)
        item.Attribute = p.ReadShort();     // nAttribute (on the item)
        e.LevelUpType  = p.ReadByte();
        e.Level        = p.ReadByte();
        e.Exp          = p.ReadInt();
        e.Durability   = p.ReadInt();
        e.Iuc          = p.ReadInt();
        e.Grade        = p.ReadByte();
        e.Chuc         = p.ReadByte();
        e.Option1      = p.ReadShort();
        e.Option2      = p.ReadShort();
        e.Option3      = p.ReadShort();
        e.Socket1      = p.ReadShort();
        e.Socket2      = p.ReadShort();
        return e;
    }

    private static bool IsRechargeable(int itemId) =>
        itemId / 10000 == 207 || itemId / 10000 == 233;
}
