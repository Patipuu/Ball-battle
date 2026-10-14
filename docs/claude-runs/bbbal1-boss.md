## Boss sweep (1000 runs per bot, 100 seeds per weapon per boss)

### Greedy bot: run win 22.6% (226/1000)

| boss | fights | first-try win % | beaten (incl. retry) % | runs that met it | median ActiveTicks | timeout % |
|---|---|---|---|---|---|---|
| shellking | 337 | 63.8 | 73.6 | 276 | 2526 | 0.0 |
| magnet | 353 | 66.1 | 75.0 | 292 | 2439 | 0.0 |
| mimic | 206 | 58.5 | 62.5 | 176 | 2530 | 0.0 |
| warden | 202 | 58.5 | 67.8 | 171 | 1633 | 0.0 |

### Random bot: run win 3.1% (31/1000)

| boss | fights | first-try win % | beaten (incl. retry) % | runs that met it | median ActiveTicks | timeout % |
|---|---|---|---|---|---|---|
| shellking | 253 | 42.1 | 53.8 | 197 | 2572 | 0.0 |
| magnet | 250 | 46.2 | 56.8 | 199 | 2393 | 0.0 |
| mimic | 49 | 51.2 | 53.5 | 43 | 2438 | 0.0 |
| warden | 62 | 12.5 | 16.7 | 48 | 1509 | 0.0 |

### Player weapon vs boss: player win % (100 seeds each; greedy build, arrives at full HP)

| weapon | shellking | magnet | mimic | warden |
|---|---|---|---|---|
| blade | 79.0 | 78.0 | 53.0 | 78.0 |
| fang | 86.0 | 90.0 | 52.0 | 88.0 |
| pike | 80.0 | 89.0 | 54.0 | 71.0 |
| brawler | 95.0 | 93.0 | 67.0 | 85.0 |
| volley | 87.0 | 84.0 | 70.0 | 66.0 |
| venom | 58.0 | 45.0 | 45.0 | 44.0 |
| aegis | 83.0 | 72.0 | 53.0 | 64.0 |
| rig | 93.0 | 84.0 | 76.0 | 69.0 |
| timeout % | 0.0 | 0.0 | 0.0 | 0.0 |

Timeout % over every boss fight measured (both bots + the weapon matrix): shellking 0.0%  magnet 0.0%  mimic 0.0%  warden 0.0%

Tuning: SHELL hp 156 arc 120 warn 30 on 180 off 150; MAGNET hp 225 pull 0.02 x45 every 300; MIMIC hp 275; WARDEN hp 230 first 450 step 9% every 480 min 56%

207 s
