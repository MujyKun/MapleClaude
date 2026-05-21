namespace MapleClaude.Domain;

/// <summary>
/// v95 body-part slot indices. Mirrors upstream Kinoko's <c>BodyPart</c>
/// enum, but only the equipped (player-visible) entries we actually need
/// for avatar composition.
/// </summary>
public static class BodyPartSlot
{
    public const int Hair = 0;
    public const int Cap = 1;
    public const int FaceAcc = 2;
    public const int EyeAcc = 3;
    public const int EarAcc = 4;
    public const int Clothes = 5;
    public const int Pants = 6;
    public const int Shoes = 7;
    public const int Gloves = 8;
    public const int Cape = 9;
    public const int Shield = 10;
    public const int Weapon = 11;
    public const int Medal = 49;
    public const int Belt = 50;
    public const int Shoulder = 51;

    public const int CashBase = 100;
    public const int CashWeapon = 111;
}
