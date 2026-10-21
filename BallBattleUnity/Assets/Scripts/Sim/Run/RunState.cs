using System;
using System.Collections.Generic;
using System.Linq;
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
        /// <summary>TUT-2: first run has a stable starter offer and a gentle first fight; saved so Continue replays it exactly.</summary>
        public bool TutorialRun;
        /// <summary>PvE chapter of this run (0-based, Chapters). Old saved runs load 0 = chapter 1.</summary>
        public int Chapter;
        /// <summary>HEAT of this run (0-based, HeatRules). Old saved runs load 0 = HEAT 1.</summary>
        public int Heat;
        /// <summary>Retries this run allows (HEAT 3: none).</summary>
        public int MaxRetries => HeatRules.Retries(Heat);
        public const uint TutorialSeed = 0x7A171A1u;

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

        /// <summary>Every unlocked ball is selectable. The seed changes fights and offers, never this list.</summary>
        public static List<string> StartChoices(uint seed, IReadOnlyList<string> unlockedWeapons, bool tutorial = false)
        {
            return unlockedWeapons == null ? new List<string>() : unlockedWeapons.Distinct().ToList();
        }

        [NonSerialized] public Skills.SkillSpec? SkillOverride;
        public int EffectiveSkillLevel => SkillOverride?.Level ?? Build.SkillLevel;
        public Skills.SkillBranch EffectiveSkillBranch => SkillOverride?.Branch ?? Build.SkillBranch;

        public static RunState Start(uint seed, string weaponId, ProgressData progress, Skills.SkillSpec? forceSkill = null, bool tutorial = false, int chapter = 0, int heat = 0)
        {
            var weapons = UnlockRules.Weapons(progress);
            if (!StartChoices(seed, weapons, tutorial).Contains(weaponId))
                throw new ArgumentException($"'{weaponId}' is not one of this run's start choices", nameof(weaponId));
            var run = new RunState { SeedBits = unchecked((int)seed), Weapons = weapons, Traits = UnlockRules.Traits(progress), TutorialRun = tutorial };
            // Chapters (21/10): the chapter fixes the boss pair, the enemy balls and the arenas. The tutorial run is chapter 1.
            run.Chapter = tutorial ? 0 : System.Math.Max(0, System.Math.Min(chapter, Chapters.Count - 1));
            run.Heat = tutorial ? 0 : HeatRules.Clamp(heat);
            var ch = Chapters.Get(run.Chapter);
            run.BossIds = new List<string>(ch.Bosses);
            foreach (var w in ch.EnemyWeapons) if (!WeaponRegistry.IsPlayerOnly(w)) run.EnemyWeapons.Add(w);
            foreach (var t in TraitRegistry.All) if (!t.PlayerOnly) run.EnemyTraits.Add(t.Id);
            run.Build.WeaponId = weaponId;
            run.Build.MaxHp = run.Build.Hp = BallPower.BaseHp(weaponId);   // Rules 24: each ball's own base HP
            // BB-12g: the ball's skill tree (points from its level) gives the run its root bonuses, skill level/branch and in-match nodes.
            if (RunTuning.SkillTree) Meta.MetaTree.ApplyToRun(run.Build, FindBall(progress, weaponId));
            run.SkillOverride = forceSkill;
            run.StartPickPending = true;
            run.DealOffer();
            return run;
        }

        static Meta.BallProgress FindBall(ProgressData p, string id)
        {
            if (p?.Balls != null) foreach (var b in p.Balls) if (b.Id == id) return b;
            return null;
        }

        /// <summary>
        /// Opponent of the current fight. MIMIC copies the player's weapon and active skill, then adapts to damage in battle,
        /// so CreateMatch (fight start) copies the final build, including cards picked after the preview.
        /// </summary>
        public RunBuild Enemy
        {
            get
            {
                var e = EnemyGenerator.Generate(Seed, FightIndex, EnemyWeapons, EnemyTraits, CurrentBossId, Chapters.Get(Chapter), Heat);
                if (TutorialRun && FightIndex == 0 && CurrentBossId == null)
                {
                    // The first fight should reach contact quickly, even when the player
                    // picked a long-range ball. The old Pike mirror could idle for 8+ seconds.
                    e.WeaponId = "brawler";
                    e.MaxHp = e.Hp = 60;
                    e.DamagePct = -Fix.Ratio(1, 4);
                    e.Traits.Clear();
                }
                if (e.BossId == Boss.BossIds.Mimic)
                {
                    if (!WeaponRegistry.IsPlayerOnly(Build.WeaponId)) e.WeaponId = Build.WeaponId;   // never a bot BGLGXLQ: keep its rolled ball
                    e.SkillLevel = EffectiveSkillLevel;
                    e.SkillBranch = EffectiveSkillBranch;
                    e.Traits = new List<TraitSlot>();   // its own adaptation replaces borrowed player traits
                }
                return e;
            }
        }

        public int OfferCount => OfferCards.Count;
        public Card OfferCard(int index)
        {
            var card = OfferCards[index].ToCard();
            if (SkillOverride.HasValue && card.Kind == CardKind.SkillUp)
                return EffectiveSkillLevel < 3 ? new Card(CardKind.SkillUp, Build.WeaponId, EffectiveSkillLevel + 1) : new Card(CardKind.MaxHp);
            return card;
        }
        public bool IsPicked(int index) => (PickedMask & (1 << index)) != 0;

        void DealOffer()
        {
            OfferCards.Clear();
            if (Over) return;
            var offerSeed = IsRetry && StartPickPending ? RunTuning.Derive(Seed, 4001 + FightIndex, 0)
                : StartPickPending ? RunTuning.Derive(Seed, 4000, 0) : Seed;
            foreach (var c in CardOffer.Generate(offerSeed, FightIndex, RerollCount, Build, Weapons, Traits, EffectiveSkillLevel))
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
                case CardKind.SkillUp: return EffectiveSkillLevel < 3 && c.Level == EffectiveSkillLevel + 1;
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

        public void Pick(int index, Skills.SkillBranch branch = Skills.SkillBranch.A)
        {
            if (!CanPick(index)) throw new InvalidOperationException($"Cannot pick card {index} now");
            var card = OfferCard(index);
            if (card.Kind == CardKind.SkillUp && SkillOverride.HasValue && EffectiveSkillLevel > 1) branch = EffectiveSkillBranch;
            Coins -= NextPickCost;
            PicksTaken++;
            PickedMask |= 1 << index;
            card.ApplyTo(Build, branch);
            if (card.Kind == CardKind.SkillUp && SkillOverride.HasValue)
                SkillOverride = new Skills.SkillSpec(EffectiveSkillLevel + 1, EffectiveSkillLevel == 1 ? branch : EffectiveSkillBranch);
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
        public string ArenaId => TutorialRun && FightIndex == 0 ? ArenaRegistry.Classic : ChapterArena(Chapters.Get(Chapter), ArenaSeed);

        static string ChapterArena(ChapterDef ch, uint seed) => ch.Arenas[(int)(seed % (uint)ch.Arenas.Length)];

        /// <summary>
        /// Fresh match for the current fight (ball 0 = player, ball 1 = enemy) with spawn hooks applied, so the
        /// countdown already shows final sizes and HP. Locking the run is RunFlow's job (it also saves).
        /// </summary>
        public MatchSim CreateMatch(MatchConfig config = null, Skills.SkillSpec? forceSkill = null)
        {
            if (Over) throw new InvalidOperationException("Run is over");
            var player = Build.ToLoadout();
            player.TapEnabled = RunTuning.TapStrike;   // TAP-1: only the player taps
            var enemy = Enemy.ToLoadout();
            enemy.TapEnabled = enemy.Boss is Boss.MimicRule;   // Mimic actually uses the skill it borrowed.
            if (enemy.Boss == null && !(TutorialRun && FightIndex == 0))
            {
                // Bots use the same active skill as the player (EMBER's fire trail, ANGLER's hook...). The first tutorial fight
                // keeps the old automatic skill so the tap lesson stays calm.
                enemy.TapEnabled = true;
                enemy.AutoSkill = true;
                var stage = RunTuning.StageOf(FightIndex);
                enemy.BotFirstTick = RunTuning.BotFirstTicks[stage];
                enemy.BotRepeatTicks = RunTuning.BotRepeatTicks[stage];
            }
            forceSkill = SkillOverride ?? forceSkill;
            if (forceSkill.HasValue)
            {
                player.SkillLevel = forceSkill.Value.Level; player.SkillBranch = forceSkill.Value.Branch;
                if (enemy.Boss is Boss.MimicRule) { enemy.SkillLevel = player.SkillLevel; enemy.SkillBranch = player.SkillBranch; }
            }
            var m = new MatchSim(config ?? ArenaRegistry.Create(ArenaId), FightSeed, new[] { player, enemy }, true);
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
                if (RetryCount < MaxRetries)
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
