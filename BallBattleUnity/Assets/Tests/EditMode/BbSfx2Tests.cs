using System.Collections.Generic;
using System.Linq;
using BallBattle.EditorTools;
using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BallBattle.Tests
{
    /// <summary>
    /// BB-SFX2: one hazard cue per WallBounce / ObstacleHit (spike_hit when it dealt damage), the audio log, the -bbForceDraw
    /// Versus switch, the comma glyph. Audio tests use the REAL SfxLibrary.asset (no injected hasClip).
    /// </summary>
    public class BbSfx2Tests
    {
        GameObject root;
        ArtLibrary art;
        SfxLibrary lib;
        float now;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            lib = AssetDatabase.LoadAssetAtPath<SfxLibrary>(PlaceholderSfxGenerator.LibraryPath);
            Assert.That(lib, Is.Not.Null, PlaceholderSfxGenerator.LibraryPath);
            Assert.That(lib.Has(AudioIds.SpikeHit), Is.True, "spike_hit is linked in the real library");
            Assert.That(lib.Has(AudioIds.Wall), Is.True);
            root = new GameObject("BbSfx2Root");
            now = 0f;
            AudioLog.ForceOn = false;
        }

        [TearDown]
        public void TearDown()
        {
            AudioLog.ForceOn = false;
            Object.DestroyImmediate(root);
        }

        AudioDirector NewDirector()
        {
            var d = AudioDirector.Create(root.transform, lib, new MemorySaveStore());
            d.Clock = () => now;
            return d;
        }

        static MatchSim Sim(string arena = null) =>
            new MatchSim(ArenaRegistry.Create(arena ?? ArenaRegistry.Classic), 5, new[] { WeaponRegistry.Create(BladeRule.WeaponId), WeaponRegistry.Create(FangRule.WeaponId) });

        static SimEvent Hazard(SimEventType type, int tick, float damage) =>
            new SimEvent { Type = type, Tick = tick, A = 0, B = type == SimEventType.ObstacleHit ? 0 : -1, Value = TFix.X(damage) };

        float Vol(AudioDirector d, float baseVolume) => baseVolume * VolumeCurve.Linear(d.SfxVolume);

        // ---- a-d: one cue per event ----

        [Test]
        public void A_DamagingWallBouncePlaysOnlySpikeHit()
        {
            var d = NewDirector();
            d.OnSimEvent(Hazard(SimEventType.WallBounce, 10, 3f), Sim());
            Assert.That(d.Played.Select(p => p.Id), Is.EqualTo(new[] { AudioIds.SpikeHit }));
            Assert.That(d.Played[0].Volume, Is.EqualTo(Vol(d, AudioDirector.WallBounceVolume)).Within(1e-6f), "the wall bounce volume (0.2)");
            Assert.That(d.Played[0].Pitch, Is.InRange(1f - AudioDirector.PitchJitter - 1e-5f, 1f + AudioDirector.PitchJitter + 1e-5f));
        }

        [Test]
        public void B_DamagingObstacleHitPlaysOnlySpikeHitAtPitchOneAndTheObstacleVolume()
        {
            var d = NewDirector();
            for (var t = 0; t < 50; t++) d.OnSimEvent(Hazard(SimEventType.ObstacleHit, 100 + t, 2f), Sim(ArenaRegistry.Bumpers));
            Assert.That(d.Played.Select(p => p.Id).Distinct(), Is.EqualTo(new[] { AudioIds.SpikeHit }), "only spike_hit, never wall");
            Assert.That(d.Played.Count, Is.EqualTo(50), "exactly one cue per event");
            foreach (var p in d.Played)
            {
                Assert.That(p.Pitch, Is.InRange(1f - AudioDirector.PitchJitter - 1e-5f, 1f + AudioDirector.PitchJitter + 1e-5f), "about 1.0: no 0.7 pitch-down");
                Assert.That(p.Volume, Is.EqualTo(Vol(d, AudioDirector.ObstacleVolume)).Within(1e-6f), "obstacle wall volume (0.25)");
            }
            Assert.That(AudioDirector.ObstacleVolume, Is.EqualTo(0.25f));
        }

        [Test]
        public void C_HarmlessObstacleHitPlaysOnlyWallAtPitchOnePointTwo()
        {
            var d = NewDirector();
            for (var t = 0; t < 50; t++) d.OnSimEvent(Hazard(SimEventType.ObstacleHit, 200 + t, 0f), Sim(ArenaRegistry.Bumpers));
            Assert.That(d.Played.Select(p => p.Id).Distinct(), Is.EqualTo(new[] { AudioIds.Wall }));
            Assert.That(d.Played.Count, Is.EqualTo(50));
            foreach (var p in d.Played)
            {
                Assert.That(p.Pitch, Is.InRange(1.2f * (1f - AudioDirector.PitchJitter) - 1e-5f, 1.2f * (1f + AudioDirector.PitchJitter) + 1e-5f), "1.2 (+ wall jitter), as before");
                Assert.That(p.Volume, Is.EqualTo(Vol(d, 0.25f)).Within(1e-6f), "same volume as before");
            }
        }

        [Test]
        public void D_HarmlessWallBouncePlaysOnlyWall()
        {
            var d = NewDirector();
            d.OnSimEvent(Hazard(SimEventType.WallBounce, 10, 0f), Sim());
            Assert.That(d.Played.Select(p => p.Id), Is.EqualTo(new[] { AudioIds.Wall }));
            Assert.That(d.Played[0].Volume, Is.EqualTo(Vol(d, 0.2f)).Within(1e-6f));
        }

        [Test]
        public void HazardCueIsPureAndNeverBoth()
        {
            foreach (var type in new[] { SimEventType.WallBounce, SimEventType.ObstacleHit })
            {
                Assert.That(AudioDirector.HazardCue(type, 3f, lib.Has), Is.EqualTo(AudioIds.SpikeHit));
                Assert.That(AudioDirector.HazardCue(type, 0f, lib.Has), Is.EqualTo(AudioIds.Wall));
                Assert.That(AudioDirector.HazardCue(type, 3f, id => false), Is.EqualTo(AudioIds.Wall), "no clip: wall");
                Assert.That(AudioDirector.HazardCue(type, 3f, null), Is.EqualTo(AudioIds.Wall));
            }
            Assert.That(AudioDirector.HazardCue(SimEventType.BallBounce, 3f, lib.Has), Is.EqualTo(AudioIds.Wall), "not a hazard");
        }

        // ---- e: the real sim on SPIKES ----

        [Test]
        public void E_EverySpikeDamageOnTheSpikesArenaPlaysSpikeHitNeverWall()
        {
            AudioLog.ForceOn = true;
            var d = NewDirector();
            var damaging = 0;
            var spikePlays = 0;
            var seeds = new uint[] { 4242, 4243, 4244, 4245, 4246, 4247, 4248, 4249 };
            foreach (var seed in seeds)
            {
                var sim = new MatchSim(ArenaRegistry.Create(ArenaRegistry.SpikeWalls), seed,
                    new[] { WeaponRegistry.Create(BrawlerRule.WeaponId), WeaponRegistry.Create(BrawlerRule.WeaponId) });
                d.OnMatchStarted();
                var guard = 0;
                while (sim.Outcome == MatchOutcome.Ongoing && guard++ < 60 * 200)
                {
                    sim.Step();
                    foreach (var e in sim.Events)
                    {
                        var hazard = e.Type == SimEventType.WallBounce || e.Type == SimEventType.ObstacleHit;
                        if (hazard) d.Played.Clear();   // Played is capped at 256: read exactly what this event played
                        d.OnSimEvent(e, sim);
                        if (!hazard) continue;
                        var line = d.LogLines[d.LogLines.Count - 1];
                        StringAssert.Contains($"{e.Type} tick={e.Tick} ", line);
                        StringAssert.Contains($" ball={e.A} ", line);
                        if (e.Value.ToFloat() <= 0f)
                        {
                            StringAssert.Contains("cue=wall", line);
                            continue;
                        }
                        damaging++;
                        StringAssert.Contains("cue=spike_hit", line, "every damaging hazard event chooses spike_hit");
                        StringAssert.DoesNotContain("cue=wall", line);
                        StringAssert.Contains($"active={sim.ActiveTick} ", line);
                        StringAssert.Contains($"hpChangedTick={e.Tick} ", line, "the spike damage changed that ball's HP this tick");
                        var played = d.Played.Select(p => p.Id).ToList();
                        Assert.That(played, Does.Not.Contain(AudioIds.Wall), "a damaging event never plays wall");
                        Assert.That(played.Count, Is.LessThanOrEqualTo(1), "at most one cue per event");
                        if (played.Count == 1) { Assert.That(played[0], Is.EqualTo(AudioIds.SpikeHit)); spikePlays++; }
                        else StringAssert.Contains("allowed=no", line, "silent only when the per-tick gate said no");
                    }
                }
                if (damaging >= 5) break;
            }
            Assert.That(damaging, Is.GreaterThanOrEqualTo(5), "at least 5 spike-damage events");
            Assert.That(spikePlays, Is.GreaterThanOrEqualTo(1));
            TestContext.WriteLine($"spike-damage events: {damaging}, spike_hit played: {spikePlays}");
        }

        [Test]
        public void AudioLogIsOffByDefaultAndListsEveryField()
        {
            Assert.That(DebugOptions.ParseAudioLog(new[] { "game.exe" }), Is.False);
            Assert.That(DebugOptions.ParseAudioLog(new[] { "game.exe", "-bbAudioLog" }), Is.True);
            Assert.That(AudioLog.Enabled, Is.False, "off by default");
            var d = NewDirector();
            d.OnSimEvent(Hazard(SimEventType.WallBounce, 7, 3f), Sim());
            Assert.That(d.LogLines, Is.Empty, "nothing logged while off");
            AudioLog.ForceOn = true;
            d.OnSimEvent(Hazard(SimEventType.ObstacleHit, 8, 2f), Sim(ArenaRegistry.Bumpers));
            Assert.That(d.LogLines.Count, Is.EqualTo(1));
            Assert.That(d.LogLines[0], Does.Match(@"^\[Audio\] ObstacleHit tick=8 active=0 ball=0 dmg=2\.00 hpChangedTick=-?\d+ cue=spike_hit allowed=yes$"));
            Assert.That(AudioLog.FileName, Is.EqualTo("audio_log.txt"));
            Assert.That(AudioLog.FilePath, Does.EndWith("audio_log.txt"));
        }

        // ---- f, h: -bbForceDraw ----

        [Test]
        public void F_ForceDrawParsesAndIsInertInRunMode()
        {
            Assert.That(DebugOptions.ParseForceDraw(null), Is.False);
            Assert.That(DebugOptions.ParseForceDraw(new[] { "game.exe" }), Is.False, "off by default");
            Assert.That(DebugOptions.ParseForceDraw(new[] { "game.exe", "-bbForceDraw" }), Is.True);
            Assert.That(DebugOptions.ParseForceDraw(new[] { "game.exe", "-BBFORCEDRAW" }), Is.True);
            var versus = new MatchFlow { ForceDraw = true };
            Assert.That(GameController.AudioWinner(1, runOpen: false, versus), Is.EqualTo(-1), "Versus: reported as a draw");
            Assert.That(GameController.AudioWinner(1, runOpen: true, versus), Is.EqualTo(1), "Run mode: untouched");
            Assert.That(GameController.AudioWinner(0, runOpen: true, versus), Is.EqualTo(0));
            Assert.That(GameController.AudioWinner(1, runOpen: false, new MatchFlow()), Is.EqualTo(1), "flag off: untouched");
            Assert.That(typeof(RunFlow).GetProperty("ForceDraw"), Is.Null, "Run mode's flow has no such switch");
        }

        [Test]
        public void H_ForceDrawVersusEndsInADrawWithOnlyResultMusic()
        {
            var d = NewDirector();
            var resultStarts = 0;
            d.Music.SlotStarted += (slot, track) => { if (track == AudioIds.MusicResult) resultStarts++; };
            var flow = new MatchFlow(3f, 2f) { ForceDraw = true };
            var overlay = OverlayView.Create(root.transform, art);
            void Frame(float dt)
            {
                now += dt;
                flow.Tick(dt);
                d.SetScreen(GameController.VersusScreen(flow.Current, false, false));
                overlay.Render(flow, dt);
                d.Tick(dt);
            }
            d.SetScreen(MusicScreen.VersusPick);
            flow.Begin(BladeRule.WeaponId, FangRule.WeaponId, 77u);
            for (var i = 0; i < 40; i++) Frame(0.1f);
            Assert.That(flow.Current, Is.EqualTo(MatchFlow.State.Playing));
            d.OnMatchStarted();

            // The arena's match ends with a real winner; the View reports a draw (same wiring as GameController).
            const int realWinner = 0;
            flow.OnRoundEnded(realWinner);
            d.OnMatchEnded(GameController.AudioWinner(realWinner, runOpen: false, flow), -1);
            for (var i = 0; i < 60; i++) Frame(0.1f);

            Assert.That(flow.Current, Is.EqualTo(MatchFlow.State.Result), "the first resolve ends the match");
            Assert.That(flow.EndedInDraw, Is.True);
            Assert.That(flow.Series.Draws, Is.EqualTo(1));
            Assert.That(overlay.ResultShown, Is.True);
            Assert.That(overlay.ResultTitleText, Is.EqualTo(RunStrings.VersusDraw));
            var ids = d.Played.Select(p => p.Id).ToList();
            foreach (var stinger in new[] { AudioIds.FightWin, AudioIds.FightLose, AudioIds.RunWin, AudioIds.RunOver })
                Assert.That(ids, Does.Not.Contain(stinger), stinger);
            Assert.That(d.Music.Current, Is.EqualTo(AudioIds.MusicResult));
            Assert.That(resultStarts, Is.EqualTo(1), "music_result starts exactly once");
            for (var i = 0; i < 30; i++) Frame(0.1f);
            Assert.That(resultStarts, Is.EqualTo(1), "and not again while the result is shown");
        }

        // ---- g: the comma ----

        [Test]
        public void G_FontHas113GlyphsAndTheCommaIsTwoPixelsWide()
        {
            Assert.That(PixelFont.Default.Count, Is.EqualTo(113));
            Assert.That(PixelFont.Default.Has(','), Is.True);
            Assert.That(PixelFont.Default.Get(',').Width, Is.EqualTo(2));
            Assert.That(PixelFont.MeasureWidth(","), Is.EqualTo(2));
            Assert.That(PixelFont.MeasureWidth("A,B"), Is.EqualTo(5 + 1 + 2 + 1 + 5));
            var t = PixelText.Create(root.transform, "T", art.Font, Vector2.zero, PixelText.Align.Left, 1, 0);
            t.Set(",", Palette.Text);
            var glyph = t.GetComponentsInChildren<SpriteRenderer>(true)[0];
            Assert.That(glyph.enabled, Is.True, "the comma draws (not the blank fallback)");
            Assert.That(glyph.sprite.rect.width, Is.EqualTo(2));
            Assert.That(t.Bounds.width, Is.EqualTo(2));
        }
    }
}
