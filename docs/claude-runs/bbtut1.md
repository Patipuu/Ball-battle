# BB-TUT1 + BB-MENU1: a main menu that tells a new player what to do, and first-run tips

Branch `dev/step3-view`, on top of the uncommitted BB-UX2 (base `86023c5`). **Not committed.** View code and tests, plus one saved field (`ProgressData.SeenTips`). No match rule change: Rules stays 13 and the goldens are unchanged (SimTests in §5).

Phu (15/10 16:33–16:35):
- a tutorial for new players;
- "as a new player I don't know what to do with the outside menu";
- the menu "looks a bit dull, needs colour and moving images".

## 1. Main menu (BB-MENU1)
| Before | Now |
|---|---|
| White title on black, no motion | Each letter of BALL BATTLE in a ball colour, a dark drop shadow, a gentle 2 px wave |
| Static background | 6 real ball sprites drift, spin in 15° steps, bounce off the edges and each other with a small spark. They are dimmed (alpha 0.26) and drawn under every button and box (`MenuBackdrop`). |
| RUN with no hint | Before the first run: "▼ NEW? START HERE ▼" above RUN (bobs 1 px). A light shine crosses RUN every 3.2 s. |
| VERSUS / CODEX with no explanation | Same-size column with a line under each: "WATCH ANY TWO BALLS FIGHT", "WHAT EVERY BALL DOES" |
| — | New **HOW TO PLAY** button and page: 4 picture steps (pick a ball, pick cards, watch it fight, win 8 fights), "BEAT BOSSES TO UNLOCK NEW BALLS", START A RUN (same as RUN / CONTINUE RUN), BACK |

**Details:**
- **Pixel-perfect motion:** positions round to whole pixels (crisp at 1x/3x).
- **Shadow:** `PixelText.EnableShadow` draws it inside the same text object, so the existing overlap checks still see one title.

**Evidence:**
- `bbtut1/menu-1-hub-new-player-*`, `menu-2-hub-continue-*`, `menu-3-how-to-play-*`;
- `bbtut1/menu-anim.gif` (24 frames, 8 fps).

## 2. First-run tips (BB-TUT1)
**When.** Tips show during a player's first run (`RunsPlayed == 0`), each one once, at the moment it matters (`TutorialCoach`).

| # | Screen | Points at | Text |
|---|---|---|---|
| 1 | Weapon pick | the 3 balls | EACH BALL FIGHTS ON ITS OWN / TAP ONE TO READ IT, AGAIN TO PICK |
| 2 | Start pick | the 3 cards | PICK 1 CARD TO START / CARDS STAY FOR THE WHOLE RUN |
| 3 | Prepare | ENEMY box | YOUR NEXT FOE AND THE ARENA / READ ITS WEAPON AND TRAITS |
| 4 | Prepare | the cards | 1 FREE CARD BEFORE EVERY FIGHT / COINS BUY ONE MORE OR A REROLL |
| 5 | Prepare | FIGHT! | READY? PRESS FIGHT / THEN WATCH. X2 MAKES IT FASTER |
| 6 | Fight result | HP box | HP LEFT CARRIES TO THE NEXT FIGHT / LOSE ONCE: YOUR 1 RETRY IS USED |

**How a tip looks:**
- The screen is dimmed except the target, which gets a blinking yellow frame.
- A box sits on the side with more room and never covers the target. It shows "TIP n/6", SKIP TIPS and OK.

**Rules:**
- **Blocking:** while a tip is open, the screen's own buttons are off, so the tap that closes the tip cannot also pick a card. They come back on OK and ignore that tap.
- **Order:** OK shows the next tip of the same screen (Prepare: 3, then 4, then 5).
- **Skip:** SKIP TIPS ends all tips for good.
- **Save:** every tip is remembered in `ProgressData.SeenTips`, saved at once. An old save loads with an empty list. Players who have finished a run never see tips.
- **Off switch:** `-bbNoTips` turns tips off (QA automation starts from fresh saves). The EditMode test assembly turns them off as well (`TipsOffForTests`), and BbTut1Tests switch them on for themselves.
- **Not covered:** the fight itself has no tip, so the countdown is never blocked. Tip 5 covers X2.

**Evidence:** `bbtut1/1-tip-pick-*` … `6-tip-result-*` (1x/3x).

## 3. Not done / next
- Not seen in a played build yet: the next Linux QA build should play one fresh run with tips, plus the menu animation at 60 fps.
- There is no "show tips again" option (could go in Settings).
- Versus has no tips.

## 4. Progression plan
`docs/design/bb12-ball-progression.md` (run, then a chest, then shards, then ball level; HEAT; decisions D1–D9), with the pacing model `docs/design/bb12/model.py`. No code yet; it waits for Phu's decisions.

## 5. Tests
| suite | total | passed | failed | skipped |
|---|---|---|---|---|
| Unity EditMode | 471 | 460 | 0 | 11 (env-gated captures) |
| SimTests | SIMTESTS | | | |

New:
- `BbMenu1Tests` (7):
  - NewPlayerSeesStartHereAndEveryButtonIsExplained
  - StartHereHidesOnceThePlayerHasPlayedOrHasASavedRun
  - TitleIsColouredWithShadowAndWaves
  - RunShineSweepsInsideTheButton
  - BackdropBallsStayOnScreenAndBounce
  - HowToPlayOpensFitsAndStartsARun
  - CaptureMenu1Screens
- `BbTut1Tests` (6):
  - FirstRunShowsEveryTipInOrderAndBlocksTheScreenUntilOk (plays fight 1 to the result tip)
  - SkipTipsEndsTheTutorialForGood
  - NoTipsOnceARunWasPlayedOrWhenOff
  - OldSaveWithoutSeenTipsLoads
  - EveryTipBoxFitsTheScreenAndKeepsItsTargetVisible
  - CaptureTut1Screens
- `TipsOffForTests` (SetUpFixture).

## 6. Files (on top of BB-UX2)
```
 M Assets/Scripts/Sim/Run/UnlockRules.cs        (ProgressData.SeenTips)
 M Assets/Scripts/View/DebugOptions.cs          (-bbNoTips)
 M Assets/Scripts/View/MenuView.cs
 M Assets/Scripts/View/PixelText.cs             (glyph colour/offset, shadow)
 M Assets/Scripts/View/Run/RunController.cs
 M Assets/Scripts/View/Run/RunFlow.cs           (TipSeen, MarkTipsSeen)
?? Assets/Scripts/View/HowToPlayView.cs (+ .meta)
?? Assets/Scripts/View/MenuBackdrop.cs (+ .meta)
?? Assets/Scripts/View/Run/TutorialCoach.cs (+ .meta)
?? Assets/Tests/EditMode/BbMenu1Tests.cs, BbTut1Tests.cs, TipsOffForTests.cs (+ .meta)
?? docs/claude-runs/bbtut1.md, docs/claude-runs/bbtut1/
?? docs/design/bb12-ball-progression.md, docs/design/bb12/model.py
```
