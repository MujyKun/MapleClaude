---
name: wz-gap-audit
description: Use when the user wants to know what WZ nodes/fields exist in a subsystem that the C# client doesn't read yet — phrases like "what's missing in our client", "what wz nodes are we not handling", "what are we missing for mobs", "what fields aren't we using", "audit our mob coverage", "what's not implemented yet on items", "compare WZ to our client", "see the full overview, what may be missing", "find unread WZ nodes", "are we using everything from X.wz". Pairs the `wz-explore` output (union of WZ nodes seen in the WZ) with grep over the matching C# service / Info file to produce a diff of *unread* nodes ranked by frequency. Output names the specific files and shows what's wired vs not. Triggers ONLY for the audit pass; for actually wiring the missing nodes use `wz-gap-implement`.
---

# wz-gap-audit

Compares what the WZ subsystem *can* carry to what the C# client *does* read.
Surfaces every WZ node name that appears in the WZ universe but is never
referenced from the matching C# code path.

## When to use

- "What WZ nodes are we not using for mobs?"
- "What might be missing in the client?"
- "What optional capabilities do we ignore on items?"
- Any "find the gap" question between WZ assets and our C# parsers.

## When NOT to use

- Bare WZ exploration (no comparison to C# code) → `wz-explore`.
- Actually fixing the gap → `wz-gap-implement`.
- Full subsystem sweep with new typed model + docs page → `wz-subsystem-research`.

## Inputs

- A WZ subsystem (`Mob.wz`, `Item.wz`, `Skill.wz`, `Npc.wz`, `Reactor.wz`,
  `Quest.wz`, `Map.wz`, `Character.wz`, …).
- The matching client-side service / info file (in most cases under
  `src/MapleClaude/Character/`, `src/MapleClaude/Item/`,
  `src/MapleClaude/Skill/`, etc.).

The audit only needs the *subsystem name*; everything else is discovered.

## Workflow

1. **Get the universe.** Run the union via the explorer (always `2>/dev/null`
   so dotnet build chatter doesn't mix into stdout):

   ```bash
   python tools/wz-explorer/wz_explorer.py union <Subsystem>.wz "" --depth 2 --top 0 2>/dev/null
   ```

   `--depth 2` catches both top-level action nodes (`stand`, `jump`, …) AND
   inner `info/*` fields (`info/maxHP`, `info/level`, …). `--top 0` returns
   the full ranked list.

   For some subsystems the entries don't sit at the WZ root — e.g. items are
   under `Item.wz/Eqp/Eqp/<category>/<id>.img`. Use `keys` first to find the
   right subtree, then `union` on that subtree.

2. **Find the C# reader file.** Locate by convention:

   | WZ          | Typical reader file |
   |-------------|---------------------|
   | `Mob.wz`    | `src/MapleClaude/Character/MobInfoService.cs` + `MobInfo.cs` |
   | `Item.wz`   | `src/MapleClaude/Item/ItemInfoService.cs` (or similar) |
   | `Skill.wz`  | `src/MapleClaude/Character/SkillInfoService.cs` + `SkillInfo.cs` |
   | `Npc.wz`    | `src/MapleClaude/Character/NpcInfoService.cs` (or similar) |
   | `Reactor.wz`| `src/MapleClaude/Character/ReactorInfoService.cs` |
   | `Quest.wz`  | `src/MapleClaude/Quest/QuestInfoService.cs` |
   | `Map.wz`    | `src/MapleClaude/Map/MapInfoService.cs` / `FieldScene.cs` |

   Verify with `Glob 'src/**/*Info*.cs'` and `Grep '<wz>.GetItem'` if the
   convention doesn't hold. Always cross-check by grepping for one known
   field (e.g. `"maxHP"`) — the right file will be the one that reads it.

3. **Extract the read set.** Grep the service file for every quoted node
   name. The names typically appear inside `GetItem("...")`,
   `ReadInt(info, "...")`, `ReadBool(info, "...")`, `ReadStr(info, "...")`,
   or as a switch/case key on a `string` from `prop.Items`:

   ```bash
   grep -oE '"[a-zA-Z][a-zA-Z0-9_]*"' <reader-file> | sort -u
   ```

   You'll get more than you want (log strings, comments). Dedup and treat the
   set as "definitely referenced"; false positives are tolerable, false
   negatives are not.

4. **Diff.** For each name in the union, mark it `[read]` if it's in the
   grep set, `[unread]` otherwise. Sort the unread rows by frequency desc.

5. **Augment with Kinoko / IDB meaning.** For the top ~10 unread rows,
   cross-reference upstream Kinoko's matching template loader (e.g.
   `kinoko/provider/mob/MobTemplate.java` for Mob.wz) and the IDB's matching
   class via `ida-lookup`. Add a one-line meaning column. Don't paste raw
   bodies — paraphrase.

6. **Report.** A single table with these columns:

   ```
   node        | count / ratio | status  | meaning (short)
   ----------- + ------------- + ------- + ----------------------------------
   info/level  | 1887  99.9%   | [read]  | base mob level
   stand       | 1338  70.8%   | [read]  | idle action canvas + delay
   jump        |  215  11.4%   | [unread]| jump action; absent on grounded mobs
   skill1      |  390  20.7%   | [unread]| visible cast canvases for skill #1
   chase       |   38   2.0%   | [unread]| separate animation while aggro'd
   …
   ```

   Then a short summary: "N nodes unread, top candidates to wire are A / B /
   C because <reason>."

## Heuristics for prioritising

- **High frequency + missing.** A node present in >20% of entries that's
  unread is almost always a real gap.
- **Visible animation.** Action nodes (`stand`, `move`, `jump`, `fly`,
  `attack*`, `skill*`, `hit`, `die*`, `regen`, `say`) that aren't read map
  directly to missing on-screen behaviour — easy to test.
- **Server-validated fields.** If Kinoko's `MobTemplate.from()` (or
  equivalent) reads a field, the server cares about it; if the client also
  cares (combat damage formulas, drop tables) the gap is functional, not
  cosmetic.
- **Cosmetic only.** Hue / colour / visual-flag fields the server doesn't
  read are still worth flagging but lower priority.

## Output discipline

- Lead with the headline: "Mob.wz exposes 42 distinct nodes; MobInfoService
  reads 28; 14 are unread."
- One table.
- One short paragraph naming the top 3–5 actionable gaps.
- Don't paste raw JSON. Don't paste full file contents.

## Boundaries

- **Read-only.** Never edit C# code from this skill — write up the audit
  and stop. Handoff to `wz-gap-implement` for the wiring.
- Never commit or push.
- The grep heuristic is best-effort. If you suspect a false positive (a
  field that *is* read indirectly), check with `Grep` for the actual usage
  before claiming it's unread.
