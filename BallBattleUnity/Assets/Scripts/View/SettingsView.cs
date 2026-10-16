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

        /// <summary>Gear button on the menu hub: (242,4), 24x24 tap target around the 12x12 gear (BB-FONT1: was 12x12).</summary>
        public static readonly Rect GearArea = ScreenRect(242, 4, 24, 24);
        /// <summary>Slider tracks 200x24 (BB-FONT1: 24 px tap target, was 16), each under its 13*2 px label.</summary>
        public static readonly Rect MusicArea = ScreenRect(35, 150, Steps * CellWidth, 24);
        public static readonly Rect SfxArea = ScreenRect(35, 214, Steps * CellWidth, 24);
        public static readonly Rect EnArea = ScreenRect(35, 280, 40, 28);
        public static readonly Rect ViArea = ScreenRect(85, 280, 40, 28);
        public static readonly Rect BackArea = ScreenRect(75, 340, 120, 32);

        /// <summary>Live value changes while tapping/dragging (volume changes at once; nothing is saved here).</summary>
        public event Action<int> MusicChanged, SfxChanged;
        public event Action BackRequested;
        public event Action<Lang> LanguageChanged;
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
            Label(art, RunStrings.Music, MusicArea.xMin, 122, 2, PixelText.Align.Left, Palette.Text, order);
            musicSlider = new Slider(this, art, MusicArea, order, x => MusicChanged?.Invoke(x), () => Released?.Invoke());
            Label(art, RunStrings.Sfx, SfxArea.xMin, 186, 2, PixelText.Align.Left, Palette.Text, order);
            sfxSlider = new Slider(this, art, SfxArea, order, x => SfxChanged?.Invoke(x), () => Released?.Invoke());

            Label(art, RunStrings.Language, EnArea.xMin, 250, 2, PixelText.Align.Left, Palette.Text, order);
            LanguageBox(art, EnArea, RunStrings.En, StringTable.Current == Lang.En, Lang.En, order);
            LanguageBox(art, ViArea, RunStrings.Vi, StringTable.Current == Lang.Vi, Lang.Vi, order);

            // Primary button: #F4C542 fill, dark text, the shared BACK text size.
            var back = PixelButton.Create(transform, "Back", art, BackArea, RunStrings.Back, RunUi.BackTextScale, Palette.PrimaryFill, Palette.PrimaryFill, Palette.PrimaryText, order + 2);
            back.ClickCue = AudioIds.UiBack;
            BackButton = back;
            back.Clicked += () => BackRequested?.Invoke();
        }

        PixelText Label(ArtLibrary art, string s, float x, float screenTop, int scale, PixelText.Align align, Color32 c, int order)
        {
            var t = PixelText.Create(transform, s, art.Font, new Vector2(x, LocalTop(screenTop) - PixelFont.CellHeight * scale), align, scale, order + 4);
            t.Set(s, c);
            return t;
        }

        void LanguageBox(ArtLibrary art, Rect r, string text, bool selected, Lang language, int order)
        {
            var button = PixelButton.Create(transform, "Language" + text, art, r, text, 2,
                selected ? Palette.HpBack : Palette.Floor, selected ? Palette.WallWarning : Palette.Wall,
                selected ? Palette.WallWarning : Palette.TextDim, order + 2);
            button.Clicked += () => { if (StringTable.Current != language) LanguageChanged?.Invoke(language); };
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
                    new Vector2(area.xMax + 6, Mathf.Floor(area.center.y - PixelFont.CellHeight)), PixelText.Align.Left, 2, order + 4);
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
