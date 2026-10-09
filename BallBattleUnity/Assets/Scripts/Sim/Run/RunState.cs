using System;
using System.Collections.Generic;
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
        public int Lives = RunTuning.Lives;
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
        /// <summary>
        /// Set when a fight starts, cleared when its result is applied. A run saved while locked resumes straight
        /// into that fight, so quitting after seeing a result can never be used to change the build and retry.
        /// </summary>
        public bool FightLocked;
        public bool Over;
        public bool Won;

        public uint Seed => unchecked((uint)SeedBits);
        public bool IsBossFight => RunTuning.IsBoss(FightIndex);
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
            foreach (var w in WeaponRegistry.All) run.EnemyWeapons.Add(w.Id);
            foreach (var t in TraitRegistry.All) run.EnemyTraits.Add(t.Id);
            run.Build.WeaponId = weaponId;
            run.DealOffer();
            return run;
        }

        public RunBuild Enemy => EnemyGenerator.Generate(Seed, FightIndex, EnemyWeapons, EnemyTraits);

        public int OfferCount => OfferCards.Count;
        public Card OfferCard(int index) => OfferCards[index].ToCard();
        public bool IsPicked(int index) => (PickedMask & (1 << index)) != 0;

        void DealOffer()
        {
            OfferCards.Clear();
            if (Over) return;
            foreach (var c in CardOffer.Generate(Seed, FightIndex, RerollCount, Build, Weapons, Traits))
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

        public bool CanReroll => !Over && !FightLocked && PicksTaken == 0 && Coins >= RunTuning.RerollCost;

        /// <summary>Cost of the next pick: 0, ExtraPickCost, or -1 when no more picks are allowed.</summary>
        public int NextPickCost => Over || FightLocked || PicksTaken >= RunTuning.MaxPicksPerFight ? -1 : (PicksTaken == 0 ? 0 : RunTuning.ExtraPickCost);

        public bool CanPick(int index)
        {
            var cost = NextPickCost;
            return cost >= 0 && Coins >= cost && index >= 0 && index < OfferCards.Count && !IsPicked(index) && StillApplies(OfferCard(index));
        }

        public void Reroll()
        {
            if (!CanReroll) throw new InvalidOperationException("Cannot reroll now");
            Coins -= RunTuning.RerollCost;
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
        }

        public uint FightSeed => RunTuning.Derive(Seed, 3000 + FightIndex, 0);

        /// <summary>
        /// Fresh match for the current fight (ball 0 = player, ball 1 = enemy) with spawn hooks applied, so the
        /// countdown already shows final sizes and HP. Locking the run is RunFlow's job (it also saves).
        /// </summary>
        public MatchSim CreateMatch(MatchConfig config = null)
        {
            if (Over) throw new InvalidOperationException("Run is over");
            var m = new MatchSim(config ?? new MatchConfig(), FightSeed, new[] { Build.ToLoadout(), Enemy.ToLoadout() });
            m.ApplySpawnHooks();
            return m;
        }

        /// <summary>
        /// Win: +coins, keep HP (as a fraction of max, so match-only bonuses like Heavy never leak) + WinHealPct of max.
        /// Loss or draw: +coins, lose a life, full HP. The run always moves on; it is won by winning the last fight.
        /// HP is kept in whole points. Clears FightLocked.
        /// </summary>
        public void ApplyResult(MatchSim match)
        {
            if (Over) throw new InvalidOperationException("Run is over");
            if (match.Outcome == MatchOutcome.Ongoing) throw new InvalidOperationException("Match is not finished");

            var won = match.WinnerIndex == 0;
            if (won)
            {
                Coins += RunTuning.WinCoins;
                Build.SetHp(match.Balls[0].HpFraction * Build.MaxHp + Build.MaxHp * RunTuning.WinHealPct);
            }
            else
            {
                Coins += RunTuning.LossCoins;
                Lives--;
                Build.Hp = Build.MaxHp;
            }

            History.Add(new FightRecord { FightIndex = FightIndex, Won = won, Draw = match.WinnerIndex < 0, EnemyWeapon = Enemy.WeaponId, HpAfter = Build.Hp });
            var last = FightIndex == RunTuning.Fights - 1;
            FightIndex++;
            RerollCount = 0;
            PicksTaken = 0;
            PickedMask = 0;
            FightLocked = false;
            if (last || Lives <= 0)
            {
                Over = true;
                Won = last && won;
            }
            DealOffer();
        }
    }
}
