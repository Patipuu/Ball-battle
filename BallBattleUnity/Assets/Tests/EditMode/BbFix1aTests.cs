using System.Linq;
using BallBattle.EditorTools;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BallBattle.Tests
{
    /// <summary>BB-FIX1a: v1 run migration + one-time notice, bounded Played log, volume save on release/close, spike zones in the view.</summary>
    public class BbFix1aTests
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
            root = new GameObject("BbFix1aRoot");
            now = 0f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        // ---- A. v1 run dropped by the update ----

        const string V1WithRun =
            "{\"Version\":1,\"Progress\":{\"Achievements\":[\"boss1\"],\"RunsPlayed\":3,\"RunsWon\":0,\"BestFight\":4},\"HasRun\":true,\"Run\":{\"FightIndex\":4,\"Coins\":7}}";
        const string V1WithoutRun =
            "{\"Version\":1,\"Progress\":{\"Achievements\":[\"boss1\"],\"RunsPlayed\":3,\"RunsWon\":0,\"BestFight\":4},\"HasRun\":false}";

        [Test]
        public void V1RunAtFightFiveIsDroppedWithCompensationAndNotice()
        {
            var before = new ProgressData { BestFight = 4, RunsPlayed = 3 };
            before.Achievements.Add(UnlockRules.Boss1);
            var store = new MemorySaveStore { Json = V1WithRun };
            var d = store.Load();
            Assert.That(d.Version, Is.EqualTo(SaveData.CurrentVersion), "no version bump beyond 2");
            Assert.That(d.HasRun, Is.False, "run dropped");
            Assert.That(d.Progress.BestFight, Is.EqualTo(5), "reached fight 5 counts");
            Assert.That(d.Progress.RunsPlayed, Is.EqualTo(3), "not counted as played");
            Assert.That(d.Progress.RunsWon, Is.EqualTo(0));
            Assert.That(d.Progress.Achievements, Is.EqualTo(new[] { UnlockRules.Boss1 }), "achievements unchanged");
            Assert.That(UnlockRules.Weapons(d.Progress), Is.EqualTo(UnlockRules.Weapons(before)), "unlocks unchanged");
            Assert.That(UnlockRules.Traits(d.Progress), Is.EqualTo(UnlockRules.Traits(before)));
            var goal = UnlockRules.NextGoal(d.Progress);
            Assert.That((goal.GoalId, goal.Current, goal.Max), Is.EqualTo((UnlockRules.Win1, 4, 8)), "next goal re-evaluated from the new BestFight");
            Assert.That(d.oldRunNotice, Is.True);
        }

        [Test]
        public void V1WithoutRunSetsNoNotice()
        {
            var d = new MemorySaveStore { Json = V1WithoutRun }.Load();
            Assert.That(d.oldRunNotice, Is.False);
            Assert.That(d.Progress.BestFight, Is.EqualTo(4));
        }

        [Test]
        public void LowerFightKeepsBestFight()
        {
            var d = new MemorySaveStore { Json = V1WithRun.Replace("\"FightIndex\":4", "\"FightIndex\":1") }.Load();
            Assert.That(d.Progress.BestFight, Is.EqualTo(4));
            Assert.That(d.oldRunNotice, Is.True);
        }

        [Test]
        public void MenuShowsTheNoticeOnceAndOkClearsItForGood()
        {
            var store = new MemorySaveStore { Json = V1WithRun };
            var menu = MenuView.Create(root.transform, art);
            Assert.That(OldRunNotice.Pending(store), Is.True);
            menu.ShowOldRunNotice(() => OldRunNotice.Dismiss(store));
            Assert.That(menu.NoticeOpen, Is.True);
            Assert.That(menu.GetComponentsInChildren<PixelText>(false).Select(t => t.Text), Does.Contain(RunStrings.OldRunEnded));
            Bb0102Tests.AssertTextFitsAndNoOverlap(menu.transform);

            menu.NoticeOkButton.SimulateClick();
            Assert.That(menu.NoticeOpen, Is.False);
            Assert.That(OldRunNotice.Pending(store), Is.False);
            Assert.That(store.Saves, Is.EqualTo(1));

            var restarted = new MemorySaveStore { Json = store.Json };   // "restart": same file, fresh load
            Assert.That(OldRunNotice.Pending(restarted), Is.False, "never again after restart");
            var d = restarted.Load();
            Assert.That(d.Progress.BestFight, Is.EqualTo(5), "compensation was saved");
            Assert.That(d.HasRun, Is.False);
        }

        [Test]
        public void OldRunNoticeTextHasNoComma() => Assert.That(RunStrings.OldRunEnded, Does.Not.Contain(","));

        // ---- B. Played is bounded ----

        [Test]
        public void PlayedKeepsOnlyTheNewest256()
        {
            var d = AudioDirector.Create(root.transform, lib, new MemorySaveStore());
            d.Clock = () => now;
            for (var i = 0; i < 1000; i++)
            {
                now = i;
                d.RequestCue(AudioIds.Parry, i);
            }
            Assert.That(d.Played.Count, Is.LessThanOrEqualTo(256));
            Assert.That(d.Played.Last().Time, Is.EqualTo(999f), "newest kept");
            Assert.That(d.Played.First().Time, Is.EqualTo(1000f - d.Played.Count), "oldest dropped, order kept");
        }

        // ---- C. volume save on release / close only ----

        [Test]
        public void VolumeIsSavedOnReleaseAndCloseNotPerStep()
        {
            var store = new MemorySaveStore();
            var audio = AudioDirector.Create(root.transform, lib, store);
            audio.Clock = () => now;
            var menu = MenuView.Create(root.transform, art);
            SettingsAudio.Bind(menu, audio, store);
            menu.ShowSettings();
            var start = store.Saves;

            for (var step = 1; step <= 5; step++)
                menu.Settings.DragSfxTo(SettingsView.SfxArea.xMin + (step - 1) * SettingsView.CellWidth + 10);
            Assert.That(audio.SfxVolume, Is.EqualTo(5), "volume changes live");
            Assert.That(store.Saves - start, Is.EqualTo(0), "no write while dragging");

            menu.Settings.ReleasePointer();
            Assert.That(store.Saves - start, Is.EqualTo(1), "one write on release");
            Assert.That(store.Load().sfxVol, Is.EqualTo(5));

            menu.ShowHub();
            Assert.That(store.Saves - start, Is.EqualTo(1), "closing without a change writes nothing");

            menu.ShowSettings();
            menu.Settings.DragMusicTo(SettingsView.MusicArea.xMin + 2 * SettingsView.CellWidth + 10);
            menu.ShowHub();
            Assert.That(store.Saves - start, Is.EqualTo(2), "a change still unsaved is written when Settings closes");
            Assert.That(store.Load().musicVol, Is.EqualTo(3));
        }

        // ---- D. spike zones in the view ----

        [Test]
        public void ZoneSegmentsAreOneFifthOfTheCurrentWalls()
        {
            var config = ArenaRegistry.Create(ArenaRegistry.SpikeWalls);
            foreach (var tick in new[] { 0, config.ShrinkStartTick + config.ShrinkDurationTicks / 2, config.ShrinkStartTick + config.ShrinkDurationTicks })
            {
                var r = config.ArenaAt(tick);
                var zones = ArenaFrameView.ZoneSegments(config.Layout.SpikeCorners, r.Left, r.Bottom, r.Right, r.Top);
                Assert.That(zones.Length, Is.EqualTo(4), "TL + BR, two walls each");
                foreach (var z in zones)
                {
                    var wall = z.Horizontal ? r.Width : r.Height;
                    Assert.That(z.Length, Is.EqualTo(wall / 5f).Within(1e-3f), $"{z.Wall} at tick {tick}");
                }
                Assert.That(zones.Any(z => z.Wall == ArenaWall.Top && Mathf.Approximately(z.From, r.Left)), "TL on the top wall");
                Assert.That(zones.Any(z => z.Wall == ArenaWall.Left && Mathf.Approximately(z.To, r.Top)), "TL on the left wall");
                Assert.That(zones.Any(z => z.Wall == ArenaWall.Bottom && Mathf.Approximately(z.To, r.Right)), "BR on the floor");
                Assert.That(zones.Any(z => z.Wall == ArenaWall.Right && Mathf.Approximately(z.From, r.Bottom)), "BR on the right wall");
            }
            Assert.That(ArenaFrameView.ZoneSegments(SpikeCorner.All, -100, -100, 100, 100).Length, Is.EqualTo(8));
        }

        [Test]
        public void FrameDrawsTeethOnlyInTheZonesAndFollowsTheShrink()
        {
            var config = ArenaRegistry.Create(ArenaRegistry.SpikeWalls);
            var frame = ArenaFrameView.Create(root.transform, art);
            foreach (var tick in new[] { 0, config.ShrinkStartTick + config.ShrinkDurationTicks })
            {
                var r = config.ArenaAt(tick);
                frame.Render(r, tick, config, ArenaRegistry.SpikeWalls);
                var zones = ArenaFrameView.ZoneSegments(config.Layout.SpikeCorners, Mathf.Round(r.Left), Mathf.Round(r.Bottom), Mathf.Round(r.Right), Mathf.Round(r.Top));
                var expected = zones.Sum(z => Mathf.FloorToInt(z.Length / ArenaFrameView.ToothSpacing));
                Assert.That(frame.TeethShown, Is.EqualTo(expected), $"tick {tick}");
                var all4 = 2 * Mathf.FloorToInt(r.Width / ArenaFrameView.ToothSpacing) + 2 * Mathf.FloorToInt(r.Height / ArenaFrameView.ToothSpacing);
                Assert.That(frame.TeethShown, Is.LessThan(all4 / 2), "not along whole walls");
            }
            art.TryArena(ArenaRegistry.SpikeWalls, out var look);
            frame.Render(config.ArenaAt(0), 0, config, ArenaRegistry.SpikeWalls);
            Assert.That(frame.WallColor, Is.EqualTo(look.WallColor), "rest of the wall: the arena's normal look");
        }

        [Test]
        public void SpikeBounceUsesSpikeHitOnlyWhenTheClipExists()
        {
            Assert.That(AudioDirector.WallBounceCue(3f, id => id == AudioIds.SpikeHit), Is.EqualTo(AudioIds.SpikeHit));
            Assert.That(AudioDirector.WallBounceCue(3f, id => false), Is.EqualTo(AudioIds.Wall), "no clip: plain wall");
            Assert.That(AudioDirector.WallBounceCue(0f, id => true), Is.EqualTo(AudioIds.Wall), "harmless bounce");
            Assert.That(lib.Has(AudioIds.SpikeHit), Is.False, "no spike_hit clip added");
            var d = AudioDirector.Create(root.transform, lib, new MemorySaveStore());
            d.OnSimEvent(new SimEvent { Type = SimEventType.WallBounce, Tick = 4, A = 0, B = -1, Value = 3f }, null);
            Assert.That(d.Played.Select(p => p.Id), Is.EqualTo(new[] { AudioIds.Wall }), "one cue, never both");
        }
    }
}
