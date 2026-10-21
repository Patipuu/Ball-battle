using BallBattle.EditorTools;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BallBattle.Tests
{
    /// <summary>The player's HUD sits at the bottom (opponent on top); X1/X2 sits beside the gear and really keeps X2 between fights.</summary>
    public class HudBottomAndSpeedTests
    {
        GameObject root;
        ArtLibrary art;
        float savedSpeed;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("HudBottomAndSpeedTests");
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            savedSpeed = PlayerPrefs.GetFloat("run_speed", 1f);
            PlayerPrefs.SetFloat("run_speed", 1f);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.SetFloat("run_speed", savedSpeed);
            Object.DestroyImmediate(root);
        }

        RunController StartFight(ArenaView arena, RunController rc = null)
        {
            if (rc == null) { rc = RunController.Create(root.transform, arena, new MemorySaveStore()); rc.Open(); }
            rc.Flow.NewRun(321, false);
            rc.Flow.ChooseWeapon("blade");
            rc.Flow.PickStart(0);
            rc.Flow.Fight();
            rc.Tick(3f);
            Assert.That(rc.Flow.Current, Is.EqualTo(RunFlow.State.Playing));
            return rc;
        }

        ArenaView NewArena()
        {
            var arena = root.AddComponent<ArenaView>();
            arena.Art = art;
            arena.AutoRestart = false;
            return arena;
        }

        [Test]
        public void RunPutsThePlayerHudAtTheBottomAndTheFoeOnTop()
        {
            var arena = NewArena();
            StartFight(arena);
            var hud = arena.Hud;
            Assert.That(hud.IsTop(0), Is.False, "player (ball 0) at the bottom");
            Assert.That(hud.NameText(0).transform.localPosition.y, Is.LessThan(-115f), "under the arena");
            Assert.That(hud.NameText(1).transform.localPosition.y, Is.GreaterThan(115f), "foe above the arena");
            Assert.That(hud.ShowsPlayerArrow(0), Is.True);
        }

        [Test]
        public void VersusWithoutAPlayerKeepsBallZeroOnTop()
        {
            var arena = NewArena();
            var sim = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 5, new[]
            {
                new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)), new BallLoadout(WeaponRegistry.Create(FangRule.WeaponId)),
            });
            arena.MarkedBall = -1;
            arena.StartMatch(sim, null, null);
            Assert.That(arena.Hud.IsTop(0), Is.True);
            arena.MarkedBall = 1;   // PvP replay round with sides swapped: the player is ball 1
            arena.StartMatch(sim, "THEM", "YOU");
            Assert.That(arena.Hud.IsTop(1), Is.False);
            Assert.That(arena.Hud.IsTop(0), Is.True);
        }

        [Test]
        public void SpeedButtonSitsRightOfTheGear()
        {
            var gear = RunOverlayView.GearArea;
            var speed = RunOverlayView.SpeedArea;
            Assert.That(speed.xMin, Is.GreaterThanOrEqualTo(gear.xMax));
            Assert.That(speed.xMin - gear.xMax, Is.LessThan(8f), "right next to it");
            Assert.That(speed.yMin, Is.GreaterThan(200f), "in the top band, out of the tap area");
            Assert.That(speed.height, Is.GreaterThanOrEqualTo(24f), "24 px tap target");
            Assert.That(speed.yMin, Is.GreaterThanOrEqualTo(TapView.TapArea.yMax));
            Assert.That(speed.yMin, Is.EqualTo(gear.yMin), "same row as the gear");
            Assert.That(speed.height, Is.EqualTo(gear.height), "same height as the gear");
            Assert.That(speed.yMax, Is.LessThanOrEqualTo(240f), "on screen");
        }

        [Test]
        public void PlayerTraitIconsSitAboveTheSkillLine()
        {
            var arena = NewArena();
            var sim = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 5, new[]
            {
                new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)).With(new BallBattle.Sim.Traits.BerserkerTrait(1), new BallBattle.Sim.Traits.RegenTrait(1)),
                new BallLoadout(WeaponRegistry.Create(FangRule.WeaponId)).With(new BallBattle.Sim.Traits.IronSkinTrait(1)),
            });
            arena.MarkedBall = 0;
            arena.StartMatch(sim, "YOU", "FOE");
            var icons = arena.Badges.GetComponentsInChildren<SpriteRenderer>(false);
            var below = 0;
            foreach (var r in icons)
            {
                if (!r.name.StartsWith("Icon_")) continue;
                var y = r.transform.localPosition.y;
                if (y < 0) { below++; Assert.That(r.bounds.min.y, Is.GreaterThan(-218f), r.name + " clears the bottom skill line (-219.5)"); }
                else Assert.That(r.bounds.min.y, Is.GreaterThan(149f), r.name + " clears the top skill line (147.5)");
            }
            Assert.That(below, Is.EqualTo(2), "the player's two trait icons are in the bottom band");
        }

        [Test]
        public void X2StaysX2InTheNextFight()
        {
            var arena = NewArena();
            var rc = StartFight(arena);
            Assert.That(arena.TimeScale, Is.EqualTo(1f));
            rc.Overlay.SpeedButton.SimulateClick();
            Assert.That(arena.TimeScale, Is.EqualTo(2f));
            Assert.That(rc.Overlay.SpeedLabel, Is.EqualTo("X2"));

            rc.Flow.Close();
            rc.Open();
            StartFight(arena, rc);   // a new fight begins: it used to reset to x1 while the label still said X2
            Assert.That(arena.TimeScale, Is.EqualTo(2f), "real speed");
            Assert.That(rc.Overlay.SpeedLabel, Is.EqualTo("X2"), "label matches");

            rc.Overlay.SpeedButton.SimulateClick();
            Assert.That(arena.TimeScale, Is.EqualTo(1f));
            Assert.That(rc.Overlay.SpeedLabel, Is.EqualTo("X1"));
        }
    }
}
