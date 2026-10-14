using System;
using System.Collections.Generic;
using BallBattle.Sim.Boss;
using BallBattle.Sim.Traits;
using BallBattle.Sim.Weapons;

namespace BallBattle.Sim.Run
{
    /// <summary>
    /// One Run, as plain serializable data plus its rules. Everything random comes from the run seed and the
    /// player's choices, so the same seed + same choices replay the same enemies, cards and fights.
    /// Flow per fight: preview enemy + offer → Reroll / Pick (free, then bought) → lock → CreateMatch → ApplyResult.
    /// Unlocks and enemy pools are snapshotted at run start, so later unlocks or new content never change it.
    /// </summary>
    [Serializable]
    public sealed class RunState
    {
        public int SeedBits;
        public int FightIndex;
        public int RetryCount;
        public bool IsRetry;
        public bool StartPickPending;
        public int Coins = RunTuning.StartCoins;
        public RunBuild Build = new RunBuild();
        public int RerollCount;
        public int PicksTaken;
        /// <summary>Bit i = offer card i already taken this fight.</summary>
        public int PickedMask;
        /// <summary>Cards on the table for this fight, fixed when the fight starts or on reroll (picks do not reshuffle them).</summary>
        public List<OfferSlot> OfferCards = new List<OfferSlot>();
        /// <summary>Unlocked content offered to the player.</summary>
        public List<string> Weapons = new List<string>();
        public List<string> Traits = new List<string>();
        /// <summary>Content enemies may use (everything implemented at run start).</summary>
        public List<string> EnemyWeapons = new List<string>();
        public List<string> EnemyTraits = new List<string>();
        /// <summary>Achievements earned during this run (for the summary; survives quitting).</summary>
        public List<string> Achievements = new List<string>();
        public List<FightRecord> History = new List<FightRecord>();
        /// <summary>BB-04: boss of each boss slot (index = slot), chosen at run start by BossSelection. Never holds a forced boss.</summary>
        public List<string> BossIds = new List<string>();
        /// <summary>BB-04 debug (-bbForceBoss): boss that replaces its slot's boss and also fights at <see cref="ForcedBossFight"/>. "" = none.</summary>
        public string ForcedBossId = "";
        /// <summary>Extra fight (0-based) that meets the forced boss; -1 = none.</summary>
        public int ForcedBossFight = -1;
        /// <summary>
        /// Set when a fight starts, cleared when its result is applied. A run saved while locked resumes straight
        /// into that fight, so quitting after seeing a result can never be used to change the build and retry.
        /// </summary>
        public bool FightLocked;
        public bool Over;
        public bool Won;

        public uint Seed => unchecked((uint)SeedBits);
        /// <summary>A boss fight now: a boss slot, or the extra fight of a forced (debug) boss.</summary>
        public bool IsBossFight => CurrentBossId != null;

        /// <summary>BB-04: boss met at fight <paramref name="fightIndex"/>, or null for a normal fight. A forced boss wins over its slot's boss.</summary>
        public string BossFor(int fightIndex)
        {
            var forced = !string.IsNullOrEmpty(ForcedBossId);
            if (forced && fightIndex == ForcedBossFight) return ForcedBossId;
            for (var s = 0; s < RunTuning.BossFights.Length; s++)
            {
                if (RunTuning.BossFights[s] != fightIndex) continue;
                if (forced && Boss.BossIds.SlotOf(ForcedBossId) == s) return ForcedBossId;
                return s < BossIds.Count && !string.IsNullOrEmpty(BossIds[s]) ? BossIds[s] : Boss.BossIds.Slots[s][0];
            }
            return null;
        }

        /// <summary>Boss of the current fight, or null.</summary>
        public string CurrentBossId => BossFor(FightIndex);

        /// <summary>
        /// -bbForceBoss (debug, Run only): <paramref name="bossId"/> takes its own slot and, when the next fight to play is not a boss
        /// fight (and is not locked), that fight too. BossIds (and so the "last boss" history) are not touched. Returns false for an
        /// unknown id.
        /// </summary>
        public bool ApplyForcedBoss(string bossId)
        {
            if (!Boss.BossIds.Exists(bossId) || Over) return false;
            // A locked fight (resumed mid-fight) must replay exactly: never change its enemy.
            if (FightLocked && RunTuning.BossFights[Boss.BossIds.SlotOf(bossId)] == FightIndex) return false;
            ForcedBossId = bossId;
            if (!FightLocked && !RunTuning.IsBoss(FightIndex)) ForcedBossFight = FightIndex;
            return true;
        }
        public int BossesDefeated
        {
            get
            {
                var n = 0;
                foreach (var f in History) if (f.Won && RunTuning.IsBoss(f.FightIndex)) n++;
                return n;
            }
        }

        /// <summary>Up to 3 distinct unlocked weapons to start a run with.</summary>
        public static List<string> StartChoices(uint seed, IReadOnlyList<string> unlockedWeapons)
        {
            var pool = new List<string>(unlockedWeapons);
            var rng = new SimRandom(RunTuning.Derive(seed, 0, 0));
            var choices = new List<string>();
            while (choices.Count < RunTuning.StartWeaponChoices && pool.Count > 0)
            {
                var i = (int)(rng.NextUInt() % (uint)pool.Count);
                choices.Add(pool[i]);
                pool.RemoveAt(i);
            }
            return choices;
        }

        public static RunState Start(uint seed, string weaponId, ProgressData progress)
        {
            var weapons = UnlockRules.Weapons(progress);
            if (!StartChoices(seed, weapons).Contains(weaponId))
                throw new ArgumentException($"'{weaponId}' is not one of this run's start choices", nameof(weaponId));
            var run = new RunState { SeedBits = unchecked((int)seed), Weapons = weapons, Traits = UnlockRules.Traits(progress) };
            run.BossIds = BossSelection.Choose(seed, progress ?? new ProgressData());
            foreach (var w in WeaponRegistry.All) run.EnemyWeapons.Add(w.Id);
            foreach (var t in TraitRegistry.All) run.EnemyTraits.Add(t.Id);
            run.Build.WeaponId = weaponId;
            run.StartPickPending = true;
            run.DealOffer();
            return run;
        }

        /// <summary>
        /// Opponent of the current fight. MIMIC (BB-04) copies the player's weapon and traits (same levels) as they are right now,
        /// so CreateMatch (fight start) copies the final build, including cards picked after the preview.
        /// </summary>
        public RunBuild Enemy
        {
            get
            {
                var e = EnemyGenerator.Generate(Seed, FightIndex, EnemyWeapons, EnemyTraits, CurrentBossId);
                if (e.BossId == Boss.BossIds.Mimic)
                {
                    e.WeaponId = Build.WeaponId;
                    e.Traits = new List<TraitSlot>();
                    foreach (var t in Build.Traits) e.Traits.Add(new TraitSlot(t.Id, t.Level));
                }
                return e;
            }
        }

        public int OfferCount => OfferCards.Count;
        public Card OfferCard(int index) => OfferCards[index].ToCard();
        public bool IsPicked(int index) => (PickedMask & (1 << index)) != 0;

        void DealOffer()
        {
            OfferCards.Clear();
            if (Over) return;
            var offerSeed = IsRetry && StartPickPending ? RunTuning.Derive(Seed, 4001 + FightIndex, 0)
                : StartPickPending ? RunTuning.Derive(Seed, 4000, 0) : Seed;
            foreach (var c in CardOffer.Generate(offerSeed, FightIndex, RerollCount, Build, Weapons, Traits))
                OfferCards.Add(OfferSlot.From(c));
        }

        /// <summary>Is the card still valid for the build as it is now (an earlier pick may have used the last trait slot)?</summary>
        public bool StillApplies(Card c)
        {
            switch (c.Kind)
            {
                case CardKind.Trait:
                    var level = Build.TraitLevel(c.Id);
                    return c.Level == 1 ? level == 0 && Build.Traits.Count < RunTuning.MaxTraits : level == c.Level - 1;
                case CardKind.SwapWeapon: return c.Id != Build.WeaponId;
                case CardKind.Heal: return Build.Hurt;
                default: return true;
            }
        }

        public int NextRerollCost => RunTuning.RerollCost + RerollCount;
        public bool CanReroll => !Over && !StartPickPending && !FightLocked && PicksTaken == 0 && Coins >= NextRerollCost;

        /// <summary>Cost of the next pick: 0, ExtraPickCost, or -1 when no more picks are allowed.</summary>
        public int NextPickCost => Over || FightLocked || PicksTaken >= (StartPickPending ? 1 : RunTuning.MaxPicksPerFight) ? -1 : (PicksTaken == 0 ? 0 : RunTuning.ExtraPickCost);

        public bool CanPick(int index)
        {
            var cost = NextPickCost;
            return cost >= 0 && Coins >= cost && index >= 0 && index < OfferCards.Count && !IsPicked(index) && StillApplies(OfferCard(index));
        }

        public void Reroll()
        {
            if (!CanReroll) throw new InvalidOperationException("Cannot reroll now");
            Coins -= NextRerollCost;
            RerollCount++;
            DealOffer();
        }

        public void Pick(int index)
        {
            if (!CanPick(index)) throw new InvalidOperationException($"Cannot pick card {index} now");
            var card = OfferCard(index);
            Coins -= NextPickCost;
            PicksTaken++;
            PickedMask |= 1 << index;
            card.ApplyTo(Build);
            if (StartPickPending)
            {
                StartPickPending = false;
                PicksTaken = 0;
                PickedMask = 0;
                if (!IsRetry) DealOffer();
            }
        }

        public uint ArenaSeed => RunTuning.Derive(Seed, 3000 + FightIndex, 0);
        public uint FightSeed => RunTuning.Derive(Seed, 3000 + FightIndex, IsRetry ? 1 : 0);

        /// <summary>Arena of the current fight, picked by its seed.</summary>
        public string ArenaId => ArenaRegistry.ForSeed(ArenaSeed);

        /// <summary>
        /// Fresh match for the current fight (ball 0 = player, ball 1 = enemy) with spawn hooks applied, so the
        /// countdown already shows final sizes and HP. Locking the run is RunFlow's job (it also saves).
        /// </summary>
        public MatchSim CreateMatch(MatchConfig config = null)
        {
            if (Over) throw new InvalidOperationException("Run is over");
            var m = new MatchSim(config ?? ArenaRegistry.Create(ArenaId), FightSeed, new[] { Build.ToLoadout(), Enemy.ToLoadout() });
            m.ApplySpawnHooks();
            return m;
        }

        /// <summary>
        /// Win: +coins, keep HP (as a fraction of max, so match-only bonuses like Heavy never leak) + WinHealPct of max.
        /// Loss or draw: +coins and use the run's single retry at 60% HP on the same fight; another loss ends the run.
        /// HP is kept in whole points. Clears FightLocked.
        /// </summary>
        public void ApplyResult(MatchSim match)
        {
            if (Over) throw new InvalidOperationException("Run is over");
            if (match.Outcome == MatchOutcome.Ongoing) throw new InvalidOperationException("Match is not finished");

            var won = match.WinnerIndex == 0;
            var attemptedRetry = IsRetry;
            if (won)
            {
                Coins += RunTuning.WinCoins;
                Build.SetHp(match.Balls[0].HpFraction * Build.MaxHp + Build.MaxHp * RunTuning.WinHealPct);
                IsRetry = false;
            }
            else
            {
                Coins += RunTuning.LossCoins;
                if (RetryCount < RunTuning.Retries)
                {
                    RetryCount++;
                    IsRetry = true;
                    StartPickPending = true;
                    Build.SetHp(Build.MaxHp * RunTuning.RetryHealPct);
                }
                else Over = true;
            }

            History.Add(new FightRecord { FightIndex = FightIndex, Won = won, Draw = match.WinnerIndex < 0, Retry = attemptedRetry, EnemyWeapon = Enemy.WeaponId, HpAfter = Build.Hp, BossId = CurrentBossId ?? "" });
            var last = FightIndex == RunTuning.Fights - 1;
            if (won) FightIndex++;
            RerollCount = 0;
            PicksTaken = 0;
            PickedMask = 0;
            FightLocked = false;
            if (last && won)
            {
                Over = true;
                Won = true;
            }
            DealOffer();
        }
    }
}
