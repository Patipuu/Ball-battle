# Chorus, Hive, and eight-boss attack follow-up — 2026-10-20

## Changes

- Run-player BGLGXLQ lit colours now last 15 active seconds. Contact between an already lit orb and a living enemy resets that colour's timer. The refresh does not apply the colour gift twice. The existing automatic Versus profile has no colour fade.
- Hive bees already dealt `DamageKind.Ability` through `MatchSim.FxBee`; added a regression test that checks both a damage event and actual enemy HP loss. No production Hive damage change was needed.
- Mimic marks the player's position every 300 active ticks after an initial 180 ticks, warns for 45 ticks, then deals 16 ability damage within a 38 px circle. Warden warns on horizontal and vertical lanes around the player's marked position, then deals 12 ability damage on either lane. Both can be dodged after their warning.
- Added telegraph drawing, existing audio-cue mappings, and EN/VI boss hints for both attacks. All eight bosses now have authored attacks that deal ability damage in the staged test.
- Bumped simulation Rules 21 → 22. Re-pinned the Rules-dependent 20-seed replay digest; the Rules-masked Versus digest remains unchanged. Old Rules 20 hashes are preserved in `CrossPlatformGoldenTests.cs` beside the new values.
- Updated older Warden/Shell King test assumptions to account for authored attacks, open-shell damage, fixed-point rounding, and normal bounce following Warden's step knockback.

## Verification

- Focused simulation regressions: 68/68 passed (`SimTests/TestResults/boss-chorus-all-relevant-pass.trx`). Includes Chorus 15-second refresh, Hive HP loss, eight-boss authored damage, both new attacks' warning and dodge cases, deterministic replay and golden hashes.
- Unity EditMode on the isolated project copy: 22/22 passed (`docs/claude-runs/boss-attack-editmode-final.xml`). Covers boss cues, localized hints, and visible Mimic/Warden warnings.
- Greedy bot, seeds 1–240: 59/240 full-run wins (24.6%). Same current code with only the new attack damage disabled: 63/240 (26.3%). Warden encounter wins were 6/21 with crossfire versus 9/20 without its damage; these are small samples and a bot, not a human playtest.
- `git diff --check` passed for the touched source and test files.

The optional all-SimTests sweep was stopped after almost nine minutes in slow balance diagnostics; no full-suite result is claimed. The narrowed suites above completed. No commit or push was made.

## Unresolved questions

- Human playtesting is needed to decide whether Warden's crossfire damage should be reduced. The current 240-seed run stayed inside the existing overall win-rate target, while Warden became the hardest individual boss in this probe.
