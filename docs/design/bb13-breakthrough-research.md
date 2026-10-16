# BB-13: genre research — where Ball Battle can break away from the usual path

Status: **research + proposals for Phu's decision**, nothing implemented. 15/10/2026. Sources at the end.

## 1. What the direct competitors already do

| Game | Status | Loop | What it owns |
|---|---|---|---|
| **Auto Ball Arena** (Big Quail Games) | Steam page, planned **Q4 2026** | Pick a warrior, equip weapons, buy charms. The physics fights run themselves. Quests unlock stronger weapons inside the run. | **Async PvP gauntlet to 10 wins**, sharing builds with friends, a roguelite run. |
| **BattleBalls** (pingus) | Released Aug 2025, $1.99 | Design a spinning ball (weapon, spin, damage, HP) and watch arena fights, up to 4 balls. | "Inspired by Earclacks", single-player, almost no reviews. |
| **Auto Ball** (Dominick Favata) | Early Access, release 16/10/2026 | Soccer auto-battler with a shop of animals, 3-to-1 merges and trait synergies. | 8-player ranked lobbies, shared unit pool. |
| **Ham's Ball Battles** | A league of videos, **not a playable game** | Physics weapon balls in seasons (boss gauntlet, 1v1, 1v1v1), a Unity simulator. | Viewers **bet a digital, non-money currency** on scheduled matches. A playable game is only an early plan. |

**What this means:**
- **The beaten path is crowded.** "Build a ball, watch it fight, async PvP against ghosts, share builds" is exactly Auto Ball Arena's pitch, and it launches this quarter. If we follow the same road, we arrive second.
- **The audience comes from watching.** It came from Earclacks / weapon-ball Shorts, the "Will the ball escape?" format (tens of millions of views per Short) and leagues like Ham's. Their natural behaviour is **watching and guessing who wins**, and Ham's already gives them a betting currency. No playable game owns that behaviour yet.

## 2. Lessons from the genre's hits and complaints

- **BALL x PIT** (2M+ sold) succeeded, per Devolver, by **respecting the player's time**:
  - every cycle gives something new;
  - no grind, no forced chores;
  - up to 10 speed levels.
  - It began as a reaction against gacha/stamina mobile design (PunBall).
- **Super Auto Pets:** players love the short async runs and the **weekly rotating packs** (always fresh). The top complaint is **pay-to-win**.
- **Backpack Battles:** players love the item depth and always-available async PvP. Complaints:
  - **matchmaking feels unfair**;
  - a few builds dominate;
  - RNG decides too much.
- **Daily runs** (Spelunky, Dead Cells, Caveblazers):
  - two rules make them work: a shared seed and a daily change;
  - one attempt raises the stakes;
  - steady daily traffic (Dead Cells about 7,000 daily runs).
- **Backlash against meta progression:** power upgrades blur whether you got better or just got stronger.
  - **Sidegrades and knowledge** feel better (Hades weapons, Noita).
  - Players with little time still like getting stronger, so it is a balance, not a ban.
- **Innovative 2026 roguelites** give the run a lasting identity:
  - **Mewgenics:** breeding, where traits pass to the next generation;
  - **Tears of Metal:** soldiers keep permanent skills.
  - Progression that tells a story beats a number going up.

## 3. Our unfair advantages (things competitors cannot copy cheaply)

1. **Bit-exact deterministic Sim** on Windows/Linux/.NET (48 golden digests, Linux player identical). One short code can reproduce any fight exactly.
2. **Fast headless Sim.** The bot already plays thousands of runs. The game can simulate a matchup many times in the background.
3. **Portrait 270x480 = 9:16**, the exact Shorts/TikTok/Reels frame. Steam competitors are landscape.
4. **Readable pixel identity:** 8 named balls with personalities and signature skills (EMBER, SPORE…). This is closer to the Earclacks "characters" fans root for than to generic warriors.

## 4. Breakthrough proposals (ranked by value ÷ cost)

### P1. "KÈO": predict the fight
A mode that turns the audience's habit into gameplay.
- **Setup:** the game shows two balls (later community builds, bosses, daily matchups) with their cards and the arena. The player predicts the winner, and optionally a bonus question: KO before 20 s? Which ball lands the last hit?
- **Stake:** a **non-purchasable, non-exchangeable** currency ("chip"). Correct calls earn chips and a streak. Chips buy cosmetics and titles only.
- **Fights:** the fight then plays with replays and slow motion.
- **Why it breaks away:**
  - nobody ships this as a playable loop (Ham's is a video league, FFTBattleground a Twitch bot);
  - it **teaches matchups** (progression by knowledge, the antidote to the meta-progression complaint);
  - it is fun in 30 seconds, a great session filler.
- **Guardrails:**
  - never real money; chips cannot be bought or cashed out;
  - UI word "DỰ ĐOÁN" (predict), not "CƯỢC" (bet);
  - check the age-rating wording before any store release.

### P2. Odds shown by the Sim: "the game tells you your chances"
- **What it shows:** on Prepare, a **win-chance bar** for the next fight (for example "37%"), computed by simulating the fight on N seeds in the background. Each card offer shows the change ("+6%").
- **Why it matters:**
  - **readability** (GDD pillar 3: "read the cause");
  - a strategic layer no competitor can fake;
  - the same engine powers KÈO odds and bot balance tools.
- **Cost and risk:**
  - one fight takes roughly 0.1 s of CPU on PC in the bot harness, so 10–20 seeds must run on a worker thread with a progressive estimate;
  - mobile needs measuring;
  - it could make choices feel solved, so it is shown as a range and not for every card (design choice).

### P3. Fight codes and one-tap 9:16 clips: growth engine
- **Fight codes:** every fight gets a short code; anyone pastes it and watches the identical fight (determinism). The PvP share-code work (BB-11 §4) already covers most of this.
- **Clips:** **SAVE CLIP** exports the last fight (or its last 10 s) as a vertical video or GIF with the code burned in. Auto-highlight comebacks, last hits and boss kills.
- **Why it matters:** the genre grows on Shorts. Every player becomes a content creator, and the portrait frame needs no cropping.

### P4. Ball DEEDS ("Chiến tích") instead of chests + shards
**Self-critique:** the chest/shard/gold plan in BB-12 (D2–D5) is the Brawl Stars path, and it is exactly what the genre's players complain about.

**Proposal:** each ball levels up by **doing things that show its identity**. Run progress counts, so every run moves a bar:
- EMBER: land 300 hits; win a fight with blade damage ≥ 20.
- MANTIS: reach max spin 20 times.
- ANGLER: land a hit at length ≥ 80.
- WRECKER: win after 30 wall bounces.
- SPARKLER: hit with all 8 arrows in one volley.
- SPORE: win by poison only.
- MIRROR: win by reflect damage.
- HIVE: have 6 turrets alive at once.
- Bosses and HEAT give bigger deeds.

**The skill tree stays unchanged:** 1 point per level. Only the source of the levels changes.

**Why it beats chests:**
- no randomness and no gacha law exposure;
- progress teaches what each ball is good at;
- every point has a story ("my SPORE killed WARDEN with poison");
- it fits BALL x PIT's "every run gives something".

**Cost:** event counters on SimEvents (Hit, Reflect, Poison…) plus a deed table. Deeds are measurable with the existing bot harness to tune pace (target: LV5 at about 10 runs, as before).

### P5. Daily run plus weekly "pack"
- **Daily:** everyone gets the same seed (enemies, arenas, card offers) and **one counted attempt**; practice runs are free and unscored.
  - The leaderboard needs a server, but phase 1 can show a **daily code** and a local best plus a share card. That needs no server.
- **Weekly:** a twist (for example "all arenas Low Grav", "only SPORE and MIRROR offered", "bosses x2"), like Super Auto Pets' weekly packs, which keeps the game fresh without new content.

### P6 (phase 2, needs a server). "Ghost nemesis"
- Your ball's build appears as a **named mid-boss** in other players' runs.
- When it wins you get a notification ("Your EMBER 'Than Hồng' beat 12 players today").
- This is async PvP with a story, different from Auto Ball Arena's 10-win gauntlet.

## 5. What not to do
- Do not sell chests, shards or power. Super Auto Pets' top complaint is pay-to-win; BALL x PIT won by refusing it.
- Do not make ranked PvP the core. Backpack Battles' biggest complaints are matchmaking and dominant builds, and it is Auto Ball Arena's home turf.
- Do not add more permanent % power than the tree already gives. The power should come from knowledge (KÈO, odds, deeds).

## 6. Suggested order
1. **P4** replaces D2–D5 (no chests). It touches the save (v3) like BB-12a would anyway.
2. **P2** odds bar (Prepare): S–M, and reused by P1.
3. **P1** KÈO, offline: daily matchups and community-style builds from the bot pool.
4. **P3** codes + clips.
5. **P5** daily/weekly.
6. **P6** later, with a server.

## 7. Decisions for Phu
1. Replace chests/shards/gold (D2–D5) with **ball deeds (P4)**? Recommended yes.
2. Add the **KÈO** prediction mode with a non-purchasable chip currency (P1)?
3. Show **win odds** on Prepare (P2): always, or only after the player unlocks it (for example after the first boss)?
4. Order of P1–P5 as in §6?

## Sources
- [Auto Ball Arena on Steam](https://store.steampowered.com/app/4114620/Auto_Ball_Arena/); [GamingOnLinux: Auto Ball Arena](https://www.gamingonlinux.com/2026/08/chaotic-physics-auto-battler-auto-ball-arena-looks-like-a-funny-time/)
- [BattleBalls on Steam](https://store.steampowered.com/app/3920370/BattleBalls/); [Auto Ball on Steam](https://store.steampowered.com/app/5182980/Auto_Ball/)
- [Ham's Ball Battles FAQ](https://hamsballbattles.com/faq.html); [Know Your Meme: Will the Ball Escape?](https://knowyourmeme.com/memes/will-the-ball-escape-ball-bouncing-brainrot)
- [AUTOMATON: why BALL x PIT hit](https://automaton-media.com/articles/newsjp/ball-pit-20251217-375231/); [Push Square: BALL x PIT 2M sales](https://www.pushsquare.com/news/2026/08/devolvers-brilliant-roguelite-dopamine-factory-ball-x-pit-is-a-smash-hit-at-2-million-sales)
- [VaporLens: Backpack Battles reviews](https://vaporlens.app/app/2427700/backpack_battles.md); [VaporLens: Super Auto Pets reviews](https://vaporlens.app/app/1714040/super_auto_pets.md)
- [Game Developer: examining daily runs](https://www.gamedeveloper.com/design/the-24-hour-ticket-examining-daily-runs-)
- [Meta progression feeling unrewarding (discussion)](https://lonestarpiefed.duckdns.org/post/24798)
- [Entalto: most innovative roguelites of 2026](https://entaltostudios.com/the-5-most-innovative-roguelites-of-2026/); [Rogueliker: Mewgenics review](https://rogueliker.com/mewgenics-review/)
- [MeinMMO: FFTBattleground fake-money betting on Twitch](https://mein-mmo.de/en/twitch-fake-geld-final-fantasy-tactics-wetten,460766/)
