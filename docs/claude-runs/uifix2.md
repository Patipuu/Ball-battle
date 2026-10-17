# UI-FIX2 — the upgrade text covered "CÁC BÓNG"

Phu, 16/10: the upgrade text, shown when a ball can be upgraded, covered the balls part.

## Cause
- **Menu.** The badge was `UP!` drawn *inside* the BALLS button, right-aligned. In Vietnamese it becomes `LÊN CẤP!` (47 px), which overlapped the centred label `CÁC BÓNG` (94 px at scale 2).
- **BALLS grid.** Each tile showed `LÊN CẤP!` right-aligned on the same line as `CẤP 10`, overlapping it by about 6 px.
- **Grid title.** The big `LÊN CẤP!` flash kept showing for up to 1.2 s after going back to the grid, over the grid title.

## Fix
- **Menu.** The badge is now a 14 px gold pill with `!` at the right end of the BALLS button, inside it (the NEXT GOAL box ends 4 px above the button). There are no words to translate or overflow. `MenuView.BallsBadgeArea` is public.
- **Tiles.** The state (`UP!` / `MAX` / `3/5`) has its own line under the level.
- **Flash.** `ShowGrid` hides the LEVEL UP flash.
- **Test.** `BbBalls1Tests.MenuHasBallsWithAnUpBadgeAndNothingOverlaps` checks that the pill is right of the label in both languages and inside the button.

## Files
`View/MenuView.cs`, `View/BallsView.cs`, `Tests/EditMode/BbBalls1Tests.cs`.

## Verification (17/10, Codex continuation)
- Unity EditMode: `continue-1017-hunter-fix-editmode.xml` — 557 total, 541 passed, 16 skipped, 0 failed. This includes the EN/VI menu badge geometry and BALLS tile checks.
- Vietnamese captures: `continue-1017-hunter-fix/balls/1-menu-balls-button-3x.png` and `2-balls-grid-3x.png`. Checked at native-pixel zoom: the `!` is to the right of `KHO BÓNG`, and `LÊN CẤP!` has its own row beneath each ball's level.
