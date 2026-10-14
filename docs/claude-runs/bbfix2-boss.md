## Boss sweep (1000 runs per bot, 100 seeds per weapon per boss)

### Greedy bot: run win 26.6% (266/1000)

| boss | fights | first-try win % | beaten (incl. retry) % | runs that met it | median ActiveTicks | timeout % |
|---|---|---|---|---|---|---|
| shellking | 332 | 68.1 | 77.4 | 279 | 2490 | 0.0 |
| magnet | 355 | 67.6 | 77.0 | 287 | 2447 | 0.0 |
| mimic | 222 | 65.1 | 67.7 | 192 | 2628 | 0.0 |
| warden | 180 | 80.0 | 82.4 | 165 | 1575 | 0.0 |

### Random bot: run win 5.2% (52/1000)

| boss | fights | first-try win % | beaten (incl. retry) % | runs that met it | median ActiveTicks | timeout % |
|---|---|---|---|---|---|---|
| shellking | 244 | 47.3 | 59.0 | 188 | 2498 | 0.0 |
| magnet | 275 | 46.5 | 55.3 | 217 | 2379 | 0.0 |
| mimic | 61 | 49.0 | 56.9 | 51 | 2312 | 0.0 |
| warden | 81 | 32.4 | 33.8 | 68 | 1511 | 0.0 |

### Player weapon vs boss: player win % (100 seeds each; greedy build, arrives at full HP)

| weapon | shellking | magnet | mimic | warden |
|---|---|---|---|---|
| blade | 79.0 | 78.0 | 53.0 | 88.0 |
| fang | 82.0 | 87.0 | 59.0 | 91.0 |
| pike | 82.0 | 90.0 | 53.0 | 81.0 |
| brawler | 97.0 | 91.0 | 64.0 | 96.0 |
| volley | 86.0 | 86.0 | 73.0 | 84.0 |
| venom | 60.0 | 45.0 | 45.0 | 58.0 |
| aegis | 81.0 | 69.0 | 54.0 | 76.0 |
| rig | 95.0 | 85.0 | 75.0 | 85.0 |
| timeout % | 0.0 | 0.0 | 0.0 | 0.0 |

Timeout % over every boss fight measured (both bots + the weapon matrix): shellking 0.0%  magnet 0.0%  mimic 0.0%  warden 0.0%

Tuning: SHELL hp 150 arc 120 warn 30 on 180 off 150; MAGNET hp 225 pull 0.02 x45 every 300; MIMIC hp 275; WARDEN hp 207 first 450 step 9% every 480 min 56%

27 s
