# Pitfalls (each one cost real time once)

## Decoding and baking
- Width/height swapped in the sprite header -> every image transposed; and 3-byte colours stored
  B,G,R -> red/blue swapped. An old corpus decoded before the fix stays wrong: delete or ignore it.
- Resolving a sub-rect by record position instead of its frame id attaches the wrong pixels when
  several records share a frame.
- Resource names differ in case between tables ("Tile04/chest01" vs "Tile04/Chest01"): look up
  case-insensitively everywhere (bake, engine object table, script image names).
- Names with trailing spaces ("Tile02/Igloo ") break folder creation on Windows: trim path parts.
- Some objects are invisible on purpose (transparent blockers): whitelist them, keep them in the
  collision data, report every other missing object.
- A second bake step that rewrites a shared index file (e.g. HUD-only `ui.json`) wipes the entries
  another step added (menu screens) -> chain the steps in one command so they always run together.
- Extract monster/actor images from the map scripts too; a regex typo (backspace byte instead of
  `\b`) silently dropped every script-only image once. Add a test: every image referenced by any
  map is baked and has its walk/stand clips.

## Tooling
- Bash heredocs can eat backslashes in code you paste (regex `\b`, `\d`, `\n`). Write code with a
  file editor or a patch script file, then grep the result for control characters.
- Background jobs killed by the harness can leave child processes running: add a lock file and
  check the process list before starting a new instance.

## Placement and sorting
- Pivot formula must flip y: `(ox/W, (H-1-oy)/H)`.
- Sorting a multi-cell body by its anchor makes tiles of its own row (same y) draw over its belly.
  A SortingGroup does NOT fix it (the group sorts badly against individual tilemap tiles and even
  put upper-row tiles in front). Moving the transform to fake a sort point moves the picture too.
  Working fix: draw big bodies one sortingOrder above the object layer.
- Depth biases added to the transform are visible offsets: keep them tiny (0.002 units).
- Full-screen HUD frame images (JPEG) go behind the arena, map at its screen offset; overlay
  canvases on top.

## Map data semantics
- Collision flags can mean the opposite of what the name suggests ("passType box=1" = blocked by
  boxes). Check against a map where the answer is obvious (small monsters must not cross walls),
  and write a test that no actor ever stands inside a wall/box.
- Bosses may need exceptions the data does not state (spawning on box cells -> must walk over boxes).
- Script text lost during extraction (non-ASCII attribute values) can leave tags unclosed:
  patch the text so the element closes, and treat the lost value as unknown.
- Stage timers: trigger phase lengths are not the stage time; look for an explicit play-time option.
- Templates (spawned later) and start objects share one list: spawn only those flagged "from start".
