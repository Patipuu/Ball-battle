# BB-BAL2 measurement: baseline

Rules 12; seeds: 300 run seeds per weapon, 100 versus indexes x 7 arenas per ordered pair.

## Run (forced-weapon Greedy)

| weapon | runs | win % | avg fights won | died at fight 1..8 | SHELL KING | MAGNET | MIMIC | WARDEN |
|---|---|---|---|---|---|---|---|---|
| blade | 300 | 16.7 | 3.36 | 67 51 28 45 12 2 6 39 | 71.0 (n=69) | 70.6 (n=85) | 53.2 (n=47) | 59.5 (n=42) |
| fang | 300 | 15.3 | 3.29 | 64 63 26 35 13 6 11 36 | 72.2 (n=72) | 80.0 (n=75) | 49.0 (n=49) | 66.7 (n=33) |
| pike | 300 | 15.3 | 3.49 | 59 54 29 42 11 3 10 46 | 66.3 (n=83) | 81.3 (n=75) | 44.9 (n=49) | 55.8 (n=43) |
| brawler | 300 | 41.0 | 4.84 | 41 41 16 37 3 0 2 37 | 88.5 (n=104) | 74.5 (n=98) | 76.9 (n=91) | 76.8 (n=69) |
| volley | 300 | 17.7 | 3.49 | 55 57 26 49 13 2 10 35 | 65.9 (n=82) | 73.8 (n=80) | 62.7 (n=51) | 56.8 (n=37) |
| venom | 300 | 3.0 | 2.42 | 75 60 33 71 13 3 5 31 | 46.2 (n=65) | 46.3 (n=67) | 26.1 (n=23) | 17.6 (n=17) |
| aegis | 300 | 5.0 | 2.85 | 67 57 32 64 5 3 12 45 | 61.2 (n=67) | 50.6 (n=77) | 29.7 (n=37) | 17.4 (n=23) |
| rig | 300 | 19.7 | 3.68 | 58 42 31 52 9 1 4 44 | 73.6 (n=87) | 64.6 (n=82) | 67.9 (n=56) | 44.7 (n=47) |

## Versus (row weapon win % vs column, both sides, all arenas)

| | blade | fang | pike | brawler | volley | venom | aegis | rig | overall |
|---|---|---|---|---|---|---|---|---|---|
| blade | - | 55.1 | 46.1 | 49.6 | 45.0 | 54.9 | 48.9 | 42.0 | 48.8 |
| fang | 44.7 | - | 43.0 | 28.3 | 54.2 | 49.9 | 50.4 | 59.1 | 47.1 |
| pike | 53.9 | 57.0 | - | 51.1 | 50.4 | 62.2 | 41.4 | 46.6 | 51.8 |
| brawler | 50.2 | 71.7 | 48.9 | - | 47.1 | 55.9 | 33.4 | 56.9 | 52.0 |
| volley | 55.0 | 45.8 | 49.6 | 52.9 | - | 61.5 | 57.1 | 49.4 | 53.1 |
| venom | 45.1 | 50.1 | 37.8 | 44.1 | 38.5 | - | 63.3 | 39.4 | 45.4 |
| aegis | 50.7 | 49.6 | 58.4 | 66.4 | 42.8 | 36.7 | - | 53.0 | 51.1 |
| rig | 58.0 | 40.9 | 53.4 | 43.1 | 50.6 | 60.6 | 46.9 | - | 50.5 |

## SPORE DRAIN fires per match

| context | opponent | matches | fires/match | SPORE win % |
|---|---|---|---|---|
| versus | aegis | 1400 | 1.319 | 63.3 |
| versus | blade | 1400 | 1.082 | 45.1 |
| versus | brawler | 1400 | 1.511 | 44.1 |
| versus | fang | 1400 | 1.160 | 50.1 |
| versus | pike | 1400 | 1.012 | 37.8 |
| versus | rig | 1400 | 0.939 | 39.4 |
| versus | venom | 700 | 1.154 | 48.4 |
| versus | volley | 1400 | 0.948 | 38.5 |
| versus | **all** | 10500 | 1.140 | 45.6 |
| run | aegis | 130 | 1.192 | 83.8 |
| run | blade | 134 | 0.896 | 53.0 |
| run | boss:magnet | 94 | 2.128 | 33.0 |
| run | boss:mimic | 31 | 2.806 | 19.4 |
| run | boss:shellking | 96 | 1.260 | 31.3 |
| run | boss:warden | 27 | 1.593 | 11.1 |
| run | brawler | 124 | 1.290 | 62.1 |
| run | fang | 118 | 1.042 | 57.6 |
| run | pike | 159 | 1.006 | 57.9 |
| run | rig | 132 | 0.902 | 61.4 |
| run | venom | 125 | 1.000 | 63.2 |
| run | volley | 145 | 0.883 | 53.8 |
| run | **all** | 1315 | 1.172 | 55.1 |

## SPORE vs MIRROR

| context | side | matches | SPORE win % | DRAIN fires/match | ready ticks/match | mean MIRROR stacks while DRAIN ready | ready ticks with >=2 / >=3 stacks | SPORE damage taken/match | reflect share |
|---|---|---|---|---|---|---|---|---|---|
| versus | SPORE ball0 | 700 | 61.4 | 1.287 | 2999 | 0.675 | 13.03% / 0.161% | 84.1 | 26.678% |
| versus | SPORE ball1 | 700 | 65.1 | 1.351 | 2936 | 0.692 | 13.795% / 0.172% | 82.9 | 27.127% |
| run | SPORE player vs MIRROR enemy | 130 | 83.8 | 1.192 | 2642 | 0.685 | 13.698% / 0.163% | 77.0 | 26.415% |

229 s
