## Boss sweep (1000 runs per bot, 100 seeds per weapon per boss)

### Greedy bot: run win 28.4% (284/1000)

| boss | fights | first-try win % | beaten (incl. retry) % | runs that met it | median ActiveTicks | timeout % |
|---|---|---|---|---|---|---|
| shellking | 352 | 65.2 | 77.9 | 290 | 2642 | 0.0 |
| magnet | 332 | 77.3 | 80.0 | 300 | 2533 | 0.0 |
| mimic | 237 | 59.5 | 65.5 | 200 | 2777 | 0.0 |
| warden | 204 | 79.3 | 83.2 | 184 | 1655 | 0.0 |

### Random bot: run win 2.9% (29/1000)

| boss | fights | first-try win % | beaten (incl. retry) % | runs that met it | median ActiveTicks | timeout % |
|---|---|---|---|---|---|---|
| shellking | 251 | 41.6 | 50.0 | 190 | 2562 | 0.0 |
| magnet | 218 | 50.5 | 56.5 | 184 | 2398 | 0.0 |
| mimic | 38 | 35.5 | 38.7 | 31 | 2800 | 0.0 |
| warden | 46 | 38.5 | 43.6 | 39 | 1636 | 0.0 |

### Player weapon vs boss: player win % (100 seeds each; greedy build, arrives at full HP)

| weapon | shellking | magnet | mimic | warden |
|---|---|---|---|---|
| blade | 78.0 | 88.0 | 50.0 | 83.0 |
| fang | 87.0 | 97.0 | 50.0 | 88.0 |
| pike | 75.0 | 89.0 | 46.0 | 81.0 |
| brawler | 92.0 | 94.0 | 70.0 | 89.0 |
| volley | 79.0 | 90.0 | 57.0 | 71.0 |
| venom | 74.0 | 67.0 | 43.0 | 65.0 |
| aegis | 85.0 | 83.0 | 50.0 | 67.0 |
| rig | 87.0 | 89.0 | 61.0 | 74.0 |
| timeout % | 0.0 | 0.0 | 0.0 | 0.0 |

Timeout % over every boss fight measured (both bots + the weapon matrix): shellking 0.0%  magnet 0.0%  mimic 0.0%  warden 0.0%

Tuning: SHELL hp 150 arc 120 warn 30 on 180 off 150; MAGNET hp 200 pull 0.02 x45 every 300; MIMIC hp 275; WARDEN hp 275 first 360 step 12% every 480 min 45%

30 s
