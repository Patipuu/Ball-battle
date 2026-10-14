using System.Linq;
using BallBattle.EditorTools;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BallBattle.Tests
{
    /// <summary>BB-FIX1b: reroll reason line, EN/VI string table (start pick hint, boss line, retry line), Versus draw audio,
    /// heat pitch caps, unlock names, summary goal count, BACK buttons, old-run notice over a drawn menu.</summary>
    public class BbFix1bTests
    {
        GameObject root;
        ArtLibrary art;
        SfxLibrary lib;
        float now;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Art/ArtLibrary.asset");
            lib = AssetDatabase.LoadAssetAtPath<SfxLibrary>(PlaceholderSfxGenerator.LibraryPath);
            Assert.That(art, Is.Not.Null);
            Assert.That(lib, Is.Not.Null);
            root = new GameObject("BbFix1bRoot");
            now = 0f;
            StringTable.Current = Lang.En;
        }

        [TearDown]
        public void TearDown()
        {
            StringTable.Current = Lang.En;
            Object.DestroyImmediate(root);
        }

        AudioDirector NewDirector()
        {
            var d = AudioDirector.Create(root.transform, lib, new MemorySaveStore());
            d.Clock = () => now;
            return d;
        }

        // ---- 11. spike_hit: linked, and a spike contact plays it instead of wall (never both) ----

        [Test]
        public void SpikeContactPlaysSpikeHitAndNotWall()
        {
            Assert.That(lib.Has(AudioIds.SpikeHit), Is.True, "spike_hit.wav linked into SfxLibrary");
            Assert.That(lib.Get(AudioIds.SpikeHit).name, Is.EqualTo(AudioIds.SpikeHit));
            var sim = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 5,
                new[] { new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)), new BallLoadout(WeaponRegistry.Create(FangRule.WeaponId)) });
            var d = NewDirector();

            d.OnSimEvent(new SimEvent { Type = SimEventType.WallBounce, Tick = 40, A = 0, Value = TFix.X(3f) }, sim);   // in a spike zone
            var spikeTick = d.Played.Select(p => p.Id).ToList();
            Assert.That(spikeTick, Is.EqualTo(new[] { AudioIds.SpikeHit }), "spike_hit only, no wall");

            d.OnSimEvent(new SimEvent { Type = SimEventType.WallBounce, Tick = 41, A = 0, Value = TFix.X(0f) }, sim);   // plain wall
            Assert.That(d.Played.Skip(1).Select(p => p.Id), Is.EqualTo(new[] { AudioIds.Wall }), "plain wall stays wall");
        }

        // ---- 1. NOT ENOUGH COINS: one small red line 2 px under REROLL ----

        [Test]
        public void NotEnoughCoinsIsOneSmallRedLineTwoPixelsBelowReroll()
        {
            var run = RunState.Start(53, BladeRule.WeaponId, new ProgressData());
            run.Pick(0);   // the start pick; this Prepare has no pick yet
            run.Coins = 0;
            var v = PrepareView.Create(root.transform, art);
            v.Show(run);

            var line = v.RerollReasonText;
            Assert.That(line.Text, Is.EqualTo("NOT ENOUGH COINS"));
            Assert.That(line.TextColor, Is.EqualTo(Palette.Hot), "red");
            Assert.That(line.TextScale, Is.EqualTo(1), "small");
            Assert.That(line.Text, Does.Not.Contain("\n"), "one line");
            var b = line.WorldBounds;
            var r = PrepareView.RerollArea;
            Assert.That(b.yMax, Is.EqualTo(r.yMin - 2f), "top of the line 2 px below the button");
            Assert.That(b.Overlaps(r), Is.False, "never over the REROLL button");
            Assert.That(b.Overlaps(PrepareView.FightArea), Is.False);
            Assert.That(b.xMin >= -135f && b.xMax <= 135f, Is.True, "on screen");
            var others = v.GetComponentsInChildren<PixelText>(false).Where(t => t != line && t.Text.Trim().Length > 0);
            foreach (var t in others) Assert.That(b.Overlaps(t.WorldBounds), Is.False, $"overlaps '{t.Text}'");
        }

        // ---- 2. Start pick hint ----

        [Test]
        public void StartPickHintComesFromTheStringTable()
        {
            var run = RunState.Start(53, BladeRule.WeaponId, new ProgressData());
            var v = StartPickView.Create(root.transform, art);
            v.Show(run);
            Assert.That(v.NoRerollText.Text, Is.EqualTo("NO REROLL ON START PICK"));
            Assert.That(StringTable.Get(StringKeys.StartPickNoReroll, Lang.En), Is.EqualTo("NO REROLL ON START PICK"));
            Assert.That(StringTable.Get(StringKeys.StartPickNoReroll, Lang.Vi), Is.EqualTo("KHÔNG ĐỔI THẺ Ở LƯỢT ĐẦU"));
        }

        [Test]
        public void EveryNewKeyIsInBothTables()
        {
            foreach (var k in StringKeys.All)
            {
                Assert.That(StringTable.Has(k, Lang.En), Is.True, $"EN {k}");
                Assert.That(StringTable.Has(k, Lang.Vi), Is.True, $"VI {k}");
            }
        }

        [Test]
        public void ParserSplitsAtTheFirstCommaAndSkipsHeaderAndComments()
        {
            var t = StringTable.Parse("﻿key,text\r\n# note\r\n\r\na,ONE, TWO\r\nb,X  Y\n");
            Assert.That(t.Count, Is.EqualTo(2));
            Assert.That(t["a"], Is.EqualTo("ONE, TWO"));
            Assert.That(t["b"], Is.EqualTo("X  Y"), "double spaces kept");
            Assert.That(StringTable.Get("no.such.key", Lang.Vi), Is.EqualTo("no.such.key"));
        }

        // ---- 3. Boss line from the boss fight list ----

        [Test]
        public void BossLineEnglishForOneTwoAndThreeBosses()
        {
            Assert.That(RunStrings.BossAtFor(new[] { 3 }, Lang.En), Is.EqualTo("BOSS AT FIGHT 4"));
            Assert.That(RunStrings.BossAtFor(new[] { 3, 7 }, Lang.En), Is.EqualTo("BOSS AT FIGHTS 4 AND 8"));
            Assert.That(RunStrings.BossAtFor(new[] { 3, 5, 7 }, Lang.En), Is.EqualTo("BOSS AT FIGHTS 4  6  8"));
        }

        [Test]
        public void BossLineVietnameseForOneTwoAndThreeBosses()
        {
            Assert.That(RunStrings.BossAtFor(new[] { 3 }, Lang.Vi), Is.EqualTo("BOSS Ở TRẬN 4"));
            Assert.That(RunStrings.BossAtFor(new[] { 3, 7 }, Lang.Vi), Is.EqualTo("BOSS Ở TRẬN 4 VÀ 8"));
            Assert.That(RunStrings.BossAtFor(new[] { 3, 5, 7 }, Lang.Vi), Is.EqualTo("BOSS Ở TRẬN 4  6  8"));
        }

        [Test]
        public void BossLineUsesTheTuningListAndNeverASlash()
        {
            Assert.That(RunStrings.BossAt, Is.EqualTo(RunStrings.BossAtFor(RunTuning.BossFights, Lang.En)));
            foreach (var lang in new[] { Lang.En, Lang.Vi })
                foreach (var list in new[] { new[] { 3 }, new[] { 3, 7 }, new[] { 3, 5, 7 }, new[] { 1, 3, 5, 7 } })
                {
                    var s = RunStrings.BossAtFor(list, lang);
                    Assert.That(s, Does.Not.Contain("/"), s);
                    if (list.Length >= 3) Assert.That(s, Does.Not.Contain(" AND ").And.Not.Contain(" VÀ "), s);
                }
        }

        // ---- 4. VI retry line ----

        [Test]
        public void VietnameseRetryLineIsExact()
        {
            Assert.That(RunStrings.LoseOnceIn(Lang.Vi), Is.EqualTo("THUA LẦN 1: CHỌN THẺ MIỄN PHÍ  ĐẤU LẠI VỚI 60% HP"));
            Assert.That(RunStrings.LoseOnceIn(Lang.En), Is.EqualTo("LOSE ONCE: FREE PICK  RETRY AT 60% HP"));
        }

        // ---- 5. Versus draw: no win/lose cue, only music_result ----

        [Test]
        public void VersusDrawPlaysNoWinOrLoseCueOnlyResultMusic()
        {
            var d = NewDirector();
            d.SetScreen(MusicScreen.Fight);
            d.OnMatchEnded(-1, -1);
            for (var i = 0; i < 20; i++) { now += 0.1f; d.Tick(0.1f); }
            Assert.That(d.Played.Select(p => p.Id), Has.None.EqualTo(AudioIds.FightWin).And.None.EqualTo(AudioIds.FightLose));
            d.SetScreen(MusicScreen.VersusResult);
            Assert.That(d.Music.Current, Is.EqualTo(AudioIds.MusicResult));

            // Control: a decided Versus round still plays fight_win.
            d.SetScreen(MusicScreen.Fight);
            d.OnMatchEnded(1, -1);
            for (var i = 0; i < 20; i++) { now += 0.1f; d.Tick(0.1f); }
            Assert.That(d.Played.Count(p => p.Id == AudioIds.FightWin), Is.EqualTo(1));
            Assert.That(d.Played.Select(p => p.Id), Has.None.EqualTo(AudioIds.FightLose));
        }

        // ---- 6. Heat pitch on the weapon's own hit clip, capped ----

        [Test]
        public void HeatPitchCapsAreTwelveAndEightPercent()
        {
            Assert.That(AudioIds.HeatPitchCap, Is.EqualTo(0.12f));
            Assert.That(AudioIds.HeatPitchCapRapid, Is.EqualTo(0.08f));
            foreach (var id in AudioIds.HitWeapons)
            {
                var cap = id == VolleyRule.WeaponId || id == RigRule.WeaponId ? 0.08f : 0.12f;
                Assert.That(AudioIds.HeatPitchCapFor(id), Is.EqualTo(cap), id);
                Assert.That(AudioIds.HeatPitch(id, 0f), Is.EqualTo(1f), id + " cold");
                Assert.That(AudioIds.HeatPitch(id, 1f), Is.EqualTo(1f + cap).Within(1e-6f), id + " hot");
                Assert.That(AudioIds.HeatPitch(id, 5f), Is.EqualTo(1f + cap).Within(1e-6f), id + " clamped");
                Assert.That(AudioIds.HeatPitch(id, 0.5f), Is.EqualTo(1f + cap * 0.5f).Within(1e-6f), id + " half");
            }
        }

        [Test]
        public void HotHitsUseTheWeaponClipAndNeverExceedTheCapWithJitter()
        {
            var d = NewDirector();
            foreach (var id in new[] { BladeRule.WeaponId, VolleyRule.WeaponId, RigRule.WeaponId })
            {
                var cap = AudioIds.HeatPitchCapFor(id);
                var start = d.Played.Count;
                for (var t = 0; t < 200; t++) d.RequestHit(id, 10000 * (start + 1) + t, 0.6f, 1f);
                var hits = d.Played.Skip(System.Math.Max(0, d.Played.Count - 200)).ToList();
                Assert.That(hits.All(p => p.Id == "hit_" + id), Is.True, "own clip");
                Assert.That(hits.Max(p => p.Pitch), Is.LessThanOrEqualTo(1f + cap + 1e-6f), id + " cap");
                Assert.That(hits.Min(p => p.Pitch), Is.GreaterThanOrEqualTo((1f + cap) * (1f - AudioDirector.PitchJitter) - 1e-6f), id + " heat applied");
            }
        }

        [Test]
        public void HitEventPitchFollowsTheAttackersHeat()
        {
            var sim = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 5,
                new[] { new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)), new BallLoadout(WeaponRegistry.Create(RigRule.WeaponId)) });
            var e = new SimEvent { Type = SimEventType.Hit, Tick = 12, A = 0, B = 1, Value = TFix.X(4f) };
            var w = sim.Balls[0].Weapon;
            Assert.That(AudioDirector.HeatOf(e, sim), Is.EqualTo(Palette.Heat(w.Id, w.StatValue.ToFloat())));
            var d = NewDirector();
            d.OnSimEvent(e, sim);
            var p = d.Played.Last();
            Assert.That(p.Id, Is.EqualTo("hit_blade"));
            var expected = AudioIds.HeatPitch(BladeRule.WeaponId, AudioDirector.HeatOf(e, sim));
            Assert.That(p.Pitch, Is.InRange(expected * (1f - AudioDirector.PitchJitter) - 1e-5f, System.Math.Min(expected * (1f + AudioDirector.PitchJitter), 1.12f) + 1e-5f));
        }

        // ---- 7. Unlock names = current ball names ----

        [Test]
        public void UnlockedNamesUseTheCurrentBallNames()
        {
            var current = new[] { "EMBER", "MANTIS", "ANGLER", "WRECKER", "SPARKLER", "SPORE", "MIRROR", "HIVE" };
            Assert.That(RunStrings.UnlockedNames(UnlockRules.Boss2), Does.Contain("SPARKLER"));
            Assert.That(RunStrings.UnlockedNames(UnlockRules.Win1), Does.Contain("MIRROR").And.Contain("SPORE"));
            Assert.That(RunStrings.UnlockedNames(UnlockRules.Win3), Does.Contain("HIVE"));
            foreach (var a in new[] { UnlockRules.Boss1, UnlockRules.Boss2, UnlockRules.Boss3, UnlockRules.Win1, UnlockRules.Win3 })
                foreach (var n in RunStrings.UnlockedNames(a))
                    foreach (var w in WeaponRegistry.All)
                        if (n == w.DisplayName && !current.Contains(n))
                            Assert.Fail($"{a}: old registry name {n}");
            Assert.That(RunStrings.UnlockedNames(null), Is.Empty);
        }

        // ---- 8. Summary goal count from data ----

        [Test]
        public void SummaryGoalCountComesFromTheGoalTable()
        {
            var max = UnlockRules.GoalRowOf(UnlockRules.Win3).Max;
            Assert.That(RunStrings.GoalTitle(UnlockRules.Win3), Is.EqualTo($"WIN {max} RUNS"));

            var run = RunState.Start(77, BladeRule.WeaponId, new ProgressData());
            run.Pick(0);
            for (var i = 0; i < RunTuning.Fights; i++) run.History.Add(new FightRecord { FightIndex = i, Won = true });
            run.FightIndex = RunTuning.Fights;
            run.Over = run.Won = true;
            run.Achievements.Add(UnlockRules.Win3);
            var after = new ProgressData { RunsPlayed = max, RunsWon = max, BestFight = RunTuning.Fights };
            after.Achievements.AddRange(new[] { UnlockRules.Boss1, UnlockRules.Boss2, UnlockRules.Win1, UnlockRules.Win3 });
            var v = RunSummaryView.Create(root.transform, art);
            v.Show(run, after);
            Assert.That(v.BoxText, Does.Contain($"WIN {max} RUNS"));
            Assert.That(v.BossesText.Text, Does.EndWith("/" + RunTuning.BossFights.Length));
        }

        // ---- 9. BACK buttons ----

        [Test]
        public void SettingsBackIsPrimaryAndEveryBackHasTheSameReadableSize()
        {
            var menu = MenuView.Create(root.transform, art);
            var title = RunTitleView.Create(root.transform, art);
            var backs = new[] { menu.Settings.BackButton, menu.VersusBackButton, title.BackButton };
            foreach (var b in backs)
            {
                Assert.That(b.LabelText, Is.EqualTo(RunStrings.Back));
                Assert.That(b.LabelScale, Is.EqualTo(backs[0].LabelScale), b.name);
                Assert.That(b.LabelScale, Is.GreaterThanOrEqualTo(2), "readable: 10 px tall");
                Assert.That(PixelFont.MeasureWidth(RunStrings.Back) * b.LabelScale, Is.LessThan(b.Area.width - 4f), "fits");
                Assert.That(PixelFont.CellHeight * b.LabelScale, Is.LessThan(b.Area.height - 4f), "fits");
            }
            var s = menu.Settings.BackButton;
            Assert.That(s.FillColor, Is.EqualTo(new Color32(0xF4, 0xC5, 0x42, 255)));
            var c = s.LabelColor;
            Assert.That(0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b, Is.LessThan(60f), "dark text");
        }

        // ---- 10. Old-run notice over a drawn menu, once ----

        [Test]
        public void OldRunNoticeShowsOverTheDrawnMenuAndOnlyOnce()
        {
            var store = new MemorySaveStore
            {
                Json = "{\"Version\":1,\"Progress\":{\"Achievements\":[\"boss1\"],\"RunsPlayed\":3,\"RunsWon\":0,\"BestFight\":4},\"HasRun\":true,\"Run\":{\"FightIndex\":4,\"Coins\":7}}",
            };
            var menu = MenuView.Create(root.transform, art);
            menu.Show(true);
            Assert.That(OldRunNotice.Pending(store), Is.True);
            menu.ShowOldRunNotice(() => OldRunNotice.Dismiss(store));

            Assert.That(menu.NoticeOpen, Is.True);
            Assert.That(menu.HubVisible, Is.True, "menu behind the notice is drawn");
            var hubTexts = menu.Hub.GetComponentsInChildren<PixelText>(false).Select(t => t.Text).ToList();
            Assert.That(hubTexts, Does.Contain("BALL BATTLE").And.Contain(RunStrings.Run).And.Contain(RunStrings.Versus));
            var hubSprites = menu.Hub.GetComponentsInChildren<SpriteRenderer>(false).Where(r => r.enabled).ToList();
            Assert.That(hubSprites, Is.Not.Empty);
            var noticeMin = menu.NoticePage.GetComponentsInChildren<SpriteRenderer>(false).Min(r => r.sortingOrder);
            Assert.That(hubSprites.Max(r => r.sortingOrder), Is.LessThan(noticeMin), "notice drawn on top");
            Assert.That(menu.RunButton.enabled, Is.False, "hub takes no taps under the notice");
            Assert.That(menu.NoticeOkButton.enabled, Is.True);

            menu.NoticeOkButton.SimulateClick();
            Assert.That(menu.NoticeOpen, Is.False);
            Assert.That(menu.HubVisible, Is.True);
            Assert.That(menu.RunButton.enabled, Is.True, "hub input back after OK");
            Assert.That(OldRunNotice.Pending(store), Is.False, "once");
            Assert.That(OldRunNotice.Pending(new MemorySaveStore { Json = store.Json }), Is.False, "not after a restart either");
        }
    }
}
