# BB-12: per-ball progression (run → chest → shards → ball level), plan for Phu's decision

Status: **proposal**, nothing implemented. Base `86023c5` (Rules 13). It extends GDD §4.2 and §13: today there is no permanent power and no per-ball progress, only achievement unlocks.

## 1. Goals
1. **Something after every run, win or lose.** Today a lost run gives nothing unless it reaches a new achievement.
2. **Attachment to one ball.** "My EMBER is level 6" gives a reason to keep playing a favourite ball and to try others.
3. **Clear next step.** It shows as a bar ("12/15 shards to LV4"), in the same spirit as the NEXT GOAL box.
4. **No harm to the fight rules.** The Sim stays deterministic and the Run stays a challenge. PvP (BB-11, postponed) stays fair.
5. **No real money in phase 1** (§6).

## 2. The loop
```text
Run ends (win, loss or abandon after fight 1)
  → 1 chest; its tier comes from how far the run went
  → open it at once: shards for the ball you played + random shards + gold
  → BALLS screen: a ball with enough shards and gold can level up (LV1 → LV10)
  → a level gives a small power bonus and, at some levels, an unlock (skill level, skin, title)
  → next run with a stronger favourite ball; HEAT tiers keep the challenge (§5)
```

## 3. Chests
| Tier | When | Played-ball shards | Random shards | Gold |
|---|---|---|---|---|
| WOOD | Run ends at fights 1–3 | 4 | 2 | 20 |
| SILVER | Run reaches fight 5 (beat boss 1) | 8 | 6 | 50 |
| GOLD | Run won | 15 | 15 | 120 |

- **Random shards:** each one goes to an unlocked ball, equal odds. Locked balls drop nothing (D4).
- **The randomness is small:** about 60% of the shards in every chest are fixed (the ball you played). Only the split of the random part is random.
- **Fair draws:**
  - The odds are shown on the chest screen (an "i" button).
  - The draw uses a seed from the run seed and the run count, and the result is saved before the animation. Closing the game cannot re-roll a chest.
- **No timers, keys or waiting:** chests open immediately at the end of the run.
- **Maxed balls:** shards of a LV10 ball turn into gold (1 shard = 2 gold).

## 4. Ball levels
**Cost to level up** (from the model in §8):

| Level up to | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | total |
|---|---|---|---|---|---|---|---|---|---|---|
| Shards | 5 | 10 | 15 | 25 | 35 | 50 | 65 | 85 | 110 | 400 |
| Gold | 10 | 20 | 35 | 50 | 70 | 90 | 120 | 150 | 190 | 735 |

**What a level gives.** Three options, my recommendation first (**D1**):

| Option | Per level | At LV10 | Good | Risk |
|---|---|---|---|---|
| **B. Light power + unlocks** (recommended) | +2% max HP, +1% damage | +18% HP, +9% damage, plus 4 unlocks | Feels like an upgrade without breaking bosses; unlocks give variety | Needs HEAT (§5) and a balance gate |
| A. Strong power | +3% HP, +3% damage | +27% / +27% | Very clear upgrade | Bosses and the 8-fight run get easy; needs strong rescaling |
| C. No power | unlocks and cosmetics only | — | Zero balance risk | Weaker "upgrade" feeling, which is less than what Phu asked for |

**Unlocks per ball** (revised 15/10 16:58, see §4b):
- **LV3:** the skill reaches **LV2**; choose branch **A or B**.
- **LV5:** a colour variant (skin) of the ball.
- **LV7:** the skill reaches **LV3** in the chosen branch.
- **LV10:** a title and a frame on the ball tile ("MASTER").

Ball perks (the former LV3/LV7 unlocks) are dropped from phase 1: the skill levels now fill those steps.

## 4b. Skill levels come from the ball level, not from the run (Phu, 15/10 16:58)
**Decision.** Skills are upgraded only through ball progression; players can no longer get skill levels inside a run.

| Ball level | Skill in every run with this ball |
|---|---|
| LV1–2 | skill LV1 |
| LV3–6 | skill LV2, branch A or B |
| LV7–10 | skill LV3 of that branch |

**Branch choice.**
- **Where:** made on the BALL screen when LV3 is reached.
- **Changing it:** free outside a run, so players can try both.
- **During a run:** the branch is locked when the run starts (it is saved with the run, as today).

**What changes in the Run:**
1. **SKILL UP leaves the card pool.**
   - Today it is offered with weight 1.5 in every Prepare / start / retry pick until skill LV3 (`CardOffer.Generate`).
   - Without it, the other cards (traits, DAMAGE, SPEED CAP, MAX HP, HEAL) come up more often.
   - Mid-run power drops for a LV1 ball, so the run must be re-measured: the combined balance pass, at HEAT 0, with LV1, LV3 and LV7 balls.
2. **Run start.** `RunBuild.SkillLevel/SkillBranch` are set from the ball's level and chosen branch.
   - **Normal play:** they no longer change during the run.
   - **SWAP WEAPON:** the new ball's own level and branch apply.
3. **MIMIC.** It copies the player's skill level as today, so it now copies the ball's progression. Nothing to change; worth a check in the balance pass.
4. **Saved runs from before the change.** A run in progress keeps the skill level it already has (it is valid as saved). New runs use the ball level.
5. **Debug.** `-bbForceSkill` stays as a debug flag (it still forces the level for a run, never saved). The FIX4/FIX5 SKILL UP card code (branch buttons on the card, TAKEN text) is no longer reached in normal play. It is removed in BB-12g together with its tests, after Phu's OK.
6. **Versus and PvP.** Not tied to ball levels. Versus keeps its own skill choice. PvP duels keep the skill level as part of the point budget (BB-11 §3), independent of anyone's ball level.

**Why it fits.**
- **Clear reward:** levelling a ball gives a visible new ability (the A/B branch, then LV3), not only a few % of stats.
- **Simpler run:** one fewer card kind, and the run focuses on traits and stats.

**Risks.**
- **New players:** a brand-new player now plays every run with skill LV1, a little weaker than today's mid-run SKILL UP path. The combined balance pass sets HEAT 0 so a LV1 ball can still win runs (gate proposal: bot run win rate with LV1 balls ≥ 25%, LV10 ≤ 70%).
- **Pace:** skill LV2 arrives at about run 3 for a regular player and skill LV3 at about run 26 (§8 model, main ball). If LV3 feels too far, it can move to LV6.

**In fights:** the level goes into the loadout as a bonus (the same path as the DAMAGE and MAX HP cards).
- **Rules bump:** this is a Sim change, so Rules 13 → 14 with new goldens.
- **Versus:** levels are not used.
- **PvP duels:** every ball is LV1 (**D6**), so duels stay skill and build only.

## 5. Keeping the Run a challenge: HEAT
**Why.** With option B a LV10 ball wins more often. The run's enemies do **not** scale with your level; otherwise levelling would feel useless.

**How it works:**
- **Unlocking:** after a first run win, the player can raise HEAT (0–5) on the run title.
- **Effect:** each HEAT gives enemies +8% HP and +4% damage, and bosses get it too.
- **Reward:** HEAT 2+ raises the chest one tier at the end of the run (a SILVER finish gives GOLD), and HEAT 4+ adds +50% gold.
- **Balance gate:** at HEAT 0, the bot sweep with a LV10 ball must not exceed a 70% run win rate (to be measured in BB-12d).

## 6. Money and loot-box rules
- **Phase 1:** chests are earned only by playing and can never be bought, with real money or with a currency bought with money. Paid loot-box rules (odds disclosure, the Belgian gambling view) apply to **paid** boxes, so this keeps us outside them.
- **Odds:** shown in game anyway, because it is fair and cheap to do.
- **If monetization comes later** (open GDD decision 8), sell cosmetics directly at a fixed price. Do not sell chests or shards:
  - **App stores:** Apple (guideline 3.1.1) and Google Play require odds before any paid random item.
  - **Belgium:** it treats paid loot boxes as gambling.
  - **UK:** store listings must say "contains loot boxes".
  - **EU:** guidelines target virtual currencies.

## 7. Data, save and screens
- **Save:** `SaveData` version 2 → **3**, with `ProgressData.Balls` [{Id, Level, Shards}], `Gold`, `ChestsOpened` and `Heat`.
  - **Migration:** an old save gets LV1 and 0 shards everywhere.
  - **Welcome chests:** it also gets "welcome back" chests: 1 SILVER per run already won, at most 5 (**D7**).
- **Code:** the meta rules (chest table, costs, draw) live in pure C# under `Sim/Meta/`. They are seeded and testable, but outside the match Sim. Only the level bonus touches the match.
- **New screens** (270x480, existing parts):
  - CHEST: after the run summary; open, shards fly to the ball tiles, gold count.
  - BALLS: 8 tiles with level, shard bar and an UPGRADE button.
  - BALL detail: level track 1–10 showing what each level gives, plus the skill branch choice (A/B).
  - Menu: a BALLS button.
  - Run title: a HEAT selector after the first win.
- **Art and sound:** 3 chests (closed, opening, open), a shard icon in the ball colours, a gold icon, a level badge, chest-open and level-up sounds. SFX4 is still on hold until Phu asks.

## 8. Pacing (model `bb12/model.py`, 200 seeds per profile)
**Setup:**
- 8 balls; 60% of runs with the main ball.
- Players upgrade greedily, main ball first.
- Chest odds per profile:
  - **new:** 75% WOOD, 22% SILVER, 3% GOLD;
  - **regular:** 45 / 40 / 15;
  - **strong:** 20 / 40 / 40.

Median run at which the main ball reaches a level:

| Profile | LV2 | LV3 | LV5 | LV7 | LV10 | All 8 at LV10 |
|---|---|---|---|---|---|---|
| new | 2 | 4 | 15 | 37 | 106 | about 600 |
| regular | 2 | 3 | 10 | 26 | 73 | 424 |
| strong | 1 | 2 | 8 | 19 | 54 | 285 |

**Reading it.** At about 10–15 minutes per run:
- **Short goals:** the first upgrade comes after the 1st–2nd run and LV5 within the first 2–3 hours.
- **Main ball:** a main ball reaches max after about 12–18 hours.
- **Everything maxed:** that is the long tail (60+ hours).
- **Tuning later:** the targets can be moved by changing the two cost rows only.

## 9. Tasks (one agent, in order)
| Task | Content | Sim/Rules | Size |
|---|---|---|---|
| BB-12a | `Sim/Meta`: chest table, draw, costs, upgrade; save v3 + migration; tests (seeded draws, no re-roll on reload, overflow) | no match change | S–M |
| BB-12b | Chest at run end + CHEST screen + odds panel | View | M |
| BB-12c | BALLS screen, detail, UPGRADE, menu button | View | M |
| BB-12d | Level bonus in the loadout (option B), Rules 14, goldens; balance gate with LV10 bots | **Sim, Rules 14** | M |
| BB-12e | (dropped from phase 1: skill levels replace the perks, §4b) | — | — |
| BB-12f | HEAT 0–5, chest tier bonus, gate | Sim (enemy scaling) | M |
| BB-12g | Skill from ball level: SKILL UP out of the card pool, run start takes the ball's skill level and branch, branch choice on the BALL screen, SWAP uses the new ball's level; old SKILL UP card UI and its tests removed | Sim/Run (card pool), View | M |
| ART/SFX | chests, shard, gold, badge; sounds when Phu asks | — | M |

**Order:**
1. 12a, 12b, 12c give "run → chest → upgrade" end to end.
2. **12g** makes levels give skills; this is the first visible reward of a level.
3. 12d adds the small stat bonus.
4. 12f adds HEAT.
5. Then one combined balance pass.

## 10. Decisions for Phu (my recommendation first)
1. **D1 What a level gives:** **B** (+2% HP and +1% damage per level, plus unlocks at LV3/5/7/10). The alternatives are A (strong power) and C (cosmetic only).
2. **D2 Max level and pace:** LV10, using the cost table in §4 (main ball max in about 70 runs for a regular player).
3. **D3 Chest tiers:** WOOD / SILVER / GOLD by how far the run went, one chest per run, no timers.
4. **D4 Locked balls:** keep the achievement unlocks (clear goals) and drop shards only for unlocked balls. The alternative is that 30 shards unlock a ball.
5. **D5 Gold:** used only for upgrades in phase 1. It could later buy a reroll of a perk choice.
6. **D6 PvP and Versus:** levels are ignored; everyone is LV1.
7. **D7 Old saves:** 1 SILVER chest per run won, at most 5.
8. **D8 Money:** none in phase 1; chests never for sale. Later monetization is cosmetics only, sold directly.
9. **D9 HEAT:** in the same phase as levels (12f), unlocked by the first run win.
10. **D10 Skill levels** (decided by Phu 15/10: only from ball progression). Still open: the level steps, recommended skill LV2 at ball LV3 and LV3 at ball LV7, branch free to change outside a run.
