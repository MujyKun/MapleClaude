# Mob.wz attribute reference (v95)

Quick-reference for every `Mob.wz/<id>.img` attribute the client cares about, with the
exact WZ node path, the C# property in our typed model, the type, semantics, and
default. Two authoritative sources:

- **Server (Kinoko `MobTemplate.from()`)** — what the server validates. Anything not
  here is purely cosmetic / client-side.
- **v95 client `CMobTemplate`** (via the IDB) — what the original client reads at
  runtime; the larger superset (movement / display / control flags).

The typed C# model lives in:
- `src/MapleClaude/Character/MobInfo.cs` — the bag of scalars + collections.
- `src/MapleClaude/Character/MobAttack.cs` — per-attack info (`attack1`/`attack2`/…).
- `src/MapleClaude/Character/MobSkillRef.cs` + `MobSkillType.cs` — per-skill info.
- `src/MapleClaude/Character/MobInfoService.cs` — parser + cache.

`info/link` is followed automatically: variant recolours inherit the canonical
template's attacks + skills + info scalars.

---

## Identity

| WZ path | C# (`MobInfo`) | Type | Semantics | Default |
| --- | --- | --- | --- | --- |
| `<id>` | `TemplateId` | int | Mob template id (from the `.img` name) | 0 |
| `info/level` | `Level` | int | Display level + exp formula input | 1 |
| `info/exp` | `Exp` | int | Base experience granted on kill | 0 |

## Stats (server-validated)

| WZ path | C# | Type | Semantics | Default |
| --- | --- | --- | --- | --- |
| `info/maxHP` | `MaxHp` | int | Mob max HP | 1 |
| `info/maxMP` | `MaxMp` | int | Mob max MP — gates skill firing | 0 |
| `info/PADamage` | `Pad` | int | Physical attack damage (CalcDamage::PDamage) | 0 |
| `info/PDRate` | `Pdr` | int | Physical defense rate (%) | 0 |
| `info/MADamage` | `Mad` | int | Magical attack damage (CalcDamage::MDamage) | 0 |
| `info/MDRate` | `Mdr` | int | Magical defense rate (%) | 0 |
| `info/acc` | `Acc` | int | Mob accuracy vs player EVA | 0 |
| `info/eva` | `Eva` | int | Mob evasion vs player ACC | 0 |
| `info/hpRecovery` | `HpRecovery` | int | Passive HP regen / tick | 0 |
| `info/mpRecovery` | `MpRecovery` | int | Passive MP regen / tick | 0 |
| `info/fixedDamage` | `FixedDamage` | int | When > 0, all damage to this mob is clamped to this value | 0 |

## Lifecycle

| WZ path | C# | Type | Semantics | Default |
| --- | --- | --- | --- | --- |
| `info/removeAfter` | `RemoveAfter` | int (sec) | When > 0 mob auto-despawns after this many seconds | 0 |
| `info/dropItemPeriod` | `DropItemPeriod` | int (ms) | When > 0 mob drops items on this interval throughout its lifetime | 0 |

## Movement / AI (client-only — Kinoko doesn't parse these)

| WZ path | C# | Type | Semantics | Default |
| --- | --- | --- | --- | --- |
| `info/moveAbility` | `MoveAbility` | int | 0=STAY, 1=WALK, 2=JUMP, 3/4=FLY | 1 |
| `info/speed` | `Speed` | int | Walk speed % modifier | 0 |
| `info/flySpeed` *or* `info/fs` | `FlySpeed` | int | Fly speed % modifier (fs wins if both present) | 0 |
| `info/fly` | `Fly` | bool | Some mobs flag flying without moveAbility==3/4 (legacy) | false |
| `info/chaseSpeed` | `ChaseSpeed` | int | Speed % modifier when aggro'd | 0 |
| — | `IsStay` | bool | `MoveAbility == 0` | — |
| — | `IsFly`  | bool | `MoveAbility >= 3 \|\| Fly` | — |
| — | `IsJump` | bool | `MoveAbility == 2` | — |

## Combat properties

| WZ path | C# | Type | Semantics | Default |
| --- | --- | --- | --- | --- |
| `info/bodyAttack` | `BodyAttack` | bool | Touch-damage mob (AABB overlap triggers a hit) | false |
| `info/pushed` | `Pushed` | int | Knockback resistance | 0 |
| `info/undead` | `Undead` | bool | Holy skills do bonus / heal-based skills reverse | false |
| `info/boss` | `Boss` | bool | Boss HP bar, CC immunity, scaled rewards | false |
| `info/noFlip` | `NoFlip` | bool | Sprite isn't symmetric — don't horizontally flip | false |
| `info/onlyNormalAttack` | `OnlyNormalAttack` | bool | Skills do 0 damage; only basic attacks work | false |
| `info/damagedByMob` | `DamagedByMob` | bool | Can be hurt by other mobs | false |
| `info/pickUp` | `PickUp` | bool | Auto-picks-up item drops; also makes MobApplyCtrl valid | false |
| `info/cannotEvade` | `CannotEvade` | bool | EVA is ignored when player attacks | false |
| `info/selfDestruction` | `SelfDestruction` | bool | Explodes on death | false |
| `info/firstSelfDestruction` | `FirstSelfDestruction` | bool | Explodes on first aggro instead of death | false |
| `info/invincible` | `Invincible` | bool | Damage always resolves to 0 | false |
| `info/disable` | `Disable` | bool | AI is suspended (test flag) | false |
| `info/notAttack` | `NotAttack` | bool | Mob never attacks (passive trainers) | false |
| `info/firstAttack` | `FirstAttack` | bool | Aggressive on sight — chases nearest player | false |

## Display

| WZ path | C# | Type | Semantics | Default |
| --- | --- | --- | --- | --- |
| `info/hpTagColor` | `HpTagColor` | int (0xRRGGBB) | HP bar fill color; 0 = default gradient | 0 |
| `info/hpTagBgcolor` | `HpTagBgColor` | int (0xRRGGBB) | HP bar background; 0 = default dark | 0 |
| `info/hpTagHide` | `HpGaugeHide` | bool | Hide HP bar entirely | false |
| `info/upperMostLayer` | `UpperMostLayer` | bool | Always render on top layer | false |
| `info/weaponID` | `WeaponID` | int | Item id of a visually-overlaid weapon | 0 |
| `info/angerGauge` | `AngerGauge` | bool | Has a visible anger/charge meter | false |
| `info/chargeCount` | `ChargeCount` | int | Number of stages on the anger gauge | 0 |

## Categorization

| WZ path | C# | Type | Semantics | Default |
| --- | --- | --- | --- | --- |
| `info/category` | `Category` | int | 0=normal, 1=elite, 2=mini-boss, … | 0 |
| `info/escortType` | `EscortType` | int | > 0 = escort mob (follows/guards) | 0 |
| `info/mobSpeciesCode` | `MobSpeciesCode` | string | Species name (dragon/zombie/demon/…) | "" |

## Per-attack collection — `MobInfo.Attacks` (Dictionary<int, MobAttack>)

Keyed by 0-based attack index. WZ path: `attack1` → index 0, …, `attackF` → 14
(hex digit). Each value reads from `<attack#>/info/*`:

### Kinoko-validated (server reads these)

| WZ key | C# (`MobAttack`) | Type | Semantics |
| --- | --- | --- | --- |
| `disease` | `SkillId` | int | `MobSkillType` id applied on hit (0 = none) |
| `level` | `SkillLevel` | int | Skill level for the applied status |
| `conMP` | `ConMp` | int | MP the mob consumes |
| `mpBurn` | `MpBurn` | int | Player MP drained on hit |
| `magic` | `Magic` | bool | 1 = magic attack (uses MAD vs MDR), 0 = physical |
| `deadlyAttack` | `DeadlyAttack` | bool | Bypasses EVA + sets player HP & MP to 1 |

### Client-side (visual / AI cooldowns)

| WZ key | C# | Type | Semantics |
| --- | --- | --- | --- |
| `bulletNumber` | `BulletNumber` | int | Projectiles fired |
| `bulletSpeed` | `BulletSpeed` | int | Projectile speed px/frame |
| `magicElemAttr` | `MagicElemAttr` | int | Element for magic attacks (Fire/Ice/Holy/…) |
| `jump` | `JumpAttack` | bool | Mob jumps when firing |
| `knockBack` | `KnockBack` | bool | Pushes player on hit |
| `attackAfter` | `AttackAfter` | int (ms) | Cooldown before next attack |
| `effectAfter` | `EffectAfter` | int (ms) | Delay before visual effect plays |
| `tremble` | `Tremble` | bool | Screen shake on hit |
| `rush` | `Rush` | bool | Mob charges toward player |
| `hitAttach` | `HitAttach` | bool | Projectile sticks on hit |
| `facingAttach` | `FacingAttach` | bool | Attack always faces player |
| `effect`, `hit`, `ball`, `areaWarning` | matching string props | string | UOL paths to FX / hit anim / projectile / AoE warning |

## Per-skill collection — `MobInfo.Skills` (Dictionary<int, MobSkillRef>)

WZ path: `info/skill/<n>/{skill,level}`. Keyed by skill id, which is also a
[`MobSkillType`](../src/MapleClaude/Character/MobSkillType.cs) value:

| Range | Category |
| --- | --- |
| **100–104** | Self-buff (PowerUp, MagicUp, PGuardUp, MGuardUp, Haste) |
| **110–115** | Multi-mob buffs (`*_M`: PowerUpM, MagicUpM, …, HealM, HasteM) |
| **120–129** | Player debuffs (Seal, Darkness, Weakness, Stun, Curse, Poison, Slow, Dispel, Attract, BanMap) |
| **130–137** | Area / control (AreaFire, AreaPoison, ReverseInput, Undead, StopPortion, StopMotion, Fear, Frozen) |
| **140–145** | Immunes + counters (PhysicalImmune, MagicImmune, HardSkin, PCounter, MCounter, PMCounter) |
| **150–158** | Mob temporary-stat buffs (Pad, Mad, Pdr, Mdr, Acc, Eva, Speed, SealSkill, BalrogCounter) |
| **160–162** | Interaction (SpreadSkillFromUser, HealByDamage, Bind) |
| **200–201** | Summons (Summon, SummonCube) |

## Elemental resistances — `MobInfo.DamagedElemAttr`

WZ path: `info/elemAttr` — a single string of 2-char pairs, e.g. `"P1F2I3"` → 
`{ 'P':'1', 'F':'2', 'I':'3' }`:
- First char = `ElementAttribute`: P=Physical, F=Fire, I=Ice, L=Light, S=Poison, H=Holy, D=Dark, U=Undead
- Second char = damage modifier: `1` = immune (0%), `2` = 50%, `3` = 150%

## Skill-immune list — `MobInfo.DamagedBySkill`

WZ path: `info/damagedBySelectedSkill/<n>` integer entries. Empty = ALL skills work;
non-empty = ONLY these skill ids damage the mob.

## Revives — `MobInfo.Revives`

WZ path: `info/revive/<n>` integer mob ids. When the mob dies, these spawn in its
place. The revive delay (when those replacements appear) is the sum of `die1` frame
delays — Kinoko calculates it the same way.

## Animations (not in MobInfo — looked up by `MobLook` directly)

Animations are siblings of `info` under `<id>.img`, NOT under `info/*`:
- `stand` / `move` / `jump` / `fly` — movement-driven (gated by `moveAbility`).
- `attack1` … `attackF` — per-attack animations (with `<attack#>/info` for stats).
- `hit1`, `hit2` — being-hit (flinch / knockback).
- `die1`, `die2` — death.
- `regen` — passive regen visual.
- `skill` — skill-specific animation frames.

Each frame is a `WzCanvas` with a `delay` int (ms). `MobLook.Load` walks them.
