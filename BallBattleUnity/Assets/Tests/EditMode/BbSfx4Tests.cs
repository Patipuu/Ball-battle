using System;
using System.Collections.Generic;
using System.Linq;
using BallBattle.EditorTools;
using BallBattle.Sim;
using BallBattle.Sim.Meta;
using BallBattle.Sim.Run;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BallBattle.Tests
{
    /// <summary>
    /// BB-SFX4: the forged cue set (short, warm hits; new tap / chest / level-up / tree cues, all linked), the thinner match mix
    /// (hit budget, projectile budget, bounces under hits, turret ticks) and the screens that play the new cues.
    /// </summary>
    public class BbSfx4Tests
    {
        GameObject root;
        SfxLibrary lib;
        ArtLibrary art;
        float now;
        readonly List<string> ui = new List<string>();
        void OnUi(string id) => ui.Add(id);

        [SetUp]
        public void SetUp()
        {
            lib = AssetDatabase.LoadAssetAtPath<SfxLibrary>(PlaceholderSfxGenerator.LibraryPath);
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            root = new GameObject("BbSfx4Root");
            now = 0f;
            ui.Clear();
            UiAudio.Requested += OnUi;
        }

        [TearDown]
        public void TearDown()
        {
            UiAudio.Requested -= OnUi;
            Object.DestroyImmediate(root);
        }

        AudioDirector NewDirector()
        {
            var d = AudioDirector.Create(root.transform, lib, new MemorySaveStore());
            d.Clock = () => now;
            return d;
        }

        static MatchSim Sim(string a, string b) =>
            new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 5, new[] { new BallLoadout(WeaponRegistry.Create(a)), new BallLoadout(WeaponRegistry.Create(b)) });

        static SimEvent Ev(SimEventType t, int tick, int a = 0, float value = 3f) =>
            new SimEvent { Type = t, Tick = tick, A = a, B = 1 - a, Value = TFix.X(value) };

        // ---------------- the forged sounds ----------------

        /// <summary>Share of energy above <paramref name="hz"/> (radix-2 FFT of the zero-padded clip).</summary>
        static double ShareAbove(float[] x, double hz)
        {
            var n = 1;
            while (n < x.Length) n <<= 1;
            var re = new double[n];
            var im = new double[n];
            for (var i = 0; i < x.Length; i++) re[i] = x[i];
            for (int i = 1, j = 0; i < n; i++)
            {
                var bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); }
            }
            for (var len = 2; len <= n; len <<= 1)
            {
                var ang = -2 * Math.PI / len;
                for (var i = 0; i < n; i += len)
                    for (var k = 0; k < len / 2; k++)
                    {
                        var wr = Math.Cos(ang * k);
                        var wi = Math.Sin(ang * k);
                        var ur = re[i + k];
                        var ui2 = im[i + k];
                        var vr = re[i + k + len / 2] * wr - im[i + k + len / 2] * wi;
                        var vi = re[i + k + len / 2] * wi + im[i + k + len / 2] * wr;
                        re[i + k] = ur + vr; im[i + k] = ui2 + vi;
                        re[i + k + len / 2] = ur - vr; im[i + k + len / 2] = ui2 - vi;
                    }
            }
            double total = 0, high = 0;
            for (var k = 1; k < n / 2; k++)
            {
                var p = re[k] * re[k] + im[k] * im[k];
                total += p;
                if (k * (double)PlaceholderSfxSynth.SampleRate / n >= hz) high += p;
            }
            return total > 0 ? high / total : 0;
        }

        [Test]
        public void ForgedHitsAreShortAndWarm()
        {
            foreach (var (id, make) in SfxForge.All)
            {
                var s = make();
                Assert.That(s.All(v => !float.IsNaN(v) && Math.Abs(v) <= 1f), Is.True, id);
                var seconds = s.Length / (float)PlaceholderSfxSynth.SampleRate;
                if (AudioIds.IsHit(id) || id == AudioIds.SpikeHit)
                {
                    Assert.That(seconds, Is.LessThanOrEqualTo(0.25f), $"{id}: a hit is short so streams of hits do not pile up");
                    Assert.That(ShareAbove(s, 6000), Is.LessThan(0.05), $"{id}: not hissy (old FANG/PIKE/VOLLEY were 87-99% above 6 kHz)");
                    Assert.That(ShareAbove(s, 1000), Is.GreaterThan(0.02), $"{id}: some presence for phone speakers");
                }
                else Assert.That(seconds, Is.LessThanOrEqualTo(1f), id);
            }
            Assert.That(ShareAbove(SfxForge.Parry(), 6000), Is.LessThan(0.05), "parry is a bell, not a hiss");
        }

        [Test]
        public void EveryForgedAndFeedbackCueIsLinked()
        {
            Assert.That(lib, Is.Not.Null);
            foreach (var (id, _) in SfxForge.All) Assert.That(lib.Has(id), Is.True, id);
            foreach (var id in AudioIds.FeedbackCues)
            {
                Assert.That(AudioIds.Cues, Does.Contain(id));
                Assert.That(lib.Has(id), Is.True, id);
            }
        }

        // ---------------- the mix ----------------

        [Test]
        public void AtMostThreeHitCuesPerWindowThenRoomAgain()
        {
            var d = NewDirector();
            var sim = Sim("fang", "blade");
            for (var t = 0; t < 8; t++) d.OnSimEvent(Ev(SimEventType.Hit, 10 + t), sim);   // one frame of a FANG combo
            Assert.That(d.Played.Count(p => AudioIds.IsHit(p.Id)), Is.EqualTo(AudioDirector.HitBudget));
            now += AudioDirector.HitWindow;
            d.OnSimEvent(Ev(SimEventType.Hit, 30), sim);
            Assert.That(d.Played.Count(p => AudioIds.IsHit(p.Id)), Is.EqualTo(AudioDirector.HitBudget + 1), "the window has passed");
        }

        [Test]
        public void AVolleyOfArrowsIsTwoCuesAndLeavesRoomForAMeleeHit()
        {
            var d = NewDirector();
            var sim = Sim("blade", "volley");
            for (var t = 0; t < 7; t++) d.OnSimEvent(Ev(SimEventType.ProjectileHit, 10 + t, 1), sim);
            Assert.That(d.Played.Count(p => p.Id == "hit_volley"), Is.EqualTo(AudioDirector.ProjectileHitBudget));
            d.OnSimEvent(Ev(SimEventType.Hit, 20, 0), sim);
            Assert.That(d.Played.Last().Id, Is.EqualTo("hit_blade"), "melee still heard in the same window");
            Assert.That(d.Played.Last(p => p.Id == "hit_volley").Volume, Is.EqualTo(AudioDirector.ProjectileHitVolume * VolumeCurve.Linear(d.SfxVolume)).Within(1e-6f));
        }

        [Test]
        public void BouncesUnderAHitAreNotHeardButSpikesAre()
        {
            var d = NewDirector();
            var sim = Sim("blade", "fang");
            d.OnSimEvent(Ev(SimEventType.Hit, 10), sim);
            d.OnSimEvent(Ev(SimEventType.WallBounce, 11, 0, 0f), sim);
            d.OnSimEvent(Ev(SimEventType.BallBounce, 12, 0, 0f), sim);
            Assert.That(d.Played.Select(p => p.Id), Is.EqualTo(new[] { "hit_blade" }), "covered by the hit");
            d.OnSimEvent(Ev(SimEventType.WallBounce, 13, 1, 2f), sim);
            Assert.That(d.Played.Last().Id, Is.EqualTo(AudioIds.SpikeHit), "a damaging spike is always heard");
            now += AudioDirector.BounceAfterHit;
            d.OnSimEvent(Ev(SimEventType.WallBounce, 30, 0, 0f), sim);
            d.OnSimEvent(Ev(SimEventType.BallBounce, 31, 0, 0f), sim);
            Assert.That(d.Played.Count(p => p.Id == AudioIds.Wall), Is.EqualTo(1), "then one bounce per BounceSpacing");
        }

        [Test]
        public void TurretVolleysTickSoftlyAndRarely()
        {
            var d = NewDirector();
            var sim = Sim("rig", "blade");
            for (var t = 0; t < 10; t++) { now = t * 0.06f; d.OnSimEvent(Ev(SimEventType.ProjectileFired, 10 + t), sim); }
            var ticks = d.Played.Where(p => p.Id == AudioIds.Wall).ToList();
            Assert.That(ticks.Count, Is.EqualTo(2), "0.54 s of fire: ticks at 0 and 0.30 s (one per 0.25 s)");
            Assert.That(ticks[0].Volume, Is.EqualTo(AudioDirector.FiredVolume * VolumeCurve.Linear(d.SfxVolume)).Within(1e-6f));
        }

        [Test]
        public void TapEventsPlayTheirCues()
        {
            var d = NewDirector();
            var sim = Sim("blade", "fang");
            d.OnSimEvent(Ev(SimEventType.TapStrike, 10), sim);
            d.OnSimEvent(Ev(SimEventType.TapPerfect, 12), sim);
            d.OnSimEvent(Ev(SimEventType.TapMiss, 40), sim);
            Assert.That(d.Played.Select(p => p.Id), Is.EqualTo(new[] { AudioIds.TapStrike, AudioIds.TapPerfect, AudioIds.TapMiss }));
            Assert.That(AudioIds.Ducks(AudioIds.TapPerfect), Is.True);
        }

        // ---------------- screens ----------------

        static MemorySaveStore Store(Action<ProgressData> setup)
        {
            var p = new ProgressData();
            setup(p);
            var s = new MemorySaveStore();
            s.Save(new SaveData { Progress = p });
            return s;
        }

        [Test]
        public void UpgradePlaysLevelUpOrDenied()
        {
            var store = Store(p => { MetaRules.Ball(p, "blade").Shards = 999; p.Gold = 99999; });
            var v = BallsView.Create(root.transform, art);
            v.Show(store);
            v.OpenDetail("blade");
            ui.Clear();
            v.UpgradeButton.SimulateClick();
            Assert.That(ui, Is.EqualTo(new[] { AudioIds.LevelUp }));
            var poor = Store(p => { });
            v.Show(poor);
            v.OpenDetail("blade");
            ui.Clear();
            v.UpgradeButton.SimulateClick();
            Assert.That(ui, Is.EqualTo(new[] { AudioIds.UiDenied }));
            Assert.That(v.UpgradeButton.ClickCue, Is.Null, "no extra click under the level-up");
        }

        [Test]
        public void TreeAddResetAndDeniedHaveTheirOwnCues()
        {
            var store = Store(p => MetaRules.Ball(p, "blade").Level = 1);
            var v = TreeView.Create(root.transform, art);
            v.Show(store, "blade");
            v.Select(TreeView.Box.Opener);
            ui.Clear();
            v.AddButton.SimulateClick();
            Assert.That(ui, Is.EqualTo(new[] { AudioIds.UiDenied }), "locked node");
            v.Select(TreeView.Box.Power);
            ui.Clear();
            v.AddButton.SimulateClick();
            Assert.That(ui, Is.EqualTo(new[] { AudioIds.TreeAdd }));
            ui.Clear();
            v.ResetButton.SimulateClick();
            Assert.That(ui, Is.EqualTo(new[] { AudioIds.TreeReset }));
            ui.Clear();
            v.ResetButton.SimulateClick();
            Assert.That(ui, Is.EqualTo(new[] { AudioIds.UiClick }), "nothing to reset: a plain click");
        }
    }
}
