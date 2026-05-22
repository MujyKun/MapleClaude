namespace MapleClaude.Domain;

/// <summary>
/// A learned skill: its id, current level, and (for 4th-job skills) master
/// level. Mirrors upstream Kinoko <c>SkillRecord</c>.
/// </summary>
public sealed class SkillRecord
{
    public int SkillId;
    public int Level;
    public int MasterLevel;
}
