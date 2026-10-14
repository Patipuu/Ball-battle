using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BallBattle.EditorTools;
using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BallBattle.Tests
{
    /// <summary>BB-03: hit routing, per-tick dedupe, unlock spacing, music mapping/crossfade, ducking, volumes, Settings layout.</summary>
    public class Bb03AudioTests
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
            root = new GameObject("Bb03Root");
            now = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            Object.DestroyImmediate(root);
        }

        AudioDirector NewDirector(ISaveStore store = null)
        {
            var d = AudioDirector.Create(root.transform, lib, store ?? new MemorySaveStore());
            d.Clock = () => now;
            return d;
        }

        static int Count(AudioDirector d, string id) => d.Played.Count(p => p.Id == id);

        // ---- hit routing ----

        [Test]
        public void EachWeaponHitResolvesToItsOwnClip()
        {
            var d = NewDirector();
            foreach (var id in AudioIds.HitWeapons)
            {
                Assert.That(d.HitCueFor(id), Is.EqualTo("hit_" + id), id);
                Assert.That(lib.Get("hit_" + id), Is.Not.Null, "clip linked: hit_" + id);
            }
            Assert.That(d.HitCueFor("spoon"), Is.EqualTo(AudioIds.Hit), "unknown id falls back");
            Assert.That(d.HitCueFor(null), Is.EqualTo(AudioIds.Hit));
            Assert.That(lib.Get(AudioIds.Hit), Is.Not.Null, "generic hit kept");
        }

        static MatchSim Sim(string a, string b) =>
            new MatchSim(ArenaRegistry.Create(ArenaRegistry.All[0].Id), 5, new[] { new BallLoadout(WeaponRegistry.Create(a)), new BallLoadout(WeaponRegistry.Create(b)) });

        [Test]
        public void ProjectileHitsUseTheOwningWeapon()
        {
            var d = NewDirector();
            var hive = Sim("rig", "blade");
            var turretShot = new SimEvent { Type = SimEventType.ProjectileHit, Tick = 10, A = 0, B = 1, Value = TFix.X(3f) };
            Assert.That(AudioDirector.HitCueFor(turretShot, hive, lib.Has), Is.EqualTo("hit_rig"));
            d.OnSimEvent(turretShot, hive);
            Assert.That(d.Played.Last().Id, Is.EqualTo("hit_rig"));

            var sparkler = Sim("blade", "volley");
            var arrow = new SimEvent { Type = SimEventType.ProjectileHit, Tick = 11, A = 1, B = 0, Value = TFix.X(2f) };
            Assert.That(AudioDirector.HitCueFor(arrow, sparkler, lib.Has), Is.EqualTo("hit_volley"));
            d.OnSimEvent(arrow, sparkler);
            Assert.That(d.Played.Last().Id, Is.EqualTo("hit_volley"));

            var melee = new SimEvent { Type = SimEventType.Hit, Tick = 12, A = 0, B = 1, Value = TFix.X(4f) };
            d.OnSimEvent(melee, sparkler);
            Assert.That(d.Played.Last().Id, Is.EqualTo("hit_blade"), "melee hit: the attacker's weapon");
        }

        // ---- dedupe ----

        [Test]
        public void SameCueOncePerTickAndAtMostThreeHits()
        {
            var d = NewDirector();
            d.RequestCue(AudioIds.Parry, 5);
            d.RequestCue(AudioIds.Parry, 5);
            Assert.That(Count(d, AudioIds.Parry), Is.EqualTo(1), "same cue, same tick");
            d.RequestCue(AudioIds.Parry, 6);
            Assert.That(Count(d, AudioIds.Parry), Is.EqualTo(2), "next tick plays again");

            d.Played.Clear();
            foreach (var w in new[] { "blade", "fang", "pike" }) d.RequestHit(w, 7);
            Assert.That(d.Played.Count(p => AudioIds.IsHit(p.Id)), Is.EqualTo(3), "3 different weapon hits layer");

            d.Played.Clear();
            foreach (var w in new[] { "blade", "fang", "pike", "brawler", "venom" }) d.RequestHit(w, 8);
            Assert.That(d.Played.Count(p => AudioIds.IsHit(p.Id)), Is.EqualTo(3), "capped at 3 per tick");

            d.Played.Clear();
            d.RequestHit("blade", 9);
            d.RequestHit("blade", 9);
            Assert.That(d.Played.Count, Is.EqualTo(1), "same weapon twice in a tick plays once");
        }

        // ---- unlock spacing ----

        [Test]
        public void ThreeMilestonesPlayThreeUnlocksQuarterSecondApart()
        {
            var d = NewDirector();
            d.OnSummaryShown(false, 3);
            for (var i = 0; i < 6; i++)
            {
                now += 0.125f;
                d.Tick(0.125f);
            }
            var times = d.Played.Where(p => p.Id == AudioIds.Unlock).Select(p => p.Time).ToArray();
            Assert.That(times, Is.EqualTo(new[] { 0f, 0.25f, 0.5f }).Within(1e-4f));
            Assert.That(Count(d, AudioIds.RunOver), Is.EqualTo(1));
        }

        // ---- music ----

        [Test]
        public void MusicFollowsTheScreenAndNeverRestartsTheSameTrack()
        {
            Assert.That(AudioIds.TrackFor(MusicScreen.Menu), Is.EqualTo(AudioIds.MusicMenu));
            Assert.That(AudioIds.TrackFor(MusicScreen.RunTitle), Is.EqualTo(AudioIds.MusicMenu));
            Assert.That(AudioIds.TrackFor(MusicScreen.VersusPick), Is.EqualTo(AudioIds.MusicMenu));
            Assert.That(AudioIds.TrackFor(MusicScreen.Settings), Is.EqualTo(AudioIds.MusicMenu));
            Assert.That(AudioIds.TrackFor(MusicScreen.Fight), Is.EqualTo(AudioIds.MusicBattle));
            Assert.That(AudioIds.TrackFor(MusicScreen.BossFight), Is.EqualTo(AudioIds.MusicBoss));
            Assert.That(AudioIds.TrackFor(MusicScreen.Summary), Is.EqualTo(AudioIds.MusicResult));
            Assert.That(AudioIds.TrackFor(MusicScreen.FightResult), Is.EqualTo(AudioIds.MusicResult));

            var d = NewDirector();
            d.SetScreen(MusicScreen.Menu);
            d.SetScreen(MusicScreen.RunTitle);
            d.SetScreen(MusicScreen.Settings);
            Assert.That(d.Music.Current, Is.EqualTo(AudioIds.MusicMenu));
            Assert.That(d.Music.Starts, Is.EqualTo(1), "menu → title → settings: one start");
            Assert.That(d.Music.Request(AudioIds.MusicMenu), Is.False, "same track again: no restart");

            d.SetScreen(MusicScreen.BossFight);
            Assert.That(d.Music.Current, Is.EqualTo(AudioIds.MusicBoss));
            Assert.That(d.MusicSource(d.Music.ActiveSlot).clip.name, Is.EqualTo(AudioIds.MusicBoss));
            d.SetScreen(MusicScreen.Fight);
            Assert.That(d.Music.Current, Is.EqualTo(AudioIds.MusicBattle));
            d.SetScreen(MusicScreen.Summary);
            Assert.That(d.Music.Current, Is.EqualTo(AudioIds.MusicResult));
            Assert.That(d.Music.Starts, Is.EqualTo(4));
        }

        [Test]
        public void CrossfadeIsEqualPowerOverPointSixSeconds()
        {
            var m = new MusicMixer();
            m.Request("a");
            m.Tick(1f);
            m.Request("b");
            m.Tick(0.3f);
            Assert.That(m.Gain[0], Is.EqualTo(Mathf.Cos(Mathf.PI / 4f)).Within(1e-4f));
            Assert.That(m.Gain[1], Is.EqualTo(Mathf.Sin(Mathf.PI / 4f)).Within(1e-4f));
            Assert.That(m.Gain[0] * m.Gain[0] + m.Gain[1] * m.Gain[1], Is.EqualTo(1f).Within(1e-4f), "equal power");
            m.Tick(0.3f);
            Assert.That(m.Gain[1], Is.EqualTo(1f).Within(1e-4f));
            Assert.That(m.Track[0], Is.Null, "old track stopped");
        }

        [Test]
        public void MusicPitchStaysOneAtDoubleSpeed()
        {
            var d = NewDirector();
            Time.timeScale = 2f;
            d.SetScreen(MusicScreen.Fight);
            for (var i = 0; i < 10; i++)
            {
                now += 0.1f;
                d.Tick(0.1f);
            }
            Assert.That(d.MusicSource(0).pitch, Is.EqualTo(1f));
            Assert.That(d.MusicSource(1).pitch, Is.EqualTo(1f));
        }

        // ---- ducking ----

        [Test]
        public void DuckEnvelopeOnRealTime()
        {
            var duck = new Ducker();
            duck.Trigger(0f);
            Assert.That(duck.Db(0f), Is.EqualTo(0f).Within(1e-4f), "t=0");
            Assert.That(duck.Db(0.03f), Is.EqualTo(-2.5f).Within(1e-3f), "30 ms: full depth");
            Assert.That(duck.Db(0.18f), Is.EqualTo(-2.5f).Within(1e-3f), "180 ms: end of hold");
            Assert.That(duck.Db(0.38f), Is.EqualTo(0f).Within(1e-3f), "380 ms: released");

            duck.Trigger(0.1f);   // during the hold: restart it, never deeper
            Assert.That(duck.Db(0.25f), Is.EqualTo(-2.5f).Within(1e-3f));
            for (var t = 0f; t < 1f; t += 0.005f) Assert.That(duck.Db(t), Is.GreaterThanOrEqualTo(-2.5f - 1e-4f));
        }

        [Test]
        public void LossCuesFadeTheFightMusicInsteadOfDucking()
        {
            foreach (var id in new[] { AudioIds.FightLose, AudioIds.RunOver })
            {
                var d = NewDirector();
                d.SetScreen(MusicScreen.Fight);
                now = 1f;
                d.Tick(1f);
                d.RequestUiCue(id);
                Assert.That(d.Duck.Db(now + 0.03f), Is.EqualTo(0f), id + ": not the -2.5 dB duck");
                Assert.That(d.Music.Current, Is.Null, id + ": fight music fading out");
                now += 0.4f;
                d.Tick(0.4f);
                Assert.That(d.Music.Gain[0] + d.Music.Gain[1], Is.EqualTo(0f).Within(1e-4f), id + ": silent after 0.4 s");
                Object.DestroyImmediate(d.gameObject);
            }

            var w = NewDirector();
            w.SetScreen(MusicScreen.Fight);
            now = 1f;
            w.Tick(1f);
            w.RequestUiCue(AudioIds.FightWin);
            Assert.That(w.Duck.Db(now + 0.03f), Is.EqualTo(-2.5f).Within(1e-3f), "fight_win ducks");
            Assert.That(w.Music.Current, Is.Null, "and fades the fight music");
        }

        // ---- volume ----

        [Test]
        public void VolumeCurveAndSavedSettings()
        {
            Assert.That(VolumeCurve.Linear(0), Is.EqualTo(0f), "0 = silent");
            Assert.That(VolumeCurve.Db(10), Is.EqualTo(0f).Within(1e-5f), "10 = 0 dB");
            Assert.That(VolumeCurve.Db(5), Is.EqualTo(20f * Mathf.Log10(0.5f)).Within(1e-4f));

            // A save written before BB-03 (no musicVol / sfxVol fields).
            var store = new MemorySaveStore { Json = "{\"Version\":2,\"Progress\":{\"Achievements\":[\"boss1\"],\"RunsPlayed\":5,\"RunsWon\":2,\"BestFight\":8},\"HasRun\":false}" };
            Assert.That(VolumeSettings.Load(store), Is.EqualTo((6, 8)));
            var d = NewDirector(store);
            Assert.That((d.MusicVolume, d.SfxVolume), Is.EqualTo((6, 8)));
            d.SetMusicVolume(3, true);
            d.SetSfxVolume(9, true);
            var back = store.Load();
            Assert.That((back.musicVol, back.sfxVol), Is.EqualTo((3, 9)));
            Assert.That(back.Version, Is.EqualTo(SaveData.CurrentVersion), "no version bump");
            Assert.That(back.Progress.RunsWon, Is.EqualTo(2), "progress kept");
            Assert.That(back.Progress.Has("boss1"), Is.True);
        }

        [Test]
        public void SfxSliderClicksAtMostEvery80ms()
        {
            var d = NewDirector();
            d.SetSfxVolume(5);
            now = 0.05f;
            d.SetSfxVolume(6);
            now = 0.1f;
            d.SetSfxVolume(7);
            Assert.That(Count(d, AudioIds.UiClick), Is.EqualTo(2));
            Assert.That(d.Played.Last().Volume, Is.EqualTo(0.7f * VolumeCurve.Linear(7)).Within(1e-5f), "at the new volume");
        }

        // ---- settings panel ----

        [Test]
        public void SettingsPanelLayout()
        {
            var menu = MenuView.Create(root.transform, art);
            Assert.That(menu.GearButton.Area, Is.EqualTo(new Rect(250 - 135, 240 - 8 - 12, 12, 12)), "gear 12x12 at (250,8)");
            menu.ShowSettings();
            Assert.That(menu.SettingsOpen, Is.True);
            Bb0102Tests.AssertTextFitsAndNoOverlap(menu.transform);
            var texts = menu.GetComponentsInChildren<PixelText>(false).Select(t => t.Text).ToList();
            foreach (var s in new[] { RunStrings.Settings, RunStrings.Music, RunStrings.Sfx, RunStrings.Language, RunStrings.En, RunStrings.Vi, RunStrings.Back })
                Assert.That(texts, Does.Contain(s), s);

            var changed = new List<int>();
            menu.Settings.SfxChanged += changed.Add;
            menu.Settings.SetValues(6, 8);
            menu.Settings.DragSfxTo(SettingsView.SfxArea.xMin + 2 * SettingsView.CellWidth + 5);
            menu.Settings.DragSfxTo(SettingsView.SfxArea.xMin - 10);
            Assert.That(changed, Is.EqualTo(new[] { 3, 0 }));
        }

        // ---- boss cues are BB-04 ----

        [Test]
        public void NoBossCueIsReferenced()
        {
            foreach (var c in lib.Clips) Assert.That(c.name, Does.Not.StartWith("boss_"), c.name);
            var ids = typeof(AudioIds).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(string)).Select(f => (string)f.GetValue(null))
                .Concat(AudioIds.Cues).Concat(AudioIds.Music);
            foreach (var id in ids) Assert.That(id, Does.Not.StartWith("boss_"), id);
        }
    }
}
