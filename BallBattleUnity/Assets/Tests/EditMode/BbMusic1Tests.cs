using System.Linq;
using BallBattle.EditorTools;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BallBattle.Tests
{
    /// <summary>
    /// BB-MUSIC1: the recomposed music set (real loops, one key family) and when music plays around the jingles:
    /// music_result waits until the win/lose jingle has finished and fades in; any jingle dips the music.
    /// </summary>
    public class BbMusic1Tests
    {
        GameObject root;
        SfxLibrary lib;
        float now;

        [SetUp]
        public void SetUp()
        {
            lib = AssetDatabase.LoadAssetAtPath<SfxLibrary>(PlaceholderSfxGenerator.LibraryPath);
            root = new GameObject("BbMusic1Root");
            now = 0f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        AudioDirector NewDirector()
        {
            var d = AudioDirector.Create(root.transform, lib, new MemorySaveStore());
            d.Clock = () => now;
            return d;
        }

        void Run(AudioDirector d, float seconds)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += 0.02f)
            {
                now += 0.02f;
                d.Tick(0.02f);
            }
        }

        float ResultGain(AudioDirector d) => d.Music.ActiveSlot >= 0 && d.Music.Current == AudioIds.MusicResult ? d.Music.Gain[d.Music.ActiveSlot] : 0f;

        [Test]
        public void TheResultTrackIsARealLoopNotAStutter()
        {
            foreach (var id in AudioIds.Music)
            {
                var c = lib.Get(id);
                Assert.That(c, Is.Not.Null, id);
                Assert.That(c.length, Is.GreaterThan(15f), $"{id}: a loop long enough not to sound repeated (old music_result was 1.26 s)");
            }
            foreach (var (id, _) in MusicForge.All.Where(m => !m.id.StartsWith("music_")))
            {
                Assert.That(AudioIds.IsJingle(id), Is.True, id);
                Assert.That(lib.Get(id).length, Is.LessThan(2.6f), id);
            }
        }

        [Test]
        public void ResultMusicWaitsForTheWinJingleThenFadesIn()
        {
            var d = NewDirector();
            d.SetScreen(MusicScreen.Fight);
            Run(d, 1f);
            d.OnMatchEnded(0, 0);                 // player won: fight_win in 0.5 s
            d.SetScreen(MusicScreen.FightResult);
            Assert.That(d.Music.Current, Is.EqualTo(AudioIds.MusicResult), "the result track is chosen at once");
            var jingleEnd = d.JingleUntil;
            Assert.That(jingleEnd, Is.GreaterThan(now + AudioDirector.ResultCueDelay), "the pending fight_win is known");
            Run(d, jingleEnd - now - 0.05f);
            Assert.That(d.Played.Any(p => p.Id == AudioIds.FightWin), Is.True);
            Assert.That(ResultGain(d), Is.EqualTo(0f), "silent under the jingle");
            Run(d, AudioDirector.ResultGap + AudioDirector.ResultFadeIn + 0.1f);
            Assert.That(ResultGain(d), Is.EqualTo(1f).Within(1e-3f), "then it fades in fully");
        }

        [Test]
        public void ScreenBeforeTheMatchEndEventIsHeldToo()
        {
            var d = NewDirector();
            d.SetScreen(MusicScreen.Fight);
            Run(d, 1f);
            d.SetScreen(MusicScreen.FightResult);   // the screen changed one frame before the match-end event
            d.OnMatchEnded(1, 0);                   // player lost: fight_lose
            Run(d, d.JingleUntil - now - 0.05f);
            Assert.That(ResultGain(d), Is.EqualTo(0f));
            Run(d, AudioDirector.ResultGap + AudioDirector.ResultFadeIn + 0.1f);
            Assert.That(ResultGain(d), Is.EqualTo(1f).Within(1e-3f));
        }

        [Test]
        public void ADrawHasNoJingleSoTheResultTrackFadesInAtOnce()
        {
            var d = NewDirector();
            d.SetScreen(MusicScreen.Fight);
            Run(d, 1f);
            d.OnMatchEnded(-1, -1);
            d.SetScreen(MusicScreen.VersusResult);
            Run(d, AudioDirector.ResultFadeIn + 0.1f);
            Assert.That(ResultGain(d), Is.EqualTo(1f).Within(1e-3f));
        }

        [Test]
        public void AnyJingleDipsTheMusicUntilItEnds()
        {
            var d = NewDirector();
            d.SetScreen(MusicScreen.Menu);
            Run(d, 1f);
            Assert.That(d.JingleDb(now), Is.EqualTo(0f));
            d.RequestUiCue(AudioIds.Unlock);
            Assert.That(d.JingleDb(now), Is.EqualTo(AudioDirector.JingleDuckDb));
            Run(d, lib.Get(AudioIds.Unlock).length + AudioDirector.JingleDuckRelease + 0.05f);
            Assert.That(d.JingleDb(now), Is.EqualTo(0f), "back after the release");
            d.RequestUiCue(AudioIds.UiClick);
            Assert.That(d.JingleDb(now), Is.EqualTo(0f), "a click is not a jingle");
        }

        [Test]
        public void HoldOnlyWorksBeforeTheTrackIsHeard()
        {
            var m = new MusicMixer();
            m.Request(AudioIds.MusicMenu);
            m.Tick(1f);
            Assert.That(m.HoldIncoming(2f, 1f), Is.False, "menu is already playing");
            m.Request(AudioIds.MusicResult, 1f, 1f);
            m.Tick(0.5f);
            Assert.That(m.HoldIncoming(1f, 1f), Is.True);
            m.Tick(0.9f);
            Assert.That(m.Gain[m.ActiveSlot], Is.EqualTo(0f));
            m.Tick(1.2f);
            Assert.That(m.Gain[m.ActiveSlot], Is.EqualTo(1f).Within(1e-4f));
        }
    }
}
