# BB-12 skill tree: recovery note (15/10/2026)

Status: **partial handoff, design only**. Source: [shared Claude session](https://claude.ai/share/99d04e2e-573c-4a91-a806-58974bbdd45a), checked 15/10/2026. Claude was designing a tree for all eight balls and running over 19,000 simulated runs to tune node values. Its eight edited files, one new file, and simulation output were in Claude's cloud container and are **not present in this checkout**. The final response failed to load. No measured balance result or per-ball value from that run is available here. Do not treat the candidate rows below as approved tuning.

Phú could not find or export the cloud files. Resume locally from `bb12-skill-trees-candidate.md`, which drafts all eight trees with **unmeasured candidate values** and a balance procedure. Do not wait for a ZIP to continue; replace the candidates with measured values after the local harness is ready.

Phú also supplied `BALL-BATTLE-FINAL-HANDOFF.md` and `ux2-overview-1x.png` from Downloads. The Markdown is an **older general project handoff**, dated 15/10 11:55, and contains no `TreeLab` code, node table, or `m1.log`. It states HEAD `c56ed41` / Rules 12, while this checkout is HEAD `86023c5` / Rules 13; use the actual checkout and newer skill-tree chat for this work. The image shows UX2 screens, not the skill-tree mockup. Neither file replaces the cloud ZIP requested for exact continuation.

Phú then pasted the BB-12 progression plan. It matches `docs/design/bb12-ball-progression.md` apart from two wording fixes ("skill level" instead of "perk" in the loop, and "skill branch choice" on BALL detail), now synced into the local document. It predates Phú's later free-spend skill-tree request and contains no eight-tree node values or simulation output.

## Confirmed from the session screenshot

- Each ball reaches LV10 and gains one point per level: ten spendable points.
- The tree has thirteen possible points/ranks, so the player cannot buy everything.
- Tier 1: VITALITY (+HP), POWER (+damage), and a ball-specific GROWTH node.
- Tier 2 unlocks after two spent points: signature skill LV2 branch A/B, plus a ball-specific START node for opening strength.
- Tier 3 unlocks after five spent points: signature skill LV3 and a KEYSTONE choice, one of two mutually exclusive options that changes play style. The example given is SPARKLER: fewer stronger projectiles versus more weaker projectiles.
- The design goal is to keep eight balls balanced through a bot sweep, rather than assume equal percentage bonuses are equally valuable to every ball.

## Existing repo contracts to reconcile

| Topic | Current repo | Skill-tree implication |
|---|---|---|
| Ball progression | `bb12-ball-progression.md` proposes LV1–10, chests, shards and gold; none implemented | Reuse the level economy. Phú explicitly chose one freely spendable tree point per ball level after that document was written, so its fixed skill-level milestones are superseded. |
| Signature skill | `SkillSpec` supports LV1 and LV2/3 branch A/B | Keep the A/B branch names and mechanics; define the cost and prerequisites of LV2 and LV3 in the tree. |
| Run skill upgrade | Normal runs currently offer SKILL UP cards | Remove this card only when the ball-level tree becomes the source of skill level; preserve existing saved runs through migration. |
| Match rules | Rules 13 | Any tree effect in a fight needs a Rules bump, new deterministic goldens and a balance sweep. |
| Versus/PvP | The BB-12 proposal keeps permanent levels out of those modes | Preserve that boundary unless Phú changes it. |

## Exact stopping point recovered from the Claude chat

- Phú's latest request there: research carefully and make balanced skill trees for **all eight** balls. Earlier he specified that skill levels come only from ball progression, and each ball level grants one point to allocate freely. Normal runs should no longer offer SKILL UP cards.
- Claude's activity panel still marked `TREE-1 Research` done; `TREE-2 Read mechanics`, `TREE-3 Design`, `TREE-4 Measure`, and `TREE-5 Doc/mockup/summary` not started. It then did partial TREE-2/3/4 work before the response failed. No final tree was delivered.
- The cloud harness path was `/tmp/claude-0/treelab`. The first sweep wrote `m1.log` and was later read by Claude; its contents are not exposed in the shared chat. A `m1_nodes.csv` sweep was still reported `running` on two polls. Therefore the claimed 19,000-run job has **no recoverable final result** here.
- Claude updated cloud `TreeLab.cs`, and made exploratory edits to `BladeRule.cs`, `FangRule.cs`, `PikeRule.cs`, `VolleyRule.cs`, `VenomRule.cs`, `AegisRule.cs`, and `RigRule.cs`. `BrawlerRule.cs` was read. Four attempted `TreeLabMain.cs` edits failed because Claude's Bash approval classifier timed out. None of these edits reached this local repository.
- Its last stated step was to change skill costs to **1 point per level**, START to **2 points**, and read parameter values from CLI arguments. The harness edit failed, so this revised cost model was **not measured**.
- An older `enum.py` command enumerated Tier 1 V/P/G at 0–2 ranks each; skill choices none/A2/B2/A3/B3; START 0/1; KEYSTONE none/A/B. It charged skill LV2 2, skill LV3 +2, START 1, KEYSTONE 2. Gate 2 required two Tier-1 points; gate 3 required five points already spent. It printed 3 builds at budget 1, 25 at budget 4, 115 at budget 7, and **76 exactly at budget 10**. The 76 count belongs to the **old** costs and cannot be reused after Claude's last revision.
- With the last proposed costs and unchanged rank limits, the maximum path is 6 Tier-1 ranks + 2 skill levels + 2 START + 2 KEYSTONE = **12 points**, while the earlier summary says 13. This is an unresolved design inconsistency. It could be corrected by changing one node's maximum/cost, but that choice must be explicit before enumeration and balance measurement.
- Re-enumerating the old algorithm with skill LV2/LV3 at 1 point each and START at 2 points (KEYSTONE stays 2) yields **44** exact 10-point configurations, not 76. This is only a combinatorial count; it does not measure their strength. It assumes the gate-3 check includes points spent in START.
- The mockup shown in the tool trace was for EMBER and included a `PRE-HEATED` node. Its final export command failed. This is evidence of a candidate node name, not an approved value or complete per-ball tree.

## Balance evidence and limitations

The existing repository includes `SimTests/RunBot.cs`, which runs complete seeded matches. The Claude cloud sweep used a copy of the Sim, not this repository. Its first results, test seeds, CSV, and tuning values cannot be audited from the shared chat. Do **not** describe any ball or node as balanced based on that run. Rebuild a local harness against the current Sim, validate a no-tree baseline, then compare the same seed set and bot policy for each candidate build. Verify both full-run win rate and matchups by arena/boss, and rerun holdout seeds after tuning.

The latest local measured report is `docs/claude-runs/bbbal3.md` (Rules 13), not the abandoned BAL2 report. Over 500 fixed seeds per ball with Greedy RunBot and the start weapon forced, its post-change Run win rates were EMBER 16.4%, MANTIS 15.0%, ANGLER 15.0%, WRECKER 39.8%, SPARKLER 18.6%, SPORE 9.6%, MIRROR 7.8%, HIVE 22.2%. The gap is mainly boss fights for SPORE/MIRROR. Its proposed LV1 ≥25% / LV10 ≤70% gate is already unmet by this pre-tree baseline. BAL3 predates removal of SKILL UP cards, which could further lower LV1 results. Equal node bonuses will not by themselves establish fair progression; the first sweep must measure and explain this baseline gap.

## Ball-specific design axes to continue from

These are **mechanic anchors, not tuned nodes**. The missing Claude diff may contain better or more advanced designs.

| Ball | Internal ID | Existing growth mechanic | Current LV2 branches | Candidate GROWTH / KEYSTONE axis |
|---|---|---|---|---|
| EMBER | `blade` | Weapon damage and heat on hits | EDGE / HONE | Faster heat growth; sustained heat versus a burst window. |
| MANTIS | `fang` | Spin speed and damage on hits | ENDURE / FURY | Spin gain; sustained frenzy versus short burst. |
| ANGLER | `pike` | Hook length and damage on hits | REACH / HOLD | Length growth; long reach versus stronger tip hits. |
| WRECKER | `brawler` | Speed cap and wall momentum | SLAM / RUSH | Wall momentum; impact burst versus stable speed. |
| SPARKLER | `volley` | Arrow count and damage | RAPID / HEAVY | Volley growth; fewer strong arrows versus more weak arrows. |
| SPORE | `venom` | Poison stack cap and melee damage | SIP / ROT | Stack growth; concentrated poison versus longer pressure. |
| MIRROR | `aegis` | Shield width and reflected damage | WALL / SPITE | Shield growth; defense versus reflect. |
| HIVE | `rig` | Turret count and shot output | BURST / DEPLOY | Turret growth; few strong turrets versus a larger swarm. |

## Resume sequence

1. Reconcile the 13-point summary with Claude's final proposed 12-point maximum, and finalize the exact spend/refund rules. The tier thresholds in the old enumerator counted points already spent.
2. Author the eight trees against the current weapon and signature-skill code. Keep every proposed value in data/config for reproducible sweeps.
3. Validate the simulator's baseline, seeds, bot policy, arena coverage and run count. Compare each node to the same baseline and report uncertainty, including per-ball and boss results.
4. Adjust node values against explicit gates; save the final table and reproducible command. Only then implement the agreed tree in Sim, save migration and View.

## Open questions

- Can the Claude cloud files or `m1.log` be exported? The share link exposes their names, but not their contents.
- Were the tier structure and thirteen-point total approved by Phú, or are they a working proposal?
- Do the KEYSTONE alternatives replace or combine with the existing skill A/B branch?
