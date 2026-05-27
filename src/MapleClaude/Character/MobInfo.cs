namespace MapleClaude.Character;

/// <summary>
/// Typed C# model of one mob's <c>Mob.wz/&lt;id&gt;.img</c> data. Mirrors the FULL set of
/// attributes the v95 client's <c>CMobTemplate</c> + the upstream Kinoko
/// <c>MobTemplate</c> parse - so any future combat / AI / drop / visual code has the
/// data already typed + documented and doesn't have to re-introduce concepts.
///
/// <para>Two authoritative sources:</para>
/// <list type="bullet">
///  <item>Kinoko <c>MobTemplate.from()</c> (server-validated parse) for combat fields the
///        server enforces (HP/MP, PAD/PDR/MAD/MDR, ACC/EVA, recovery, attacks, skills,
///        elem resistances, skill-immune set, revives).</item>
///  <item>v95 client <c>CMobTemplate</c> (via the IDB) for runtime semantics + the
///        client-only fields the server doesn't parse (moveAbility, speed, flySpeed,
///        chaseSpeed, bodyAttack, pushed, invincible/disable/notAttack flags, display
///        flags like hpTagHide / upperMostLayer / weaponID / angerGauge, categorization
///        like category / escortType / mobSpeciesCode).</item>
/// </list>
///
/// <para>All properties are immutable post-construction. Empty collection defaults are
/// used when the corresponding WZ node is missing.</para>
/// </summary>
public sealed class MobInfo
{
    // -- Identity ----------------------------------------------------------------

    public int    TemplateId      { get; init; }

    /// <summary>WZ <c>info/level</c>. Drives exp formula + display level. Default 1.</summary>
    public int    Level           { get; init; } = 1;

    /// <summary>WZ <c>info/exp</c>. Base experience granted on kill.</summary>
    public int    Exp             { get; init; }

    // -- Stats (server-validated by Kinoko's CalcDamage) ------------------------

    /// <summary>WZ <c>info/maxHP</c>. Default 1 (avoids /0 in HP-percent math).</summary>
    public int    MaxHp           { get; init; } = 1;

    /// <summary>WZ <c>info/maxMP</c>. Drives mob skill firing (skill is gated by MP).</summary>
    public int    MaxMp           { get; init; }

    /// <summary>WZ <c>info/PADamage</c> - Physical Attack Damage. CalcDamage::PDamage.</summary>
    public int    Pad             { get; init; }

    /// <summary>WZ <c>info/PDRate</c> - Physical Defense Rate (% reduction).</summary>
    public int    Pdr             { get; init; }

    /// <summary>WZ <c>info/MADamage</c> - Magical Attack Damage. CalcDamage::MDamage.</summary>
    public int    Mad             { get; init; }

    /// <summary>WZ <c>info/MDRate</c> - Magical Defense Rate (% reduction).</summary>
    public int    Mdr             { get; init; }

    /// <summary>WZ <c>info/acc</c>. Mob accuracy vs player EVA.</summary>
    public int    Acc             { get; init; }

    /// <summary>WZ <c>info/eva</c>. Mob evasion vs player ACC.</summary>
    public int    Eva             { get; init; }

    /// <summary>WZ <c>info/hpRecovery</c> - passive HP regen per tick.</summary>
    public int    HpRecovery      { get; init; }

    /// <summary>WZ <c>info/mpRecovery</c> - passive MP regen per tick.</summary>
    public int    MpRecovery      { get; init; }

    /// <summary>WZ <c>info/fixedDamage</c>. When &gt; 0, ALL damage dealt to this mob is
    /// clamped to this value (used for training / event mobs). 0 = normal calc.</summary>
    public int    FixedDamage     { get; init; }

    // -- Lifecycle ---------------------------------------------------------------

    /// <summary>WZ <c>info/removeAfter</c> - seconds. When &gt; 0 the mob auto-despawns
    /// this many seconds after spawn (event / summoned mobs).</summary>
    public int    RemoveAfter     { get; init; }

    /// <summary>WZ <c>info/dropItemPeriod</c> - ms. When &gt; 0 the mob drops items on
    /// this interval throughout its lifetime (resource mobs like mushroom).</summary>
    public int    DropItemPeriod  { get; init; }

    // -- Movement / AI (client-only - Kinoko doesn't parse these) ---------------

    /// <summary>WZ <c>info/moveAbility</c>. 0 = STAY (mushroom-like, never moves),
    /// 1 = WALK, 2 = JUMP, 3 or 4 = FLY (the v95 IDB shows <c>bFly = (v == 4)</c> in
    /// CMob::GenerateMovePath; both 3 and 4 are treated as fly by <see cref="IsFly"/>).
    /// Default 1 (walk).</summary>
    public int    MoveAbility     { get; init; } = 1;

    /// <summary>WZ <c>info/speed</c> - walk-speed % modifier. Final px/s approx
    /// base * (1 + Speed/100).</summary>
    public int    Speed           { get; init; }

    /// <summary>WZ <c>info/flySpeed</c> OR <c>info/fs</c> - fly-speed % modifier. We
    /// merge them at parse time (fs wins when both present).</summary>
    public int    FlySpeed        { get; init; }

    /// <summary>WZ <c>info/fly</c>. Some mobs set this even when MoveAbility isn't 3/4
    /// (legacy). Drives <see cref="IsFly"/>.</summary>
    public bool   Fly             { get; init; }

    /// <summary>WZ <c>info/chaseSpeed</c> - speed % modifier when aggro'd; typically &gt;
    /// Speed for chasers.</summary>
    public int    ChaseSpeed      { get; init; }

    // -- Combat properties ------------------------------------------------------

    /// <summary>WZ <c>info/bodyAttack</c>. Touch-damage mob: player AABB overlap with
    /// the mob triggers a body-attack hit (drives the touch-damage path; see
    /// <c>GameStage</c> touch-damage trigger).</summary>
    public bool   BodyAttack      { get; init; }

    /// <summary>WZ <c>info/pushed</c> - knockback resistance (higher = less knockback
    /// applied when this mob is hit).</summary>
    public int    Pushed          { get; init; }

    /// <summary>WZ <c>info/undead</c>. Undead mobs take bonus damage from holy skills
    /// (Heal etc.) and are healable instead of harmed by certain spells.</summary>
    public bool   Undead          { get; init; }

    /// <summary>WZ <c>info/boss</c>. Boss flag: bigger HP bar, immune to many CC,
    /// scaled exp / drops.</summary>
    public bool   Boss            { get; init; }

    /// <summary>WZ <c>info/noFlip</c>. Sprite isn't symmetric - don't horizontally flip
    /// when the mob faces the other direction (some directional bosses).</summary>
    public bool   NoFlip          { get; init; }

    /// <summary>WZ <c>info/onlyNormalAttack</c>. When true the mob takes damage ONLY from
    /// basic attacks (skillId 0); all skills are no-ops.</summary>
    public bool   OnlyNormalAttack{ get; init; }

    /// <summary>WZ <c>info/damagedByMob</c>. When true the mob takes damage from other
    /// mobs (rare; used in mob-on-mob scenarios).</summary>
    public bool   DamagedByMob    { get; init; }

    /// <summary>WZ <c>info/pickUp</c>. Mob auto-picks-up nearby item drops (and may
    /// re-drop them). Also enables the special-control path (MobApplyCtrl is valid
    /// for pickUp mobs alongside firstAttack mobs).</summary>
    public bool   PickUp          { get; init; }

    /// <summary>WZ <c>info/cannotEvade</c>. EVA is ignored when computing player hits
    /// against this mob.</summary>
    public bool   CannotEvade     { get; init; }

    /// <summary>WZ <c>info/selfDestruction</c>. Explodes on death dealing AoE damage
    /// (bomb slimes etc.).</summary>
    public bool   SelfDestruction { get; init; }

    /// <summary>WZ <c>info/firstSelfDestruction</c>. Variant: detonates on first aggro
    /// rather than on death.</summary>
    public bool   FirstSelfDestruction { get; init; }

    /// <summary>WZ <c>info/invincible</c>. Damage dealt to this mob always resolves to 0
    /// (used for event mobs / boss phases that toggle invincibility).</summary>
    public bool   Invincible      { get; init; }

    /// <summary>WZ <c>info/disable</c>. AI tick returns early - mob doesn't move or
    /// attack (testing flag).</summary>
    public bool   Disable         { get; init; }

    /// <summary>WZ <c>info/notAttack</c>. Mob never attacks the player (passive trainers,
    /// quest mobs).</summary>
    public bool   NotAttack       { get; init; }

    /// <summary>WZ <c>info/firstAttack</c>. Aggressive on sight: the AI chases the
    /// nearest player without needing to be hit first.</summary>
    public bool   FirstAttack     { get; init; }

    // -- Display ----------------------------------------------------------------

    /// <summary>WZ <c>info/hpTagColor</c> - RGB 0xRRGGBB. Fill color of the over-head
    /// HP bar; 0 = default green/yellow/red gradient by HP %.</summary>
    public int    HpTagColor      { get; init; }

    /// <summary>WZ <c>info/hpTagBgcolor</c> - RGB 0xRRGGBB. Background color of the
    /// HP bar; 0 = default dark.</summary>
    public int    HpTagBgColor    { get; init; }

    /// <summary>WZ <c>info/hpTagHide</c>. Hide the over-head HP bar entirely (some NPCs
    /// /summons / quest mobs).</summary>
    public bool   HpGaugeHide     { get; init; }

    /// <summary>WZ <c>info/upperMostLayer</c>. Always render this mob on top of other
    /// mobs/objects regardless of normal z-order.</summary>
    public bool   UpperMostLayer  { get; init; }

    /// <summary>WZ <c>info/weaponID</c> - item id of a weapon the mob visually equips
    /// (overlaid on the sprite).</summary>
    public int    WeaponID        { get; init; }

    /// <summary>WZ <c>info/angerGauge</c>. Mob has a visible anger/charge meter
    /// (boss-style).</summary>
    public bool   AngerGauge      { get; init; }

    /// <summary>WZ <c>info/chargeCount</c> - number of charge stages for the anger gauge
    /// or charge attack.</summary>
    public int    ChargeCount     { get; init; }

    // -- Categorization --------------------------------------------------------

    /// <summary>WZ <c>info/category</c> - mob class (0 = normal, 1 = elite, 2 = mini-
    /// boss, ...). Drives stat / drop scaling.</summary>
    public int    Category        { get; init; }

    /// <summary>WZ <c>info/escortType</c>. &gt; 0 marks this as an "escort" mob (follows /
    /// guards the player); affects special-AI behavior.</summary>
    public int    EscortType      { get; init; }

    /// <summary>WZ <c>info/mobSpeciesCode</c> - dragon / zombie / demon / ... species
    /// taxonomy. Stored as the raw WZ string (the client maps it to an int via
    /// <c>get_mobspecies_code_from_name</c>). Empty = NORMAL.</summary>
    public string MobSpeciesCode  { get; init; } = string.Empty;

    // -- Collections -----------------------------------------------------------

    /// <summary>Per-attack data from <c>Mob.wz/&lt;id&gt;.img/attack1, attack2, ...,
    /// attackF</c> (siblings of <c>info</c>). Keyed by 0-based attack index
    /// (<c>attack1</c> -&gt; 0). Empty when the mob has no attack sub-nodes (body-attack
    /// only mobs).</summary>
    public IReadOnlyDictionary<int, MobAttack> Attacks { get; init; }
        = new Dictionary<int, MobAttack>();

    /// <summary>Status / buff skills the mob can fire, from
    /// <c>info/skill/&lt;n&gt;/{skill,level}</c>. Keyed by skill id (which is a
    /// <see cref="MobSkillType"/> value). Empty when the mob has no skill list.</summary>
    public IReadOnlyDictionary<int, MobSkillRef> Skills { get; init; }
        = new Dictionary<int, MobSkillRef>();

    /// <summary>Elemental resistance map from <c>info/elemAttr</c>. The WZ string is a
    /// sequence of 2-char pairs: first char = ElementAttribute (P=Physical / F=Fire /
    /// I=Ice / L=Light / S=Poison / H=Holy / D=Dark / U=Undead), second char = damage
    /// modifier (1 = immune / 0% , 2 = 50%, 3 = 150%). Empty = neutral to all elements.
    /// </summary>
    public IReadOnlyDictionary<char, char> DamagedElemAttr { get; init; }
        = new Dictionary<char, char>();

    /// <summary>Skill ids the mob is vulnerable to (from
    /// <c>info/damagedBySelectedSkill</c>). Empty = ALL skills work; non-empty = only
    /// these skill ids damage it.</summary>
    public IReadOnlySet<int> DamagedBySkill { get; init; }
        = new HashSet<int>();

    /// <summary>Mob ids spawned in this mob's place when it dies (from
    /// <c>info/revive</c>). Empty = no revives.</summary>
    public IReadOnlyList<int> Revives { get; init; }
        = new List<int>();

    // -- Derived helpers -------------------------------------------------------

    public bool IsStay => MoveAbility == 0;
    public bool IsFly  => MoveAbility >= 3 || Fly;
    public bool IsJump => MoveAbility == 2;
}