# BB-BAL2 measurement: it1

Rules 13; seeds: 300 run seeds per weapon, 100 versus indexes x 7 arenas per ordered pair.

## Run (forced-weapon Greedy)

| weapon | runs | win % | avg fights won | died at fight 1..8 | SHELL KING | MAGNET | MIMIC | WARDEN |
|---|---|---|---|---|---|---|---|---|
| blade | 300 | 16.0 | 3.27 | 68 53 29 46 11 2 6 37 | 69.7 (n=66) | 69.0 (n=84) | 50.0 (n=46) | 64.1 (n=39) |
| fang | 300 | 14.0 | 3.10 | 69 66 32 27 14 6 10 34 | 79.1 (n=67) | 80.3 (n=66) | 53.3 (n=45) | 58.1 (n=31) |
| pike | 300 | 13.0 | 3.31 | 62 57 28 44 12 6 7 45 | 63.8 (n=80) | 79.5 (n=73) | 43.5 (n=46) | 50.0 (n=38) |
| brawler | 300 | 36.0 | 4.41 | 47 47 18 41 5 1 2 31 | 85.4 (n=96) | 70.7 (n=92) | 78.2 (n=78) | 77.0 (n=61) |
| volley | 300 | 16.3 | 3.41 | 58 57 26 50 11 2 10 37 | 65.0 (n=80) | 72.2 (n=79) | 59.6 (n=52) | 52.9 (n=34) |
| venom | 300 | 3.0 | 2.73 | 61 59 30 75 13 6 9 38 | 51.4 (n=72) | 48.7 (n=78) | 21.4 (n=28) | 15.8 (n=19) |
| aegis | 300 | 6.0 | 2.93 | 67 54 29 64 8 5 10 45 | 64.3 (n=70) | 51.3 (n=80) | 35.1 (n=37) | 19.2 (n=26) |
| rig | 300 | 20.3 | 3.71 | 59 43 31 47 9 1 4 45 | 77.6 (n=85) | 65.9 (n=82) | 67.2 (n=58) | 45.8 (n=48) |

## Versus (row weapon win % vs column, both sides, all arenas)

| | blade | fang | pike | brawler | volley | venom | aegis | rig | overall |
|---|---|---|---|---|---|---|---|---|---|
| blade | - | 55.1 | 46.1 | 49.6 | 45.0 | 48.7 | 45.1 | 42.0 | 47.4 |
| fang | 44.7 | - | 43.0 | 28.3 | 54.2 | 45.8 | 47.1 | 59.1 | 46.0 |
| pike | 53.9 | 57.0 | - | 51.1 | 50.4 | 56.9 | 35.2 | 46.6 | 50.2 |
| brawler | 50.2 | 71.7 | 48.9 | - | 47.1 | 48.4 | 21.0 | 56.9 | 49.2 |
| volley | 55.0 | 45.8 | 49.6 | 52.9 | - | 55.5 | 56.6 | 49.4 | 52.1 |
| venom | 51.2 | 54.1 | 43.1 | 51.5 | 44.5 | - | 69.4 | 44.4 | 51.2 |
| aegis | 54.7 | 52.9 | 64.8 | 78.8 | 43.3 | 30.5 | - | 54.7 | 54.2 |
| rig | 58.0 | 40.9 | 53.4 | 43.1 | 50.6 | 55.6 | 45.2 | - | 49.5 |

## SPORE DRAIN fires per match

| context | opponent | matches | fires/match | SPORE win % |
|---|---|---|---|---|
| versus | aegis | 1400 | 1.274 | 69.4 |
| versus | blade | 1400 | 1.049 | 51.2 |
| versus | brawler | 1400 | 1.461 | 51.5 |
| versus | fang | 1400 | 1.119 | 54.1 |
| versus | pike | 1400 | 0.986 | 43.1 |
| versus | rig | 1400 | 0.914 | 44.4 |
| versus | venom | 700 | 1.106 | 48.6 |
| versus | volley | 1400 | 0.919 | 44.5 |
| versus | **all** | 10500 | 1.103 | 51.0 |
| run | aegis | 134 | 1.127 | 85.8 |
| run | blade | 127 | 0.890 | 63.8 |
| run | boss:magnet | 106 | 2.123 | 35.8 |
| run | boss:mimic | 38 | 2.842 | 15.8 |
| run | boss:shellking | 104 | 1.202 | 35.6 |
| run | boss:warden | 27 | 1.593 | 11.1 |
| run | brawler | 125 | 1.288 | 74.4 |
| run | fang | 125 | 0.976 | 64.0 |
| run | pike | 172 | 0.965 | 61.6 |
| run | rig | 143 | 0.846 | 59.4 |
| run | venom | 143 | 0.986 | 61.5 |
| run | volley | 163 | 0.859 | 52.8 |
| run | **all** | 1407 | 1.149 | 58.1 |

## SPORE vs MIRROR

| context | side | matches | SPORE win % | DRAIN fires/match | ready ticks/match | mean MIRROR stacks while DRAIN ready | ready ticks with >=2 / >=3 stacks | SPORE damage taken/match | reflect share |
|---|---|---|---|---|---|---|---|---|---|
| versus | SPORE ball0 | 700 | 67.6 | 1.244 | 2905 | 0.671 | 12.948% / 0.16% | 80.5 | 28.987% |
| versus | SPORE ball1 | 700 | 71.3 | 1.304 | 2842 | 0.689 | 13.765% / 0.171% | 79.3 | 29.457% |
| run | SPORE player vs MIRROR enemy | 134 | 85.8 | 1.127 | 2502 | 0.679 | 13.529% / 0.16% | 70.9 | 29.319% |

221 s
