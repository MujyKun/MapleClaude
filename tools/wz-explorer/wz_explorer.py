#!/usr/bin/env python3
"""wz_explorer — Python frontend over the wz-dump C# CLI.

The C# binary (tools/wz-dump) does the actual WZ parsing using the project's
own reader (src/MapleClaude.Wz). This script only orchestrates: it calls
wz-dump with the right args, parses JSON, and adds the higher-level surfaces
needed by the wz-explore / wz-gap-audit / wz-gap-implement skills.

Why a Python wrapper? Two things wz-dump intentionally doesn't do:

  1. **Wide coverage matrix.** "Of every mob in Mob.wz, which child node names
     appear in at least one of them, and which ones appear in mob X?" The
     wz-dump `coverage` command emits per-entry rows; this script joins them
     into a sparse matrix and surfaces the *union* + per-entry deltas.

  2. **Frequency / rarity stats.** The same union, sorted by how many entries
     use each capability. Drives the gap audit ("99% of mobs use `info/level`
     and we read it; 47% use `info/skill` and we don't").

Both let an agent see at a glance which optional capabilities exist in a WZ
subsystem and which ones the C# client has wired up.

Usage:

    wz_explorer.py tree     <wz> <path> [--depth N]
    wz_explorer.py keys     <wz> <path>
    wz_explorer.py search   <wz> <path> <regex> [--depth N] [--limit N]
    wz_explorer.py detail   <wz> <path> [--depth N]
    wz_explorer.py coverage <wz> <path> [--depth N] [--limit N]
    wz_explorer.py union    <wz> <path> [--depth N] [--limit N] [--top N]
    wz_explorer.py with-key <wz> <path> <key> [--depth N] [--limit N]

All commands emit JSON on stdout. Errors go to stderr with non-zero exit.

`wz` may be an absolute path or a bare name (e.g. `Mob.wz`) resolved against
`$MAPLECLAUDE_WZ_DIR`.

Examples:

    # Every direct child of Mob.wz/100100.img (one mob's full action set)
    wz_explorer.py keys Mob.wz 100100.img

    # Which mobs in Mob.wz have a `jump` action node?
    wz_explorer.py with-key Mob.wz "" jump

    # Across all mobs, what optional action nodes can a mob have?
    wz_explorer.py union Mob.wz "" --depth 1 --top 40

    # Every leaf field inside one mob's `info` block
    wz_explorer.py detail Mob.wz 100100.img/info
"""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
from collections import Counter
from pathlib import Path
from typing import Any

# ── locate wz-dump ──────────────────────────────────────────────────────────

_REPO_ROOT = Path(__file__).resolve().parents[2]


def _find_wz_dump() -> list[str]:
    """Return the argv prefix that invokes the wz-dump CLI.

    Prefers the prebuilt artifact if present; otherwise falls back to
    `dotnet run` against the csproj. Either way avoids relying on `PATH`.
    """
    project = _REPO_ROOT / "tools" / "wz-dump" / "wz-dump.csproj"
    # Prebuilt? Pick the first Debug or Release output we find.
    bin_dir = _REPO_ROOT / "tools" / "wz-dump" / "bin"
    if bin_dir.is_dir():
        for cfg in ("Release", "Debug"):
            for tfm_dir in (bin_dir / cfg).glob("*"):
                exe = tfm_dir / "wz-dump.exe"
                if exe.is_file():
                    return [str(exe)]
                dll = tfm_dir / "wz-dump.dll"
                if dll.is_file():
                    return ["dotnet", str(dll)]
    # Fallback — dotnet run will incrementally rebuild.
    if not project.is_file():
        raise FileNotFoundError(
            f"wz-dump.csproj not found at {project} — run from inside the "
            f"MapleClaude repo, or build tools/wz-dump first."
        )
    return ["dotnet", "run", "--project", str(project), "-c", "Debug", "--"]


def _run_wz_dump(*args: str) -> dict[str, Any]:
    """Call wz-dump with the given args and parse the JSON document on stdout."""
    cmd = _find_wz_dump() + list(args)
    proc = subprocess.run(
        cmd,
        capture_output=True,
        text=True,
        encoding="utf-8",
        check=False,
    )
    if proc.returncode != 0:
        sys.stderr.write(proc.stderr)
        raise SystemExit(proc.returncode)
    out = proc.stdout.strip()
    if not out:
        raise SystemExit("wz-dump emitted no output")
    try:
        return json.loads(out)
    except json.JSONDecodeError as exc:
        # The first json.loads sees both the dotnet build chatter (when using
        # `dotnet run`) AND the trailing JSON. Try to recover by scanning back
        # to the last '{'.
        last_open = out.rfind("\n{")
        if last_open >= 0:
            try:
                return json.loads(out[last_open + 1 :])
            except json.JSONDecodeError:
                pass
        sys.stderr.write(f"wz-dump output is not JSON: {exc}\n{out[:400]}…\n")
        raise SystemExit(1) from exc


# ── thin pass-throughs ──────────────────────────────────────────────────────

def cmd_tree(args: argparse.Namespace) -> dict[str, Any]:
    return _run_wz_dump("tree", args.wz, args.path, "--depth", str(args.depth))


def cmd_keys(args: argparse.Namespace) -> dict[str, Any]:
    return _run_wz_dump("keys", args.wz, args.path)


def cmd_search(args: argparse.Namespace) -> dict[str, Any]:
    return _run_wz_dump(
        "search", args.wz, args.path, args.regex,
        "--depth", str(args.depth), "--limit", str(args.limit),
    )


def cmd_detail(args: argparse.Namespace) -> dict[str, Any]:
    return _run_wz_dump("detail", args.wz, args.path, "--depth", str(args.depth))


def cmd_coverage(args: argparse.Namespace) -> dict[str, Any]:
    extra = ["--depth", str(args.depth)]
    if args.limit:
        extra += ["--limit", str(args.limit)]
    return _run_wz_dump("coverage", args.wz, args.path, *extra)


# ── value-added surfaces ────────────────────────────────────────────────────

def cmd_union(args: argparse.Namespace) -> dict[str, Any]:
    """Aggregate `coverage` into a frequency table.

    Output:
        {
          "path": "...", "entries": N, "unique_children": M,
          "ranked": [ { "name": "info", "count": 1889, "ratio": 1.00 }, ... ]
        }

    The ranked list is sorted desc by count, then alpha. Truncated to --top
    rows. Use it to spot:
      - common nodes the client probably reads (ratio near 1.0)
      - long-tail optional nodes the client may not handle (low ratio)
    """
    raw = cmd_coverage(args)
    counter: Counter[str] = Counter()
    for row in raw["rows"]:
        for child in row["children"]:
            counter[child] += 1
    entries = max(raw["entries"], 1)
    ranked = sorted(
        ({"name": k, "count": v, "ratio": round(v / entries, 4)} for k, v in counter.items()),
        key=lambda r: (-r["count"], r["name"]),
    )
    if args.top:
        ranked = ranked[: args.top]
    return {
        "path": raw["path"],
        "depth": raw["depth"],
        "entries": raw["entries"],
        "unique_children": len(counter),
        "ranked": ranked,
    }


def cmd_with_key(args: argparse.Namespace) -> dict[str, Any]:
    """List every entry of <path> whose subtree contains <key>.

    Example:
        wz_explorer.py with-key Mob.wz "" jump
    returns:
        { "key": "jump", "matches": ["100100.img", "100130.img", ...],
          "count": N, "entries": M, "ratio": N/M }

    Matching is on the *child name only* (case-insensitive). Use this when you
    want a quick "which entries have feature X" answer. For deeper queries
    (regex, sub-paths) use `search` instead.
    """
    raw = cmd_coverage(args)
    needle = args.key.lower()
    matches = []
    for row in raw["rows"]:
        for child in row["children"]:
            if child.lower() == needle or child.lower().endswith("/" + needle):
                matches.append(row["entry"])
                break
    entries = max(raw["entries"], 1)
    return {
        "path": raw["path"],
        "key": args.key,
        "depth": raw["depth"],
        "entries": raw["entries"],
        "count": len(matches),
        "ratio": round(len(matches) / entries, 4),
        "matches": matches,
    }


# ── CLI plumbing ────────────────────────────────────────────────────────────

def _build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(
        prog="wz_explorer",
        description="Read-only explorer for GMS v95 WZ files (wraps wz-dump).",
    )
    sub = p.add_subparsers(dest="cmd", required=True)

    def common(sp: argparse.ArgumentParser) -> None:
        sp.add_argument("wz", help="WZ file (absolute path or name in $MAPLECLAUDE_WZ_DIR)")
        sp.add_argument("path", help="Slash-separated node path, '' = root")

    sp = sub.add_parser("tree", help="Recursive JSON dump")
    common(sp)
    sp.add_argument("--depth", type=int, default=4)
    sp.set_defaults(func=cmd_tree)

    sp = sub.add_parser("keys", help="Direct children of a node")
    common(sp)
    sp.set_defaults(func=cmd_keys)

    sp = sub.add_parser("search", help="Regex search by leaf name")
    common(sp)
    sp.add_argument("regex")
    sp.add_argument("--depth", type=int, default=8)
    sp.add_argument("--limit", type=int, default=500)
    sp.set_defaults(func=cmd_search)

    sp = sub.add_parser("detail", help="Deep recursive dump of one node, primitives included")
    common(sp)
    sp.add_argument("--depth", type=int, default=12)
    sp.set_defaults(func=cmd_detail)

    sp = sub.add_parser("coverage", help="Per-entry child-name listing for the subtree")
    common(sp)
    sp.add_argument("--depth", type=int, default=2)
    sp.add_argument("--limit", type=int, default=0, help="0 = no cap")
    sp.set_defaults(func=cmd_coverage)

    sp = sub.add_parser("union", help="Coverage rolled up into a name → count/ratio ranking")
    common(sp)
    sp.add_argument("--depth", type=int, default=2)
    sp.add_argument("--limit", type=int, default=0)
    sp.add_argument("--top", type=int, default=0, help="0 = unbounded")
    sp.set_defaults(func=cmd_union)

    sp = sub.add_parser("with-key", help="List entries whose subtree contains <key>")
    common(sp)
    sp.add_argument("key")
    sp.add_argument("--depth", type=int, default=2)
    sp.add_argument("--limit", type=int, default=0)
    sp.set_defaults(func=cmd_with_key)

    return p


def main(argv: list[str] | None = None) -> int:
    parser = _build_parser()
    args = parser.parse_args(argv)
    result = args.func(args)
    json.dump(result, sys.stdout, ensure_ascii=False, separators=(",", ":"))
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
