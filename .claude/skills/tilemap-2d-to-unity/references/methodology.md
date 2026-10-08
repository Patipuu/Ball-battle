# Methodology: finding the formats of an unknown 2D game

Work from evidence, write every finding down with its source (file offset, VA, data count), and
test each hypothesis against ALL files, not one.

## 1. Inventory
- List every container (packs/archives: index + data file pairs, WADs, zips with odd extensions).
- Count members per extension/prefix; find the ones named like `Map/*`, `Tile*`, `*Object*`, `Sprite*`.
- Keep a JSON inventory (name, size, offset, flags) - later steps join on it.

## 2. Encryption / compression
- Entropy per member: ~8 bits/byte = encrypted or compressed. Try zlib/deflate/LZ at small offsets first.
- If a member flag says "encrypted", find the routine in the client binary (search for the
  index parser, then its callers; a disassembler script that annotates string pushes helps).
  Re-implement it exactly; verify with a CRC/checksum field if the index has one.
- Do not search for "keystreams" statistically: position-dependent ciphers (e.g. XOR with a
  polynomial of the dword index) defeat it. Read the code.

## 3. Sprites
- Find the header: frame count, per-frame (w, h), per-frame packed size. A good header makes all
  sizes add up to the member length exactly - check that on every member.
- Pixel stream: RLE/literal/transparent-run command bytes are common in 90s-2000s clients.
- Unknown layout: dump one decoded buffer, run `scripts/raw_image_probe.py buf.bin --size WxH`.
  A transposed picture means rows and columns are swapped (w/h meaning); wrong colours mean
  BGR vs RGB or 565 vs 555. Verify on a sprite whose true look you know (a HUD button, a letter).
- Keep blank frames: animation timing depends on them.

## 4. Anchors / sub-rects
- Sprites usually carry per-record geometry: which atlas frame, a sub-rectangle, and an
  origin/anchor. Resolve the frame through its id field, never by record index (several records
  can share a frame).
- Prove the anchor convention with a tall tile: its base must sit on its cell and the top overflow
  upward. Typical: anchor = bottom-left of the owner's cell, y down in the PNG.

## 5. Tile ids and object definitions
- Map cells hold numbers; a name table (id -> "Tileset_Kind/Name") maps them to objects.
- Object definitions map object -> states -> resource sprite + frames (record, duration). The
  object name is often different from the sprite name - always go through the definition.
- Collision/behaviour classes (floor, wall, box...) live in a separate tile-info table.

## 6. Map layout
- Header: version, size, player count, spawns... Find width/height by trying (w, h) such that
  `header + w*h*layers*4 (+ name table)` fits the file EXACTLY; the layer count often depends on
  the version. Accept the layout only when it fits every map file.
- Trailing data after the grid often holds item names, drop tables and embedded scripts (XML/Lua).
  These scripts are frequently not well-formed: parse with tolerant regexes, case-insensitive.

## 7. Independent check
- Before any engine work, render a map with a tiny separate renderer (`reference_render.py`) and
  look at it next to a screenshot of the original game. Fix decoding here.

## 8. Behaviour (beyond pictures)
- Game rules live in the client code and (for online games) on the server. Class names from RTTI,
  string references and packet handlers point to them. Mark anything the server decided as a guess.
- Parallel research agents work well per subsystem (attack functions, script engine, special
  modes); give each the tools, VAs/paths and a report path.
