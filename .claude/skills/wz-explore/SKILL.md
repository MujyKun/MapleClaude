---
name: wz-explore
description: Use when the user wants to see what nodes / fields / actions a WZ subsystem can contain, or which entries have a specific optional capability — phrases like "what nodes does X have", "which mobs have a `jump` node", "all possible nodes for a mob", "what action nodes can a mob have", "find every item with a `req` block", "show me every field under one mob", "what optional fields exist on a skill", "enumerate the WZ structure of Y". Drives the `tools/wz-explorer/wz_explorer.py` script (which wraps `tools/wz-dump`) to surface the WZ structure as JSON. Triggers ONLY for *read-only* exploration; for gap auditing against the C# client use `wz-gap-audit`, for implementing missing capabilities use `wz-gap-implement`, for a full typed-model sweep use `wz-subsystem-research`.
---

# wz-explore

Quick, read-only WZ exploration. Answers questions like:

- "What action nodes can a mob have?"
- "Which mobs have a `jump` node?"
- "What does the `info` block of mob 100100 contain?"
- "Find every `skill\d+` node in the Empress Cygnus mob."

Backed by `tools/wz-explorer/wz_explorer.py` (Python frontend) and
`tools/wz-dump` (C# CLI using `src/MapleClaude.Wz`).

## When NOT to use

- Comparing WZ contents against what the C# client reads → `wz-gap-audit`.
- Implementing the discovered missing nodes → `wz-gap-implement`.
- Full sweep producing a typed C# model + docs/ page → `wz-subsystem-research`.
- Editing the WZ reader code itself → `wz-reader`.
- Single-symbol disasm lookup → `ida-lookup`.

## Prerequisites

- `MAPLECLAUDE_WZ_DIR` must be set to a directory containing the `.wz` files
  (it already is on the user's machine — see `CLAUDE.local.md`).
- First call may rebuild `tools/wz-dump` via `dotnet run`; subsequent calls
  use the cached binary. If you want one explicit build first, run:

  ```bash
  dotnet build tools/wz-dump/wz-dump.csproj -c Debug
  ```

## Commands

`python tools/wz-explorer/wz_explorer.py <command> <wz> <path> [...flags]`

| Command   | Purpose | Typical use |
|-----------|---------|-------------|
| `keys`    | Direct children + types | "What are the top-level nodes of mob 100100?" |
| `tree`    | Recursive shape (no leaf primitives by default — use `detail`) | "What's the structure of `Map.wz/.../obj`?" |
| `detail`  | Recursive shape **with primitive values** | "Give me every field of one mob's `info` block." |
| `search`  | Regex by leaf name across the subtree | "Find every `skill\d+` node in this mob." |
| `coverage`| For each direct child of `<path>`, list its own child names | Raw input for cross-entry comparisons. |
| `union`   | Coverage rolled up: name → (count, ratio) ranked desc | "What optional fields exist on mobs and how rare are they?" |
| `with-key`| List entries whose subtree contains `<key>` | "Which mobs have `jump`?" |

`<wz>` is the file (e.g. `Mob.wz`). `<path>` is slash-separated; `""` = root.

All commands emit JSON on stdout. Pipe to `python -c` or `jq` for shaping.

## Standard workflow

When the user asks "what does X have" or "which Y has Z", do this:

1. **Pick the right command.**
   - "what fields does ENTRY have" → `keys` or `detail`
   - "which entries have FIELD" → `with-key`
   - "what fields can entries have, ranked" → `union`
   - "find every node matching PATTERN" → `search`

2. **Run it via Bash.** Always `2>/dev/null` (or redirect stderr to a file)
   so dotnet's build chatter doesn't mix into the JSON stream. Example:

   ```bash
   python tools/wz-explorer/wz_explorer.py union Mob.wz "" --depth 1 --top 30 2>/dev/null
   ```

3. **Summarise the JSON for the user.** Show the *counts* and *interesting
   outliers*, not the raw blob. For `union`, sort by ratio: ratios near 1.0
   are core, low ratios are optional/rare. For `with-key`, give the count and
   a short sample of matches.

4. **Cross-reference Kinoko + the IDB sparingly.** This skill stops at "here's
   what the WZ has". If the user wants the *runtime semantics* of a discovered
   field, hand off to `ida-lookup` for the C++ usage or grep Kinoko's
   `kinoko/provider/.../X.java` for the server-side loader.

## Output expectations

- Be terse. One paragraph + a small table beats a wall of JSON.
- Include the `entries` count so the reader sees the denominator.
- For `union`, surface 10–30 rows max; fold the long tail into a "+N more"
  line if needed.
- Quote the JSON only when the user explicitly wants the raw output.

## Notes

- The Python script is read-only — it never writes to the WZ or any project
  file. Safe to invoke without user confirmation.
- `with-key` matches case-insensitively on the leaf node name and supports a
  trailing `/<name>` form for nested keys (rarely needed at default depth=1).
- For one-off node lookups that don't need cross-entry stats, use `keys` /
  `detail` rather than `coverage` to keep wall-clock time low.
