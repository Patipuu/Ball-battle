# BB-11: async PvP ("Code Duel") design doc, for Phu's decision

Status: **proposal**, nothing implemented. Base `2a95b08` (Rules 13). Builds on `docs/design/pvp-research.md`, `pvp-tech.md` and GDD §18.

## 0. What is already decided (Phu, 11/10 14:51)
- **Phase 1 has no server and costs nothing.** A player exports a loadout as a **short share code**; the opponent pastes it and fights it **on their own device**. The result is deterministic.
- **Pool and limits:** one shared card pool (nothing owned is needed) with a **point budget** and at most **1 sustain trait**. No ranked, 2v2 or FFA. A free-tier backend is optional later.
- **Precondition:** the Sim must be deterministic across platforms (trig table). A code from another `SimVersion` is refused.

This doc turns that decision into a buildable spec, fills the gaps it left open (skills, which did not exist on 11/10, the budget, arenas and the seed rule), and lists the decisions only Phu can make (§7).

## 1. Precondition status: determinism (measured today)

| Check | Result |
|---|---|
| Sim code | All Q16.16 fixed point since 6a.6b/c (`FixedTrig` table); no float in `Sim/` except one display conversion (`Fix.ToFloat`). |
| Windows x64, .NET 10 (SimTests) | 49 golden digests pass (`CrossPlatformGoldenTests` ×40, `VersusGoldenTests`, 7 arena digests, Rules 13). |
| **Linux x64, .NET 8** (new, today) | **All 49 digests identical** to the Windows values. |
| **Linux Unity player (Mono)**, build bc3c460 | `-roundrobin -seeds 2 -arena classic` CSV (64 matchups, winners and tick counts) is **byte-identical** to .NET 8 on Linux. |
| Unity Editor (Mono, Windows) | `FixedMathTests.FixedOperationsHaveCrossPlatformGoldenHash` passes. |
| **IL2CPP: Android ARM, WebGL** | **Not measured yet.** Needs a device or browser run (task BB-11d). |

**Conclusion:** codes are safe between Windows, Linux and Mac Mono/.NET today. Android and WebGL must pass BB-11d before we promise "same result on every device" there.

## 2. Format
- **Code Duel, Bo3, 1 ball vs 1 ball.** Both loadouts are fixed for the 3 games (no reaction turns in async).
- **Two sides:**
  - **Host:** builds a loadout, gets a code, and shares it anywhere (chat, Discord, a forum).
  - **Challenger:** builds or picks a loadout, pastes the host code, bans an arena, and the Bo3 plays out.
- **Same pair, same result everywhere:** two given codes always produce the same Bo3, on any device (seed rule §4). This is the selling point against the direct competitor (Auto Ball Arena, async PvP, Q4 2026). Duels are reproducible and shareable as a single **match code** (both codes together) that anyone can replay.
- **No rewards** in phase 1: no unlocks, no rating (recommendation; D6).

## 3. Loadout and budget (proposal; the numbers are for the PvP gate to confirm)

| Part | Options | Cost |
|---|---|---|
| Weapon | any of the 8 (shared pool, nothing to own) | 0 |
| Skill | its signature skill: LV1, LV2 A/B, or LV3 A/B | LV1 0, LV2 **1**, LV3 **2** |
| Traits | up to 3 of the 10, LV1 or LV2; weapon eligibility as in Run (no Parry Master / Twin Blade / Poison Tip without a blade; no Glass Cannon on WRECKER) | LV1 **1**, LV2 **2** |
| Stat cards | DAMAGE +15% (now also SPORE poison and MIRROR reflects, BB-BAL3), SPEED CAP +10%, MAX HP +15 | **1** each, at most 2 in total |
| **Budget** | | **6 points** |
| **Sustain limit** | at most 1 of VAMPIRE, SECOND WIND, BUBBLE | |

- HP always starts full. No bosses, no MIMIC, no Run-only cards (HEAL, SWAP).
- Example builds of 6 points:
  - EMBER, skill LV3 (2), THORNS LV2 (2), DAMAGE ×2 (2).
  - SPORE, skill LV2 (1), POISON-free build with VAMPIRE LV2 (2), HEAVY LV1 (1), MAX HP ×2 (2).
- Validation lives in Sim (`PvpLoadout.Validate`), so an invalid or hacked code is refused with a reason.

## 4. Share code, arenas and seed
- **Code** (proposal): `BB13-7K2Q-M9XA-4FTD`.
  - The prefix holds `Rules`.
  - The body is about 8 bytes in Crockford base32 (no I/L/O/U, case-insensitive):
    - format version, weapon, skill level and branch;
    - 3 trait slots (id + level);
    - stat card counts;
    - the host's **arena ban**.
  - It ends with a 16-bit checksum, so a typo is detected.
- **No player name in the code.** Free text needs moderation (pvp-research §8).
- **Version:** a code whose Rules ≠ the game's is refused with "CODE FROM RULES 12 – BOTH PLAYERS NEED THE SAME VERSION".
- **Arenas:** a pool of all 7 arenas.
  - The host's ban is inside the code; the challenger bans 1 when fighting.
  - The 3 games use 3 arenas drawn from the remaining 5 by the duel seed, **shown before the challenger locks their loadout** (pvp-research §3).
  - Spikes stays in the pool: the ban is the counter to SPORE on spikes (decision of 11/10 03:05).
- **Seed** (recommended): `duelSeed = Derive(hash(codeA), hash(codeB))`, with the codes in a fixed order.
  - Neither side chooses it, and the same two codes always give the same Bo3, so the match code works as a replay.
  - Game i uses `Series.SeedFor(duelSeed, i)`, which already exists.
  - **Downside:** a challenger can tweak their own build until they beat a known code. That is harmless without ranking, and it fits a theorycrafting / bragging use. The alternative (a random seed per duel, D4) makes every rematch different.

## 5. Screens (pixel UI, 270x480, reusing existing parts)

| Screen | Content | Reuse |
|---|---|---|
| Menu | new **DUEL** button (or a tab inside VERSUS) | MenuView |
| Builder | weapon tiles; skill level/branch; up to 3 traits with level; stat cards; **budget meter "4/6"**; reason when invalid (red, like Prepare) | Versus tiles, SkillBranchButtons, CardFace, trait badges |
| My code | big code text, COPY, ban 1 arena, "share this code" | ArenaThumbnail |
| Fight a code | PASTE (reads the clipboard) plus manual entry; shows the opponent's build before the fight; ban 1 arena; shows the 3 arenas; FIGHT | Prepare-style boxes |
| Bo3 | the existing Versus Bo3 flow (`MatchFlow`, `Series`) | as is |
| Result | winner, 3 game lines, **match code** (replay), REMATCH (replays the same Bo3), BACK | RunResultView style |

**Clipboard:**
- **Desktop and Android:** `GUIUtility.systemCopyBuffer` works.
- **WebGL:** browsers restrict it, so a manual entry field is needed. The game has no text input yet; this is new UI work.

## 6. Tasks and rough size (one agent, sequential)

| Task | Content | Sim / Rules impact | Size |
|---|---|---|---|
| **BB-11a** PvpLoadout + code | `Sim/Pvp/PvpLoadout.cs`: budget validation, encode/decode with checksum, `ToLoadout()`, duel seed and arena draw. SimTests: round-trip of every valid loadout; invalid and hacked codes refused; same pair gives the same digest. | New Sim file, no match rule change, Rules stays | S–M |
| **BB-11b** Duel UI | builder, my code, paste, ban, Bo3, result, replay; EditMode tests and 1x/3x shots | View only | M–L |
| **BB-11c** PvP balance gate | harness: 200 random valid loadouts per candidate, all arenas; gate "no loadout > 65%"; a meta-hunter hill-climb to find the best builds; outputs the budget/ban list. Runs inside the combined balance pass. | none (measurement) | M |
| **BB-11d** IL2CPP determinism check | player flag `-hashcheck` prints the 49 golden digests; run on Android (IL2CPP ARM) and WebGL; must equal the Windows/Linux values before codes are promised there | View flag only | S (+ device time) |
| (later) | "Code of the day" (a bot loadout per date), free-tier ghost board, Run ghost | — | — |

**Suggested order:** BB-11a, then BB-11d (cheap, de-risks the platforms), then BB-11b, then BB-11c together with the combined balance pass.

## 7. Decisions for Phu (my recommendation first)
1. **D1 First platform for duels:** PC (Windows/Linux) first; Android after BB-11d passes. This drives the clipboard work and the IL2CPP check.
2. **D2 Budget:** 6 points; skill LV2 = 1 and LV3 = 2; trait LV1 = 1 and LV2 = 2; at most 2 stat cards at 1 each; at most 1 sustain trait. To be confirmed by the BB-11c gate, which may change the numbers.
3. **D3 Arenas:** all 7 in the pool, each side bans 1, the 3 games drawn from the remaining 5 and shown before lock-in.
4. **D4 Seed:** derived from both codes (same pair, same result, shareable replay). Alternative: random per duel.
5. **D5 Identity:** no name in the code (no moderation). Later: pick a title from a fixed list.
6. **D6 Rewards:** none in phase 1 (no unlocks from duels, so nobody farms against friends' codes).
7. **D7 Bosses/MIMIC in duels:** no.
8. **D8 "Code of the day"** (a daily bot loadout, same for everyone, no server): yes, as a cheap extra after BB-11b.

## 8. Risks
- **IL2CPP untested:** if Android or WebGL differ, codes there would give different results. BB-11d finds this before launch; the fallback is to label the result "result on this device".
- **Meta and combos:** PvP exposes strong builds (bot: WRECKER + VAMPIRE2 + SECOND WIND2 + THORNS2 at 85.7%). The budget, the sustain limit and the BB-11c gate are the defence. BB-BAL3 made DAMAGE cards stronger on SPORE/MIRROR, so the gate must include them.
- **Small player base:** without a server, duels depend on players sharing codes. Code of the day gives solo players a target.
- **Text input:** the game has none yet. PASTE covers desktop and Android; WebGL needs a manual field.
