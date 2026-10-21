# BB-18 follow-up — BGLGXLQ Versus-only balance (Rules 17 → 18)

Phu chose a So tài-only correction and asked that Vượt ải keep its current tuning.

## Rule

`MatchSim.IsPlainVersus` is true for matches without a Run marker, tree, boss or tap. `RunState.CreateMatch` passes the Run marker explicitly, including for older saves without a tree. The profile is applied only when this flag is true and the ball is BGLGXLQ (`ChorusRule`).

- Outgoing melee damage ×0.70. Outgoing orb touch, UNITY and RED burn damage ×0.50.
- Incoming body attack damage ×0.38, projectile damage ×0.65, and ability damage ×0.50. This is the orbiting orbs cushioning direct impacts; poison/status and reflected damage remain unchanged.
- No HP, radius, speed or tree values changed. All other balls retain their rules.

The first trial used outgoing melee ×0.40 and body damage ×0.35. It fixed Wrecker but reduced win rates against Volley, Rig and Serpent to 8.5%, 13.5% and 2% on 100 seeds/side, so it was rejected. The final profile was measured on 200 seeds/side for both sides, then on independent seeds 201–400.

## Versus results

Win percentage of BGLGXLQ, with both spawn sides represented. The old column is Rules 17 on seeds 1–200 (`Builds/bb18/versus-p1b.md`). Each new column uses 200 seeds per side.

| Opponent | Old 1–200 | New 1–200 | New 201–400 |
|---|---:|---:|---:|
| EMBER (blade) | 88.5 | 63.8 | 61.3 |
| MANTIS (fang) | 78.5 | 40.3 | 43.8 |
| ANGLER (pike) | 80.8 | 44.5 | 48.0 |
| WRECKER (brawler) | 11.3 | 69.0 | 67.5 |
| SPARKLER (volley) | 36.3 | 26.3 | 26.3 |
| SPORE (venom) | 92.8 | 72.5 | 70.8 |
| MIRROR (aegis) | 95.8 | 64.3 | 66.5 |
| HIVE (rig) | 33.3 | 24.8 | 24.8 |
| HUNTER (serpent; on hold for presentation) | 47.5 | 20.8 | 20.5 |
| **Mean** | **62.7** | **47.3** | **47.7** |

All nine new matchups fall in the 20–80% target on both measured seed ranges. Serpent is close to the lower edge and needs watching if either weapon changes later. Raw tables: `bb18-versus-final-1-200.md` and `bb18-versus-final-201-400.md`.

## Run preservation and verification

- 270 Run-mode matches (30 seeds against each of the other nine balls) were played twice with the Versus multipliers set to 1 and to the final values. Winner, tick count, active ticks, both final HP values and completed ring count matched exactly in every pair.
- The full .NET SimTests suite: 354 passed, 0 failed. After adding the incoming-ability regression, the targeted Versus/Tribute/CrossPlatform tests: 14 passed, 0 failed. The Rules 18 goldens were re-pinned; the 20 masked non-Chorus digests are unchanged.
- Unity EditMode: 557 total, 538 passed, 19 skipped capture/diagnostic tests, 0 failed (`bb18-versus-editmode.xml`). Unity returned 0. The first two batch launches with `-quit` stopped before running tests; the third launch without `-quit` produced this XML.

No commit or push was made.
