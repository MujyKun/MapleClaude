# Map.wz reference

Field structure of `Map.wz/Map/Map<prefix>/<mapId>.img` (GMS v95), as consumed by
`src/MapleClaude/Map/FieldScene.cs` and the upstream Kinoko `MapProvider` parser.

The map is split into **two kinds of children**:
1. **Named nodes** for global metadata (`info`, `back`, `foothold`, `life`, `portal`, `reactor`,
   `ladderRope`, `seat`, `miniMap`).
2. **Eight numeric layer nodes** `0..7` — each holds its own `foothold`, `tile`, and `obj`
   subtrees and represents one of the visual depth bands the v95 client draws in order.

The two are not the same: the top-level `foothold` exists for global iteration, but each
foothold's actual layer comes from its position in the numeric layer tree.

## Top-level named children

| Node | Type | Purpose |
| ---- | ---- | ------- |
| `info` | property | Map flags (`town`, `swim`, `fly`, `returnMap`, `forcedReturn`, `fieldLimit`, `mobRate`, `bgm`, `VRLeft`/`VRTop`/`VRRight`/`VRBottom`, …). |
| `back` | property | Parallax background layers — each entry has `bS`, `no`, `x`, `y`, `rx`, `ry`, `cx`, `cy`, `a` (alpha), `f` (flip), `front`, `type` (HMove/VMove/Tiled/HTiled/VTiled). `front=1` entries draw above all map layers. |
| `foothold` | property | Global tree by layer → group → id. Lines, walls. See per-foothold fields below. |
| `life` | property | NPC and mob spawn definitions (`type` "n" / "m", `id`, `x`, `cy`, `fh`, `rx0`/`rx1`, `f`). |
| `portal` | property | Warp portals (`pn`, `pt`, `x`, `y`, `tm`, `tn`, `delay`, `onlyOnce`). |
| `reactor` | property | Interactive destructible/clickable objects. |
| `ladderRope` | property | Ladder & rope nodes (`l`, `uf`, `x`, `y1`, `y2`, `page`). Global, not under any layer. |
| `seat` | property | Sittable spots (chairs, benches). |
| `miniMap` | property | Minimap canvas + (`width`, `height`, `centerX`, `centerY`, `mag`) transform. |
| `clock` | property | Map-wide visible clock face (when present). |

## The eight numeric layers (0..7)

Each layer `<N>` contains **three subtrees**: `foothold`, `tile`, `obj`. Tiles and objs are
drawn in ascending layer order (layer 0 furthest back, layer 7 furthest forward), and within a
layer they sort by their own `z` sub-key. The v95 client iterates the same 8 layers once and
inside each layer's pass also draws every entity (mob / NPC / character / drop) whose
foothold belongs to that layer.

### `foothold/<group>/<id>`

A single line segment of ground geometry — walkable floor when `X1 != X2`, a vertical wall
when `X1 == X2`. The layer is encoded by the WZ path (`…/N/foothold/…` → layer N).

| Field | Meaning |
| ----- | ------- |
| `x1`, `y1`, `x2`, `y2` | Segment endpoints (world space). |
| `prev`, `next` | Foothold ids of the adjacent segments in the polyline. 0 = open end. |
| `cantThrough` | If non-zero, blocks down-jump (cannot fall through this floor). |
| `forbidFallDown` | If non-zero, treats the foothold as solid for downward gravity (no drop-through). |
| `drag` | Friction multiplier for icy floors. |
| `force` | Knockback impulse (e.g. conveyor belts). |
| `piece` | Editor-only grouping hint. |

Each foothold also derives a **ZMass** at load (`(layer, group)` pair → unique id, set on
`Foothold.ZMass`); wall collision is gated on ZMass so a wall on a different platform never
blocks an airborne entity. ZMass is a physics concept; the WZ **layer** is a render concept,
even though the layer number is part of the ZMass key.

### `tile/<idx>`

Static decoration sprites with the same x/y semantics as objs, but resolved through the
`Tile.wz` package using the layer's `info/tS` tileset name. The render-order sort key is
`zM` (a fallback on the tile node), with the actual `z` value typically embedded inside the
referenced canvas in `Tile.wz/<tS>.img/<u>/<no>`.

| Field | Meaning |
| ----- | ------- |
| `x`, `y` | World position. |
| `u` | Tile subset key (e.g. "bsc", "edD"). Indexes into `Tile.wz/<tS>.img/<u>`. |
| `no` | Canvas index inside that subset. |
| `zM` | Fallback z sort key (used when the canvas itself has no `z` property). |

### `obj/<idx>`

Animated decoration sprites resolved through `Obj.wz`. These are the trees, signs, scrolling
banners, NPC speech-bubble props, etc. The render-order sort key is **`z`** directly on the
WZ node.

| Field | Meaning |
| ----- | ------- |
| `oS` | Object set name → `Obj.wz/<oS>.img`. |
| `l0`, `l1`, `l2` | Nested category path inside that file. Resolves to an animation node. |
| `x`, `y` | World position. |
| `z` | Sort key within the layer. Higher z = drawn on top. |
| `zM` | Fallback / global z modifier (rarely used). |
| `f` | Horizontal flip. |
| `name` | Editor name (sometimes referenced by scripts/quests). |
| `quest` | Quest gate (visible only while a given quest is active). |
| `flow` | Animation flow mode. |
| `rx`, `ry`, `cx`, `cy` | Rotation pivot / canvas center hints. |

## How entities pick their layer

When an entity (mob / NPC / character / drop) is placed on a foothold, **it inherits that
foothold's layer**. The v95 client's render loop then draws the entity inside the same
per-layer pass as the layer's tiles and objs.

In MapleClaude:

```
FieldScene.LayerOfFoothold(int id)         // direct lookup by foothold id
FieldScene.LayerAt(float x, float y)       // closest standable floor below (x, y)
```

`MobLook.Layer`, `NpcLook.Layer`, `OtherCharLook.Layer`, `DropSprite.Layer`, and
`CharLook.Layer` each hold the current layer. They are set at spawn from the server's
foothold id (mobs) or `LayerAt` (NPCs / drops / remote players whose initial UserEnterField
doesn't carry a foothold id) and updated on every move packet for moving entities.

For the **local player**, the value is synced each tick in `GameStage.Update` from
`PlayerController.CurrentFoothold` via `FieldScene.LayerOfFoothold(...)`. The controller
preserves the foothold id across airborne frames, so a jump sustains the takeoff layer until
landing.

## Per-layer draw order

Inside one layer's pass, MapleClaude (matching the v83 open-source reference) draws:

1. The layer's **tiles** (sorted ascending by canvas `z`, falling back to `zM`).
2. The layer's **objs** (sorted ascending by `z`).
3. **NPCs** whose `Layer == N`.
4. **Mobs** whose `Layer == N`.
5. **Remote players** whose `Layer == N`.
6. **Tombstone** + **local player** + the player's per-skill effects, when the player's
   `Layer == N`.
7. **Drops** whose `Layer == N` — last so loot stays visible when you stand on top of it.

Then the next layer's pass begins. After layer 7, parallax fronts and portal swirls draw.
Screen-locked overlays (chat balloons, damage numbers, emotion bubbles, UI panels, fade)
draw above the entire field, outside the layered pass.

## Caveats

- `MapInfo` (the C# `MapInfo` POD in `src/MapleClaude/Map/MapInfo.cs`) only carries the
  global flags; tiles + objs live on `FieldScene` directly (`_tileLayers[8]`, `_objLayers[8]`).
- `back` entries with `front=true` are stored separately as `_fronts` and draw after all 8
  layer passes — they are **not** part of the numeric-layer tree.
- The `info/tS` field is per-layer; each layer may use a different tileset.
- The `f` (flip) flag on obj/back means horizontally mirrored; the WZ has no rotation field
  even though some entries carry a `rx`/`ry` pivot for future-compat.
