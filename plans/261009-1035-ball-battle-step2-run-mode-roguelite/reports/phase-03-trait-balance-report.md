# Phase 3 - trait balance report

Measured with `TraitBalanceReport` (24 seeds per side, every old-weapon pair, both spawn sides). Value = change in win-rate points of a ball that has the trait, against the same ball without it. Gate: `TraitBalanceThresholdTests` (level 1 in +3..+15, level 2 > level 1).

| trait | level 1 | level 2 | final numbers (level 1 / 2) |
|---|---|---|---|
| heavy | +3.7 | +10.3 | r x1.2 / x1.35 (cap 24), +35% / +70% max HP, knockback resist 40% / 60%, speed 0 |
| vampire | +11.8 | +16.5 | heal 20% / 28% of damage dealt |
| spiky | +12.5 | +18.4 | 1 / 1.5 per body bump, 2 s cooldown |
| thorns | +10.5 | +15.6 | reflect 15% / 22% of melee damage |
| second-wind | +13.7 | +17.7 | once at 30% HP (or instead of dying): heal 20 / 28 |
| glass-cannon | +3.7 | +7.9 | x1.5 / x1.9 damage, -25 / -35 max HP; never offered with Brawler |
| parry-master | +13.5 | +22.6 | weapon grows on every 2nd / every parry; blade weapons only |
| bubble | +9.9 | +17.1 | shield every 25 s / 18 s, one banked at most |
| twin-blade | +9.1 | +20.6 | second blade 30% / 40% long, hits x0.45; blade weapons only |
| poison-tip | +10.9 | +21.6 | 0.7 / 0.8 dps x 3 s / 4 s, max 1 / 2 stacks on a target; blade weapons only |

## Changes from the plan table
Plan numbers were far too strong or too weak when measured (first pass: Twin Blade +50, Spiky +48, Bubble +36, Heavy -10, Glass Cannon -2). Changed: all numbers above; Spiky got a cooldown; Parry Master grows every Nth parry; Poison Tip is capped per target; Twin Blade trades damage for reach. Eligibility (new): Parry Master / Twin Blade / Poison Tip need a blade, Glass Cannon is banned on Brawler (measured 90% win combos). Enemies, card offers and weapon-swap cards respect it.

## 3-trait combos
300 random combos (not the 500 of the plan, for time) vs 8 random 3-trait builds, then the top 8 re-measured with 6x the matches: best 85.7% (Brawler + Vampire 2 + Second Wind 2 + Thorns 2), others <= 82%. Mean 51%. Slightly over the 85% line for one Brawler combo at 2 levels; in Run, enemies carry at most 2 traits and the player 3 only after several picks. Left for the Phase 7 balance pass (candidate: Brawler hit cooldown or Second Wind level 2).