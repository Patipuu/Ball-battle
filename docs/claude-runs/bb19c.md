# BB-19c - end-of-tree (FINALE) effects for the tapped skills

Status: implemented, uncommitted (HEAD 75e5fb2, dev/step3-view). Rules stays 20.

## Why
After BB-19b the old FINALE rules read `Skill.Active` (FRENZY, LUNGE) or old mechanics (parry, arrow count, wall boost) that the
Run player's tapped skills never set. Phu approved the redesign on 2026-10-17.

## Effects (all live in `Sim/Skills/ActiveSkill.cs`; the old per-weapon finales are retired: `Fin => '-'`)
| Ball | A | B |
|---|---|---|
| EMBER | SMOLDER: burn lasts 2 s after leaving the trail (half pulse damage every 24 ticks) | BACKDRAFT: weapon hits on a burning foe +25%, trail 15% shorter |
| MANTIS | AFTERSPIN: after the blink the next 3 hits spin +50% (12 s gate) | BLOODLUST: slash +20%, cooldown +10% |
| ANGLER | LONG CAST: hook life +12%, stun -10% | YANK: pull +20%, hook damage +25%, life -10% |
| WRECKER | STEADY: backfire stun halved | OVERCHARGE: released past full charge = +30%, backfire stun +50% |
| SPARKLER | SPLIT: last bounce splits into 2 sparks (40% damage) | CHAIN: 4 hits, each hit +1 bounce, a bounce re-arms the hit, bolt -20% speed |
| SPORE | OUTBREAK: an expiring cloud leaves a small one (45% radius, 2.5 s) | LINGER: poison from the cloud lasts +50% |
| MIRROR | RIPOSTE: parry reflect +50% | FLOW: parry refunds 60% of cooldown, a miss costs +25% |
| HIVE | QUEEN: one drone guards, blocks 1 blow while the swarm is out | STING STORM: +2 drones, each -25% damage |
| BGLGXLQ | EVERLIGHT (unchanged) | 2X RAINBOW (unchanged) |
| HUNTER | HYDRA (unchanged) | SHED SKIN: 1 s after the coil ends the next blow is blocked (replaces keep-tail) |

## Code
- `ActiveSkill.cs`: `Fin`/`FinA`/`FinB`/`Proc`, per-skill finale code; `FxPool.cs`: `SkillFxEvent.FinaleProc`.
- `MatchSim.Fx.cs`: trail ignites (`EmberTrail.Ignite`), cloud outbreak/linger, coil end hook.
- `Projectile.Tag` (+ `ProjectileSpec.Tag`, hashed only when non-zero), `SplitSpark`, chain re-arm in `MatchSim.Projectiles.cs`.
- `FangRule.AfterspinHits`; WRECKER wall boost/spike finale removed (`BrawlerRule`, `MatchSim.Obstacles.cs`); SERPENT keep-tail removed.
- View: `TreeText.cs` names/effect lines, `SkillFxView` burst for `FinaleProc`, 44 rows in `ui-vi.txt`.
- Tests: `SimTests/FinaleTests.cs` (7: determinism for 10 balls x A/B under random taps, AFTERSPIN, cooldown, bolt tags, outbreak, queen/storm, trail life).

## Checks
- SimTests: 369 passed + 7 new passed; 2 expected failures (`GreedyBotWins22To40Percent`, `RandomBotWins1To6Percent`, balance not done yet).
- Goldens unchanged (new state hashed only when used). viewcheck build succeeded. Not run in Unity.
- Orphan VI rows for the retired finale names/lines remain (harmless).

## Follow-up 1 (Phu approved): three small fixes
- ANGLER OPENER text: "PIKE STARTS 1 LONGER" (was "HOOK", clashed with the HOOK skill).
- SPORE INSTINCT (SEEP): +3% per rank now also scales the cloud poison; text "POISON AND CLOUD DAMAGE".
- HUNTER FINALE A (HYDRA): COIL bites heal 0.2 per segment too (`SerpentRule.HydraHeal`).

## Follow-up 2: skill level 3 was identical to level 2
Found while answering Phu's question about the node text "HOOK LV2 THEN LV3": the BB-19b active skills only read `Level > 1` + branch,
so the level-3 purchase gave nothing. Now `ActiveSkill.L3` strengthens the branch's main number (about half the LV2 gain again):

| Ball | A LV2 -> LV3 | B LV2 -> LV3 |
|---|---|---|
| EMBER | trail life +50% -> +75% | trail hits +70% -> +100% |
| MANTIS | decoy 60% -> 90%, dodge +50% -> +75% | execute under 30% -> 45% HP |
| ANGLER | hook speed/range +30% -> +45%, size +20% -> +30% | hit +50% -> +75%, stun +60% -> +90%, pull +2 -> +3 |
| WRECKER | backfire limit +25% -> +50% | ram cap 75% -> 88% |
| SPARKLER | +2 -> +3 bounces | bolt damage +90% -> +125%, bigger |
| SPORE | cloud +35% -> +55% wide, +30% -> +50% long | cloud poison +80% -> +120% |
| MIRROR | window +67% -> +100% | reflect +50% -> +75%, stun +33% -> +50% |
| HIVE | 9 -> 10 drones | sting +90% -> +125% |
| BGLGXLQ | (already had L2/L3 in ChorusRule) heal 1% -> 1.5% | damage +60% -> +90% |
| HUNTER | hold +50% -> +80% | 4 -> 5 bites, heal 30% -> 45% |

Node cards now show two clear lines per level ("LV2: ..." / "LV3: ...", `TreeText.SkillFx`, `TreeView` passes the level); 62 VI rows added.
Test: `FinaleTests.SkillLevel3IsStrongerThanLevel2` (EMBER, HIVE, SPORE, ANGLER, SPARKLER). Numbers are a first pass (balance not done).
