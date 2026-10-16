# BB-14b: BGLGXLQ ball sprite from the real logo

Status: **done, uncommitted.** This is an art-only change; there is no Sim or Rules change.

## What changed
- Phu sent the logo of Ban Giáo Lý – Giáo Xứ Lạc Quang. It shows:
  - a hand whose fingers are yellow, blue, green and pink, on a red palm with a white cross;
  - a laurel branch around it.
- Those are the five colours of the ball's ring.
- The four sprites the game loads were redrawn from it. Their `.meta` files and GUIDs are kept, so the ArtLibrary links are unchanged:
  - `Assets/Art/Sprites/ball_chorus.png` (32×32);
  - `Assets/Art/Sprites/ball_sizes/ball_chorus_{38,43,48}.png`.
- **Pixel-art translation:**
  - The disc is white with the usual 1 px rim (20,18,28) and diagonal light.
  - The hand is centred and stretched 1.3× in width, so each finger stays 2–3 px wide.
  - Each finger keeps the logo's gradient as two tones: yellow → orange, blue → deep blue, green → dark green, pink → hot pink.
  - The palm is red to dark red, with a dark wrist and a white cross.
  - The laurel is an arc inside the left rim, teal at the bottom and green at the top, with small leaves.
  - The lettering is dropped (it cannot be read at 32 px).
- **Reproducible:**
  - `docs/design/art/tribute/gen_bglgxlq_ball.py` draws the shapes in logo coordinates at 8× and picks each pixel's colour by majority, for any size.
  - The source logo is `docs/design/art/tribute/bglgxlq-logo.png`.
- **Unchanged:** the skill icon (HYMN) and the role icon (HARMONIST).

## Checks
- The pixels on the laptop are identical to the generated files (the file transfer only adds a metadata chunk).
- EditMode on HEAD plus these sprites: 550 tests, 532 passed, **0 failed**, 18 skipped. This includes `Night1ArtTests` (rim colour, sizes) and `BbTribute1Tests`.
- SimTests on HEAD: 351/351. Together these complete M0 of the launch plan.
- Screenshots: `docs/claude-runs/bb14b/` (`1-bglgxlq-ring`, `3-both`).

## HUNTER face (added 16/10, 12:45)
- Phu sent a photo of his friend for the HUNTER ball. The four sprites were redrawn as a pixel portrait (GUIDs kept):
  - `ball_serpent.png`;
  - `ball_sizes/ball_serpent_{38,43,48}.png`.
- **What the portrait keeps:**
  - short spiky black hair;
  - big round light-gold glasses with squinting, smiling eyes;
  - a wide toothy grin;
  - rosy cheeks;
  - a red shirt collar.
- **The ball's colour stays teal** (name, HP bar and tail).
- **Generator:** `docs/design/art/tribute/gen_hunter_ball.py`.
  - The photo itself is **not** stored in the repo (private person); the generator draws from shapes only.
- **Checks:** `BbTribute1Tests`, `Night1ArtTests` and `BbFont1Tests` pass (16 passed, 0 failed, 1 opt-in skipped). Fight screenshots refreshed in `docs/claude-runs/bb14b/`.
- **Still open:** the friend's display name (currently "HUNTER").
