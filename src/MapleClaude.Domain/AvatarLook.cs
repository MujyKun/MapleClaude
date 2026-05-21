namespace MapleClaude.Domain;

/// <summary>
/// The full per-character avatar look (gender, skin, face, hair, equips).
/// Decoded from / encoded to the wire by <c>AvatarLook.encode</c> in upstream
/// Kinoko. The two equip dictionaries map a v95 BodyPart index → itemId.
/// </summary>
public sealed class AvatarLook
{
    public byte Gender { get; set; }
    public byte Skin { get; set; }
    public int Face { get; set; }
    public int Hair { get; set; }

    /// <summary>Visible equips (cap, top, pants, shoes, gloves, cape, weapon, etc.).</summary>
    public Dictionary<int, int> HairEquip { get; } = new();

    /// <summary>The equip slot itemIds that have a cash overlay (server overlays cash items on top of these).</summary>
    public Dictionary<int, int> UnseenEquip { get; } = new();

    /// <summary>Weapon sticker (cosmetic weapon overlay) item id, 0 if none.</summary>
    public int WeaponStickerId { get; set; }

    /// <summary>Up to 3 active pet item ids; 0 entries are empty slots.</summary>
    public int[] PetIds { get; } = new int[3];
}
