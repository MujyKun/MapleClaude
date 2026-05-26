namespace MapleClaude.Character;

/// <summary>
/// One mob attack entry parsed from <c>Mob.wz/&lt;id&gt;.img/attack&lt;n&gt;/info/*</c>.
/// Carries both the Kinoko-validated fields (the server checks these on every UserHit
/// to score the hit, drain MP, apply status, etc.) AND the most-used client-side
/// fields the v95 client reads to render the attack + drive the mob AI's per-attack
/// cooldowns.
///
/// <para>Per attack index: <c>attack1</c> -&gt; <see cref="AttackIndex"/> 0,
/// <c>attack2</c> -&gt; 1, …, <c>attackF</c> -&gt; 14. The body-contact attack
/// (<c>info/bodyAttack=true</c>) uses index 0 implicitly — there's no <c>attack0</c>
/// sub-node; the body hit just maps to <see cref="MobAttack"/> 0 in the kinoko
/// <c>HitHandler.handleMobAttack</c> resolver.</para>
/// </summary>
public sealed class MobAttack
{
    public int AttackIndex { get; init; }

    // ── Kinoko-validated fields (server reads + applies these) ──────────────────

    /// <summary>WZ <c>info/disease</c> — the <see cref="MobSkillType"/> id that this
    /// attack applies on hit (0 = none, just damage). Resolved by Kinoko's
    /// <c>HitHandler.handleMobAttack</c> to apply a status effect to the player.</summary>
    public int  SkillId      { get; init; }

    /// <summary>WZ <c>info/level</c> — skill level for the applied status (1..N).</summary>
    public int  SkillLevel   { get; init; }

    /// <summary>WZ <c>info/conMP</c> — MP the mob consumes to fire this attack.
    /// Mob can't pick this attack if its MP &lt; conMP.</summary>
    public int  ConMp        { get; init; }

    /// <summary>WZ <c>info/mpBurn</c> — MP drained from the player on hit.</summary>
    public int  MpBurn       { get; init; }

    /// <summary>WZ <c>info/magic</c> — 1 = magic attack (uses mob MAD vs player MDR),
    /// 0 = physical (PAD vs PDR).</summary>
    public bool Magic        { get; init; }

    /// <summary>WZ <c>info/deadlyAttack</c> — bypasses evasion AND sets player HP to 1
    /// (and MP to 1) on hit. Used by some boss-style instakill mechanics.</summary>
    public bool DeadlyAttack { get; init; }

    // ── Client-side fields (visual / AI cooldowns; not server-validated) ────────

    /// <summary>WZ <c>info/bulletNumber</c> — number of projectiles fired (default 1).</summary>
    public int  BulletNumber { get; init; } = 1;

    /// <summary>WZ <c>info/bulletSpeed</c> — projectile speed px/frame.</summary>
    public int  BulletSpeed  { get; init; }

    /// <summary>WZ <c>info/magicElemAttr</c> — element type for magic attacks
    /// (Fire/Ice/Poison/Holy/…). Drives elemental resistance roll on the player.</summary>
    public int  MagicElemAttr{ get; init; }

    /// <summary>WZ <c>info/jump</c> — mob jumps when starting this attack.</summary>
    public bool JumpAttack   { get; init; }

    /// <summary>WZ <c>info/knockBack</c> — pushes the player on hit.</summary>
    public bool KnockBack    { get; init; }

    /// <summary>WZ <c>info/attackAfter</c> — ms cooldown before the mob can attack again
    /// after THIS one. Drives the per-attack pacing in the mob AI tick.</summary>
    public int  AttackAfter  { get; init; }

    /// <summary>WZ <c>info/effectAfter</c> — ms delay between attack-start and the visual
    /// effect / projectile spawn.</summary>
    public int  EffectAfter  { get; init; }

    /// <summary>WZ <c>info/tremble</c> — screen-shake on hit.</summary>
    public bool Tremble      { get; init; }

    /// <summary>WZ <c>info/rush</c> — mob charges / dashes toward the player during the
    /// attack.</summary>
    public bool Rush         { get; init; }

    /// <summary>WZ <c>info/hitAttach</c> — projectile sticks / attaches on hit.</summary>
    public bool HitAttach    { get; init; }

    /// <summary>WZ <c>info/facingAttach</c> — attack always faces the player regardless
    /// of mob facing.</summary>
    public bool FacingAttach { get; init; }

    /// <summary>WZ <c>info/effect</c> — UOL / path to the visual effect played on attack.</summary>
    public string Effect       { get; init; } = string.Empty;

    /// <summary>WZ <c>info/hit</c> — UOL to the hit animation frame reference.</summary>
    public string Hit          { get; init; } = string.Empty;

    /// <summary>WZ <c>info/ball</c> — UOL to the projectile sprite / animation.</summary>
    public string Ball         { get; init; } = string.Empty;

    /// <summary>WZ <c>info/areaWarning</c> — UOL to the AoE warning indicator (red
    /// circle, etc).</summary>
    public string AreaWarning  { get; init; } = string.Empty;
}
