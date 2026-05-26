namespace MapleClaude.Character;

/// <summary>
/// One mob skill entry parsed from <c>Mob.wz/&lt;id&gt;.img/info/skill/&lt;n&gt;/{skill,level}</c>.
/// The skill id maps to a <see cref="MobSkillType"/> (status effects, buffs, summons —
/// see that enum for the full taxonomy). The level scales the effect's potency /
/// duration. Kinoko's <c>HitHandler.handleMobAttack</c> looks the type up via
/// <c>MobSkillType.getByValue(skillId)</c> to decide which status to apply.
/// </summary>
public sealed record class MobSkillRef(MobSkillType Type, int SkillId, int Level);
