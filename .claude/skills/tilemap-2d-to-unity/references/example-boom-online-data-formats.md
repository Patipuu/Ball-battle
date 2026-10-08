# Data formats (original client -> JSON/PNG)

Scripts live in `project/re/scripts`. Raw packs: `project/re/bvat/raw/<Pack>/<Pack>.idd` + `.idx` (VN 1010 client).
CN live client (read-only reference, extra `FxTile` pack): `E:\ÅÝÅÝÌÃ`. Never patch or inject CA.exe.

## Packs and cipher (`boom_pack_cipher.py`, `boom_sprite_decode.load_idx`)
- `.idx` record: `u32 nameLen; name; u32 offset; u32 size; u32 key; u32 crc` (crc only in idx v15). Idx itself is dword-CBC XOR with key = first dword.
- `key != 0` = encrypted member (maps, object tables, scripts). Decrypt (VA 0x13c0670), n = size>>2 dwords:
  `dst[0]=src[0]^key^0x9C6C95CE; dst[i]=src[i]^dst[i-1]^0x9C6C95CE^(i*i*(i+0x3C6)*(i+0x7E4))`, tail bytes XOR dst[n-1].
- Member lookup across packs: `boom_bake_unity_sprites.Packs` (`where`, `records`, `frames(member)`, `members_for(res)` - also case-insensitive and `<res>LayerAdjust`/`LayerStatic` pairs).

## Sprites (`boom_sprite_decode.py`)
- Blob: `u32 nframes, type=2, used, fmt; used*(w,h); nframes*48-byte records; used*(u32 packed, bytes)`.
- Packed stream: optional 0xC0 prefix -> 3-byte colours (else RGB565); 0xC1 -> + alpha stream; command byte bits7-6 mode (00 literal, 01 RLE, 1x transparent run), bits5-0 count.
- FIXED rules (2026-09-24): header `(w,h)` = rows x row length, so the upright image is h x w; 3-byte colours are B,G,R.
- Some members are JPEG (full-screen 800x600 backgrounds: `Lobby/CAStageRenew`, `Prepare/CAStage*`, `Game/CAStage`, `MonsterSeason2/CAStage_M2`, `MonsterSeason2/BackGround_*`): the sprite decoder shows them empty; `boom_bake_screen_ui.jpeg_members` reads them.

## Layer-attach records (48 bytes, `boom_layer_attach.py`, `rebuild/layer-attach/<Pack>.json`)
- One record = a sub-rect of atlas frame `frame_id`: x in [z, h), y in [pad, w) of the upright image. Always resolve the frame through `frame_id`, never by record position.
- `origin_x/origin_y` = anchor, y down = bottom-left corner of the owner's cell.
- Unity pivot = `(ox / W, (H - 1 - oy) / H)`.
- UI placement (engine stretch draw 0x13e0590): top-left = pos - origin.

## Object table and object defs
- `rebuild/objecttables/fx-ObjectTable.json`: index = map cell id -> name (`Tile16_Box/Block02`). `0xFFFFFFFF` = empty cell.
- `rebuild/objecttables/fx-object-defs.json` (`boom_objectdefs.py`): object -> states -> `res` member + `frames [[record, dur]]` (dur in sim ticks, G). Tiles use state `Default`.
- Object name != sprite name (`Tile16_Box/Block02` -> res `Tile16/ArmyBlock02`). Characters are two layers `<res>LayerAdjust` + `<res>LayerStatic`.
- Monster state indexes agree across sheets: 00 In/Start, 01 Out/Die, 02..05 walk L U R D, 06..09 stop. The rest by name ("11 BeforeAttack Left", "20_BeforeAttackRight", "32 LockIn", "Locking", "Kicked Left"...). Separators vary (space or `_`), casing varies.
- Tile collision classes: `rebuild/tileinfo` (`boom_tileinfo.py`, class Box / Static / ...).

## Maps (`boom_map_parse.py` -> `rebuild/maps/<Map>.json`)
- Header u32: version (1000: 2 layers, 1001/1003: 3), theme/mode, width, height, max players, ..., spawns (x,y) pairs, 0, then `width*height*layers` cell ids (layer-major per cell), then the name table (`names`).
- `names` = item names + the embedded `<mapscript>` XML (split into lines) + the drop trailer (`trailer_offset`).
- 15x13 classic, S2 maps 40/67/100 x 10 (40x12 for one).
- Cell -> sprite: cell id -> ObjectTable name -> object def `Default` state -> res member + frame 0 record -> layer-attach rect + origin, anchored at the cell's bottom-left.

## Drops (`boom_map_drop.py <Map...>` -> `rebuild/drops/<Map>.json`)
- Trailer = weighted pool `[{name, weight}]` + fixed placements `(x, y, k, name)`; k per cell sums to 100 on always-drop maps.

## Map script (`<mapscript>` in `names`)
- `<mapscriptoption playTime bgm ...>` (often missing its `>`), `<GamePlayAreaScript>` (S2 areas), `<MapUIScript MapOrgPos>` (S2), `<StageClearScript><ItemTables>`, `<ScriptObjects><ScriptObject><InitInfo .../><TriggerTable><Time until><Trigger type actionlist/>...</Time></TriggerTable><ActionList name next><action .../>...</ActionList>`.
- XML is not well formed: parse with regexes, case-insensitively (`Sim/Data/MapScript.cs`).
- Semantics: `plans/260924-1749-unity-all-pve-maps/reports/research-260924-1805-*.md`.

## PvE chains (`boom_export_pve_chains.py` -> `BoomOnlineUnity/_source/rebuild/pve-chains.json`)
- From `stages.json` (b = next id, c = step) + `fxdynmapinfo` (title, theme, kind, `listed` flag = shown in map select). 144 chains, 83 listed, 231 stages (227 with a map file; SuperFighter maps missing).

## Strings
- `rebuild/clientdata/ClientString.json` (KR, lang 1042) and `rebuild/locale-csysstr.json` (`map`) hold the monster `say` keys; Vietnamese lines are ours: `_source/rebuild/localization/monster-speech-vi.json` (`boom_merge_monster_speech_vi.py`).
- VN client button BMPs: `project/re/pack-jpeg/bmp/vh` (magenta transparent), used by `boom_bake_screen_ui.py`; KR labels on HUD pieces are repainted by `boom_bake_ui_vietnamese_labels.py`.
