# MapleClaude — Phase Roadmap

This is the multi-phase build plan for MapleClaude. Each phase ships its own
`phase-N/*` branch family, an entry-criteria definition, an exit-criteria
definition, and a single PR to `master` at completion.

## Phase 1 — Pre-game flow (shipped)

**Scope.** Launch → title → login → world/server select → character list →
character create → PIN entry (dormant on Kinoko) → channel migrate handoff.

**Exit criteria.** With a running Kinoko server, the client connects, logs in,
shows the world list, lets the user create/select a character, reaches the
channel migrate, and logs:

```
MigrateIn ACK observed — Phase 2 boundary reached
```

## Phase 2 — Field load & avatar render (shipped)

**Scope.** Decode `SetField(141)` (the first in-game packet after migrate). Load
the player's starting map from `Map.wz/Map/Map<prefix>/<mapId>.img` (footholds,
portals, multi-layer backgrounds). Render the player's `AvatarLook`
(body + head + face + hair + equips from `Character.wz`) standing on the map.

**Exit criteria.** First-spawn map renders with the player's avatar visible at
the correct starting coordinates and standing on the correct foothold.

## Phase 3 — Movement & camera (shipped)

**Scope.** Arrow keys → walk; Alt → jump. v95 `UserMove(44)` outgoing
encoding (the move-path with NORMAL / JUMP / TELEPORT / STAT_CHANGE /
START_FALL_DOWN / FLYING_BLOCK sub-action categories). Gravity, foothold
ground-snap. `Camera2D` follow with deadzone + VR-bounds clamp.

**Exit criteria.** Player can walk, jump, and fall onto footholds; the server
accepts the outgoing `UserMove` packets without disconnecting.

## Phase 3.5 — In-game cosmetic UI (shipped)

**Scope.** All the in-game panels needed for a real client are laid out
visually with placeholder data — no server wiring yet. `StatusBar` (HP/MP/EXP
gauges + quickslot bar + submenu buttons), `ChatBar`, `MiniMap`,
`ItemInventory` (4-tab grid), `EquipInventory`, `SkillBook`, `StatsInfo`
(STR/DEX/INT/LUK + AP), `QuestLog`, `KeyConfig` (re-bindable keys),
`OptionMenu`, `CharInfo`, `Shop`, `NpcTalk`, `Notice`, `WorldMap`,
`UserList` (friends/party/guild tabs), `ChannelSelect`, `StatusMessenger`
(floating loot/exp/buff messages), `CashShopStage` (1024×768 overlay with
9 category tabs + paginated 7×2 item grid).

**Exit criteria.** Every panel renders with sensible placeholder data,
opens/closes via its StatusBar button or hotkey, and the user can interact
with the controls. Server data wiring is deferred to phases 4–10.

## Phase 4 — Mobs & combat

**Scope.** `MobEnterField`, `MobMove`, `MobChangeController`. Mob sprites,
animations, health. `UserMeleeAttack` packet construction with attack info.
Damage display.

**Exit criteria.** Player can kill a low-level mob, see damage numbers, and
have the mob respawn.

## Phase 5 — NPCs & dialog

**Scope.** `NpcEnterField`, click-to-talk, `ScriptMessage` decoder for
all dialog kinds (say/ask/menu/quiz/get-text/get-number/yes-no/icon).
Dialog UI widget.

**Exit criteria.** Click an NPC; talk through a multi-message script with
choices; trigger a server-side script effect.

## Phase 6 — Inventory & items

**Scope.** Inventory tabs (equip/use/setup/etc/cash), drag/drop,
`InventoryOperation` packet decoding, equip swap with stat updates,
tooltip widget. Cash shop UI shell (no real transactions).

**Exit criteria.** Pick up an item; equip it; see stats change; drop it.

## Phase 7 — Skills, jobs, buffs

**Scope.** Skill tree UI, skill bar, key configuration, `UserSkillUseRequest`,
`TemporaryStatSet` / `TemporaryStatReset` decoders, buff icon HUD.

**Exit criteria.** Use a job's first-class skill, see the buff appear, watch
it expire.

## Phase 8 — Social

**Scope.** Chat (`UserChat`), whispers, party invite / leave / chat, friend
list, guild basics.

**Exit criteria.** Two clients can join a party, chat in party channel, and
see each other's positions on the minimap.

## Phase 9 — Loot

**Scope.** Drop spawn (`DropEnterField`), pickup (`DropLeaveField` reasons),
meso pickup, EXP gain popups, item pickup.

**Exit criteria.** Killing mobs reliably drops loot the player can pick up.

## Phase 10 — Polish

**Scope.** Map transitions (portals), channel/cash-shop migrate, XAudio2 BGM
and SFX via MonoGame, settings persistence, full keybind editor, error
recovery, AOT publish (single self-contained `.exe`).

**Exit criteria.** First-time install → AOT-published `.exe` runs without the
.NET runtime; user can play from level 1 to mid-game continuously.

## Conventions across phases

- **Branch per phase**, commit per logical unit (file compiles, test passes,
  opcode decodes, stage renders).
- **Never commit on master**; PR per phase at exit.
- **Ask the user before each commit** and before opening a PR.
- **PR body**: Summary, Test plan, Risk assessment, Recommendation.
- **Privacy guard**: `git grep` for local paths / private project names
  before every commit; abort if anything matches.
