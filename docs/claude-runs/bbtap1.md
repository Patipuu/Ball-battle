# BB-TAP1: tap to strike — prototype, per-ball measurement and tuning

Status: **Sim + View implemented in the working tree (uncommitted)**. Measured with the TapLab bot harness, 200 seeds per ball × tap policy. Goldens are unchanged and Rules stays 13. **Pending:** Unity EditMode run + Linux play build. The laptop's PowerShell bridge hung during `dotnet test`, see §6.

## 1. What the player does
Phu's direction was "tap để tấn công": **one input only, a tap anywhere on the arena (or Space)** during a Run fight. The tap triggers the ball's own strike, and each ball strikes in its own way:

| Ball | Strike (on tap) | PERFECT window |
|---|---|---|
| EMBER | blade spin ×2 for 8 ticks | first hit within 14 ticks |
| MANTIS | spin ×1.25 for 30 ticks | 30 ticks |
| ANGLER | blade length +20% for 30 ticks | 30 ticks |
| WRECKER | turns its flight 1/4 of the way toward the foe (speed unchanged) | 30 ticks |
| SPARKLER | fires the volley now with the charge built so far ×1.2; the volley timer restarts | first arrow hit within 45 ticks |
| SPORE | spore puff: 1 weak poison stack (¼ DPS, 1 s) on foes within reach +20 px, obeying SPORE's stack cap | perfect when the puff reaches someone |
| MIRROR | reflects ×1.25 for 30 ticks | 30 ticks |
| HIVE | every turret fires now with the charge built so far; the turret timer restarts | first shot hit within 45 ticks |

**Common rules:**
- Cooldown is **2.0 s** (120 ticks).
- **PERFECT** means the first damaging hit inside the window: **×1.25 damage**, the word "PERFECT!", sparks, a 2 px shake and a 0.06 s hit-stop.
- **MISS** means the window closed without a hit: **+1 s cooldown** and a grey "MISS".
- SPARKLER and HIVE need ⅔ of a charge before they can strike. Their taps move shots in time; they do not add shots.

## 2. Measurement (TapLab, Greedy RunBot, 200 seeds 1001–1200, Run win %)

**Policies:**
- `none`: never taps.
- `random`: when ready, taps with a 1/60 chance per tick (a casual player).
- `spam`: taps the moment the strike is ready.
- `smart`: taps when the foe is in the weapon's reach (a player who watches the fight).

| Ball | No tap | Random | Spam | Smart | Smart uplift | Spam uplift | Taps/fight (smart) | PERFECT % (smart) |
|---|---|---|---|---|---|---|---|---|
| EMBER | 19.5 | 22.5 | 29.0 | 32.0 | **+12.5** | +9.5 | 14.0 | 44.4 |
| MANTIS | 13.0 | 20.0 | 23.5 | 28.5 | **+15.5** | +10.5 | 14.4 | 60.9 |
| ANGLER | 17.0 | 23.5 | 25.5 | 30.0 | **+13.0** | +8.5 | 15.5 | 44.9 |
| WRECKER | 43.0 | 59.0 | 62.0 | 54.0 | **+11.0** | +19.0 | 12.4 | 57.6 |
| SPARKLER | 22.5 | 18.5* | 32.0 | 32.0 | **+9.5** | +9.5 | 15.2 | 75.2 |
| SPORE | 10.5 | 12.5* | 10.5 | 19.5 | **+9.0** | +0.0 | 18.5 | 100.0 |
| MIRROR | 10.0 | 14.0* | 16.5 | 21.5 | **+11.5** | +6.5 | 17.6 | 54.6 |
| HIVE | 28.0 | 29.0 | 36.5 | 36.5 | **+8.5** | +8.5 | 18.0 | 80.4 |

\* Random for SPARKLER, SPORE and MIRROR comes from the run before their last adjustment, so it is a little stale.

**Reading the table:**
- **Smart uplift is +8.5 to +15.5 Run-win points for every ball** (mean +11.3), inside the target band of +8–15. The spread is ±3.5 around the mean, also inside the ±5 target.
- One standard error on a 200-seed difference is about 4 points, so differences between balls under about 8 points are noise.
- Run win compounds over 8 fights, so this is a small per-fight edge (+0.3 to +1.0 fights won per run).
- **Skill expression is modest.** Spam gets most of the uplift for most balls:
  - **SPORE** rewards timing clearly (spam +0, smart +9).
  - **EMBER, MANTIS, ANGLER and MIRROR** reward it somewhat.
  - **WRECKER, SPARKLER and HIVE** do not: spam is as good as or better than the bot's "smart".
- The bot's "smart" is crude (reach only). A real player who aims SPARKLER volleys or WRECKER turns should beat it, but this needs a playtest.

## 3. How the numbers got here (2 tuning rounds)
1. **First version** (1.2 s cooldown, PERFECT ×1.5, big strikes, no miss cost): **+40 to +90** points. It was far too strong, and spam was as good as smart.
   - SPARKLER/HIVE taps stacked an extra volley on the natural one, a double-fire bug.
2. **Round 1:**
   - added the **miss penalty**;
   - made SPARKLER/HIVE charge-based (no extra shots);
   - cooldown 2 s, PERFECT ×1.25;
   - smaller strikes (EMBER spin 2, MANTIS 1.25, ANGLER +20%, MIRROR 1.125);
   - WRECKER turn instead of a dash.
   - Result: +7 to +23, except SPORE (+80: the puff kept poison topped up).
3. **Round 2:**
   - SPORE puff weak (¼ DPS, 1 s) and capped;
   - WRECKER turn ¼;
   - MIRROR 1.25 for 30 ticks;
   - SPARKLER tapped volley ×1.2.
   - Result: the table above.

The sweep CSVs and the harness are in `bbtap1/`.

## 4. Determinism
- **Only the Run player gets a TapState** (`BallLoadout.TapEnabled`, set by `RunState.CreateMatch` from `RunTuning.TapStrike`). Versus, bosses and enemies never tap.
- **An untouched TapState is not hashed** and changes nothing.
  - Equivalence run: 8 balls × 6 seeds, the whole Run hashed every 97 ticks and at every fight end. Original build vs tap build gave **IDENTICAL** results (`ALL DCFCCC1D0515EDD5`) after every change.
  - Goldens are therefore safe and **Rules stays 13** (hash unchanged).
- **Taps are inputs.** `MatchSim.Tap(i)` queues a tap that applies at the next tick, so the same taps on the same ticks replay the same fight. A test covers this.
- **`TapTuning` values are static fields** like `WeaponTuning`. The game never changes them; only the lab harness does.

## 5. Files

**Sim:**
- New file `Sim/Tap/TapState.cs` (TapTuning + TapState).
- `BallState`, `BallLoadout`, `MatchSim` (`Tap()`, tick hook), `MatchSim.Motion` (`SpinNow`), `MatchSim.Boss` (speed cap), `MatchSim.Damage` (PERFECT on hits/reflects).
- `PikeRule` (length), `RigRule` (charged fire-now, timer restart), `VolleyRule` (charged fire-now).
- `SimEvent` (TapStrike, TapPerfect, TapMiss), `RunState` and `RunTuning` (TapStrike = true).

**View:**
- New file `View/Run/TapView.cs`:
  - tap area over the arena (x2 keeps its own button);
  - 2 px cooldown arc around the player's ball: grey while charging, red after a MISS, pulsing yellow when ready;
  - PERFECT!/MISS words, sparks;
  - blinking "TAP TO STRIKE!" in the first 5 s of a fight until the first tap.
- `PixelButton.Pressed` (strike on press, not release).
- `ArenaView.HitStop`, `FxView.Kick`, `DamagePopups.ShowText(..., scale)`.
- `RunController` creates and ticks TapView.
- Tutorial "fight" tip now says "IN THE FIGHT TAP TO STRIKE!".

**Tests:** `Tests/EditMode/BbTap1Tests.cs`, covering:
- untouched = identical, for all 8 balls;
- cooldown;
- PERFECT/MISS accounting and the miss penalty;
- every ball can strike;
- same taps give the same fight;
- player-only;
- a press on the arena strikes;
- PERFECT word + hit-stop;
- arc sprite;
- tutorial text;
- screenshots (`BB_TAP1_OUT`).

## 6. Not done yet
- `dotnet test` (SimTests) on the laptop started but the PowerShell bridge stopped answering, so I have no result yet. After that: Unity EditMode, screenshots, and a Linux build for Phu to play.
- No tap sounds (SFX4 is deferred until Phu asks). Vietnamese text deferred ("PERFECT!" instead of "CHUẨN!"; the pixel font has no Vietnamese marks).
- **Follow-ups after a playtest:**
  - if spam feels as good as aiming, raise the miss penalty (1 s → 1.5 s) or shorten the windows;
  - Run difficulty was tuned for no input; with tapping it is about +11 points easier for everyone, so it may need a retune once the feel is settled.
