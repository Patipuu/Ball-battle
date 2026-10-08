# Worked example: Boom Online (Crazy Arcade) -> Unity

Everything that turns the original client's data into Unity maps, and proves the result matches.
Repo root: `E:\boom-online-handoff-260922` (`project/re` = extraction, `BoomOnlineUnity` = Unity 6 URP 2D).

Formats of this game: `example-boom-online-data-formats.md`. Generic rules: `baked-format.md`, `unity-setup-and-verify.md`, `pitfalls.md`.

## Generic tools on Boom data (verified 2026-09-25)

Boom's baked maps (`_source/baked/maps/<Map>.json`) hold only the palette (`cells`); the grid is in `_source/rebuild/maps/<Map>.json`:

```bash
S=.claude/skills/tilemap-2d-to-unity/scripts; B=BoomOnlineUnity/_source/baked
python $S/validate_baked.py $B --grid-dir BoomOnlineUnity/_source/rebuild/maps --ignore TileTransParent_Static/Static01 TileTransparent_Passable/Passable01
python $S/reference_render.py $B Monster01 --grid BoomOnlineUnity/_source/rebuild/maps/Monster01.json --out ref.png
python $S/compare_images.py BoomOnlineUnity/docs/screens/monster01-stage1.png ref.png   # OK 0.0000%
```

Result: 1039 objects, 6145 PNGs, 229 maps, 0 errors; Monster01 reference = Boom renderer = Unity capture (0%).
The project keeps its own importer/builder (`BoomOnlineUnity/Assets/Scripts/Editor/BakedSpriteImport.cs`, `ArenaImporter.cs`, `ArenaBuild.cs`); the templates are the generic form of them.

## Pipeline at a glance

```
FxMap/Fx* packs (bvat/raw/<Pack>/<Pack>.idd + .idx)
  -> boom_export_maps.py / boom_map_parse.py        rebuild/maps/<Map>.json   (grid, spawns, names+<mapscript>)
  -> boom_map_drop.py <Map...>                      rebuild/drops/<Map>.json  (pool + fixed placements)
  -> boom_export_pve_chains.py                      _source/rebuild/pve-chains.json (stage chains, listed rows)
  -> boom_bake_unity_sprites.py (+ screen UI auto)  _source/baked/{sprites,ui}/*.png, objects.json, ui.json, ui-origin.json, maps/<Map>.json
  -> Unity: BakedSpriteImport.Import()              Assets/Art/Baked + BakedSpriteLibrary.asset
  -> Unity: ArenaImporter.BuildMap(map, library)    Assets/Scenes/<Map>.unity (+ Assets/Art/Maps/<Map>/tile_*.asset)
  -> ArenaBuild capture  vs  boom_map_sample_export.py render  -> compare_capture_to_reference.py (must be 0%)
```

## Quick commands

Commands (from `project/re` unless noted):

| Step | Command |
|---|---|
| Drops for maps | `python scripts/boom_map_drop.py Monster01 Monster02` then copy `rebuild/drops/*.json` to `BoomOnlineUnity/_source/rebuild/drops/` |
| Bake sprites + UI | `python scripts/boom_bake_unity_sprites.py` (bakes every stage of every listed chain + `MAPS`, then runs `boom_bake_screen_ui.py`). Must print `missing []` |
| Reference render | `python scripts/boom_map_sample_export.py Monster01 ...` -> `bvat/map-samples/<Map>.png` |
| Compile check (Editor open) | `cd BoomOnlineUnity && python _tools/roslyn_compile_check.py` |
| Refresh Editor | `python _tools/unity_mcp_client.py refresh` |
| Import sprites | `python _tools/unity_mcp_client.py exec 'return "n " + Boom.EditorTools.BakedSpriteImport.Import().Entries.Count;'` (retry after `wait` if it returns success:false) |
| Build + capture one map | `python _tools/unity_mcp_client.py exec 'return Boom.EditorTools.ArenaBuild.BuildAndCaptureMaps(new[]{"Monster01"}, new[]{300}, true);'` |
| Build many maps | `bash _tools/build_all_maps_in_open_editor.sh [firstMap]` (one instance only, lock file) |
| Pixel compare | `python _tools/compare_capture_to_reference.py docs/screens/monster01-stage1.png ../project/re/bvat/map-samples/Monster01.png` |
| Batch (Editor closed) | `"D:/Unity/6000.5.3f1/Editor/Unity.exe" -batchmode -projectPath <P> -executeMethod Boom.EditorTools.ArenaBuild.BuildAllAndCapture -logFile <P>/Logs/batch.log` (Git Bash: `export MSYS2_ARG_CONV_EXCL='*'`) |

## Workflow for a new map / theme

1. Make sure `rebuild/maps/<Map>.json` exists (else run the map export). A map is data, not an image: 2 layers of cell ids per cell -> `fx-ObjectTable` names.
2. Export drops; if it is PvE, check it is in `pve-chains.json` (listed chains feed the bake and `GameSetup.Maps`).
3. Bake. Read the `missing [...]` list: every missing object is a real problem except the invisible blockers listed in `INVISIBLE`.
4. Import in Unity (once), then build + capture the map, one MCP call per map.
5. LOOK at `docs/screens/<map>-stage1.png` and `<map>-t300.png` (full 800x600 with HUD). Compare stage1 to the Python render (0% diff expected for static tiles).
6. If a sprite is wrong, fix the decoder/bake, never the PNG by hand; re-bake, re-import, re-capture.

## Rules

- The only source of Unity sprites is `boom_bake_unity_sprites.py`. The old corpus `bvat/sprites` / `_source/sprites` is transposed and R/B swapped: do not use it.
- After any Python bake, Unity sees nothing until `BakedSpriteImport.Import()` runs (menu **Boom > Import Baked Sprites** or Build All Scenes).
- Keep `_source/rebuild` in sync with `project/re/rebuild` (`BoomOnlineUnity/_tools/verify-source-manifest.sh`). Unity-only data (`pve-chains.json`, `localization/`) lives in `_source/rebuild` directly.
- Write C#/Python containing regex backslashes with the Write tool or a `.py` patch file, never through a Bash heredoc (backslashes get eaten: `\b` became a backspace byte once and silently broke the monster bake).
- Tag every gameplay number you add in `Assets/StreamingAssets/feel-params.json` as X (data) / C (client code, VA) / G (guess) / S (server).
