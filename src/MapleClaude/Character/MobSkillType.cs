namespace MapleClaude.Character;

/// <summary>
/// The full set of mob "skill" types a server-side mob can fire (status effects, buffs,
/// summons). Values mirror upstream Kinoko's <c>MobSkillType</c> enum byte-for-byte
/// (<c>og_kinoko/.../provider/mob/MobSkillType.java</c>), which is itself derived from
/// the v95 client's enumeration. The integer id is what lives in
/// <c>Mob.wz/&lt;id&gt;.img/info/skill/&lt;n&gt;/skill</c>.
///
/// <para>Ranges:</para>
/// <list type="bullet">
///  <item>100s — self buffs (PowerUp, Haste, …) and their "_M" multi-mob counterparts (110s).</item>
///  <item>120s — player debuffs (Seal, Darkness, Stun, Slow, Curse, Poison, Dispel, AttractBanMap, …).</item>
///  <item>140s — physical / magical immunes + counters (PhysicalImmune, MagicImmune, PCounter, …).</item>
///  <item>150s — mob temporary-stat buffs (PAD, MAD, PDR, MDR, ACC, EVA, Speed, SealSkill, BalrogCounter).</item>
///  <item>160s — interaction (SpreadSkillFromUser, HealByDamage, Bind).</item>
///  <item>200s — summons (Summon, SummonCube).</item>
/// </list>
/// </summary>
public enum MobSkillType
{
    Unknown        = 0,

    // 100s — self / nearby buffs.
    PowerUp        = 100,
    MagicUp        = 101,
    PGuardUp       = 102,
    MGuardUp       = 103,
    Haste          = 104,
    PowerUpM       = 110,
    MagicUpM       = 111,
    PGuardUpM      = 112,
    MGuardUpM      = 113,
    HealM          = 114,
    HasteM         = 115,

    // 120s — player debuffs.
    Seal           = 120,
    Darkness       = 121,
    Weakness       = 122,
    Stun           = 123,
    Curse          = 124,
    Poison         = 125,
    Slow           = 126,
    Dispel         = 127,
    Attract        = 128,
    BanMap         = 129,
    AreaFire       = 130,
    AreaPoison     = 131,
    ReverseInput   = 132,
    Undead         = 133,
    StopPortion    = 134,    // [sic] - Kinoko keeps the original v95 spelling
    StopMotion     = 135,
    Fear           = 136,
    Frozen         = 137,

    // 140s — immunes + counters.
    PhysicalImmune = 140,
    MagicImmune    = 141,
    HardSkin       = 142,
    PCounter       = 143,
    MCounter       = 144,
    PMCounter      = 145,

    // 150s — mob temporary-stat buffs (drive MobTemporaryStat slots).
    Pad            = 150,
    Mad            = 151,
    Pdr            = 152,
    Mdr            = 153,
    Acc            = 154,
    Eva            = 155,
    Speed          = 156,
    SealSkill      = 157,
    BalrogCounter  = 158,

    // 160s — interaction.
    SpreadSkillFromUser = 160,
    HealByDamage   = 161,
    Bind           = 162,

    // 200s — summons.
    Summon         = 200,
    SummonCube     = 201,
}
