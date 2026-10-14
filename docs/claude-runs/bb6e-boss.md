## Boss sweep (1000 runs per bot, 100 seeds per weapon per boss)

### Greedy bot: run win 17.3% (173/1000)

| boss | fights | first-try win % | beaten (incl. retry) % | runs that met it | median ActiveTicks | timeout % |
|---|---|---|---|---|---|---|
| shellking | 357 | 63.9 | 75.9 | 294 | 2585 | 0.0 |
| magnet | 367 | 67.1 | 75.5 | 298 | 2618 | 0.0 |
| mimic | 221 | 56.4 | 58.5 | 188 | 2789 | 0.0 |
| warden | 242 | 31.2 | 33.9 | 186 | 1477 | 0.0 |

### Random bot: run win 1.8% (18/1000)

| boss | fights | first-try win % | beaten (incl. retry) % | runs that met it | median ActiveTicks | timeout % |
|---|---|---|---|---|---|---|
| shellking | 233 | 40.5 | 53.2 | 173 | 2588 | 0.0 |
| magnet | 237 | 39.3 | 47.1 | 191 | 2519 | 0.0 |
| mimic | 39 | 51.5 | 54.5 | 33 | 2630 | 0.0 |
| warden | 27 | 0.0 | 0.0 | 22 | 1237 | 0.0 |

### Player weapon vs boss: player win % (100 seeds each; greedy build, arrives at full HP)

| weapon | shellking | magnet | mimic | warden |
|---|---|---|---|---|
| blade | 78.0 | 83.0 | 51.0 | 55.0 |
| fang | 87.0 | 91.0 | 52.0 | 58.0 |
| pike | 81.0 | 83.0 | 46.0 | 49.0 |
| brawler | 95.0 | 91.0 | 67.0 | 52.0 |
| volley | 83.0 | 87.0 | 56.0 | 42.0 |
| venom | 75.0 | 58.0 | 42.0 | 28.0 |
| aegis | 90.0 | 72.0 | 50.0 | 32.0 |
| rig | 89.0 | 82.0 | 60.0 | 45.0 |
| timeout % | 0.0 | 0.0 | 0.0 | 0.0 |

Timeout % over every boss fight measured (both bots + the weapon matrix): shellking 0.0%  magnet 0.0%  mimic 0.0%  warden 0.0%

Tuning: SHELL hp 150 arc 120 warn 30 on 180 off 150; MAGNET hp 225 pull 0.02 x45 every 300; MIMIC hp 275; WARDEN hp 207 first 450 step 9% every 480 min 56%

23 s
