# BB-UX1: final V3 icons, a clear weapon pick, the boss portrait fix, signature-skill icons

Branch `dev/step3-view`, base `2a95b08` (BB-FIX5). **Not committed.** View code, art and tests only: no Sim change, Rules stays 13, goldens unchanged. BB-11 PvP is postponed (Phu, 15/10 15:10).

## 1. Icons: the final V3 set is now in the game
- **Problem.** The game showed older icon versions: `Assets/Art/Sprites/icons` still held the first drawings.
- **Fix.** All 16 card and trait icons are replaced by the approved V3 set from `E:\ball-battle-art-hold\icons_v3\`:
  - same names, `.meta` kept;
  - the `_v2/_v2b/_v3` second-wind variants were not used.
- **Evidence.** `bbux1/5-prepare-v3-icons-*`, `6-startpick-v3-icons-*`.
- **Test.** `CardAndTraitIconsAreTheV3Set` checks that every icon is 14x14.

## 2. Weapon pick: the player now sees how each ball plays
**Before:** one tap picked the ball, and the only information was the role, 4 bars and the skill name.

**Now** (`PickWeaponView`):
- **First tap** selects the ball (yellow border) and fills a panel under the tiles:
  - name and role;
  - **HOW:** how it fights and grows;
  - **GOOD:** what it does well (green);
  - **WEAK:** what beats it (light red);
  - its skill and what it does (yellow);
  - the two LV2 branches A and B.
- **Picking:** **PICK <NAME>** or a second tap on the same tile starts the run.
- **Before any tap:** the panel says "TAP A BALL TO SEE HOW IT PLAYS".

**Text source:** `View/Run/WeaponGuide.cs`, from the weapon bible and the BAL2/BAL3 numbers. For example, SPORE reads "HITS POISON. MORE STACKS PER HIT / LONG FIGHTS. BEATS MIRROR / WEAK START. BURST BEATS IT".

**Codex:** the same three lines on every page. PREV/NEXT moved down 14 px so the WEAK line no longer touches them.

**Colour:** WEAK was first drawn in `Palette.HpLost` (120,40,48), which was unreadable. It is now (240,120,110).

Evidence: `bbux1/1-pick-weapon-none-*`, `2-pick-weapon-spore-*`, `3-pick-weapon-mirror-*`, `4-codex-ember-*`.

## 3. Prepare: boss portrait no longer covers the label (QA bc3c460, finding 1)
- **Cause.** A boss portrait is 51x58 px of opaque pixels. That is wider than the 49 px between the box border and the text column, and it reached the boss name label.
- **Fix.** For a boss portrait only:
  - the portrait moves to (-99,146), 6 px lower;
  - the enemy text column moves 2 px right (-74 → -72).
- **Unchanged:** normal enemies keep the old layout.
- **Test:** `BossPortraitClearsTheLabelTextAndBorder` (SHELL KING, MAGNET, WARDEN) checks every opaque pixel against the border, the label and the text column.
- **Evidence:** `bbux1/7-prepare-boss-{magnet,warden,shellking}-*`.

## 4. Signature-skill icons (Phu chose set B, 15/10 15:53)
**How the icons were made.**
- **Grok** (`grok.exe` in `E:\ball-battle-ui-lab`):
  - studied the V3 icons and wrote the style rules (`out\style_notes.md`): 14x14, frame #14121C, a 3-tone diagonal tile in the ball's colours, one flat white motif, 5 colours;
  - drew set A (round 1);
  - round 2 hung twice waiting for the model and was stopped.
- **Claude:** drew set B with the same rules.
- **Comparison:** `bbux1/skill-icons-A-grok-vs-B-claude.png`.
- **Choice:** Phu chose **B**.

**In the game:**
- **Files.** `Assets/Art/Sprites/icons/skill_{blade,fang,pike,brawler,volley,venom,aegis,rig}.png`, plus new `.meta` files with the same import settings as the trait icons.
- **Library.** `PlaceholderArtGenerator.Run` (batchmode) filled `ArtLibrary.Skills`; the `ArtLibrary.asset` diff only adds the `Skills` list.
- **Side effect, reverted.** The generator also rewrote 9 boss `.meta` files (pivot 0.5 → 0.46875, extrude 1 → 0). The committed values differ from what the generator writes, which is older drift. The 9 files were written back to the HEAD content (hash-object = HEAD).
- **Where the icon shows:**

| Screen | Where |
|---|---|
| SKILL UP card (Start pick, Prepare, retry pick) | The card icon is the ball's skill icon (was the SWAP icon). `ArtLibrary.CardIcon` uses the card's weapon id. |
| Weapon pick tiles | 1x icon between the stat bars and the skill name |
| Weapon pick panel | 1x icon at the start of the skill row; the text moves 17 px right |
| Prepare YOU / ENEMY | 1x icon before the skill line ("LUNGE 1"); the text moves 17 px right. Bosses (no skill line, except MIMIC) show none. |
| Codex | 2x icon left of the skill name, 6 px gap, the pair centred |

**Evidence:** `bbux1/2-pick-weapon-spore-*`, `8-prepare-skillup-icon-*`, `9-codex-page5-*` (SPORE), `9-codex-page7-*` (HIVE).

## 5. UI audit: what is still weak (proposal, not done)
| # | Screen | Problem | Proposal |
|---|---|---|---|
| 1 | Prepare | Arena thumbnails are almost black (LOW GRAV, SPIKES) | Brighter v4-style thumbnails for all 7 arenas (spike-walls v4 is waiting for approval) |
| 2 | Prepare (boss) | The boss's base-ball name ("MIRROR") under the boss label is confusing | Show the boss's role/skill instead, or hide it |
| 3 | All | Many 1x lines use TextDim (140,140,168) on a dark panel and are hard to read | Raise TextDim to about (165,165,190) after a contrast check |
| 4 | Menu | Buttons have different sizes (VERSUS large, CODEX small); large empty area | One button column of the same size, with an icon per button |
| 5 | Fight | The skill popup word can cover the WARDEN body (QA finding 2) | Popup above the HP bar, never over a boss |

## 6. Tests
| suite | total | passed | failed | skipped |
|---|---|---|---|---|
| Unity EditMode | TOTAL | PASSED | 0 | 9 (older env-gated captures incl. Fix4/Fix5; `CaptureUx1Screens` ran with `BB_UX1_OUT`) |
| SimTests | not run (no Sim change) | | | |

`BbUx1Tests` (15):
- EveryBallHasAGuide
- FirstTapExplainsSecondTapOrPickStartsTheRun
- DetailsFitForEveryBall
- CodexShowsTheGuideAndFits
- BossPortraitClearsTheLabelTextAndBorder ×3
- NormalEnemyKeepsTheOldColumn
- EveryBallHasA14pxSkillIcon
- SkillUpCardShowsTheBallsSkillIcon
- PickTilesAndDetailsShowTheSkillIcon
- PrepareShowsSkillIconsBesideTheSkillLines
- CodexSkillIconSitsLeftOfTheSkillName
- CardAndTraitIconsAreTheV3Set
- CaptureUx1Screens

## 7. Files
```
 M Assets/Art/ArtLibrary.asset (Skills list)
 M Assets/Art/Sprites/icons/*.png (16, V3)
?? Assets/Art/Sprites/icons/skill_*.png (+ .meta) (8, set B)
 M Assets/Scripts/Editor/PlaceholderArtGenerator.cs
 M Assets/Scripts/View/ArtLibrary.cs
 M Assets/Scripts/View/Run/CardFace.cs
 M Assets/Scripts/View/Run/PickWeaponView.cs
 M Assets/Scripts/View/Run/PrepareView.cs
 M Assets/Scripts/View/SkillCodexView.cs
?? Assets/Scripts/View/Run/WeaponGuide.cs (+ .meta)
?? Assets/Tests/EditMode/BbUx1Tests.cs (+ .meta)
?? docs/claude-runs/bbux1.md, docs/claude-runs/bbux1/
```
