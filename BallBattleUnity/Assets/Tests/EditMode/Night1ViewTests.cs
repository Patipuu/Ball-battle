using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BallBattle.EditorTools;
using BallBattle.Sim;
using BallBattle.Sim.Boss;
using BallBattle.Sim.Run;
using BallBattle.Sim.Skills;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BallBattle.Tests
{
    public partial class Night1ViewTests
    {
        GameObject root;
        ArtLibrary art;
        SfxLibrary library;
        [SetUp] public void SetUp()
        {
            root = new GameObject("Night1");
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(PlaceholderSfxGenerator.LibraryPath);
            AudioLog.ForceOn = false;
        }
        [TearDown] public void TearDown() { AudioLog.ForceOn = false; Object.DestroyImmediate(root); }
        static string Out => Environment.GetEnvironmentVariable("BB_NIGHT1_OUT");
        static bool Phase(string phase) => Environment.GetEnvironmentVariable("BB_NIGHT1_PHASE") == phase;

        static RunState PrepareRun(string weapon = "blade", int level = 1, SkillBranch branch = SkillBranch.None)
        {
            var p = new ProgressData();
            var run = RunState.Start(31, RunState.StartChoices(31, UnlockRules.Weapons(p))[0], p);
            run.Pick(0); run.Build.WeaponId = weapon; run.Build.SkillLevel = level; run.Build.SkillBranch = branch;
            run.Coins = 100;
            return run;
        }
        static void SkillOffer(RunState run, int slot = 2)
        { run.OfferCards[slot] = OfferSlot.From(new Card(CardKind.SkillUp, run.Build.WeaponId, run.EffectiveSkillLevel + 1)); }

        [TestCase("blade")] [TestCase("fang")] [TestCase("pike")] [TestCase("brawler")]
        [TestCase("volley")] [TestCase("venom")] [TestCase("aegis")] [TestCase("rig")]
        public void BranchNamesAndOneLineEffectsExistAndFit(string weapon)
        {
            foreach (var level in new[] {1, 2})
            {
                var run = PrepareRun(weapon, level, level == 1 ? SkillBranch.None : SkillBranch.B); SkillOffer(run);
                var view = PrepareView.Create(root.transform, art); view.Show(run);
                var buttons = view.Card(2).GetComponentsInChildren<PixelButton>().Where(b => b.name.StartsWith("Skill")).ToArray();
                Assert.That(buttons.Length, Is.EqualTo(level == 1 ? 2 : 1));
                foreach (var button in buttons)
                {
                    Assert.That(button.LabelText, Is.Not.Empty);
                    Assert.That(button.LabelText, Does.Contain(level == 1 ? " " : "LV 3"));
                    var effect = button.transform.Find("Effect").GetComponent<PixelText>();
                    Assert.That(effect.Text, Is.Not.Empty);
                    foreach (var text in button.GetComponentsInChildren<PixelText>())
                    {
                        Assert.That(text.Bounds.xMin, Is.GreaterThanOrEqualTo(button.Area.xMin + 1));
                        Assert.That(text.Bounds.xMax, Is.LessThanOrEqualTo(button.Area.xMax - 1));
                        Assert.That(text.Bounds.yMin, Is.GreaterThanOrEqualTo(button.Area.yMin + 1));
                        Assert.That(text.Bounds.yMax, Is.LessThanOrEqualTo(button.Area.yMax - 1));
                    }
                }
                Bb0102Tests.AssertTextFitsAndNoOverlap(view.transform);
                view.Hide();
            }
        }

        PrepareView TakenAfterRerolls(int rerolls, bool forced)
        {
            var run = PrepareRun();
            if (forced) run.SkillOverride = new SkillSpec(2, SkillBranch.A);
            var view = PrepareView.Create(root.transform, art); view.Show(run);
            for (var n = 0; n < rerolls; n++) { run.Reroll(); view.Show(run); }
            SkillOffer(run); view.Show(run);
            Assert.That(PrepareView.ShownCard(run, 2).Kind, Is.EqualTo(CardKind.SkillUp));
            run.Pick(2, SkillBranch.A); view.Show(run);
            Assert.That(view.CardTag(2).Text, Is.EqualTo("TAKEN"));
            Assert.That(PrepareView.ShownCard(run, 2).Kind, Is.EqualTo(CardKind.SkillUp));
            Assert.That(view.CardTitle(2), Is.EqualTo("SKILL UP"));
            return view;
        }
        [TestCase(0, false)] [TestCase(1, false)] [TestCase(2, false)]
        [TestCase(0, true)] [TestCase(1, true)] [TestCase(2, true)]
        public void TakenIdentitySurvivesZeroOneAndTwoRerolls(int rerolls, bool forced) => TakenAfterRerolls(rerolls, forced);

        [Test]
        public void RetryCopySaysFreePickAndOnePickStartsTheFight()
        {
            var flow = new RunFlow(new MemorySaveStore(), 0, 0); flow.Open(); flow.NewRun(42);
            flow.ChooseWeapon(flow.StartChoices[0]); flow.PickStart(0); flow.Fight(); flow.Tick(.01f);
            var m = flow.CurrentMatch; m.DealDamage(null, m.Balls[0], (Fix)30000, DamageKind.Hazard); m.Step();
            flow.OnMatchEnded(m); flow.Tick(.01f); flow.NextAfterResult();
            Assert.That(flow.Current, Is.EqualTo(RunFlow.State.StartPick));
            var view = StartPickView.Create(root.transform, art); view.Show(flow.Run);
            var texts = view.GetComponentsInChildren<PixelText>().Select(t => t.Text).ToArray();
            Assert.That(texts, Does.Contain("FREE PICK")); Assert.That(texts, Does.Not.Contain("FREE PICK 1 OF 3"));
            flow.PickStart(0); Assert.That(flow.Current, Is.EqualTo(RunFlow.State.Countdown));
        }

        [TestCase(false)] [TestCase(true)]
        public void EverySkillFireHasOneLogAndPlayedLinesMatchCueRequestsIncludingMimic(bool mimic)
        {
            AudioLog.ForceOn = true;
            var d = AudioDirector.Create(root.transform, library, new MemorySaveStore());
            var cfg = ArenaRegistry.Create(ArenaRegistry.Classic);
            var m = new MatchSim(cfg, 71, new[] {
                new BallLoadout(WeaponRegistry.Create("blade")),
                new BallLoadout(WeaponRegistry.Create(mimic ? "blade" : "fang")) { Boss = mimic ? BossRegistry.Create(BossIds.Mimic) : null }
            });
            var fires = 0; var cueRequests = 0; var lines = new List<string>();
            while (m.Outcome == MatchOutcome.Ongoing)
            {
                m.Step();
                foreach (var e in m.Events.Where(e => e.Type == SimEventType.SkillFired))
                {
                    fires++; var before = d.Played.Count; d.OnSimEvent(e, m);
                    if (d.Played.Count > before) cueRequests++;
                    lines.Add(d.LogLines.Last());
                }
            }
            Assert.That(fires, Is.GreaterThan(0));
            Assert.That(lines.Count, Is.EqualTo(fires));
            Assert.That(lines.Count(l => l.EndsWith("result=played")), Is.EqualTo(cueRequests));
            Assert.That(d.Played.Count(p => p.Id.StartsWith("skill_")), Is.EqualTo(cueRequests));
            Assert.That(lines.All(l => l.StartsWith("[Skill] tick=") && l.Contains("branch=1 cue=skill_")), Is.True);
            if (mimic) Assert.That(lines, Has.Some.Contains("ball=1 skill=blade_whetstone"));
            else Assert.That(lines.Count, Is.EqualTo(cueRequests));
            if (Phase("B")) File.WriteAllLines(Path.Combine(Out, "phaseB-skills" + (mimic ? "-mimic" : "") + ".txt"), lines);
        }

        [Test]
        public void MirrorReflectLineReadsExistingEventsAndAddsNoCue()
        {
            AudioLog.ForceOn = true;
            var d = AudioDirector.Create(root.transform, library, new MemorySaveStore());
            var m = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 81, new[] {WeaponRegistry.Create("aegis"), WeaponRegistry.Create("brawler")});
            var lines = new List<string>();
            while (m.Outcome == MatchOutcome.Ongoing)
            {
                m.Step();
                foreach (var e in m.Events)
                {
                    var line = AudioLog.ReflectLine(e, m);
                    if (line == null) continue;
                    var before = d.Played.Count; d.OnSimEvent(e, m);
                    Assert.That(d.LogLines.Last(), Is.EqualTo(line));
                    Assert.That(d.Played.Count, Is.EqualTo(before), "reflect Damage only logs; no new Play call");
                    lines.Add(line);
                }
            }
            Assert.That(lines, Is.Not.Empty);
            if (Phase("B")) File.WriteAllLines(Path.Combine(Out, "phaseB-reflect.txt"), lines);
        }

        static void Shoot(Camera camera, string file, int zoom = 1)
        {
            var rt = new RenderTexture(270 * zoom, 480 * zoom, 24) {filterMode = FilterMode.Point};
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply();
            File.WriteAllBytes(file, texture.EncodeToPNG()); RenderTexture.active = null; camera.targetTexture = null;
            Object.DestroyImmediate(texture); rt.Release(); Object.DestroyImmediate(rt);
        }
        Camera CaptureCamera()
        {
            var go = new GameObject("Night1Camera"); go.transform.SetParent(root.transform);
            go.transform.position = new Vector3(0, 0, -10);
            var camera = go.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 240;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Palette.Background;
            return camera;
        }
        [Test]
        public void CapturePhaseBBranchTakenAndRetryScreens()
        {
            if (!Phase("B")) Assert.Ignore("Phase B capture only");
            var camera = CaptureCamera();
            for (var level = 1; level <= 2; level++)
            {
                var run = PrepareRun("venom", level, level == 1 ? SkillBranch.None : SkillBranch.B); SkillOffer(run, 0);
                var view = PrepareView.Create(root.transform, art); view.Show(run);
                Shoot(camera, Path.Combine(Out, "phaseB-skill-up-level" + (level + 1) + ".png")); view.Hide();
            }
            var taken = TakenAfterRerolls(2, true); Shoot(camera, Path.Combine(Out, "phaseB-taken-after-two-rerolls.png")); taken.Hide();
            var retry = PrepareRun(); retry.StartPickPending = true; retry.IsRetry = true;
            var start = StartPickView.Create(root.transform, art); start.Show(retry);
            Shoot(camera, Path.Combine(Out, "phaseB-retry-free-pick.png"));
        }
    }
}
