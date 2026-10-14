using BallBattle.EditorTools;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BallBattle.Tests
{
    /// <summary>
    /// 6a.6b/c part 0: volume slider driven through the real input path (PixelInput.Inject → Dispatch → PixelButton →
    /// SettingsView), with the pointer position injected for the frames in between (PixelInput.SetTestPointer + Tick).
    /// Bug: a drag released on the track reset the value to the press point (Clicked used LastPressNative).
    /// </summary>
    public class SliderInputTests
    {
        GameObject root;
        ArtLibrary art;
        SfxLibrary lib;
        MemorySaveStore store;
        AudioDirector audio;
        MenuView menu;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Art/ArtLibrary.asset");
            lib = AssetDatabase.LoadAssetAtPath<SfxLibrary>(PlaceholderSfxGenerator.LibraryPath);
            Assert.That(art, Is.Not.Null);
            root = new GameObject("SliderInputRoot");
            PixelInput.SetTestClock(0f);
            PixelInput.ResetCapture();
            store = new MemorySaveStore();
            VolumeSettings.Save(store, 6, 5);   // start at SFX 5 (not the default 8, so a drag to 8 is a change)
            (audio, menu) = Open(store, root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            PixelInput.ResetCapture();
            PixelInput.SetTestClock(null);
            Object.DestroyImmediate(root);
        }

        (AudioDirector, MenuView) Open(ISaveStore s, Transform parent)
        {
            var a = AudioDirector.Create(parent, lib, s);
            a.Clock = () => 0f;
            var m = MenuView.Create(parent, art);
            SettingsAudio.Bind(m, a, s);
            m.ShowSettings();
            return (a, m);
        }

        /// <summary>Native point inside SFX cell <paramref name="step"/> (1-based: StepAt gives that step).</summary>
        static Vector2 SfxAt(int step) =>
            new Vector2(SettingsView.SfxArea.xMin + (step - 1) * SettingsView.CellWidth + 10, SettingsView.SfxArea.center.y);

        void Dispatch() => PixelInput.Dispatch(() => root.GetComponentsInChildren<PixelButton>(false));

        /// <summary>One frame: the pointer is at <paramref name="at"/>; queued events are dispatched first (the pump runs first), then the sliders follow.</summary>
        void Frame(Vector2 at)
        {
            PixelInput.SetTestPointer(at);
            Dispatch();
            menu.Settings.Tick();
        }

        void Press(Vector2 at) { PixelInput.Inject(true, at); Frame(at); }
        void Release(Vector2 at) { PixelInput.Inject(false, at); Frame(at); }

        [Test]
        public void DragReleasedOnTheTrackKeepsTheDraggedValue()
        {
            var start = store.Saves;
            Press(SfxAt(3));
            Assert.That(menu.Settings.SfxValue, Is.EqualTo(3));
            for (var s = 4; s <= 8; s++) Frame(SfxAt(s));
            Assert.That(menu.Settings.SfxValue, Is.EqualTo(8));
            Assert.That(store.Saves - start, Is.EqualTo(0), "no write while dragging");

            Release(SfxAt(8));
            Assert.That(menu.Settings.SfxValue, Is.EqualTo(8), "release on the track must not jump back to the press point");
            Assert.That(audio.SfxVolume, Is.EqualTo(8));
            Assert.That(store.Saves - start, Is.EqualTo(1), "one write on release");
            Assert.That(store.Load().sfxVol, Is.EqualTo(8));

            // Reopen: a fresh menu + audio built from the same save shows 8.
            var again = new GameObject("Reopened");
            again.transform.SetParent(root.transform, false);
            var (_, menu2) = Open(store, again.transform);
            Assert.That(menu2.Settings.SfxValue, Is.EqualTo(8));
            Assert.That(store.Saves - start, Is.EqualTo(1), "reopening writes nothing");
        }

        [Test]
        public void DragReleasedOffTheTrackWritesOnce()
        {
            var start = store.Saves;
            Press(SfxAt(3));
            for (var s = 4; s <= 8; s++) Frame(SfxAt(s));
            var outside = new Vector2(SfxAt(8).x, SettingsView.SfxArea.yMax + 40);
            Frame(outside);   // pointer leaves the track while held: the value stays at the last on-track step
            Release(outside);
            Assert.That(menu.Settings.SfxValue, Is.EqualTo(8));
            Assert.That(store.Saves - start, Is.EqualTo(1));
            Assert.That(store.Load().sfxVol, Is.EqualTo(8));
            Frame(outside);
            Assert.That(store.Saves - start, Is.EqualTo(1), "no second write on later frames");
        }

        [Test]
        public void DragThenBackWithoutReleaseSavesOnClose()
        {
            var start = store.Saves;
            Press(SfxAt(3));
            for (var s = 4; s <= 6; s++) Frame(SfxAt(s));
            menu.ShowHub();   // BACK / Esc while the pointer is still down
            Assert.That(store.Saves - start, Is.EqualTo(1), "saved when Settings closes");
            Assert.That(store.Load().sfxVol, Is.EqualTo(6));
            Release(SfxAt(6));
            menu.ShowSettings();
            Frame(SfxAt(6));
            menu.ShowHub();
            Assert.That(store.Saves - start, Is.EqualTo(1), "exactly one write");
        }

        [Test]
        public void SingleTapWritesOnceWithTheTappedValue()
        {
            var start = store.Saves;
            PixelInput.InjectTap(SfxAt(4));
            Frame(SfxAt(4));
            Assert.That(menu.Settings.SfxValue, Is.EqualTo(4));
            Assert.That(store.Saves - start, Is.EqualTo(1));
            Assert.That(store.Load().sfxVol, Is.EqualTo(4));

            // A tap whose press and release are dispatched in different frames (the slider follows in between).
            Press(SfxAt(7));
            Release(SfxAt(7));
            Assert.That(menu.Settings.SfxValue, Is.EqualTo(7));
            Assert.That(store.Saves - start, Is.EqualTo(2));
            Assert.That(store.Load().sfxVol, Is.EqualTo(7));
        }

        [Test]
        public void DragBackToTheStartWritesNothing()
        {
            Press(SfxAt(audio.SfxVolume));
            var start = store.Saves;
            var v0 = audio.SfxVolume;
            for (var s = v0 - 1; s >= 1; s--) Frame(SfxAt(s));
            for (var s = 1; s <= v0; s++) Frame(SfxAt(s));
            Release(SfxAt(v0));
            Assert.That(menu.Settings.SfxValue, Is.EqualTo(v0));
            Assert.That(store.Saves - start, Is.EqualTo(0));
        }

        [Test]
        public void OpeningTheHubAfterASavedValueWritesNothing()
        {
            Press(SfxAt(2));
            for (var s = 3; s <= 9; s++) Frame(SfxAt(s));
            Release(SfxAt(9));
            var saved = store.Saves;
            Assert.That(store.Load().sfxVol, Is.EqualTo(9));
            menu.ShowHub();
            Frame(SfxAt(9));
            Assert.That(store.Saves, Is.EqualTo(saved), "the hub must not save again");
        }
    }
}
