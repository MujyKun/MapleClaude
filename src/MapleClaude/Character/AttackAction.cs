namespace MapleClaude.Character;

/// <summary>
/// The body <em>actions</em> — the named animation sequences under
/// <c>Character/0000200X.img</c> that a basic attack plays (<c>swingO1</c>,
/// <c>stabO1</c>, …), as opposed to the looping movement <see cref="Stance"/>s.
/// In v95 data every action is a real frame sequence (body/arm canvases + a
/// per-frame <c>delay</c>), so it renders exactly like a stance through
/// <see cref="CharacterRenderer"/>; the only attack-specific logic is choosing
/// <em>which</em> action a swing uses.
///
/// The set is keyed off the weapon's <c>info/attack</c> type and one action is
/// picked at random per swing — which is why a melee weapon visibly cycles
/// through several poses. Mirrors the v95 client's CAvatar attack-action pick.
/// </summary>
public static class AttackAction
{
    // Weapon info/attack → candidate basic-attack actions.
    private static readonly string[] OneHand  = ["stabO1", "stabO2", "swingO1", "swingO2", "swingO3"]; // 1: sword/axe/mace/dagger (1H)
    private static readonly string[] Spear    = ["stabT1", "swingP1"];                                 // 2: spear / pole-arm
    private static readonly string[] Bow      = ["shoot1"];                                            // 3: bow
    private static readonly string[] Crossbow = ["shoot2"];                                            // 4: crossbow
    private static readonly string[] TwoHand  = ["stabO1", "stabO2", "swingT1", "swingT2", "swingT3"]; // 5: sword/axe/mace (2H)
    private static readonly string[] Wand     = ["swingO1", "swingO2"];                                // 6: wand / staff
    private static readonly string[] Claw     = ["swingO1", "swingO2"];                                // 7: claw
    private static readonly string[] Gun      = ["shot"];                                              // 9: gun
    private static readonly string[] BareHand = ["swingO1", "swingO2", "swingO3"];                     // no weapon

    /// <summary>The crouch stab, used whenever the attacker is prone.</summary>
    public const string ProneStab = "proneStab";

    /// <summary>
    /// A random basic-attack action for the given weapon <paramref name="attackType"/>
    /// (the weapon's <c>info/attack</c> int), or <see cref="ProneStab"/> when prone.
    /// An unknown / zero type falls back to the bare-handed set.
    /// </summary>
    public static string Pick(int attackType, bool prone, Random rng)
    {
        if (prone)
        {
            return ProneStab;
        }
        var set = attackType switch
        {
            1 => OneHand,
            2 => Spear,
            3 => Bow,
            4 => Crossbow,
            5 => TwoHand,
            6 => Wand,
            7 => Claw,
            9 => Gun,
            _ => BareHand,
        };
        return set[rng.Next(set.Length)];
    }
}
