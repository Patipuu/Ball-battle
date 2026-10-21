using BallBattle.EditorTools;
using BallBattle.Sim.Run;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BallBattle.Tests
{
    /// <summary>HEAT picker on the Run title, heat-aware runs, and the chapter backdrop around the arena (21/10).</summary>
    public class HeatAndBackdropTests
    {
        GameObject root;
        ArtLibrary art;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("HeatAndBackdropTests");
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        ArenaView NewArena()
        {
            var arena = root.AddComponent<ArenaView>();
            arena.Art = art;
            arena.AutoRestart = false;
            return arena;
        }

        [Test]
        public void TitleShowsHeatStepsAndLocksTheUnwonOnes()
        {
            var rc = RunController.Create(root.transform, NewArena(), new MemorySaveStore());
            rc.Open();
            rc.Flow.Progress.RunsPlayed = 1;   // past the tutorial run
            var title = rc.Title;
            title.Show(rc.Flow);
            Assert.That(title.SelectedHeat, Is.Zero);
            Assert.That(title.SelectedHeatOpen, Is.True);
            title.SelectHeat(1);
            Assert.That(title.SelectedHeatOpen, Is.False, "heat 2 opens after a heat 1 win");
            Assert.That(title.HeatButton(1).BorderColor, Is.EqualTo((Color32)Palette.Wall));

            rc.Flow.Progress.ChapterHeat.Add(1);   // heat 1 of chapter 1 won
            rc.Flow.Progress.ChaptersCleared = 1;
            title.Show(rc.Flow);
            title.SelectChapter(0);
            Assert.That(title.SelectedHeat, Is.EqualTo(1), "defaults to the highest open heat");
            Assert.That(title.SelectedHeatOpen, Is.True);
            title.SelectHeat(2);
            Assert.That(title.SelectedHeatOpen, Is.False);
            title.SelectHeat(1);
            title.PressNewRun();
            rc.Flow.ChooseWeapon(rc.Flow.StartChoices[0]);
            Assert.That(rc.Flow.Run.Heat, Is.EqualTo(1));
            Assert.That(rc.Flow.Run.Chapter, Is.Zero);
        }

        [Test]
        public void TitleRowsDoNotOverlapAndEveryLineFits()
        {
            var previous = StringTable.Current;
            var title = RunTitleView.Create(root.transform, art);
            var areas = new[] { RunTitleView.NewRunArea, RunTitleView.PrevChapterArea, RunTitleView.NextChapterArea, RunTitleView.BackArea,
                RunTitleView.HeatAreas[0], RunTitleView.HeatAreas[1], RunTitleView.HeatAreas[2] };
            for (var i = 0; i < areas.Length; i++)
            for (var j = i + 1; j < areas.Length; j++)
                Assert.That(areas[i].Overlaps(areas[j]), Is.False, $"{i} vs {j}");
            for (var h = 0; h < HeatRules.Count; h++)
            {
                Assert.That(TextWrap.FitsScreen(RunStrings.HeatRule(h), 1), Is.True, RunStrings.HeatRule(h));
                Assert.That(TextWrap.FitsScreen(RunStrings.HeatReward(h), 1), Is.True, RunStrings.HeatReward(h));
                Assert.That(PixelFont.MeasureWidth(RunStrings.HeatName(h)), Is.LessThan(RunTitleView.HeatAreas[h].width - 4f));
            }
            StringTable.Current = Lang.Vi;
            try
            {
            Assert.That(UiVietnamese.Translate(RunStrings.HeatRule(1)), Is.EqualTo("ĐỊCH +1 TRAIT, +10% MÁU"));
            Assert.That(UiVietnamese.Translate(RunStrings.HeatUnlocked(0, 1)), Is.EqualTo("CHƯƠNG 1: MỞ HEAT 2!"));
            }
            finally { StringTable.Current = previous; }
        }

        [Test]
        public void RunFightsDrawTheChapterBackdropAndVersusDoesNot()
        {
            var arena = NewArena();
            var rc = RunController.Create(root.transform, arena, new MemorySaveStore());
            rc.Open();
            rc.Flow.Progress.RunsPlayed = 1;
            rc.Flow.Progress.ChaptersCleared = 3;
            rc.Flow.NewRun(321, false, 2);
            rc.Flow.ChooseWeapon("blade");
            rc.Flow.PickStart(0);
            rc.Flow.Fight();
            rc.Tick(3f);
            Assert.That(rc.Flow.Run.Chapter, Is.EqualTo(2));
            Assert.That(arena.ChapterIndex, Is.EqualTo(2));
            Assert.That(arena.Frame.BackdropShown, Is.EqualTo(2));
            var hole = arena.Frame.BackdropHole;
            Assert.That(hole.width, Is.GreaterThan(150), "the arena stays a transparent hole");
            Assert.That(hole.Contains(Vector2Int.zero), Is.True);

            rc.Flow.Close();
            Assert.That(arena.ChapterIndex, Is.EqualTo(-1));
        }

        [Test]
        public void EveryChapterHasItsOwnDarkBackdrop()
        {
            var seen = new System.Collections.Generic.HashSet<Color32>();
            for (var c = 0; c < ChapterBackdrop.Count; c++)
            {
                var px = ChapterBackdrop.Pattern(c);
                Assert.That(px.Length, Is.EqualTo(ChapterBackdrop.Width * ChapterBackdrop.Height));
                Assert.That(seen.Add(ChapterBackdrop.BaseColor(c)), Is.True, "distinct base colours");
                long sum = 0; var detail = 0;
                foreach (var p in px)
                {
                    sum += p.r + p.g + p.b;
                    if (!p.Equals(ChapterBackdrop.BaseColor(c))) detail++;
                }
                Assert.That(sum / px.Length / 3f, Is.LessThan(60f), "dark enough for the HUD text on top");
                Assert.That(detail, Is.GreaterThan(500), "has a pattern, not a flat fill");
                var tex = ChapterBackdrop.Build(c, new RectInt(-117, -117, 234, 234));
                Assert.That(tex.GetPixel(135, 240).a, Is.Zero, "screen centre is the arena hole");
                Assert.That(tex.GetPixel(2, 2).a, Is.EqualTo(1f));
                Object.DestroyImmediate(tex);
            }
        }
    }
}
