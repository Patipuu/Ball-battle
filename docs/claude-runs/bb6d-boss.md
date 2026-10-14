## Boss sweep (1000 runs per bot, 100 seeds per weapon per boss)

### Greedy bot: run win 18.5% (185/1000)

| boss | fights | first-try win % | beaten (incl. retry) % | runs that met it | median ActiveTicks | timeout % |
|---|---|---|---|---|---|---|
| shellking | 345 | 66.3 | 77.7 | 291 | 2581 | 0.0 |
| magnet | 366 | 76.3 | 83.6 | 317 | 2508 | 0.0 |
| mimic | 259 | 53.7 | 58.8 | 216 | 2777 | 0.0 |
| warden | 271 | 25.7 | 28.7 | 202 | 1399 | 0.0 |

### Random bot: run win 1.5% (15/1000)

| boss | fights | first-try win % | beaten (incl. retry) % | runs that met it | median ActiveTicks | timeout % |
|---|---|---|---|---|---|---|
| shellking | 250 | 39.6 | 50.3 | 187 | 2616 | 0.0 |
| magnet | 239 | 44.4 | 50.5 | 198 | 2439 | 0.0 |
| mimic | 40 | 38.9 | 38.9 | 36 | 2652 | 0.0 |
| warden | 31 | 3.8 | 3.8 | 26 | 1182 | 0.0 |

### Player weapon vs boss: player win % (100 seeds each; greedy build, arrives at full HP)

| weapon | shellking | magnet | mimic | warden |
|---|---|---|---|---|
| blade | 78.0 | 89.0 | 50.0 | 42.0 |
| fang | 86.0 | 93.0 | 51.0 | 41.0 |
| pike | 82.0 | 89.0 | 45.0 | 38.0 |
| brawler | 94.0 | 92.0 | 69.0 | 46.0 |
| volley | 82.0 | 90.0 | 56.0 | 27.0 |
| venom | 76.0 | 65.0 | 42.0 | 20.0 |
| aegis | 88.0 | 77.0 | 50.0 | 23.0 |
| rig | 88.0 | 88.0 | 60.0 | 35.0 |
| timeout % | 0.0 | 0.0 | 0.0 | 0.0 |

Timeout % over every boss fight measured (both bots + the weapon matrix): shellking 0.0%  magnet 0.0%  mimic 0.0%  warden 0.0%

Tuning: SHELL hp 150 arc 120 warn 30 on 180 off 150; MAGNET hp 200 pull 0.02 x45 every 300; MIMIC hp 275; WARDEN hp 206 first 450 step 9% every 480 min 56%

32 s
