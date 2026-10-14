using System;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Settings page of the main menu (BB-03): MUSIC and SFX sliders (10 steps, tap or drag), an EN/VI language row that is
    /// drawn only (no behaviour yet), BACK. Text from RunStrings. Positions in comments are 270x480 screen pixels, top-left origin.
    /// </summary>
    public sealed class SettingsView : MonoBehaviour
    {
        public const int Steps = VolumeCurve.Max;
        public const int CellWidth = 20;

        /// <summary>Gear button on the menu hub: (250,8), 12x12.</summary>
        public static readonly Rect GearArea = ScreenRect(250, 8, 12, 12);
        public static readonly Rect MusicArea = ScreenRect(35, 136, Steps * CellWidth, 16);
        public static readonly Rect SfxArea = ScreenRect(35, 192, Steps * CellWidth, 16);
        public static readonly Rect EnArea = ScreenRect(35, 248, 40, 20);
        public static readonly Rect ViArea = ScreenRect(85, 248, 40, 20);
        public static readonly Rect BackArea = ScreenRect(75, 330, 120, 22);

        /// <summary>Live value changes while tapping/dragging (volume changes at once; nothing is saved here).</summary>
        public event Action<int> MusicChanged, SfxChanged;
        public event Action BackRequested;
        /// <summary>The pointer was released after pressing a slider: the moment to save (SettingsAudio).</summary>
        public event Action Released;

        Slider musicSlider, sfxSlider;

        /// <summary>The BACK button (tests).</summary>
        public PixelButton BackButton { get; private set; }

        /// <summary>Screen rect (top-left origin, y down) to the menu's local space (centre origin, y up).</summary>
        public static Rect ScreenRect(float x, float y, float w, float h) =>
            new Rect(x - TextWrap.ScreenWidth / 2f, TextWrap.ScreenHeight / 2f - y - h, w, h);

        static float LocalTop(float screenY) => TextWrap.ScreenHeight / 2f - screenY;

        public static SettingsView Create(Transform parent, ArtLibrary art, int order)
        {
            var go = new GameObject("SettingsPage");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<SettingsView>();
            v.Build(art, order);
            return v;
        }

        void Build(ArtLibrary art, int order)
        {
            Label(art, RunStrings.Settings, 0, 60, 3, PixelText.Align.Center, Palette.Text, order);
            Label(art, RunStrings.Music, MusicArea.xMin, 120, 2, PixelText.Align.Left, Palette.Text, order);
            musicSlider = new Slider(this, art, MusicArea, order, x => MusicChanged?.Invoke(x), () => Released?.Invoke());
            Label(art, RunStrings.Sfx, SfxArea.xMin, 176, 2, PixelText.Align.Left, Palette.Text, order);
            sfxSlider = new Slider(this, art, SfxArea, order, x => SfxChanged?.Invoke(x), () => Released?.Invoke());

            Label(art, RunStrings.Language, EnArea.xMin, 232, 2, PixelText.Align.Left, Palette.Text, order);
            LanguageBox(art, EnArea, RunStrings.En, true, order);
            LanguageBox(art, ViArea, RunStrings.Vi, false, order);

            // Primary button: #F4C542 fill, dark text, the shared BACK text size.
            var back = PixelButton.Create(transform, "Back", art, BackArea, RunStrings.Back, RunUi.BackTextScale, Palette.PrimaryFill, Palette.PrimaryFill, Palette.PrimaryText, order + 2);
            back.ClickCue = AudioIds.UiBack;
            BackButton = back;
            back.Clicked += () => BackRequested?.Invoke();
        }

        PixelText Label(ArtLibrary art, string s, float x, float screenTop, int scale, PixelText.Align align, Color32 c, int order)
        {
            var t = PixelText.Create(transform, s, art.Font, new Vector2(x, LocalTop(screenTop) - PixelFontData.GlyphHeight * scale), align, scale, order + 4);
            t.Set(s, c);
            return t;
        }

        /// <summary>Drawn only: the language switch arrives with the i18n task.</summary>
        void LanguageBox(ArtLibrary art, Rect r, string text, bool selected, int order)
        {
            PixelRect.Set(PixelRect.Create(transform, "LangBorder", art.Pixel, selected ? Palette.WallWarning : Palette.Wall, order + 1), r);
            PixelRect.Set(PixelRect.Create(transform, "LangFill", art.Pixel, selected ? Palette.HpBack : Palette.Floor, order + 2), r.xMin + 1, r.yMin + 1, r.xMax - 1, r.yMax - 1);
            var t = PixelText.Create(transform, text, art.Font, new Vector2(r.center.x, Mathf.Floor(r.center.y - PixelFontData.GlyphHeight)), PixelText.Align.Center, 2, order + 4);
            t.Set(text, selected ? Palette.WallWarning : Palette.TextDim);
        }

        public void SetValues(int music, int sfx)
        {
            musicSlider.Set(music, false);
            sfxSlider.Set(sfx, false);
        }

        public int MusicValue => musicSlider.Value;
        public int SfxValue => sfxSlider.Value;
        public PixelButton MusicTrack => musicSlider.Track;
        public PixelButton SfxTrack => sfxSlider.Track;

        /// <summary>Automation / tests: drag a slider to native x.</summary>
        public void DragMusicTo(float nativeX) => musicSlider.SetFromX(nativeX);
        public void DragSfxTo(float nativeX) => sfxSlider.SetFromX(nativeX);

        /// <summary>Tests / automation: the pointer is lifted after a drag.</summary>
        public void ReleasePointer() => Released?.Invoke();

        void Update() => Tick();

        /// <summary>One frame of slider following (Update calls it; EditMode tests call it between PixelInput dispatches).</summary>
        public void Tick()
        {
            musicSlider.Follow();
            sfxSlider.Follow();
        }

        /// <summary>Step from a native x on a track: cell i (0-based) = step i+1; left of the first cell's first 4 px = 0.</summary>
        public static int StepAt(Rect track, float nativeX)
        {
            var x = nativeX - track.xMin;
            if (x < 4f) return 0;
            return Mathf.Clamp(Mathf.FloorToInt(x / CellWidth) + 1, 0, Steps);
        }

        sealed class Slider
        {
            public readonly PixelButton Track;
            readonly SpriteRenderer[] cells = new SpriteRenderer[Steps];
            readonly PixelText value;
            readonly Action<int> changed;
            readonly Action released;
            bool dragging;
            public int Value { get; private set; } = -1;

            public Slider(SettingsView owner, ArtLibrary art, Rect area, int order, Action<int> onChanged, Action onReleased)
            {
                changed = onChanged;
                released = onReleased;
                Track = PixelButton.Create(owner.transform, "Slider", art, area, null, 1, Palette.Floor, Palette.Wall, Palette.Text, order + 2);
                Track.ClickCue = null;   // the slider's own feedback is the SFX click at the new volume
                Track.Clicked += () =>
                {
                    // A tap sets the value at the release point. A drag released on the track keeps the value the drag
                    // reached (it used to jump back to the press point: 6a.6b/c part 0).
                    if (!dragging) SetFromX(Track.LastReleaseNative.x);
                    dragging = false;
                    released?.Invoke();
                };
                for (var i = 0; i < Steps; i++)
                {
                    cells[i] = PixelRect.Create(Track.transform, "Cell", art.Pixel, Palette.HpBack, Track.IconOrder);
                    var x0 = area.xMin + i * CellWidth + 2;
                    PixelRect.Set(cells[i], x0, area.yMin + 3, x0 + CellWidth - 3, area.yMax - 3);
                }
                value = PixelText.Create(owner.transform, "Value", art.Font,
                    new Vector2(area.xMax + 6, Mathf.Floor(area.center.y - PixelFontData.GlyphHeight)), PixelText.Align.Left, 2, order + 4);
            }

            /// <summary>Each frame: follow the pointer while the slider is held; a release off the track also counts as a release.</summary>
            public void Follow()
            {
                if (Track.IsHeld)
                {
                    dragging = true;
                    if (PixelInput.Held) SetFromX(PixelInput.Current.x);
                }
                else if (dragging)
                {
                    dragging = false;
                    released?.Invoke();
                }
            }

            public void SetFromX(float nativeX) => Set(StepAt(Track.Area, nativeX), true);

            public void Set(int v, bool notify)
            {
                v = Mathf.Clamp(v, 0, Steps);
                if (v == Value) return;
                Value = v;
                for (var i = 0; i < Steps; i++) cells[i].color = i < v ? Palette.WallWarning : Palette.HpBack;
                value.Set(v.ToString(), Palette.Text);
                if (notify) changed?.Invoke(v);
            }
        }
    }
}
