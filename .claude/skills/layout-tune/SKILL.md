---
name: layout-tune
description: Use when the user wants to nudge an already-rendered UI element by a small amount — phrases like "move it [N]px [direction]", "move it 3px down", "lower it like 3px", "move it 2 pixels to the right, 4 down", "icon too high", "icon too low", "the [element] is wrong [direction]", "needs to be a bit higher", "needs to be a bit lower", "nudge [element]", "position is wrong", "the position is off", "needs to be [N]px [direction]". Redirects the user to the live debug overlay (`watch.ps1` + `MAPLECLAUDE_DEBUG=1`) for tuning without rebuild, and only when overlay tuning isn't possible, makes the smallest constant-edit in the right Stage/UI file. Triggers ONLY for nudging an existing element; NOT for adding new UI (use `authentic-ui-rebuild`) and NOT for reading WZ origins from scratch (use `ida-lookup` or the `ui-origin-finder` agent).
---

# layout-tune

The MapleClaude project has a live debug overlay (`MAPLECLAUDE_DEBUG=1` enables it) that
lets you drag any registered position knob in real time while `watch.ps1` runs the client
under `dotnet watch`. Most pixel-level nudges should happen through the overlay — not by
hand-editing constants and rebuilding.

This skill catches "move it 3px down" / "lower it a bit" / "icon too high" prompts and
redirects them to the overlay, falling back to a single-constant code edit only when the
element isn't a registered knob.

## Procedure

### 1. Check if `watch.ps1` is running

If the user is mid-iteration, ask one short clarifying question if it's not obvious:

> "Are you running `watch.ps1` with `MAPLECLAUDE_DEBUG=1`? If yes, just tick `drag` in the
> overlay and pull the knob — no rebuild needed. Tell me the final (x, y) once you've got
> it and I'll bake it into the default."

If they confirm overlay mode → stop. Wait for the baked value.
If they want it changed in code immediately → step 2.

### 2. Locate the constant

The position lives in one of:

| Layer | Where to look | Pattern |
| --- | --- | --- |
| Screen-level layout | `src/MapleClaude/Stages/<X>Stage.cs` | A `Vector2` or `(int x, int y)` constant near the top of the stage class |
| Reusable widget | `src/MapleClaude/UI/<...>/Widget.cs` | An `_offset`, `_origin`, `_padding`, etc. field |
| WZ-origin-relative | wherever `canvas.Origin` is consumed | `var pos = origin + new Vector2(dx, dy)` — change `(dx, dy)` |

Use `Grep` for the existing numeric values the user references (e.g. if the icon is at
y=12 and they want y=16, grep for `12` near the relevant class).

### 3. Apply a single-constant change

Make the smallest possible edit — change just the numeric constant. Don't refactor the
layout, don't extract new helpers, don't rename anything. Three example shapes:

```csharp
// before
_iconOffset = new Vector2(12, 4);
// after
_iconOffset = new Vector2(12, 8);   // +4 y per user request
```

```csharp
private const int MinimapIconY = 14;
// →
private const int MinimapIconY = 18;
```

```csharp
var pos = canvas.Origin + new Vector2(2, 6);
// →
var pos = canvas.Origin + new Vector2(4, 10);
```

### 4. Validate

`dotnet build` + (if `watch.ps1` is running) just save the file — Hot Reload picks up
method-body edits live; constants in field initializers count as a rude edit and trigger
an auto-rebuild.

### 5. Promote to overlay if churn is high

If the same element has been nudged 3+ times in the recent history, suggest:

> "We've moved this 4 times. Want me to register it as a debug-overlay knob so you can
> drag-tune it next time without rebuilds?"

The overlay registration is a one-liner per knob — see existing `CharCreate` panels for
the pattern.

## What to avoid

- Hand-eyeballing a "good enough" value when the WZ canvas carries an authoritative
  origin. If the value is wrong systematically (not just one pixel off), the issue is
  probably an `origin` not being honored — that's a `ui-origin-finder` job, not this
  skill.
- Changing more than one constant per request. Resist "fix the whole layout while you're
  in there" — the user asked for a 3px nudge.
- Re-rebuilding the screen from scratch. If the user is in pixel-tuning mode, they've
  already chosen the WZ-authentic layout; respect it.

## Related skills

- `authentic-ui-rebuild` — for "the whole UI is wrong" (not a per-pixel nudge).
- `ida-lookup` — for "where does the original client put this element". The IDB has the
  authoritative coordinate.
- `ui-origin-finder` (agent) — for full-screen origin extraction.

## Linked memories

`[[ui-origin-tooling]]`, `[[ingame-ui-windows]]`, `[[avatar-rendering]]`,
`[[minimap-system]]`, `[[keyconfig-1to1]]`.
