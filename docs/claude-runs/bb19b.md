# BB-19b: ten tapped skills (Sim Rules 20)

Phu (17/10): every ball gets a complete skill the player times by hand; the branches change with them. Balance pass NOT done yet (Phu: not needed now).

## What exists now
Run player only (ball with a TapState). Enemies, bosses and Versus keep the automatic skills, so the Versus golden digests are unchanged (only the Rules number moved to 20).

| Ball | Skill | Tap | Branch A / B |
|---|---|---|---|
| EMBER | FIRE TRAIL | dash along the heading, fire pieces burn the foe 4 s | LONG TRAIL (longer) / WILDFIRE (hotter, shorter) |
| MANTIS | PHANTOM | blink behind the foe + slash; 0.33 s dodge swallows a blow and counters | AFTERIMAGE (decoy hits) / EXECUTE (x2 under 30% HP) |
| ANGLER | HOOK | slow hook; hit = reel in + stun | LONG LINE / BARB |
| WRECKER | RAM | tap 1 wind-up (held, charge), tap 2 ram; over-hold backfires | PATIENCE / HAIR TRIGGER |
| SPARKLER | RICOCHET | big bolt along the blade, bounces 4x | MORE BOUNCES / HEAVY SHOT |
| SPORE | SPORE CLOUD | cloud: poison + slow on the foe, heal for you | GROWTH / BLIGHT |
| MIRROR | PARRY | 0.4 s window: blow thrown back x3 + stun; a miss adds 1.5 s cooldown | WIDE GLASS / SHARD |
| HIVE | SWARM | drones hunt and sting 5 s | SWARM / STINGERS |
| BGLGXLQ | UNITY | no auto burst; tap bursts the lit orbs (5 lit = x1.5); orbs fade after 6 s | CHORUS / RADIANCE |
| HUNTER | COIL | lunge, coil, 3 bites (hold 1.5 s) | CONSTRICT / SWALLOW |

Code: `Sim/Skills/ActiveSkill.cs` (10 classes + `SkillTuning`), `FxPool.cs`, `MatchSim.Fx.cs` (effect entities), stun/slow on `BallState`, `Guard` hook in `ComputeHitDamage`, `ChorusRule.ManualUnity`, `SimEventType.SkillFx`. View: `View/SkillFxView.cs`, TapView wind-up ring. Strings: SkillDefinition names/summaries/branches (ids and audio ids keep the old key), 55 VI rows.

## Tests
SimTests 369/371. New `ActiveSkillTests` (16) pass: each skill fires and shows its event, determinism under random taps for all weapons x all branches. Failing: `GreedyBotWins22To40Percent` (68.9%) and `RandomBotWins1To6Percent` (35%): the bots tap every skill the moment it is ready, so the numbers need the balance pass. Goldens re-pinned (Rules 20).
The View compiles (checked with the Unity DLLs outside Unity); EditMode and a visual check were NOT run (Unity open, MCP bridge not connected).

## Known gaps
- Tree FINALE texts/effects that read the old skill (FRENZY, LUNGE, ...) do nothing for the player now; they need redesign with the new skills.
- Tutorial wording still says TAP TO STRIKE.
- Cooldowns are 8 s for all (UNITY 2 s); all numbers in `SkillTuning` are first guesses.
