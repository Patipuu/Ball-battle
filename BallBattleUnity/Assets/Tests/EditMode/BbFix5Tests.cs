using System;
using System.IO;
using System.Linq;
using BallBattle.EditorTools;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using BallBattle.Sim.Skills;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BallBattle.Tests
{
    /// <summary>BB-FIX5: a level-3 skill taken under -bbForceSkill survives CONTINUE; spike-zone teeth span the whole zone.</summary>
    public class BbFix5Tests
    {
        GameObject root;
        ArtLibrary art;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            root = new GameObject("BbFix5Root");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        static RunFlow Flow(MemorySaveStore store, SkillSpec? force)
        {
            var flow = new RunFlow(store, 0f, 0f) { ForceSkill = force };
            flow.Open();
            return flow;
        }

        /// <summary>New run (seed 2) with <paramref name="force"/>, start card taken, then a SKILL UP to level 3 taken on Prepare.</summary>
        static RunFlow ReachLevel3(MemorySaveStore store, SkillSpec? force)
        {
            var flow = Flow(store, force);
            flow.NewRun(2);
            flow.ChooseWeapon(flow.StartChoices[0]);
            var start = Enumerable.Range(0, flow.Run.OfferCount).First(i => flow.Run.OfferCard(i).Kind != CardKind.SkillUp && flow.Run.CanPick(i));
            flow.PickStart(start);
            Assert.That(flow.Current, Is.EqualTo(RunFlow.State.Prepare));
            flow.Run.Coins = 50;   // a second card on the same Prepare costs ExtraPickCost
            if (flow.Run.Build.SkillLevel < 2)
            {
                flow.Run.OfferCards[1] = OfferSlot.From(new Card(CardKind.SkillUp, flow.Run.Build.WeaponId, 2));
                flow.Pick(1, SkillBranch.A);
            }
            flow.Run.OfferCards[0] = OfferSlot.From(new Card(CardKind.SkillUp, flow.Run.Build.WeaponId, 3));
            flow.Pick(0, SkillBranch.A);
            Assert.That((flow.Run.EffectiveSkillLevel, flow.Run.EffectiveSkillBranch), Is.EqualTo((3, SkillBranch.A)), "fixture: level 3 reached");
            return flow;
        }

        static void AssertLevel(RunFlow flow, int level, string what)
        {
            Assert.That(flow.Current, Is.EqualTo(RunFlow.State.Prepare), what);
            Assert.That((flow.Run.EffectiveSkillLevel, flow.Run.EffectiveSkillBranch), Is.EqualTo((level, SkillBranch.A)), what + ": effective");
            var m = flow.Run.CreateMatch(null, flow.ForceSkill);
            Assert.That(m.Balls[0].Skill.Level, Is.EqualTo(level), what + ": the fight plays that level");
            if (level == 3)
                Assert.That(Enumerable.Range(0, flow.Run.OfferCount).Where(i => !flow.Run.IsPicked(i)).All(i => !flow.Run.CanPick(i) || flow.Run.OfferCard(i).Kind != CardKind.SkillUp),
                    Is.True, what + ": no SKILL UP can be taken after level 3");
        }

        [Test]
        public void LevelThreeTakenUnderForceSkill2aSurvivesContinue()
        {
            var store = new MemorySaveStore();
            var force = new SkillSpec(2, SkillBranch.A);
            var first = ReachLevel3(store, force);
            Assert.That((first.Run.Build.SkillLevel, first.Run.Build.SkillBranch), Is.EqualTo((2, SkillBranch.A)),
                "the save holds only the real pick (one SKILL UP), never the forced level (BB-10a)");
            AssertLevel(Resume(store, force), 3, "CONTINUE with -bbForceSkill 2a");
            AssertLevel(Resume(store, force), 3, "CONTINUE again");
            AssertLevel(Resume(store, null), 2, "CONTINUE without the flag: only the real pick remains (flag never serialized)");
        }

        [Test]
        public void LevelThreeWithoutTheFlagSurvivesContinue()
        {
            var store = new MemorySaveStore();
            var first = ReachLevel3(store, null);
            Assert.That((first.Run.Build.SkillLevel, first.Run.Build.SkillBranch), Is.EqualTo((3, SkillBranch.A)));
            AssertLevel(Resume(store, null), 3, "normal run");
        }

        [Test]
        public void ForceSkillStillRaisesALowerSavedRun()
        {
            var store = new MemorySaveStore();
            var flow = Flow(store, null);
            flow.NewRun(2);
            flow.ChooseWeapon(flow.StartChoices[0]);
            flow.PickStart(Enumerable.Range(0, flow.Run.OfferCount).First(i => flow.Run.OfferCard(i).Kind != CardKind.SkillUp && flow.Run.CanPick(i)));
            Assert.That(flow.Run.Build.SkillLevel, Is.EqualTo(1));
            var resumed = Resume(store, new SkillSpec(3, SkillBranch.B));
            Assert.That((resumed.Run.EffectiveSkillLevel, resumed.Run.EffectiveSkillBranch), Is.EqualTo((3, SkillBranch.B)));
            Assert.That(resumed.Run.CreateMatch(null, resumed.ForceSkill).Balls[0].Skill.Level, Is.EqualTo(3));
        }

        [TestCase("1", 1, 1, 0)] [TestCase("1", 2, 2, 2)] [TestCase("1", 3, 3, 2)]
        [TestCase("2a", 1, 2, 1)] [TestCase("2a", 2, 3, 1)] [TestCase("2b", 2, 3, 2)]
        [TestCase("3a", 1, 3, 1)] [TestCase("3b", 3, 3, 2)]
        public void ResumedOverrideIsForcedLevelPlusRealPicks(string flag, int buildLevel, int level, int branch)
        {
            var force = DebugOptions.ParseForceSkill(new[] { "-bbForceSkill", flag });
            var build = new RunBuild { WeaponId = "blade", SkillLevel = buildLevel, SkillBranch = buildLevel > 1 ? SkillBranch.B : SkillBranch.None };
            var spec = RunFlow.ResumedSkillOverride(force, build).Value;
            Assert.That((spec.Level, (int)spec.Branch), Is.EqualTo((level, branch)));
            Assert.That(RunFlow.ResumedSkillOverride(null, build), Is.Null);
        }

        /// <summary>BB_FIX5_OUT: spike-walls arena frame at 1x and 3x (whole frame + top-left corner crop).</summary>
        [Test]
        public void CaptureFix5SpikeTeeth()
        {
            var dir = Environment.GetEnvironmentVariable("BB_FIX5_OUT");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("BB_FIX5_OUT not set");
            Directory.CreateDirectory(dir);
            var config = ArenaRegistry.Create(ArenaRegistry.SpikeWalls);
            var frame = ArenaFrameView.Create(root.transform, art);
            frame.Render(config.ArenaAt(0), 0, config, ArenaRegistry.SpikeWalls);
            var camGo = new GameObject("Cam");
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true; cam.orthographicSize = 240f; cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Palette.Background; camGo.transform.position = new Vector3(0, 0, -10);
                foreach (var zoom in new[] { 1, 3 })
                {
                    var rt = new RenderTexture(270 * zoom, 480 * zoom, 24) { filterMode = FilterMode.Point };
                    cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                    var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                    File.WriteAllBytes(Path.Combine(dir, $"spike-walls-frame-{zoom}x.png"), tex.EncodeToPNG());
                    RenderTexture.active = null; cam.targetTexture = null;
                    Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
                }
                var lines = ArenaFrameView.ZoneSegments(config.Layout.SpikeCorners, config.ArenaAt(0))
                    .Select(z => $"{z.Wall} zone {z.From:0.##}..{z.To:0.##} ({z.Length:0.##} px): {ArenaFrameView.ToothCount(z.From, z.To)} teeth");
                File.WriteAllLines(Path.Combine(dir, "spike-zones.txt"), lines);
            }
            finally { Object.DestroyImmediate(camGo); }
        }

        static RunFlow Resume(MemorySaveStore store, SkillSpec? force)
        {
            var flow = Flow(store, force);
            Assert.That(flow.HasSavedRun, Is.True);
            flow.Continue();
            return flow;
        }

        [Test]
        public void SpikeTeethSpanTheWholeZoneRoundedOut()
        {
            var config = ArenaRegistry.Create(ArenaRegistry.SpikeWalls);
            var frame = ArenaFrameView.Create(root.transform, art);
            foreach (var tick in new[] { 0, config.ShrinkStartTick + config.ShrinkDurationTicks / 2, config.ShrinkStartTick + config.ShrinkDurationTicks })
            {
                var r = config.ArenaAt(tick);
                frame.Render(r, tick, config, ArenaRegistry.SpikeWalls);
                var zones = ArenaFrameView.ZoneSegments(config.Layout.SpikeCorners, r);
                var n = 0;
                foreach (var z in zones)
                {
                    var count = ArenaFrameView.ToothCount(z.From, z.To);
                    var along = Enumerable.Range(n, count).Select(i => z.Horizontal ? frame.ToothPosition(i).x : frame.ToothPosition(i).y).ToList();
                    n += count;
                    var lo = Mathf.Floor(Mathf.Min(z.From, z.To));
                    var hi = Mathf.Ceil(Mathf.Max(z.From, z.To));
                    var half = ArenaFrameView.ToothWidth / 2f;
                    Assert.That(along.Min() - half, Is.EqualTo(lo).Within(0.51f), $"{z.Wall} tick {tick}: first tooth at the zone start");
                    Assert.That(along.Max() + half, Is.EqualTo(hi).Within(0.51f), $"{z.Wall} tick {tick}: last tooth at the zone end");
                    var sorted = along.OrderBy(a => a).ToList();
                    for (var i = 1; i < sorted.Count; i++)
                    {
                        Assert.That(sorted[i] - sorted[i - 1], Is.GreaterThanOrEqualTo(ArenaFrameView.ToothWidth), "teeth do not overlap");
                        Assert.That(sorted[i] - sorted[i - 1], Is.LessThanOrEqualTo(ArenaFrameView.ToothSpacing + 4), "gaps stay small (at most 6 px of bare wall)");
                    }
                }
                Assert.That(n, Is.EqualTo(frame.TeethShown));
            }
            // 230 px wall: a 34.5 px zone now draws 35 px of teeth (5 teeth; was 4 teeth = 30 px centred in it).
            var start = ArenaFrameView.ZoneSegments(config.Layout.SpikeCorners, config.ArenaAt(0));
            Assert.That(start.Any(z => Mathf.Approximately(z.Length, 34.5f)), Is.True, "fixture: 34.5 px zones");
            Assert.That(start.All(z => ArenaFrameView.ToothCount(z.From, z.To) == 5), Is.True);
        }
    }
}
