namespace MapleClaude.Character;

/// <summary>
/// Player animation stances. Names match the v95 WZ data subkeys under
/// <c>Character/0000200X.img/&lt;stance&gt;</c> exactly (case-sensitive) so
/// they can be used as lookup keys directly.
/// </summary>
public enum Stance
{
    Stand1,
    Stand2,
    Walk1,
    Walk2,
    Jump,
    Alert,
    Fly,
    Ladder,
    Rope,
    Sit,
    Prone,
    ProneStab,
    Dead,
}

public static class StanceExtensions
{
    public static string ToWzKey(this Stance s) => s switch
    {
        Stance.Stand1 => "stand1",
        Stance.Stand2 => "stand2",
        Stance.Walk1 => "walk1",
        Stance.Walk2 => "walk2",
        Stance.Jump => "jump",
        Stance.Alert => "alert",
        Stance.Fly => "fly",
        Stance.Ladder => "ladder",
        Stance.Rope => "rope",
        Stance.Sit => "sit",
        Stance.Prone => "prone",
        Stance.ProneStab => "proneStab",
        Stance.Dead => "dead",
        _ => "stand1",
    };
}
