# PvP ranked v1 — research and implementation plan

Date: 2026-10-20. Status: design confirmed by user; simulation core, self-hosted service and Unity PvP flow implemented. Deployment, production progression trust, and two-device QA remain. “Choose 3 people, fight 1” means three real opponent offers, then one selected opponent. The user chose online play with shared server MMR.

## Existing project

- `MenuView`'s SO TÀI / VERSUS page lets one local user choose both balls and one of seven arenas. `MatchFlow` runs a best-of-three series with two weapon IDs. There is no opponent identity, network transport, match service, or MMR.
- `ProgressData.Balls` stores each ball's permanent level and `MetaTree` allocation. `MetaTree.ApplyToRun` applies HP, damage, active-skill branch, opener, instinct, and finale to a `RunBuild`; ordinary Versus builds neither player's profile. This is the intended source for PvP stats, with a fresh full-HP build per ranked match.
- The simulator is deterministic and already has a rules version and state hash. This makes server-run or server-verified asynchronous challenges practical. A local result or editable save must not be authoritative for ranked MMR.

## External examples and decisions

| Example | Documented rule | Adopt here |
| --- | --- | --- |
| [Brawl Stars Power League](https://supercell.com/en/games/brawlstars/blog/news/power-league-changes-and-improvements-2/) | Opposing bans are blind; duplicate bans can occur. | Each side submits one ball and one arena ban without seeing the other's choices. Duplicate bans consume both votes and remove that item once. |
| [Brawl Stars Ranked](https://support.supercell.com/brawl-stars/en/articles/about-ranked-3.html) | Higher ranks combine bans, picks, and best-of-three; ranked has minimum eligible roster sizes. | Keep the current best-of-three series. Require at least three distinct prepared defense balls so two global bans cannot leave the defender without a pick. Do not gate the first PvP match behind upgrades. |
| [Clash Royale matchmaking](https://support.supercell.com/clash-royale/en/articles/matchmaking-3.html) and [Brawl Stars ranked matchmaking](https://support.supercell.com/brawl-stars/en/articles/ranked-matchmaking-5.html) | Look for similar rating, expand the search when necessary; points gained/lost depend on opponent rank. | Offer three real profiles near the player's MMR and power band; expand the range in controlled steps. Display the expected rating change on each offer. |
| [Wild Rift matchmaking](https://wildrift.leagueoflegends.com/en-us/news/game-updates/more-updates-coming-to-matchmaking/) | MMR predicts performance; visible rank can be a separate reward/progression display. | Use one numeric MMR for matching and postmatch delta in v1. Cosmetic leagues can be added later without affecting matching. |
| [Legends of Runeterra tournament policy](https://playruneterra.com/en-sg/news/seasonal-tournaments-global-player-policy/) | A ban phase precedes a pick phase; expired actions get deterministic fallbacks. | Lock bans, reveal, then pick. The beta expires unfinished challenges after 15 minutes; automatic fallback remains planned. |

These are design precedents, not claims that those games use this exact three-offer asynchronous format.

## Mode and player flow

1. Replace the hub SO TÀI button and old local Versus page with PvP. Keep internal old `MatchFlow` and deterministic test harness until migrated; remove local two-sided selection from the player-facing flow. The PvP page shows the player's MMR, record, equipped defense roster, and connection state.
2. A player signs in, publishes a defense roster of three distinct balls (ordered fallback), one precommitted blind ball ban, and one precommitted blind arena ban. The server stores immutable level/tree snapshots for that roster until the player publishes an update. Public opponent cards reveal nickname, MMR, record, and rough power range, not bans or exact roster.
3. The server returns exactly three distinct eligible human profiles. The player picks one. Offers are short lived and bound to the initiating account; no self-match, repeated opponent within a cooldown, bot, or locally fabricated profile. If the pool is too small, show “not enough opponents” and allow retry, without touching MMR.
4. The challenger independently submits one ball ban and one arena ban. The defender's previously committed choices were locked before this challenge. Reveal both choices together. The union removes one or two balls and one or two arenas globally. Duplicates are legal. The beta expires an unfinished challenge after 15 minutes: before the reveal it is dropped with no MMR change; after the reveal the server auto-picks the challenger's highest-level legal ball and settles normally (no dodge).
5. Challenger picks one of their remaining unlocked balls. Defender uses the first unbanned ball in its ordered, committed defense roster. Both sides use their own permanent ball level/tree snapshot, with the same conversion rules as `MetaTree.ApplyToRun`, fresh full HP, no Run cards/temporary traits, and the same deterministic auto-tap policy to drive the full Run active skill for both sides. The server chooses one arena from the unbanned set by an auditable match seed. Bans and arena remain fixed for the whole best-of-three series.
6. The deterministic series swaps starting sides in round two; round three uses a seeded side. First to two wins. A drawn round replays with a new fixed seed; after a bounded number of draws, the series is a draw with zero MMR change. Show opponent, bans, arena, each ball's level/tree, round score, result, MMR before/after, and replay seed/rules version.
7. The challenger alone gains or loses ranked MMR. The asynchronous defender's MMR never changes while offline; defense wins/losses are a separate statistic. A future live synchronous mode may rate both active participants. This avoids penalizing a player for unattended challenges.

## Rating and candidate rules

- New account: 1000 MMR, 10 provisional matches. Expected score `E = 1/(1+10^((R_opp-R_self)/400))`; `K = 32` provisional, `24` thereafter. Win score 1, loss 0. Delta is rounded `K × (score−E)`, clamped to at least +1 for a win and at most −1 for a loss; MMR floor 100. Draw or invalid match: 0. The server computes and commits this exactly once by match ID. No rematch can award points on the same ID.
- Three offers should span roughly lower/equal/higher MMR when candidates exist. Initial range ±150 MMR and a power band based on the average level of the three committed defense balls; expand to ±300 then ±500 only if fewer than three verified profiles are available. Do not silently fill with bots. The shown projected deltas use the exact server formula and frozen ratings from offer creation.
- Selecting a weaker opponent gives fewer points for a win and loses more on defeat. Limit repeat challenges to the same account within 24 hours and refresh offers no more than once every 10 minutes. Do not let leaving the opponent page roll unlimited easy options.
- No fixed stat normalization: PvP honours each ball's actual permanent level and tree, as requested. Power-band matching, visible level at reveal, and test sweeps protect fairness. Avoid paid refreshes, stat boosts, or extra PvP-only upgrades in v1.

## Trust, persistence, and service

- A production-ranked result needs account authentication, HTTPS, server-generated offer/challenge IDs, nonce and expiry, idempotent result submission, rate limits, and transactional MMR updates. The simulator's rules version and immutable loadout snapshots are stored on every match. Server recomputes the match from seed and snapshots (or runs it directly) rather than accepting the client's claimed winner.
- Permanent levels/tree cannot be read from an editable client save as trusted PvP stats. Server must own PvP progression or verify all upgrade inputs and reward events. Existing local save import is not automatically trusted for rated play. Migration should preserve local Run progress while a server-authoritative account profile is established; the exact account identity/hosting provider is a deployment decision.
- Suggested service boundary: `POST /auth`, `PUT /pvp/defense`, `GET /pvp/offers`, `POST /pvp/challenges`, `POST /pvp/challenges/{id}/ban`, `POST /pvp/challenges/{id}/pick`, `GET /pvp/challenges/{id}`, `GET /pvp/history`, and server-only settlement. Never expose the defender's committed bans or roster before the challenger's ban is locked. Database unique keys protect one settlement per match and cooldown constraints.
- Unity client shows explicit offline/server-unavailable state. Network error before settlement leaves the challenge pending for recovery; on restart it re-fetches status. A completed server result is displayed even if the client crashed during animation. Local practice may be offered separately, clearly unranked, without fake MMR.

## Implementation sequence

1. Pure C# rules: immutable player/defense snapshots, eligibility/ban union, deterministic arena and defender selection, meta-tree-to-PvP loadouts, MMR calculation, series seeds and result record. Unit tests cover duplicates, both bans, exclusion, tree/level on both sides, draw, +/− deltas, idempotency, and eight-to-ten-ball balance.
2. Server and persistence: account ownership, defense snapshots, three-offer matching, challenge state machine, blind commitments, simulation/settlement, audit log, migration. Integration tests use multiple real test accounts and restart/retry cases.
3. Unity flow: replace SO TÀI with PvP, offer cards, ban/pick screens, pre-fight reveal, series HUD, result/rating screen, history, offline and timeout states. Keep visual scale inside 270×480. Localize EN/VI and update onboarding hints.
4. Qualification: all existing SimTests and Unity EditMode tests, deterministic cross-platform replays, server/client integration, rules-version migration, 500+ seeded matchups across level/tree/arena combinations, side-swap fairness, candidate distribution, crash/retry/double-settlement tests, and manual two-device playtest.

## Open decisions before live deployment

- Choose a public HTTPS host and configure the Unity client URL. No server is deployed from this workspace.
- Move ball progression to the server or verify signed Run replays before a public competitive launch. The current beta validates level caps and tree allocation, but imports player-edited local progress.
- Replace anonymous bearer-only accounts with recoverable sign-in before a public launch.
