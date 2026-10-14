using System.Collections.Generic;
using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Boss;
using BallBattle.Sim.Run;
using BallBattle.Sim.Traits;
using NUnit.Framework;
using static BallBattle.SimTests.RunFixtures;

namespace BallBattle.SimTests
{
    /// <summary>BB-04 boss pool v1: SHELL KING, MAGNET, MIMIC, WARDEN mechanics, selection and save-independent run rules.</summary>
    public class BossTests
    {
        static readonly Fix BossR = RunTuning.BossRadius;

        /// <summary>Fixed-damage blade that counts its growth hook.</summary>
        sealed class CountingBlade : WeaponRule
        {
            public int Grown;
            public CountingBlade() { BladeLength = 24; SpinDegPerTick = Fix.Zero; }
            public override string Id => "test-counting-blade";
            public override Fix Damage(in HitContext ctx) => 5;
            public override void OnHit(in HitContext ctx) => Grown++;
            protected override ulong HashState(ulong h) => SimHash.Mix(h, Grown);
        }

        static BallLoadout BossLoadout(BossRule rule, WeaponRule weapon = null) =>
            new BallLoadout(weapon ?? new HarmlessRule()) { Radius = BossR, MaxHp = 160, Boss = rule };

        static List<SimEvent> StepCollect(MatchSim m, int ticks, System.Action<MatchSim> after = null)
        {
            var list = new List<SimEvent>();
            for (var i = 0; i < ticks; i++)
            {
                m.Step();
                list.AddRange(m.Events);
                after?.Invoke(m);
            }
            return list;
        }

        static void StepTo(MatchSim m, int activeTick, System.Action<MatchSim> after = null)
        {
            while (m.ActiveTick < activeTick) { m.Step(); after?.Invoke(m); }
        }

        // ---------------- SHELL KING ----------------

        /// <summary>Attacker (ball 0) far left while the shell turns on (so it faces left), boss (ball 1) at the centre.</summary>
        static (MatchSim m, CountingBlade blade, ShellKingRule shell) ShellFight()
        {
            var blade = new CountingBlade();
            var shell = new ShellKingRule();
            var m = Stage.Build(Stage.StillConfig(), (new BallLoadout(blade), FT.V(-90, 0), 0f), (BossLoadout(shell), FT.V(0, 0), 0f));
            var shellOn = BossTuning.ShellFirstWarnTick + BossTuning.ShellWarnTicks;
            StepTo(m, shellOn, s => Stage.Park(s, FT.V(-90, 0), FT.V(0, 0)));
            Assert.That(shell.ShellOn, Is.True);
            Assert.That(shell.Facing.X, Is.EqualTo(-Fix.One), "faces the target (left) when it turns on");
            Assert.That(shell.Facing.Y, Is.EqualTo(Fix.Zero));
            return (m, blade, shell);
        }

        [Test]
        public void ShellHitInsideTheArcDealsZeroAndDoesNotGrowTheWeapon()
        {
            var (m, blade, _) = ShellFight();
            var hp = m.Balls[1].Hp;
            // Blade points right (+x) from x = -55: its closest point to the boss is (-19, 0), straight in front of the shell.
            var events = StepCollect(m, 30, s => Stage.Park(s, FT.V(-55, 0), FT.V(0, 0)));
            var hits = events.Where(e => e.Type == SimEventType.Hit).ToList();
            Assert.That(hits, Is.Not.Empty, "the blade touched the boss");
            Assert.That(hits.All(e => e.Variant == 1 && e.Value == Fix.Zero), Is.True, "every hit stopped by the shell");
            Assert.That(m.Balls[1].Hp, Is.EqualTo(hp), "0 damage");
            Assert.That(blade.Grown, Is.Zero, "no weapon growth");
            Assert.That(m.Balls[0].HitCount, Is.Zero);
            Assert.That(events.Any(e => e.Type == SimEventType.StatChanged && e.A == 0), Is.False);
        }

        [Test]
        public void ShellHitOutsideTheArcDealsNormalDamage()
        {
            var (m, blade, _) = ShellFight();
            var hp = m.Balls[1].Hp;
            // From behind: blade at x = +55 pointing left; contact (19, 0) is opposite the shell.
            m.Balls[0].WeaponAngleDeg = 180;
            var events = StepCollect(m, 30, s => Stage.Park(s, FT.V(55, 0), FT.V(0, 0)));
            var hits = events.Where(e => e.Type == SimEventType.Hit).ToList();
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits.All(e => e.Variant == 0 && e.Value == 5), Is.True);
            Assert.That(m.Balls[1].Hp, Is.EqualTo(hp - 5 * hits.Count));
            Assert.That(blade.Grown, Is.EqualTo(hits.Count));
        }

        [Test]
        public void ShellOffDealsNormalDamageFromTheFront()
        {
            var (m, blade, shell) = ShellFight();
            var off = BossTuning.ShellFirstWarnTick + BossTuning.ShellWarnTicks + BossTuning.ShellOnTicks;
            StepTo(m, off, s => Stage.Park(s, FT.V(-90, 0), FT.V(0, 0)));
            Assert.That(shell.ShellOn, Is.False);
            var hp = m.Balls[1].Hp;
            var events = StepCollect(m, 30, s => Stage.Park(s, FT.V(-55, 0), FT.V(0, 0)));
            var hits = events.Where(e => e.Type == SimEventType.Hit).ToList();
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits.All(e => e.Variant == 0), Is.True);
            Assert.That(m.Balls[1].Hp, Is.EqualTo(hp - 5 * hits.Count));
            Assert.That(blade.Grown, Is.EqualTo(hits.Count));
        }

        [Test]
        public void PoisonTicksThroughTheShell()
        {
            var (m, _, shell) = ShellFight();
            m.ApplyPoison(m.Balls[1], m.Balls[0], 3, 3 * MatchConfig.TicksPerSecond);
            var hp = m.Balls[1].Hp;
            var events = StepCollect(m, 2 * MatchConfig.TicksPerSecond, s => Stage.Park(s, FT.V(-90, 0), FT.V(0, 0)));
            Assert.That(shell.ShellOn, Is.True, "still on");
            var poison = events.Where(e => e.Type == SimEventType.StatusTick && e.A == 1).Sum(e => e.Value.Raw);
            Assert.That(poison, Is.GreaterThan(0));
            Assert.That(hp - m.Balls[1].Hp, Is.EqualTo(Fix.FromRaw(poison)));
        }

        [Test]
        public void ShellCycleEmitsWarnOnOffOnActiveTicks()
        {
            var shell = new ShellKingRule();
            var m = Stage.Build(Stage.StillConfig(), (new BallLoadout(new HarmlessRule()), FT.V(-90, 0), 0f), (BossLoadout(shell), FT.V(0, 0), 0f));
            var seen = new List<(BossEvent kind, int active)>();
            for (var i = 0; i < 800; i++)
            {
                if (i == 100) m.ExtendHitstop(20);   // hit-stop never advances boss timers
                m.Step();
                foreach (var e in m.Events.Where(e => e.Type == SimEventType.Boss)) seen.Add(((BossEvent)e.Variant, m.ActiveTick));
            }
            Assert.That(seen.Take(6), Is.EqualTo(new[]
            {
                (BossEvent.ShellWarn, 60), (BossEvent.ShellOn, 90), (BossEvent.ShellOff, 270),
                (BossEvent.ShellWarn, 420), (BossEvent.ShellOn, 450), (BossEvent.ShellOff, 630),
            }));
        }

        // ---------------- MAGNET ----------------

        static (MatchSim m, MagnetRule magnet) MagnetFight(params TraitRule[] playerTraits)
        {
            var cfg = Stage.StillConfig();
            cfg.ArenaWidth = 2400;   // room to accelerate for the whole pull without touching the boss
            var magnet = new MagnetRule();
            var m = Stage.Build(cfg, (Stage.L(new HarmlessRule(), playerTraits), FT.V(-1000, 0), 0f), (BossLoadout(magnet), FT.V(0, 0), 0f));
            m.ApplySpawnHooks();
            var pull = BossTuning.MagnetFirstWarnTick + BossTuning.MagnetWarnTicks;
            StepTo(m, pull - 1, s => Stage.Park(s, FT.V(-1000, 0), FT.V(0, 0)));
            Assert.That(magnet.Pulling, Is.False);
            return (m, magnet);
        }

        [Test]
        public void MagnetPullsTowardTheBossAndIsCappedAtMaxSpeed()
        {
            var (m, magnet) = MagnetFight();
            var step = m.Config.MaxSpeed * BossTuning.MagnetPullPerTick;   // MagnetPullPerTick of max speed per tick
            var cap = m.Config.MaxSpeed;
            var speeds = new List<Fix>();
            for (var i = 0; i < BossTuning.MagnetPullTicks; i++)
            {
                m.Step();
                m.Balls[1].Pos = FixVec2.Zero;
                m.Balls[1].Vel = FixVec2.Zero;
                speeds.Add(m.Balls[0].Vel.X);
            }
            Assert.That(speeds[0], Is.EqualTo(step), "first pull tick: +MagnetPullPerTick of max speed toward the boss");
            for (var i = 1; i < speeds.Count; i++)
                Assert.That(speeds[i], Is.EqualTo(FixMath.Min(cap, speeds[i - 1] + step)), $"tick {i}: +step, capped");
            Assert.That(speeds.All(s => s <= cap), Is.True, "never above the normal max speed");
            m.Step();
            Assert.That(magnet.Pulling, Is.False, "pull ends after MagnetPullTicks");
            var after = m.Balls[0].Vel.X;
            m.Step();
            Assert.That(m.Balls[0].Vel.X, Is.EqualTo(after), "no pull while resting");
        }

        [Test]
        public void MagnetPullNeverExceedsMaxSpeedEvenWhenStrong()
        {
            var saved = BossTuning.MagnetPullPerTick;
            try
            {
                BossTuning.MagnetPullPerTick = Fix.Ratio(1, 4);   // 2.5 px/tick per tick: hits the cap within the pull
                var (m, _) = MagnetFight();
                var max = Fix.Zero;
                for (var i = 0; i < BossTuning.MagnetPullTicks; i++)
                {
                    m.Step();
                    m.Balls[1].Pos = FixVec2.Zero;
                    m.Balls[1].Vel = FixVec2.Zero;
                    Assert.That(m.Balls[0].Vel.Length, Is.LessThanOrEqualTo(m.SpeedCap(m.Balls[0])));
                    if (m.Balls[0].Vel.X > max) max = m.Balls[0].Vel.X;
                }
                Assert.That(max, Is.EqualTo(m.Config.MaxSpeed), "capped at exactly the normal max speed");
            }
            finally { BossTuning.MagnetPullPerTick = saved; }
        }

        [Test]
        public void MagnetPullIsHalvedWithHeavy()
        {
            var (m, _) = MagnetFight(new HeavyTrait(1));
            m.Step();
            Assert.That(m.Balls[0].Vel.X, Is.EqualTo(m.Config.MaxSpeed * BossTuning.MagnetPullPerTick * BossTuning.MagnetHeavyScale));
            for (var i = 0; i < BossTuning.MagnetPullTicks; i++)
            {
                m.Step();
                Assert.That(m.Balls[0].Vel.Length, Is.LessThanOrEqualTo(m.SpeedCap(m.Balls[0])));
            }
        }

        // ---------------- MIMIC ----------------

        [Test]
        public void MimicCopiesThePlayersLoadoutAtFightStartIncludingCardsPickedAfterThePreview()
        {
            var run = NewRun();
            run.BossIds[1] = BossIds.Mimic;
            run.FightIndex = RunTuning.BossFights[1];
            var preview = run.Enemy;
            Assert.That(preview.BossId, Is.EqualTo(BossIds.Mimic));
            // Cards taken after the preview: a new trait, a level-2 upgrade and a weapon swap.
            run.Build.Traits.Clear();
            run.Build.Traits.Add(new TraitSlot(VampireTrait.TraitId, 2));
            run.Build.Traits.Add(new TraitSlot(ThornsTrait.TraitId, 1));
            run.Build.WeaponId = "pike";
            var m = run.CreateMatch();
            var foe = m.Balls[1];
            Assert.That(foe.Weapon.Id, Is.EqualTo("pike"));
            Assert.That(foe.Traits.Select(t => (t.Id, t.Level)), Is.EqualTo(new[] { (VampireTrait.TraitId, 2), (ThornsTrait.TraitId, 1) }));
            Assert.That(foe.MaxHp, Is.EqualTo(BossTuning.MimicHp));
            Assert.That(foe.Radius, Is.EqualTo(BossR));
            Assert.That(m.Boss, Is.InstanceOf<MimicRule>());
            m.Step();
            Assert.That(m.Events.Any(e => e.Type == SimEventType.Boss && e.Variant == (byte)BossEvent.MimicCopied), Is.True);
        }

        // ---------------- WARDEN ----------------

        static (MatchSim m, WardenRule warden) WardenFight(ArenaLayout layout = null, FixVec2? p0 = null, FixVec2? p1 = null)
        {
            var cfg = Stage.StillConfig();
            if (layout != null) cfg.Layout = layout;
            var warden = new WardenRule();
            var m = Stage.Build(cfg, (new BallLoadout(new HarmlessRule()), p0 ?? FT.V(-60, 0), 0f), (BossLoadout(warden), p1 ?? FT.V(40, 0), 0f));
            return (m, warden);
        }

        /// <summary>The arena rect after <paramref name="steps"/> Warden steps (ArenaRect.Centered halves each size toward zero).</summary>
        static ArenaRect Rect(MatchSim m, int steps) =>
            ArenaRect.Centered(WardenRule.SizeAfter(m.Config.ArenaWidth, steps), WardenRule.SizeAfter(m.Config.ArenaHeight, steps));

        [Test]
        public void WardenBoundsStepDownEveryStepToTheMinimum()
        {
            var (m, warden) = WardenFight();
            var widths = new Dictionary<int, Fix>();
            var heights = new Dictionary<int, Fix>();
            var events = new List<(BossEvent kind, int active)>();
            var last = WardenRule.StepTick(BossTuning.WardenMaxSteps);
            for (var i = 0; m.ActiveTick < last + 500; i++)
            {
                if (i == 50) m.ExtendHitstop(30);   // hit-stop never advances the Warden
                m.Step();
                Stage.Park(m, FT.V(-20, 0), FT.V(30, 0));
                widths[m.ActiveTick] = m.Arena.Width;
                heights[m.ActiveTick] = m.Arena.Height;
                foreach (var e in m.Events.Where(e => e.Type == SimEventType.Boss)) events.Add(((BossEvent)e.Variant, m.ActiveTick));
            }
            var w0 = m.Config.ArenaWidth;
            Assert.That(widths[WardenRule.StepTick(1) - 1], Is.EqualTo(w0), "full size until the first step");
            var expected = new List<(BossEvent kind, int active)>();
            for (var k = 1; k <= BossTuning.WardenMaxSteps; k++)
            {
                var t = WardenRule.StepTick(k);
                Assert.That(widths[t], Is.EqualTo(Rect(m, k).Width), $"step {k}: {BossTuning.WardenStepPct}% of the ORIGINAL width");
                Assert.That(heights[t], Is.EqualTo(Rect(m, k).Height));
                Assert.That(widths[t - 1], Is.EqualTo(Rect(m, k - 1).Width), "nothing between steps");
                expected.Add((BossEvent.WardenWarn, t - BossTuning.WardenWarnTicks));
                expected.Add((BossEvent.WardenStep, t));
            }
            Assert.That(widths[last + 499], Is.EqualTo(widths[last]), "stops at the minimum");
            Assert.That(widths[last], Is.GreaterThanOrEqualTo(FixMath.MulDiv(w0, BossTuning.WardenMinPct, 100) - Fix.FromRaw(1)));
            Assert.That(warden.Steps, Is.EqualTo(BossTuning.WardenMaxSteps));
            Assert.That(events, Is.EqualTo(expected), "a warning WardenWarnTicks before each step, nothing after the last");
        }

        [Test]
        public void WardenCombinesWithTheNormalShrinkUsingTheSmallerBound()
        {
            var t = WardenRule.StepTick(2) + 10;
            var (m, _) = WardenFight();
            m.Config.ShrinkStartTick = WardenRule.StepTick(1);
            m.Config.ShrinkDurationTicks = 60;
            StepTo(m, t, s => Stage.Park(s, FT.V(-20, 0), FT.V(30, 0)));
            Assert.That(m.Config.ArenaAt(t).Width, Is.LessThan(WardenRule.SizeAfter(m.Config.ArenaWidth, 2)));
            Assert.That(m.Arena.Width, Is.EqualTo(m.Config.ArenaAt(t).Width), "normal shrink smaller: it wins");
            var (w, _) = WardenFight();
            StepTo(w, t, s => Stage.Park(s, FT.V(-20, 0), FT.V(30, 0)));
            Assert.That(w.Arena.Width, Is.EqualTo(Rect(w, 2).Width), "Warden smaller before the normal shrink starts");
        }

        [Test]
        public void BallsCaughtOutsideArePushedInWithoutDamageAndDoNotOverlap()
        {
            // Player at the left wall, boss at the top wall: the 480 step pushes them toward each other (32 px apart after the push).
            var (m, _) = WardenFight(null, FT.V(-99, 60), FT.V(-62, 91));
            var hp0 = m.Balls[0].Hp;
            var hp1 = m.Balls[1].Hp;
            Assert.That((m.Balls[1].Pos - m.Balls[0].Pos).Length, Is.GreaterThan(m.Balls[0].Radius + m.Balls[1].Radius), "no overlap before");
            var events = StepCollect(m, WardenRule.StepTick(1));
            Assert.That(m.ActiveTick, Is.EqualTo(WardenRule.StepTick(1)));
            var a = m.Arena;
            foreach (var b in m.Balls)
            {
                Assert.That(b.Pos.X - b.Radius, Is.GreaterThanOrEqualTo(a.Left));
                Assert.That(b.Pos.Y + b.Radius, Is.LessThanOrEqualTo(a.Top));
            }
            Assert.That((m.Balls[1].Pos - m.Balls[0].Pos).Length, Is.GreaterThanOrEqualTo(m.Balls[0].Radius + m.Balls[1].Radius - Fix.Ratio(1, 16)),
                "not overlapping after the push (within 1/16 px)");
            Assert.That(m.Balls[0].Hp, Is.EqualTo(hp0));
            Assert.That(m.Balls[1].Hp, Is.EqualTo(hp1));
            Assert.That(events.Where(e => e.Tick == m.Tick).Any(e => e.Type == SimEventType.WallBounce), Is.False, "a push is not a bounce");
        }

        [Test]
        public void SpikeZonesFollowTheWardenWallsAtTheirFraction()
        {
            var layout = new ArenaLayout(null, ArenaTuning.SpikeWallDamage, SpikeCorner.TopLeft);
            var (m, _) = WardenFight(layout, FT.V(-20, -60), FT.V(40, -60));
            StepTo(m, WardenRule.StepTick(1));
            var a = m.Arena;
            Assert.That(a.Width, Is.EqualTo(Rect(m, 1).Width));
            var zone = SpikeZones.ZoneLength(a.Width);
            Assert.That(zone, Is.EqualTo(FixMath.MulDiv(a.Width, ArenaTuning.SpikeZoneNum, ArenaTuning.SpikeZoneDen)), "3/20 of the CURRENT wall");
            // Ball moving up just inside the new top-left zone: the bounce hurts. One just outside it: no damage.
            var r = m.Balls[0].Radius;
            m.Balls[0].Pos = new FixVec2(a.Left + zone - 2, a.Top - r - 1);
            m.Balls[0].Vel = FT.V(0, 3);
            m.Balls[1].Pos = new FixVec2(a.Left + zone + 60, a.Top - m.Balls[1].Radius - 1);
            m.Balls[1].Vel = FT.V(0, 3);
            var bounces = StepCollect(m, 2).Where(e => e.Type == SimEventType.WallBounce).ToList();
            Assert.That(bounces.Single(e => e.A == 0).Value, Is.EqualTo(ArenaTuning.SpikeWallDamage));
            Assert.That(bounces.Single(e => e.A == 1).Value, Is.EqualTo(Fix.Zero));
        }

        [Test]
        public void ObstaclesWhoseCentreIsOutsideTheNewWallsAreDisabled()
        {
            var layout = new ArenaLayout(new[] { Obstacle.Circle(FT.V(100, 100), 5), Obstacle.Circle(FT.V(0, -50), 8, boost: Fix.Ratio(1, 10)) });
            var (m, _) = WardenFight(layout, FT.V(-60, 30), FT.V(40, 30));
            StepTo(m, WardenRule.StepTick(1), s => Stage.Park(s, FT.V(-60, 30), FT.V(40, 30)));
            Assert.That(m.ObstacleActive(0), Is.True, "(100,100) still inside after step 1");
            StepTo(m, WardenRule.StepTick(2), s => Stage.Park(s, FT.V(-60, 30), FT.V(40, 30)));
            Assert.That(WardenRule.SizeAfter(m.Config.ArenaWidth, 2) / 2, Is.LessThan(Fix.FromInt(100)), "fixture: step 2 puts (100,100) outside");
            Assert.That(m.ObstacleActive(0), Is.False, "(100,100) outside after step 2");
            Assert.That(m.ObstacleActive(1), Is.True);
            StepTo(m, WardenRule.StepTick(BossTuning.WardenMaxSteps) + 10, s => Stage.Park(s, FT.V(-40, 30), FT.V(30, 30)));
            Assert.That(m.ObstacleActive(0), Is.False, "never comes back");
            Assert.That(m.ObstacleActive(1), Is.True, "(0,-50) stays inside the smallest Warden walls");
        }

        // ---------------- selection ----------------

        static ProgressData Veteran(params string[] last)
        {
            var p = new ProgressData { RunsPlayed = 1 };
            for (var i = 0; i < last.Length; i++) p.SetLastBoss(i, last[i]);
            return p;
        }

        [Test]
        public void FirstRunOnASaveGetsShellKingAndMimic()
        {
            for (uint seed = 1; seed <= 50; seed++)
                Assert.That(BossSelection.Choose(seed, new ProgressData()), Is.EqualTo(new[] { BossIds.ShellKing, BossIds.Mimic }));
            var run = RunState.Start(9, RunState.StartChoices(9, UnlockRules.Weapons(Fresh))[0], new ProgressData());
            Assert.That(run.BossFor(RunTuning.BossFights[0]), Is.EqualTo(BossIds.ShellKing));
            Assert.That(run.BossFor(RunTuning.BossFights[1]), Is.EqualTo(BossIds.Mimic));
        }

        [Test]
        public void SelectionIsDeterministicPerSeedAndUsesBothBosses()
        {
            var p = Veteran();
            var seen = new HashSet<string>();
            for (uint seed = 1; seed <= 200; seed++)
            {
                var a = BossSelection.Choose(seed, p);
                Assert.That(BossSelection.Choose(seed, p), Is.EqualTo(a));
                Assert.That(BossIds.Slots[0], Does.Contain(a[0]));
                Assert.That(BossIds.Slots[1], Does.Contain(a[1]));
                seen.UnionWith(a);
            }
            Assert.That(seen, Is.EquivalentTo(BossIds.All), "every boss appears for some seed");
        }

        [Test]
        public void NoRepeatOfThePreviousRunsBossInTheSameSlot()
        {
            for (uint seed = 1; seed <= 100; seed++)
            {
                Assert.That(BossSelection.Choose(seed, Veteran(BossIds.ShellKing, BossIds.Mimic)), Is.EqualTo(new[] { BossIds.Magnet, BossIds.Warden }));
                Assert.That(BossSelection.Choose(seed, Veteran(BossIds.Magnet, BossIds.Mimic)), Is.EqualTo(new[] { BossIds.ShellKing, BossIds.Warden }));
            }
            // Remember stores the run's bosses, so back-to-back runs alternate.
            var progress = new ProgressData();
            var last = new List<string>();
            for (uint seed = 1; seed <= 6; seed++)
            {
                var run = RunState.Start(seed, RunState.StartChoices(seed, UnlockRules.Weapons(progress))[0], progress);
                if (last.Count > 0)
                    for (var s = 0; s < 2; s++) Assert.That(run.BossIds[s], Is.Not.EqualTo(last[s]), $"seed {seed} slot {s}");
                BossSelection.Remember(progress, run);
                last = new List<string>(run.BossIds);
            }
        }

        [Test]
        public void RetryFightsTheSameBossAndThePreviewMatchesTheFight()
        {
            var run = RunState.Start(31, RunState.StartChoices(31, UnlockRules.Weapons(Fresh))[0], Veteran(BossIds.ShellKing, BossIds.Mimic));
            run.Pick(0);
            for (var i = 0; i < RunTuning.BossFights[0]; i++) run.ApplyResult(Forced(run, true));
            Assert.That(run.CurrentBossId, Is.EqualTo(BossIds.Magnet));
            var preview = run.Enemy;
            var m = run.CreateMatch();
            Assert.That(m.Balls[1].Weapon.Id, Is.EqualTo(preview.WeaponId), "preview = fight");
            Assert.That(m.Boss, Is.InstanceOf<MagnetRule>());
            run.ApplyResult(Forced(run, false));
            Assert.That(run.IsRetry, Is.True);
            Assert.That(run.CurrentBossId, Is.EqualTo(BossIds.Magnet), "retry keeps the boss");
            Assert.That(run.CreateMatch().Boss, Is.InstanceOf<MagnetRule>());
            Assert.That(run.History.Last().BossId, Is.EqualTo(BossIds.Magnet));
        }

        [Test]
        public void BossCodexRecordsMetAndDefeatedAndRewardsStayOnTheSlot()
        {
            var progress = Veteran(BossIds.ShellKing, BossIds.Mimic);
            var run = RunState.Start(31, RunState.StartChoices(31, UnlockRules.Weapons(progress))[0], progress);
            run.Pick(0);
            for (var i = 0; i < RunTuning.BossFights[0]; i++) { run.ApplyResult(Forced(run, true)); UnlockRules.Record(progress, run); }
            run.ApplyResult(Forced(run, false));
            UnlockRules.Record(progress, run);
            Assert.That(progress.BossMet(BossIds.Magnet), Is.True);
            Assert.That(progress.BossDefeated(BossIds.Magnet), Is.False);
            Assert.That(progress.Has(UnlockRules.Boss1), Is.False);
            run.ApplyResult(Forced(run, true));
            UnlockRules.Record(progress, run);
            Assert.That(progress.BossDefeated(BossIds.Magnet), Is.True);
            Assert.That(progress.Has(UnlockRules.Boss1), Is.True, "beating MAGNET in slot 1 earns boss1");
            Assert.That(progress.BossMet(BossIds.ShellKing), Is.False);
        }

        [Test]
        public void ForcedBossTakesItsSlotAndTheNextFightButNeverTheHistory()
        {
            var progress = Veteran(BossIds.Magnet, BossIds.Mimic);
            var run = RunState.Start(5, RunState.StartChoices(5, UnlockRules.Weapons(progress))[0], progress);
            BossSelection.Remember(progress, run);
            var natural = new List<string>(run.BossIds);
            Assert.That(run.ApplyForcedBoss(BossIds.Warden), Is.True);
            Assert.That(run.CurrentBossId, Is.EqualTo(BossIds.Warden), "fight 1 is not a boss fight: it becomes one");
            Assert.That(run.IsBossFight, Is.True);
            Assert.That(run.BossFor(RunTuning.BossFights[1]), Is.EqualTo(BossIds.Warden), "its own slot");
            Assert.That(run.BossFor(RunTuning.BossFights[0]), Is.EqualTo(natural[0]), "the other slot keeps its boss");
            Assert.That(run.BossIds, Is.EqualTo(natural), "BossIds never hold the forced boss");
            Assert.That(progress.LastBoss, Is.EqualTo(natural), "history = natural bosses");
            Assert.That(run.CreateMatch().Boss, Is.InstanceOf<WardenRule>());
            Assert.That(run.ApplyForcedBoss("nope"), Is.False);
        }

        [Test]
        public void BossesTakeTheirHpTraitsAndRadius()
        {
            foreach (var id in BossIds.All)
            {
                var slot = BossIds.SlotOf(id);
                var e = EnemyGenerator.Generate(9, RunTuning.BossFights[slot], id);
                Assert.That(e.BossId, Is.EqualTo(id));
                Assert.That(e.Radius, Is.EqualTo(RunTuning.BossRadius));
                Assert.That(e.MaxHp, Is.EqualTo(EnemyGenerator.BossHp(id)));
                Assert.That(e.Traits.Count, Is.EqualTo(EnemyGenerator.BossTraitCount(id)));
            }
            Assert.That(EnemyGenerator.BossHp(BossIds.ShellKing), Is.EqualTo(BossTuning.ShellKingHp));
            Assert.That(EnemyGenerator.BossTraitCount(BossIds.Warden), Is.EqualTo(BossTuning.WardenTraits));
            Assert.That(EnemyGenerator.Generate(9, 0).BossId, Is.Null, "normal fights have no boss");
        }
    }
}
