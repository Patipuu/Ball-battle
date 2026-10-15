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
    /// <summary>
    /// BB-FIX4 (View only): boss hint on the loss result and retry screens, SKILL UP branch buttons clear the card icon, the TAKEN
    /// SKILL UP card names the chosen branch, one [Reflect] line per reflect in Player.log. Optional output: BB_FIX4_OUT = folder for
    /// 1x / 3x screenshots.
    /// </summary>
    public class BbFix4Tests
    {
        GameObject root;
        ArtLibrary art;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            Assert.That(art, Is.Not.Null);
            root = new GameObject("BbFix4Root");
            AudioLog.ForceOn = false;
        }

        [TearDown]
        public void TearDown()
        {
            AudioLog.ForceOn = false;
            Object.DestroyImmediate(root);
        }

        // ---------------- Run flow helpers (the real RunFlow, a memory save; as BbView3Tests) ----------------

        static RunFlow NewFlow()
        {
            var flow = new RunFlow(new MemorySaveStore(), 0f, 0f);
            flow.Open();
            return flow;
        }

        static void StartRun(RunFlow flow, uint seed, int fightIndex = 0, string boss = null)
        {
            flow.NewRun(seed);
            flow.ChooseWeapon(flow.StartChoices[0]);
            flow.PickStart(0);
            Assert.That(flow.Current, Is.EqualTo(RunFlow.State.Prepare));
            flow.Run.FightIndex = fightIndex;
            if (boss != null) flow.Run.BossIds[BossIds.SlotOf(boss)] = boss;
        }

        static void LoseFight(RunFlow flow)
        {
            if (flow.Current == RunFlow.State.Prepare) flow.Fight();
            else if (flow.Current == RunFlow.State.StartPick) flow.PickStart(0);
            Assert.That(flow.Current, Is.EqualTo(RunFlow.State.Countdown));
            var m = flow.CurrentMatch;
            flow.Tick(0.01f);
            var tiny = Fix.Ratio(1, 100);
            for (var i = 0; i < 20000 && m.Outcome == MatchOutcome.Ongoing; i++)
            {
                if (m.Balls[0].Hp > tiny) m.Balls[0].Hp = tiny;
                m.Step();
            }
            Assert.That(m.WinnerIndex, Is.EqualTo(1), "forced loss");
            flow.OnMatchEnded(m);
            flow.Tick(0.01f);
            Assert.That(flow.Current, Is.EqualTo(RunFlow.State.FightResult));
        }

        static RunState PrepareRun(uint seed = 21)
        {
            var p = new ProgressData();
            var run = RunState.Start(seed, RunState.StartChoices(seed, UnlockRules.Weapons(p))[0], p);
            run.Pick(0);
            run.Coins = 100;
            return run;
        }

        static void SkillOffer(RunState run, int slot = 2) =>
            run.OfferCards[slot] = OfferSlot.From(new Card(CardKind.SkillUp, run.Build.WeaponId, run.EffectiveSkillLevel + 1));

        static RunState WeaponRun(string weapon)
        {
            var run = PrepareRun();
            run.Build.WeaponId = weapon;
            return run;
        }

        static string DrawnTitle(PrepareView v, int i) =>
            v.TitleWrapped(i) ? (v.CardTitleLine(i, 1).Text + " " + v.CardTitleLine(i, 2).Text).Trim() : v.CardTitle(i);

        static Rect Bounds(SpriteRenderer r) => Rect.MinMaxRect(r.bounds.min.x, r.bounds.min.y, r.bounds.max.x, r.bounds.max.y);

        // ---------------- 1. boss hint on the loss result and CHOOSE YOUR RETRY screens ----------------

        static void AssertHint(List<string> lines, string boss)
        {
            Assert.That(lines.Count, Is.InRange(2, 3), "name + one or two hint lines");
            Assert.That(lines[0], Is.EqualTo(BossText.Name(boss)), "the boss name, as on Prepare");
            Assert.That(string.Join(" ", lines.Skip(1)), Is.EqualTo(BossText.Hint(boss)), "the Prepare hint, same string");
            Assert.That(lines.Skip(1), Is.EqualTo(PrepareView.BossLines(null, boss, PrepareView.TraitsWidth)), "wrapped like Prepare");
        }

        static void AssertDrawn(Func<int, PixelText> line, List<string> lines)
        {
            for (var i = 0; i < 3; i++) Assert.That(line(i).Text, Is.EqualTo(i < lines.Count ? lines[i] : ""));
            Assert.That((Color)line(0).TextColor, Is.EqualTo((Color)Palette.WallWarning), "name in the Prepare boss color");
            Assert.That((Color)line(1).TextColor, Is.EqualTo((Color)Palette.Text), "hint in the Prepare hint color");
        }

        [TestCase(3, BossIds.ShellKing)]
        [TestCase(3, BossIds.Magnet)]
        [TestCase(7, BossIds.Warden)]
        public void LossResultAndRetryShowTheBossHint(int fightIndex, string boss)
        {
            var flow = NewFlow();
            StartRun(flow, 31, fightIndex, boss);
            LoseFight(flow);
            var result = RunResultView.Create(root.transform, art);
            result.Show(flow.LastReport);
            var lines = RunResultView.BossHint(flow.LastReport);
            AssertHint(lines, boss);
            AssertDrawn(result.BossHintLine, lines);
            Bb0102Tests.AssertTextFitsAndNoOverlap(result.transform);
            for (var i = 0; i < lines.Count; i++)
            {
                var b = result.BossHintLine(i).WorldBounds;
                Assert.That(b.yMax, Is.LessThanOrEqualTo(-38f), "under the NEXT box");
                Assert.That(b.Overlaps(result.ContinueButton.Area), Is.False, "clear of NEXT FIGHT");
            }
            result.Hide();

            flow.NextAfterResult();
            Assert.That(flow.Current, Is.EqualTo(RunFlow.State.StartPick));
            Assert.That(flow.Run.IsRetry, Is.True);
            var pick = StartPickView.Create(root.transform, art);
            pick.Show(flow.Run);
            Assert.That(StartPickView.BossHint(flow.Run), Is.EqualTo(lines), "same text on both screens");
            AssertDrawn(pick.BossHintLine, lines);
            Bb0102Tests.AssertTextFitsAndNoOverlap(pick.transform);
            for (var i = 0; i < lines.Count; i++)
            {
                var b = pick.BossHintLine(i).WorldBounds;
                Assert.That(b.yMax, Is.LessThanOrEqualTo(-97f), "under the footer band");
                for (var c = 0; c < flow.Run.OfferCount; c++) Assert.That(b.Overlaps(StartPickView.CardArea(c)), Is.False);
            }
        }

        [Test]
        public void NoBossHintAfterANormalLossOrOnTheStartPick()
        {
            var flow = NewFlow();
            flow.NewRun(31);
            flow.ChooseWeapon(flow.StartChoices[0]);
            var pick = StartPickView.Create(root.transform, art);
            pick.Show(flow.Run);
            Assert.That(StartPickView.BossHint(flow.Run), Is.Empty, "first start pick");
            Assert.That(pick.BossHintLine(0).Text, Is.Empty);
            flow.PickStart(0);
            LoseFight(flow);   // fight 1: no boss
            Assert.That(RunResultView.BossHint(flow.LastReport), Is.Empty);
            var result = RunResultView.Create(root.transform, art);
            result.Show(flow.LastReport);
            Assert.That(result.BossHintLine(0).Text, Is.Empty);
            flow.NextAfterResult();
            pick.Show(flow.Run);
            Assert.That(flow.Run.IsRetry, Is.True);
            Assert.That(pick.BossHintLine(0).Text, Is.Empty, "retry of a normal fight");
        }

        // ---------------- 2. SKILL UP branch buttons clear the card icon ----------------

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void BranchButtonsLeaveThePrepareIconFullyVisible(int slot)
        {
            var run = PrepareRun();
            SkillOffer(run, slot);
            var view = PrepareView.Create(root.transform, art);
            view.Show(run);
            var a = view.Card(slot).GetComponentsInChildren<PixelButton>().Single(b => b.name == "SkillA");
            var icon = Bounds(view.CardIcon(slot));
            Assert.That(view.CardIcon(slot).enabled, Is.True);
            Assert.That(icon.yMin, Is.GreaterThanOrEqualTo(a.Area.yMax + 1f), "icon above the branch button's top border");
            Assert.That(icon.yMax, Is.LessThanOrEqualTo(PrepareView.CardArea(slot).yMax - 2f), "inside the card frame");
            Assert.That(icon.xMax, Is.LessThan(view.CardTitleText(slot).WorldBounds.xMin), "left of the title");
            run.Pick(slot, SkillBranch.A);
            view.Show(run);
            Assert.That(view.CardIcon(slot).transform.localScale.x, Is.EqualTo(2f), "buttons gone (TAKEN): the usual 2x icon");
            Assert.That(view.CardIcon(slot).transform.localPosition.y, Is.EqualTo(PrepareView.CardArea(slot).center.y));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void BranchButtonsLeaveTheStartPickIconFullyVisible(int slot)
        {
            var p = new ProgressData();
            var run = RunState.Start(23, RunState.StartChoices(23, UnlockRules.Weapons(p))[0], p);
            run.OfferCards[slot] = OfferSlot.From(new Card(CardKind.SkillUp, run.Build.WeaponId, 2));
            var view = StartPickView.Create(root.transform, art);
            view.Show(run);
            var card = view.GetComponentsInChildren<PixelButton>().Where(b => b.name != "SkillA" && b.name != "SkillB").ElementAt(slot).transform;
            var a = card.GetComponentsInChildren<PixelButton>().Single(b => b.name == "SkillA");
            var icon = card.Find("Icon").GetComponent<SpriteRenderer>();
            Assert.That(icon.transform.localScale.x, Is.EqualTo(2f), "start pick keeps the 2x icon");
            Assert.That(Bounds(icon).yMin, Is.GreaterThanOrEqualTo(a.Area.yMax + 1f), "icon above the branch button's top border");
            Assert.That(Bounds(icon).yMax, Is.LessThanOrEqualTo(StartPickView.CardArea(slot).yMax - 2f), "inside the card frame");
            for (var other = 0; other < run.OfferCount; other++)
            {
                if (other == slot) continue;
                var otherCard = view.GetComponentsInChildren<PixelButton>().Where(b => b.name != "SkillA" && b.name != "SkillB").ElementAt(other).transform;
                Assert.That(otherCard.Find("Icon").localPosition.y, Is.EqualTo(StartPickView.CardArea(other).yMax - 30f), "other cards unchanged");
            }
        }

        // ---------------- 3. TAKEN SKILL UP names the branch ----------------

        [TestCase("blade")] [TestCase("fang")] [TestCase("pike")] [TestCase("brawler")]
        [TestCase("volley")] [TestCase("venom")] [TestCase("aegis")] [TestCase("rig")]
        public void TakenSkillUpNamesTheChosenBranchAfterAReroll(string weapon)
        {
            foreach (var branch in new[] { SkillBranch.A, SkillBranch.B })
            {
                var run = WeaponRun(weapon);
                var view = PrepareView.Create(root.transform, art);
                view.Show(run);
                run.Reroll();
                view.Show(run);
                SkillOffer(run);
                view.Show(run);
                Assert.That(DrawnTitle(view, 2), Is.EqualTo("SKILL UP"), "offer: generic until a branch is chosen");
                Assert.That(view.CardDetail(2, 1).Text, Is.Empty, "branch buttons replace the details");
                view.GetComponentsInChildren<PixelButton>().Single(b => b.name == (branch == SkillBranch.A ? "SkillA" : "SkillB") && b.gameObject.activeInHierarchy).SimulateClick();
                Assert.That(run.IsPicked(2), Is.False, "the view only raises SkillPicked");
                run.Pick(2, branch);
                view.Show(run);
                var data = SkillDefinition.ForWeapon(weapon);
                var name = branch == SkillBranch.A ? data.BranchA : data.BranchB;
                Assert.That(view.CardTag(2).Text, Is.EqualTo("TAKEN"));
                Assert.That(PrepareView.ShownFace(run, 2, art).Title, Is.EqualTo("SKILL UP - " + name));
                Assert.That(DrawnTitle(view, 2), Is.EqualTo("SKILL UP - " + name), weapon + " " + branch);
                Assert.That(view.CardTitleLine(2, 2).Text, Is.Empty, "one line (2x, or 1x beside TAKEN when too long), never split after the dash");
                Assert.That(view.CardDetail(2, 1).Text, Is.EqualTo(branch == SkillBranch.A ? data.SummaryA : data.SummaryB));
                Assert.That(view.CardDetail(2, 1).Text, Does.Not.Contain("CHOOSE"));
                Bb0102Tests.AssertTextFitsAndNoOverlap(view.transform);
                foreach (var text in new[] { view.CardTitleText(2), view.CardTitleLine(2, 1), view.CardTitleLine(2, 2), view.CardDetail(2, 1) })
                    if (text.Text.Length > 0) Assert.That(text.WorldBounds.xMax, Is.LessThanOrEqualTo(PrepareView.CardTextRight), $"'{text.Text}' fits the card");
                Object.DestroyImmediate(view.gameObject);
            }
        }

        // ---------------- 4. one [Reflect] line per reflect in the console (Player.log) ----------------

        [TestCase(true)]
        [TestCase(false)]
        public void ReflectAndSkillLinesReachTheConsoleOnce(bool audioLog)
        {
            AudioLog.ForceOn = audioLog;
            var lib = AssetDatabase.LoadAssetAtPath<SfxLibrary>(PlaceholderSfxGenerator.LibraryPath);
            var d = AudioDirector.Create(root.transform, lib, new MemorySaveStore());
            var reflectConsole = new List<string>();
            var skillConsole = 0;
            Application.LogCallback onLog = (message, stack, type) =>
            {
                if (message.StartsWith("[Reflect]")) reflectConsole.Add(message);
                if (message.StartsWith("[Skill]")) skillConsole++;
            };
            Application.logMessageReceived += onLog;
            try
            {
                var m = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 81, new[] {
                    new BallLoadout(WeaponRegistry.Create(AegisRule.WeaponId)) { SkillLevel = 1 },
                    new BallLoadout(WeaponRegistry.Create(BrawlerRule.WeaponId)) { SkillLevel = 1 } });
                var expected = new List<string>();
                var fires = 0;
                while (m.Outcome == MatchOutcome.Ongoing)
                {
                    m.Step();
                    foreach (var e in m.Events)
                    {
                        var line = AudioLog.ReflectLine(e, m);
                        if (line != null) expected.Add(line);
                        if (e.Type == SimEventType.SkillFired) fires++;
                        d.OnSimEvent(e, m);                         // -bbAudioLog path: AudioLog.Write = console + audio_log.txt
                        ArenaView.TraceReflect(e, m, trace: true);  // -bbTrace path, as ArenaView runs it for every event
                    }
                }
                Assert.That(expected, Is.Not.Empty, "fixture: MIRROR reflects in this match");
                Assert.That(reflectConsole, Is.EqualTo(expected), "each [Reflect] line exactly once in the console");
                if (audioLog) Assert.That(d.LogLines, Has.Member(expected.Last()), "the audio log path still writes it (audio_log.txt unchanged)");
                Assert.That(fires, Is.GreaterThan(0), "fixture: skills fire");
                Assert.That(skillConsole, Is.EqualTo(audioLog ? fires : 0), "[Skill] has a single path (the trace prints '[BallBattle] SkillFired ...')");
            }
            finally
            {
                Application.logMessageReceived -= onLog;
            }
        }

        // ---------------- screenshots (BB_FIX4_OUT) ----------------

        static string OutDir()
        {
            var dir = Environment.GetEnvironmentVariable("BB_FIX4_OUT");
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            return string.IsNullOrEmpty(dir) ? null : dir;
        }

        static void Shoot(Camera cam, string dir, string name)
        {
            foreach (var zoom in new[] { 1, 3 })
            {
                var rt = new RenderTexture(270 * zoom, 480 * zoom, 24) { filterMode = FilterMode.Point };
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(dir, $"{name}-{zoom}x.png"), tex.EncodeToPNG());
                RenderTexture.active = null;
                cam.targetTexture = null;
                Object.DestroyImmediate(tex);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }

        [Test]
        public void CaptureFix4Screens()
        {
            var dir = OutDir();
            if (dir == null) Assert.Ignore("BB_FIX4_OUT not set");
            var camGo = new GameObject("CaptureCam");
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 240f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Palette.Background;
                camGo.transform.position = new Vector3(0f, 0f, -10f);

                // 1. Loss result on a boss fight (fight 4, SHELL KING, retry left).
                var flow = NewFlow();
                StartRun(flow, 31, 3, BossIds.ShellKing);
                LoseFight(flow);
                var result = RunResultView.Create(root.transform, art);
                result.Show(flow.LastReport);
                Shoot(cam, dir, "1-result-loss-shellking");
                result.Hide();

                // 2. CHOOSE YOUR RETRY after that loss.
                flow.NextAfterResult();
                Assert.That(flow.Current, Is.EqualTo(RunFlow.State.StartPick));
                var pick = StartPickView.Create(root.transform, art);
                pick.Show(flow.Run);
                Shoot(cam, dir, "2-retry-shellking");
                pick.Hide();

                // 3. WARDEN loss result and retry (the hint GT quoted: MOVING WALLS SPIKE YOU).
                var flowW = NewFlow();
                StartRun(flowW, 31, 7, BossIds.Warden);
                LoseFight(flowW);
                result.Show(flowW.LastReport);
                Shoot(cam, dir, "3-result-loss-warden");
                result.Hide();
                flowW.NextAfterResult();
                pick.Show(flowW.Run);
                Shoot(cam, dir, "4-retry-warden");
                pick.Hide();

                // 5. SKILL UP branch buttons on Prepare (after one reroll), then 6. the TAKEN card after picking branch A.
                var run = PrepareRun();
                var prep = PrepareView.Create(root.transform, art);
                prep.Show(run);
                run.Reroll();
                SkillOffer(run);
                prep.Show(run);
                Shoot(cam, dir, "5-skillup-buttons-prepare");
                run.Pick(2, SkillBranch.A);
                prep.Show(run);
                Shoot(cam, dir, "6-skillup-taken-branch-a");
                prep.Hide();

                // 7. SKILL UP branch buttons on a start pick card.
                var sp = RunState.Start(23, RunState.StartChoices(23, UnlockRules.Weapons(new ProgressData()))[0], new ProgressData());
                sp.OfferCards[1] = OfferSlot.From(new Card(CardKind.SkillUp, sp.Build.WeaponId, 2));
                pick.Show(sp);
                Shoot(cam, dir, "7-skillup-buttons-startpick");
                pick.Hide();

                // 8. The 140 px START button (Versus page), unchanged by the icon fix.
                var menu = MenuView.Create(root.transform, art);
                menu.gameObject.SetActive(true);
                menu.ShowVersus();
                Shoot(cam, dir, "8-versus-start-button-140px");
            }
            finally
            {
                Object.DestroyImmediate(camGo);
            }
        }
    }
}
