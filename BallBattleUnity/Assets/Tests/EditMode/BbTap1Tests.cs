using System;
using System.IO;
using System.Linq;
using BallBattle.EditorTools;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using BallBattle.Sim.Tap;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BallBattle.Tests
{
    /// <summary>TAP-1..4: tap to strike. Sim rules (determinism, cooldown, PERFECT, miss penalty) and the Run fight view.</summary>
    public class BbTap1Tests
    {
        const float Frame = 1f / 60f;
        static readonly string[] Weapons = { "blade", "fang", "pike", "brawler", "volley", "venom", "aegis", "rig" };
        GameObject root;
        ArtLibrary art;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            root = new GameObject("BbTap1Root");
        }

        [TearDown]
        public void TearDown()
        {
            PixelInput.SetTestClock(null);
            PixelInput.ResetCapture();
            Object.DestroyImmediate(root);
        }

        static MatchSim Match(string weapon, bool tap, uint seed = 3)
        {
            var a = new BallLoadout(WeaponRegistry.Create(weapon)) { TapEnabled = tap };
            var b = new BallLoadout(WeaponRegistry.Create("fang"));
            return new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), seed, new[] { a, b });
        }

        // ---------------- Sim ----------------

        [Test]
        public void AnUntouchedTapStateChangesNothing([ValueSource(nameof(Weapons))] string weapon)
        {
            var with = Match(weapon, true);
            var without = Match(weapon, false);
            for (var t = 0; t < 3600 && with.Outcome == MatchOutcome.Ongoing; t++)
            {
                with.Step();
                without.Step();
                if (t % 50 == 0) Assert.That(with.ComputeHash(), Is.EqualTo(without.ComputeHash()), $"{weapon} tick {t}");
            }
            Assert.That(with.ComputeHash(), Is.EqualTo(without.ComputeHash()));
            Assert.That(with.WinnerIndex, Is.EqualTo(without.WinnerIndex));
        }

        [Test]
        public void ATapStrikesOnceThenTheCooldownRuns()
        {
            var m = Match("blade", true);
            for (var i = 0; i < 30; i++) m.Step();
            var tap = m.Balls[0].Tap;
            Assert.That(tap.Ready, Is.True);
            Assert.That(m.Tap(0), Is.True);
            m.Step();
            Assert.That(m.Events.Count(e => e.Type == SimEventType.TapStrike && e.A == 0), Is.EqualTo(1));
            Assert.That(tap.Taps, Is.EqualTo(1));
            Assert.That(tap.Ready, Is.False);
            m.Tap(0);
            m.Step();
            Assert.That(tap.Taps, Is.EqualTo(1), "a tap during the cooldown does nothing");
            var guard = 0;
            while (!tap.Ready && guard++ < 1200) m.Step();
            Assert.That(tap.Ready, Is.True, "ready again after the cooldown");
            Assert.That(m.ActiveTick - tap.StruckAt, Is.InRange(TapTuning.CooldownTicks, TapTuning.CooldownTicks + TapTuning.MissPenaltyTicks),
                "2 s cooldown, +1 s after a miss (counted in active ticks)");
            Assert.That(m.Balls[1].Tap, Is.Null, "the foe never taps");
            Assert.That(m.Tap(1), Is.False);
        }

        [Test]
        public void TappingAsSoonAsReadyGivesPerfectsAndMisses()
        {
            var m = Match("blade", true, 5);
            int perfectEvents = 0, missEvents = 0;
            for (var t = 0; t < 3600 && m.Outcome == MatchOutcome.Ongoing; t++)
            {
                if (m.Balls[0].Tap.Ready) m.Tap(0);
                var readyAtBefore = m.Balls[0].Tap.ReadyAt;
                m.Step();
                foreach (var e in m.Events.Where(e => e.A == 0))
                {
                    if (e.Type == SimEventType.TapPerfect) { perfectEvents++; Assert.That(e.Value.ToFloat(), Is.GreaterThan(0f), "PERFECT adds damage"); }
                    if (e.Type == SimEventType.TapMiss)
                    {
                        missEvents++;
                        Assert.That(m.Balls[0].Tap.LastMissed, Is.True);
                        Assert.That(m.Balls[0].Tap.ReadyAt, Is.EqualTo(readyAtBefore + TapTuning.MissPenaltyTicks), "a miss stretches the cooldown");
                    }
                }
            }
            var tap = m.Balls[0].Tap;
            Assert.That(perfectEvents, Is.EqualTo(tap.Perfects).And.GreaterThan(0));
            Assert.That(missEvents, Is.EqualTo(tap.Misses).And.GreaterThan(0));
            Assert.That(tap.Perfects + tap.Misses, Is.InRange(tap.Taps - 1, tap.Taps), "every strike is judged (the last may still be open)");
        }

        [Test]
        public void EveryBallCanStrike([ValueSource(nameof(Weapons))] string weapon)
        {
            var m = Match(weapon, true, 9);
            for (var t = 0; t < 3600 && m.Outcome == MatchOutcome.Ongoing && m.Balls[0].Tap.Taps < 3; t++)
            {
                if (m.Balls[0].Tap.Ready) m.Tap(0);
                m.Step();
            }
            Assert.That(m.Balls[0].Tap.Taps, Is.GreaterThanOrEqualTo(1), weapon);
        }

        [Test]
        public void SameTapsSameFight()
        {
            ulong Play()
            {
                var m = Match("rig", true, 11);
                for (var t = 0; t < 2400 && m.Outcome == MatchOutcome.Ongoing; t++) { if (t % 40 == 0) m.Tap(0); m.Step(); }
                return m.ComputeHash();
            }
            Assert.That(Play(), Is.EqualTo(Play()));
        }

        // ---------------- View ----------------

        (ArenaView arena, RunController rc) Rig()
        {
            var arena = root.AddComponent<ArenaView>();
            arena.Art = art;
            arena.AutoRestart = false;
            var rc = RunController.Create(root.transform, arena, new MemorySaveStore());
            rc.Open();
            return (arena, rc);
        }

        static void Step(ArenaView arena, RunController rc) { rc.Tick(Frame); arena.Advance(Frame); }

        static void StartFight(ArenaView arena, RunController rc, uint seed = 7)
        {
            var flow = rc.Flow;
            flow.NewRun(seed);
            Step(arena, rc);
            flow.ChooseWeapon(RunState.StartChoices(seed, UnlockRules.Weapons(new ProgressData()))[0]);
            Step(arena, rc);
            flow.PickStart(0);
            Step(arena, rc);
            flow.Fight();
            var guard = 0;
            while (flow.Current != RunFlow.State.Playing && guard++ < 600) Step(arena, rc);
            Assert.That(flow.Current, Is.EqualTo(RunFlow.State.Playing));
            Step(arena, rc);
        }

        [Test]
        public void OnlyTheRunPlayerGetsTapAndTheTargetIsLiveOnlyInTheFight()
        {
            var (arena, rc) = Rig();
            Step(arena, rc);
            Assert.That(rc.Tap.Active, Is.False, "title screen");
            StartFight(arena, rc);
            Assert.That(arena.Sim.Balls[0].Tap, Is.Not.Null);
            Assert.That(arena.Sim.Balls[1].Tap, Is.Null);
            Assert.That(rc.Tap.Active, Is.True);
            Assert.That(rc.Tap.Button.gameObject.activeSelf, Is.True);
            Assert.That(rc.Tap.Prompt.gameObject.activeSelf, Is.True, "TAP TO STRIKE! before the first tap");
            Assert.That(rc.Tap.Ring.enabled, Is.True);
            Assert.That(TapView.TapArea.Overlaps(new Rect(-22, -238, 44, 24)), Is.False, "x2 keeps its own tap");
        }

        [Test]
        public void APressOnTheArenaStrikes()
        {
            var (arena, rc) = Rig();
            StartFight(arena, rc);
            var tap = arena.Sim.Balls[0].Tap;
            PixelInput.SetTestClock(100f);
            PixelInput.Inject(true, new Vector2(10, 20));   // press alone: a strike must not wait for the release
            PixelInput.Dispatch(() => new[] { rc.Tap.Button });
            Assert.That(rc.Tap.TapsSent, Is.EqualTo(1));
            var guard = 0;
            while (tap.Taps == 0 && guard++ < 10) Step(arena, rc);
            Assert.That(tap.Taps, Is.EqualTo(1));
            Step(arena, rc);
            Assert.That(rc.Tap.Prompt.gameObject.activeSelf, Is.False, "prompt gone after the first tap");
            PixelInput.Inject(false, new Vector2(10, 20));
            PixelInput.Dispatch(() => new[] { rc.Tap.Button });
        }

        [Test]
        public void PerfectShowsTheWordAndAHitStop()
        {
            var (arena, rc) = Rig();
            StartFight(arena, rc);
            var guard = 0;
            var sawHitStop = false;
            while (rc.Tap.PerfectsShown == 0 && rc.Flow.Current == RunFlow.State.Playing && guard++ < 60 * 120)
            {
                if (arena.Sim.Balls[0].Tap.Ready) rc.Tap.Request();
                Step(arena, rc);
                sawHitStop |= arena.InHitStop;
            }
            Assert.That(rc.Tap.PerfectsShown, Is.GreaterThan(0), "spamming the strike lands a PERFECT within 2 minutes");
            Assert.That(sawHitStop, Is.True);
            Assert.That(rc.Tap.Popups.Showing().Any(p => p.value == TapView.PerfectWord && p.scale == 2), Is.True);
        }

        [Test]
        public void TheCooldownArcFillsClockwiseFromTheTop()
        {
            Texture2D T(int k) => TapView.Arc(18f, k).texture;
            int Ink(Texture2D t, Func<int, int, bool> where)
            {
                var n = 0;
                for (var y = 0; y < t.height; y++) for (var x = 0; x < t.width; x++) if (t.GetPixel(x, y).a > 0f && where(x, y)) n++;
                return n;
            }
            var empty = T(0);
            Assert.That(Ink(empty, (x, y) => true), Is.Zero);
            var quarter = T(TapView.RingSegments / 4);
            var c = quarter.width / 2;
            Assert.That(Ink(quarter, (x, y) => x >= c && y >= c), Is.GreaterThan(0), "top right first");
            Assert.That(Ink(quarter, (x, y) => !(x >= c && y >= c)), Is.Zero);
            var full = T(TapView.RingSegments);
            Assert.That(Ink(full, (x, y) => x < c && y < c), Is.EqualTo(Ink(full, (x, y) => x >= c && y >= c)), "full ring is symmetric");
        }

        [Test]
        public void CaptureTapScreens()
        {
            var dir = Environment.GetEnvironmentVariable("BB_TAP1_OUT");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("BB_TAP1_OUT not set");
            Directory.CreateDirectory(dir);
            var (arena, rc) = Rig();
            var camGo = new GameObject("Cam");
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true; cam.orthographicSize = 240f; cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Palette.Background; camGo.transform.position = new Vector3(0, 0, -10);
                void Shoot(string name)
                {
                    foreach (var zoom in new[] { 1, 3 })
                    {
                        var rt = new RenderTexture(270 * zoom, 480 * zoom, 24) { filterMode = FilterMode.Point };
                        cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                        File.WriteAllBytes(Path.Combine(dir, $"{name}-{zoom}x.png"), tex.EncodeToPNG());
                        RenderTexture.active = null; cam.targetTexture = null;
                        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
                    }
                }
                StartFight(arena, rc);
                Shoot("1-ready-prompt");
                rc.Tap.Request();
                for (var i = 0; i < 40; i++) Step(arena, rc);
                Shoot("2-cooldown");
                var guard = 0;
                while (rc.Tap.PerfectsShown == 0 && guard++ < 60 * 120)
                {
                    if (arena.Sim.Balls[0].Tap.Ready) rc.Tap.Request();
                    Step(arena, rc);
                }
                Step(arena, rc); Step(arena, rc);
                Shoot("3-perfect");
                guard = 0;
                var misses = rc.Tap.MissesShown;
                while (rc.Tap.MissesShown == misses && guard++ < 60 * 120)
                {
                    if (arena.Sim.Balls[0].Tap.Ready) rc.Tap.Request();
                    Step(arena, rc);
                }
                Step(arena, rc); Step(arena, rc);
                Shoot("4-miss");
            }
            finally { Object.DestroyImmediate(camGo); }
        }
    }
}
