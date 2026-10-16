# BB-UI1: livelier buttons (bevel, rounded corners, pressed state)

Status: **implemented, uncommitted.** One file: `View/PixelButton.cs`. No layout, Sim or save change.

## Why
Phu: "các nút bấm hơi đơn điệu". Every button was a flat rectangle: a 1px border around one solid fill. When pressed, only the fill went darker.

## What changed (every `PixelButton` in the game, no screen code touched)
- **Rounded corners:** the four corner pixels of the border are left out (the border is drawn as a plus of two rects).
- **Bevel inside the button:**
  - a 1px **shine** along the top;
  - the **face**;
  - a darker 2px **lip** along the bottom (1px on buttons under 14 px tall). Buttons under 8 px tall keep the flat look.
  - The three strips never overlap, so the sort order of icons and labels is unchanged.
- **Shine colour:**
  - bright buttons (RUN, UPGRADE, ADD) get a lighter tint of their fill;
  - dark secondary buttons (BALLS, VERSUS, CODEX, BACK, tree boxes) get a tint of their **accent (border) colour**, so a yellow-bordered button has a yellow glint.
- **Pressed:**
  - the face darkens (as before);
  - the shine goes dark and the lip takes the face colour, so the button looks pushed in;
  - the label drops 1px.
  - It springs back on release or when the finger slides off.
- **Size and layout:** everything stays inside the button's `Area`. Sizes, hit areas, text positions (when not pressed) and every layout test are unchanged.

## Results
- Full EditMode run: see `bbsfx4.md` (UI1, SFX4 and BB-12g were tested together).
- **Screenshots** (`ui1/`, the same capture tests as before, now with the new buttons):
  - `balls/` menu + BALLS;
  - `chest/`;
  - `tap/`;
  - `tree/`.
- Compare with `bb12c/`, `bb12b/`, `bbtap1/` and `bb12g/` for the old look.
