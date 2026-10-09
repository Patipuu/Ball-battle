# Phase 4 - new weapons balance report

8x8 matrix, 200 seeds per side (`MatchupReport`, gate `BalanceThresholdTests`): all 28 non-mirror pairs 30-70%, mirror draws < 5%, no match reaches the 180 s cap, median length 20-90 s. Step 1 Versus results are unchanged (`VersusGoldenTests` is limited to the four Step 1 weapons and still matches).

## Final design (WeaponTuning)
| weapon | design | stat |
|---|---|---|
| volley | stubby blade; every 1.25 s a fan of arrows aimed at the nearest foe; each melee hit = +1 arrow (max 8) and +0.1 arrow damage | ARROWS |
| venom | wide slow blade; hit poisons while target has fewer stacks than allowed; each hit +1 allowance (max 8) and +0.25 melee damage | VENOM |
| aegis | broad shield; parry reflects 40% of the parried blow, 50% of a body blow (via new `WeaponRule.OnHitTaken`), absorbs 20% of shot damage, regen 0.25/s, rage from 15 s (mirror ends); widens +0.25 per hit; no reflect against another Aegis | WIDTH |
| rig | each melee hit drops a ghost turret at the ball (max 6, oldest replaced, lost when the shrinking arena reaches it); turrets fire together every 1.1 s | RIGS |

## Changes from the plan
- Volley aims at the nearest foe (blade-direction aim scattered arrows at random while the blade spins).
- Rig turrets are ghost emplacements, not colliding obstacles: no Layout change, no ball-trap risk (KISS). Turret and arrow drawing is Phase 6.
- Aegis needed regen, shot absorption, body reflect and late rage to fit 30-70%; the plan numbers made it lose 100% to Brawler/ranged and stall in the mirror (64% hit the cap).
- Trait balance (Phase 3) is still measured over the four Step 1 weapons only; trait x new weapon combos are not gated (Phase 7).

## Run difficulty with the new weapons in enemy pools
Bots: Random 6.4% wins, Greedy 41.4% (targets 5-20% / 30-60%).