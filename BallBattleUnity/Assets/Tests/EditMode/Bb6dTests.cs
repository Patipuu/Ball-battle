using System.Collections.Generic;
using System.Linq;
using BallBattle.EditorTools;
using BallBattle.Sim;
using BallBattle.Sim.Boss;
using BallBattle.Sim.Run;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BallBattle.Tests
{
    /// <summary>BB-6d View side: WARDEN's spiked walls sound like normal spikes (one spike_hit per contact, no wall cue) and reuse the teeth.</summary>
    public class Bb6dTests
    {
        GameObject root;
        ArtLibrary art;
        SfxLibrary lib;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            lib = AssetDatabase.LoadAssetAtPath<SfxLibrary>(PlaceholderSfxGenerator.LibraryPath);
            root = new GameObject("Bb6dRoot");
            AudioLog.ForceOn = false;
        }

        [TearDown]
        public void TearDown()
        {
            AudioLog.ForceOn = false;
            Object.DestroyImmediate(root);
        }

        /// <summary>Player (blade, ball 0) and WARDEN (ball 1) far apart and still, on the classic arena (no spike zones).</summary>
        static MatchSim WardenSim()
        {
            var player = new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)) { MaxHp = 9999 };
            var warden = new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)) { MaxHp = 9999, Radius = RunTuning.BossRadius, Boss = new WardenRule() };
            return new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 3, new[] { player, warden });
        }

        static void Hold(MatchSim m)
        {
            m.Balls[0].Pos = TFix.V(-60, -40); m.Balls[0].Vel = FixVec2.Zero;
            m.Balls[1].Pos = TFix.V(60, 40); m.Balls[1].Vel = FixVec2.Zero;
        }

        static void StepToFirstWardenStep(MatchSim m)
        {
            m.ApplySpawnHooks();
            while (m.ActiveTick < WardenRule.StepTick(1) && m.Outcome == MatchOutcome.Ongoing) { m.Step(); Hold(m); }
        }

        [Test]
        public void OneWardenSpikeContactLogsExactlyOneSpikeHitAndNoWallCue()
        {
            Assert.That(lib.Has(AudioIds.SpikeHit), Is.True);
            AudioLog.ForceOn = true;
            var d = AudioDirector.Create(root.transform, lib, new MemorySaveStore());
            var m = WardenSim();
            StepToFirstWardenStep(m);
            Assert.That(m.BossSpikedWalls, Is.True);
            d.OnMatchStarted();
            // Into the middle of the left wall (no spike zone on classic): only WARDEN's spikes can hurt.
            var b = m.Balls[0];
            b.Pos = new FixVec2(m.Arena.Left + b.Radius + 1, Fix.Zero);
            b.Vel = TFix.V(-3, 0);
            var played = d.Played.Count;
            var logs = d.LogLines.Count;
            m.Step();
            var bounces = m.Events.Where(e => e.Type == SimEventType.WallBounce && e.A == 0).ToList();
            Assert.That(bounces.Sum(e => e.Value.Raw), Is.EqualTo(BossTuning.WardenSpikeDamage.Raw), "one spike hit");
            foreach (var e in m.Events) d.OnSimEvent(e, m);
            // Same tick, same ball, a harmless bounce: must not add a wall cue.
            d.OnSimEvent(new SimEvent { Type = SimEventType.WallBounce, Tick = m.Tick, A = 0, B = -1, Value = Fix.Zero }, m);
            var newLines = d.LogLines.Skip(logs).Where(l => l.Contains(" ball=0 ")).ToList();
            Assert.That(newLines.Count(l => l.Contains("cue=spike_hit") && l.Contains("allowed=yes")), Is.EqualTo(1), string.Join("\n", newLines));
            Assert.That(newLines.Count(l => l.Contains("cue=wall") && l.Contains("allowed=yes")), Is.Zero, "no wall cue for that ball in that tick");
            var cues = d.Played.Skip(played).Select(p => p.Id).ToList();
            Assert.That(cues.Count(c => c == AudioIds.SpikeHit), Is.EqualTo(1));
            Assert.That(cues, Does.Not.Contain(AudioIds.Wall));
            Assert.That(b.Hp, Is.EqualTo(Fix.FromInt(9999) - BossTuning.WardenSpikeDamage));
        }

        // ---------------- Task 4: QA aids ----------------

        static void Quiet(System.Action a)
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try { a(); }
            finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false; }
        }

        [Test]
        public void ForceArenaParsing()
        {
            Assert.That(DebugOptions.ParseForceArena(new[] { "-bbForceArena", "tight" }), Is.EqualTo(ArenaRegistry.Tight));
            Assert.That(DebugOptions.ParseForceArena(new[] { "x", "-BBFORCEARENA", "Spike-Walls" }), Is.EqualTo(ArenaRegistry.SpikeWalls));
            Assert.That(DebugOptions.ParseForceArena(new[] { "-bbForceArena", "low_gravity" }), Is.EqualTo(ArenaRegistry.LowGravity));
            foreach (var e in ArenaRegistry.All)
                Assert.That(DebugOptions.ParseForceArena(new[] { "-bbForceArena", e.Id }), Is.EqualTo(e.Id));
            Quiet(() => Assert.That(DebugOptions.ParseForceArena(new[] { "-bbForceArena", "volcano" }), Is.Null, "unknown: warned and ignored"));
            Quiet(() => Assert.That(DebugOptions.ParseForceArena(new[] { "-bbForceArena" }), Is.Null));
            Assert.That(DebugOptions.ParseForceArena(new[] { "-bbForceBoss", "warden" }), Is.Null, "off by default");
        }

        [Test]
        public void ForceArenaIsRunOnlyAndCombinesWithForceBoss()
        {
            // A seed whose natural fight-1 arena is not tight, so the override is visible.
            uint seed = 1;
            var probe = new ProgressData();
            while (RunState.Start(seed, RunState.StartChoices(seed, UnlockRules.Weapons(probe))[0], probe).ArenaId == ArenaRegistry.Tight) seed++;

            var store = new MemorySaveStore();
            var flow = new RunFlow(store) { ForceBossId = BossIds.Magnet, ForceArenaId = ArenaRegistry.Tight };
            flow.Open();
            flow.NewRun(seed);
            flow.ChooseWeapon(flow.StartChoices[0]);
            var natural = flow.Run.ArenaId;
            Assert.That(natural, Is.Not.EqualTo(ArenaRegistry.Tight));
            flow.PickStart(0);
            flow.Fight();
            var m = flow.CurrentMatch;
            var tight = ArenaRegistry.Create(ArenaRegistry.Tight);
            Assert.That((m.Config.ArenaWidth, m.Config.ArenaHeight), Is.EqualTo((tight.ArenaWidth, tight.ArenaHeight)), "the fight is on the forced arena");
            Assert.That(flow.FightArenaId, Is.EqualTo(ArenaRegistry.Tight));
            Assert.That(m.Boss, Is.InstanceOf<MagnetRule>(), "with the forced boss");
            Assert.That(m.Seed, Is.EqualTo(flow.Run.FightSeed), "fight seed untouched");
            Assert.That(flow.Run.ArenaId, Is.EqualTo(natural), "the run's own arena choice is untouched");
            StringAssert.DoesNotContain("tight", store.Json, "nothing about the forced arena is saved");

            var versus = new GameObject("Versus").AddComponent<ArenaView>();
            versus.transform.SetParent(root.transform);
            versus.Art = art;
            versus.AutoRestart = false;
            versus.StartMatch(BladeRule.WeaponId, FangRule.WeaponId, 3);
            Assert.That(versus.Sim.Config.ArenaWidth, Is.EqualTo(ArenaRegistry.Create(ArenaRegistry.Classic).ArenaWidth), "Versus: its own arena");
        }

        /// <summary>Attacker (blade, no spin) parked at <paramref name="pos"/> pointing <paramref name="angle"/> at SHELL KING (centre, facing left).</summary>
        static List<string> ShellHitLines(FixVec2 pos, float angle)
        {
            var cfg = new MatchConfig { Gravity = Fix.Zero, MinHorizontalSpeed = Fix.Zero, ShrinkStartTick = int.MaxValue, CapTicks = int.MaxValue };
            var blade = WeaponRegistry.Create(BladeRule.WeaponId);
            blade.SpinDegPerTick = Fix.Zero;
            var player = new BallLoadout(blade) { MaxHp = 9999 };
            var shell = new ShellKingRule();
            var boss = new BallLoadout(WeaponRegistry.Create(VolleyRule.WeaponId)) { MaxHp = 9999, Radius = RunTuning.BossRadius, Boss = shell };
            var m = new MatchSim(cfg, 1, new[] { player, boss });
            void Park(FixVec2 p) { m.Balls[0].Pos = p; m.Balls[0].Vel = FixVec2.Zero; m.Balls[1].Pos = FixVec2.Zero; m.Balls[1].Vel = FixVec2.Zero; }
            m.Balls[0].WeaponAngleDeg = TFix.X(angle);
            Park(TFix.V(-90, 0));
            while (!shell.ShellOn) { m.Step(); Park(TFix.V(-90, 0)); }
            var lines = new List<string>();
            for (var i = 0; i < 40; i++)
            {
                m.Step();
                Park(pos);
                // The player's blade hits only (a deflected boss arrow can also hit the boss, from another angle).
                foreach (var e in m.Events) { var l = ShellTrace.Line(e, m); if (l != null && e.A == 0 && e.Type == SimEventType.Hit) lines.Add(l); }
            }
            return lines;
        }

        [Test]
        public void ShellTraceLogsTheContactAngleAndWhetherTheShellBlocked()
        {
            Assert.That(BossTuning.ShellArcDeg, Is.EqualTo(120));
            var centre = ShellHitLines(TFix.V(-55, 0), 0f);   // blade pointing right at the shell's centre (facing left)
            Assert.That(centre, Is.Not.Empty);
            Assert.That(centre.All(l => l.Contains("blocked=true") && l.Contains("angle=0.0")), Is.True, string.Join("\n", centre));
            var side = ShellHitLines(TFix.V(0, 55), 270f);    // from above, pointing down: 90 degrees off the facing
            Assert.That(side, Is.Not.Empty);
            Assert.That(side.All(l => l.Contains("blocked=false") && l.Contains("angle=90.0")), Is.True, string.Join("\n", side));
        }

        [Test]
        public void WardenSpikedWallsReuseTheSpikeTeeth()
        {
            var view = root.AddComponent<ArenaView>();
            view.Art = art;
            view.AutoRestart = false;
            var m = WardenSim();
            view.ArenaId = ArenaRegistry.Classic;
            view.StartMatch(m);
            view.Advance(0f);
            Assert.That(view.Frame.TeethShown + view.Frame.ZoneStripsShown, Is.Zero, "classic: no spikes before the first step");
            StepToFirstWardenStep(m);
            view.Advance(0f);
            Assert.That(view.Frame.TeethShown + view.Frame.ZoneStripsShown + view.BossArt.StripsShown, Is.GreaterThan(0), "spikes drawn along the walls after the step");
            Assert.That(view.Frame.FullWallSpikes, Is.True);
            var first = m.ActiveTick;
            while (m.ActiveTick < first + BossTuning.SpikeWindowTicks)
            {
                Hold(m);
                m.Step();
            }
            view.Advance(0f);
            Assert.That(view.Frame.FullWallSpikes, Is.False, "window expires on ActiveTick, independent of drawing frames");
            Assert.That(view.Frame.TeethShown + view.Frame.ZoneStripsShown + view.BossArt.StripsShown, Is.Zero, "classic has no normal spikes to draw after expiry");
        }

        [TestCase((byte)8, ArenaWall.Top)]
        [TestCase((byte)1, ArenaWall.Left)]
        [TestCase((byte)2, ArenaWall.Right)]
        public void WardenPlaceholderUsesOnlyTheMovedWallMask(byte mask, ArenaWall wall)
        {
            var segments = ArenaFrameView.FullWallSegments(ArenaRect.Centered(200, 180), mask);
            Assert.That(segments.Length, Is.EqualTo(1));
            Assert.That(segments[0].Wall, Is.EqualTo(wall));
            Assert.That(ArenaFrameView.FullWallSegments(ArenaRect.Centered(200, 180), 4), Is.Empty, "never draw floor spikes for WARDEN");
            var placeholder = Object.Instantiate(art);
            placeholder.SpikeTooth = null;
            try
            {
                var frame = ArenaFrameView.Create(root.transform, placeholder);
                var config = ArenaRegistry.Create(ArenaRegistry.SpikeWalls);
                frame.SpikedWallMask = mask;
                frame.Render(config.ArenaAt(0), 0, config);
                var during = frame.ZoneStripsShown;
                var fullSpan = frame.GetComponentsInChildren<SpriteRenderer>().Where(s => s.name == "SpikeZone" && s.enabled)
                    .Max(s => wall == ArenaWall.Top ? s.transform.localScale.x : s.transform.localScale.y);
                frame.SpikedWallMask = 0;
                frame.Render(config.ArenaAt(0), 0, config);
                Assert.That(frame.ZoneStripsShown, Is.EqualTo(4), "normal corner spikes remain after WARDEN's window");
                Assert.That(during, Is.EqualTo(4), "one full moved wall replaces its normal corner segment");
                var normalSpan = frame.GetComponentsInChildren<SpriteRenderer>().Where(s => s.name == "SpikeZone" && s.enabled)
                    .Max(s => wall == ArenaWall.Top ? s.transform.localScale.x : s.transform.localScale.y);
                Assert.That(fullSpan, Is.GreaterThan(normalSpan), "expiry removes the full-wall placeholder");
            }
            finally { Object.DestroyImmediate(placeholder); }
        }
    }
}
