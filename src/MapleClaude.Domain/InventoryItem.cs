namespace MapleClaude.Domain;

/// <summary>v95 item slot discriminator (GW_ItemSlotBase nType): EQUIP=1, BUNDLE=2, PET=3.</summary>
public enum InvItemType
{
    Equip = 1,
    Bundle = 2,
    Pet = 3,
}

/// <summary>
/// v95 inventory tab. Matches upstream Kinoko <c>InventoryType</c>:
/// EQUIPPED=0 (worn), EQUIP=1, CONSUME=2, INSTALL=3, ETC=4, CASH=5.
/// </summary>
public enum InventoryType
{
    Equipped = 0,
    Equip = 1,
    Consume = 2,
    Install = 3,
    Etc = 4,
    Cash = 5,
}

/// <summary>
/// A single item in the player's inventory or an equipped slot. Decoded from the
/// v95 <c>GW_ItemSlot*</c> wire structure. POD — no dependencies.
/// </summary>
public sealed class InventoryItem
{
    public int          ItemId;
    public InvItemType  Type;
    public bool         Cash;
    public long         ItemSn;
    public long         DateExpire;

    // Bundle (and the resolved display quantity for any item).
    public short        Quantity = 1;
    public string       Title = string.Empty;
    public short        Attribute;

    // Equip-only stat block (null for bundles/pets).
    public EquipStats?  Equip;

    // Pet-only.
    public string       PetName = string.Empty;
}

/// <summary>The GW_ItemSlotEquip stat block.</summary>
public sealed class EquipStats
{
    public byte  Ruc, Cuc;
    public short Str, Dex, Int, Luk, MaxHp, MaxMp, Pad, Mad, Pdd, Mdd, Acc, Eva, Craft, Speed, Jump;
    public byte  LevelUpType, Level;
    public int   Exp, Durability, Iuc;
    public byte  Grade, Chuc;
    public short Option1, Option2, Option3, Socket1, Socket2;
}
