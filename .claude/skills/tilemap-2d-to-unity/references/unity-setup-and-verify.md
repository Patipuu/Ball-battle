# Unity setup, scene building, capture and automation

## Project settings (URP 2D, Unity 6)
- Transparency sort: Renderer2D asset -> Transparency Sort Mode = Custom Axis `(-0.01, 1, 0)`:
  rows sort by y (lower row in front), ties by x (right in front). Built-in pipeline: same in
  Graphics settings.
- Sprites: `SpriteSortPoint.Pivot` on SpriteRenderers (sort by the feet/anchor, not the centre),
  so a rider or a tall character does not sort behind the row above.
- Tilemaps: `TilemapRenderer.Mode.Individual` (tiles interleave with characters per row),
  `tileAnchor = (0,0,0)`, sortingOrder = map layer (0 ground, 1 objects).

## Import (templates/unity/Editor/BakedTilemapImporter.cs)
- Point filter, uncompressed, no mipmaps, pixels-per-unit = cell size, pivot = anchor,
  FullRect mesh. Result: sharp at any screen size (Bilinear = softer, like a stretched old client).
- Copy only changed files; re-import everything when anchors (objects.json) changed.
- Build size: uncompressed RGBA is large; pack into Sprite Atlases before shipping if needed.

## Scene building (TilemapSceneBuilder.cs)
- Create the new scene BEFORE creating Tile assets (NewScene(Single) unloads unreferenced assets).
- One Tile asset per cell id; file y down -> Unity y = height - 1 - y.
- Big multi-cell objects (bosses): see pitfalls - they need their own sorting order.

## Runtime drawing of animated objects
- Pool SpriteRenderers per kind; each frame: object -> state -> frame index from age and durations.
- State names vary between sheets ("02 Walk Left", "02_WalkLeft", "Walk Left"): look up by index
  prefix or by a normalised name (letters+digits, lower case).
- Interpolate positions between sim ticks in the view only; never feed view values back into rules.

## Capture and compare
- Capture = camera.Render() into a RenderTexture of the map's pixel size, ReadPixels, PNG.
  Screen-space-overlay canvases never reach a camera RT: switch them to ScreenSpaceCamera for
  full-screen shots, then back.
- Compare with `scripts/compare_images.py capture.png ref.png --diff diff.png`; 0% for static maps.
- For gameplay screenshots, run the sim N ticks with the player made invulnerable, capture at
  several tick counts (e.g. 150/600/1500), look at every picture.

## Driving the OPEN Editor (no batchmode needed)
- MCP-for-Unity package + its Python server (HTTP, default 127.0.0.1:8082/mcp). `scripts/unity_mcp_client.py`:
  `exec "<C# method body>"`, `exec file.cs`, `refresh` (import + compile + wait), `wait`, `console [n]`.
- One short call per map. A call that blocks the Editor more than ~25 s drops the plugin
  connection (the work still finishes): `wait`, then continue/retry. Import sprites once, then
  reuse the loaded library for every map.
- `EditorApplication.delayCall` does not fire while the Editor is in the background: run work
  synchronously in the call instead.
- `EditorSceneManager.OpenScene` fails in Play mode: set `EditorApplication.isPlaying = false` first
  (the user may be playing - say so).
- In execute_code, `Object` is ambiguous: write `UnityEngine.Object`.
- Git Bash mangles JSON arguments to Python: add subcommands to the client instead of passing JSON.
- Batchmode (Editor closed): `Unity.exe -batchmode -projectPath P -executeMethod Ns.Class.Method -logFile P/Logs/x.log`;
  in Git Bash `export MSYS2_ARG_CONV_EXCL='*'`. Only call `EditorApplication.Exit` when `Application.isBatchMode`.

## Compile check without Unity focus
Unity's Roslyn (`Editor/Data/DotNetSdkRoslyn/csc.dll`) with the project's `.rsp` files from
`Library/Bee/artifacts/*.dag/` (copy, redirect `-out`, append new .cs files) compiles each
assembly in seconds while the Editor stays open.
