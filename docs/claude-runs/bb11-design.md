# BB-11 design task: async PvP ("Code Duel")

Deliverable: `docs/design/bb11-async-pvp.md`, a design doc for Phu's decision. No code changed.

## What was done
- Read the existing PvP research (`pvp-research.md`), the technical review (`pvp-tech.md`) and GDD §18. Phu already chose the direction on 11/10: share codes, no server, no cost.
- Filled in what that decision left open:
  - the loadout with skills (BB-10a);
  - the budget;
  - the share-code format;
  - the arena bans and the Bo3;
  - the seed rule;
  - the screens;
  - the tasks BB-11a–d.
- Listed 8 decisions for Phu (D1–D8) with recommendations.
- Measured the determinism precondition.

## Determinism evidence (new)
- **Linux x64, .NET 8:** a standalone program built from the repo's `Assets/Scripts/Sim` at `2a95b08` recomputes the golden digests. All 48 equal the values pinned on Windows .NET 10:
  - `ExpectedBySeed` ×20 (Rules 13) and `ExpectedBySeedMasked` ×20;
  - the Versus digest `0x39B83A07907096FF`;
  - 7 arena digests.
- **Linux Unity player (Mono, build bc3c460):** the `-roundrobin -seeds 2 -arena classic` CSV is byte-identical to the same round robin computed by .NET 8 on Linux (65 lines: 64 matchups plus a header). Versus is unchanged between bc3c460 and 2a95b08.
- **Not yet measured:** IL2CPP (Android ARM, WebGL). That is task BB-11d.

## Files
- `docs/design/bb11-async-pvp.md` (new)
- `docs/claude-runs/bb11-design.md` (this file)
