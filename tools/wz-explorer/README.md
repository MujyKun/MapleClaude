# wz-explorer

Read-only Python frontend for exploring GMS v95 WZ files. Used by the
`wz-explore`, `wz-gap-audit`, and `wz-gap-implement` skills/agents.

Calls into the C# `tools/wz-dump` binary under the hood — that uses the
project's own `src/MapleClaude.Wz` reader, so there's no second copy of the
PKG1 / AES / canvas-decompress logic to keep in sync.

## Requirements

- .NET SDK (the script invokes `dotnet run` if the prebuilt binary is missing)
- `MAPLECLAUDE_WZ_DIR` set to a directory containing the `.wz` files
- Python 3.9+ (uses only stdlib)

## Commands

```
wz_explorer.py tree     <wz> <path> [--depth N]
wz_explorer.py keys     <wz> <path>
wz_explorer.py search   <wz> <path> <regex> [--depth N] [--limit N]
wz_explorer.py detail   <wz> <path> [--depth N]
wz_explorer.py coverage <wz> <path> [--depth N] [--limit N]
wz_explorer.py union    <wz> <path> [--depth N] [--limit N] [--top N]
wz_explorer.py with-key <wz> <path> <key> [--depth N] [--limit N]
```

`<wz>` is either an absolute path to the `.wz` file or a bare name like
`Mob.wz` resolved against `$MAPLECLAUDE_WZ_DIR`. `<path>` is a
slash-separated node path; `""` means the root directory.

All output is JSON on stdout. Non-zero exit code on error.

## Recipes

### What action nodes does mob 100100 have?

```bash
python tools/wz-explorer/wz_explorer.py keys Mob.wz 100100.img
```

### What does the full info block of a mob contain?

```bash
python tools/wz-explorer/wz_explorer.py detail Mob.wz 100100.img/info --depth 4
```

### Which mobs have a `jump` action node?

```bash
python tools/wz-explorer/wz_explorer.py with-key Mob.wz "" jump --depth 1
```

### Of all mobs, what action nodes are possible and how rare are they?

```bash
python tools/wz-explorer/wz_explorer.py union Mob.wz "" --depth 1 --top 50
```

The ranked output sorts by frequency. Common rows (`info`, `stand`, `die1`)
are presumably already wired up; low-ratio rows (`fly`, `chase`, `skill5`)
flag optional behaviour that may not yet be implemented in the client.

### Find every `skill*` node anywhere under one mob

```bash
python tools/wz-explorer/wz_explorer.py search Mob.wz 100100.img "^skill\d+$" --depth 3
```
