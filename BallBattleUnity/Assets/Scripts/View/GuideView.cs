using System;
using System.Collections.Generic;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// BI the guide ball talks the player through each screen the first time it appears (GuideDirector picks the scene).
    /// A dialogue box (portrait, name, up to 4 lines typed out) sits at the bottom of the screen, or at the top when the part of
    /// the screen it talks about is down there; that part stays lit inside a blinking frame while the rest is dimmed.
    /// Tap anywhere: finish the line, then the next line. SKIP ends the scene. While it shows, every other button is off and the
    /// game waits (<see cref="Blocking"/>: Run flow and arena do not tick). Seen scenes are kept in PlayerPrefs (guide_seen);
    /// Settings > REPLAY GUIDE clears them.
    /// </summary>
    public sealed class GuideView : MonoBehaviour
    {
        public const int Order = 300;
        public const string SeenKey = "guide_seen";
        public const float CharsPerSecond = 45f;
        public const string Name = "BI";
        /// <summary>Off with -bbNoTips and in the EditMode test assembly (TipsOffForTests); guide tests switch it on.</summary>
        public static bool Enabled = !DebugOptions.NoTips;
        public static readonly Rect BottomPanel = new Rect(-131, -236, 262, 92);
        public static readonly Rect TopPanel = new Rect(-131, 144, 262, 92);
        public const int TextLeft = -74, TextWidth = 198, MaxLines = 4;

        static GuideView instance;
        /// <summary>True while BI is talking: the Run flow, PvP replays and the arena wait.</summary>
        public static bool Blocking => instance != null && instance.Showing;

        readonly SpriteRenderer[] shade = new SpriteRenderer[4];
        readonly SpriteRenderer[] frame = new SpriteRenderer[4];
        SpriteRenderer panelBorder, panelFill, portrait;
        PixelText nameText, counter, tapHint;
        readonly PixelText[] lines = new PixelText[MaxLines];
        PixelButton next, skipAbove, skipBelow;
        readonly List<PixelButton> disabled = new List<PixelButton>();
        readonly List<string> wrapped = new List<string>();
        Rect panel;
        float reveal, clock, blinkClock;
        int totalChars;

        public GuideScene Current { get; private set; }
        public int LineIndex { get; private set; }
        public bool Showing => Current != null;
        public event Action<string> Finished;
        /// <summary>The whole current line (as wrapped), whether or not it is fully typed out yet.</summary>
        public string LineText => string.Join(" ", wrapped);
        public bool LineComplete => reveal >= totalChars;
        public Rect PanelArea => panel;
        public PixelButton SkipButton => skipAbove.gameObject.activeSelf ? skipAbove : skipBelow;
        public PixelButton NextButton => next;

        public static GuideView Create(Transform parent, ArtLibrary art)
        {
            var go = new GameObject("Guide");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<GuideView>();
            v.Build(art);
            go.SetActive(false);
            instance = v;
            return v;
        }

        void Build(ArtLibrary art)
        {
            var dim = new Color32(8, 8, 16, 175);
            for (var i = 0; i < 4; i++)
            {
                shade[i] = PixelRect.Create(transform, "Shade", art.Pixel, dim, Order);
                frame[i] = PixelRect.Create(transform, "Frame", art.Pixel, Palette.WallWarning, Order + 1);
            }
            // Tap anywhere = next. Invisible; under the SKIP button (both get the tap: SKIP wins, see Next()).
            var clear = new Color32(0, 0, 0, 0);
            next = PixelButton.Create(transform, "GuideNext", art, new Rect(-200, -300, 400, 600), null, 1, clear, clear, clear, Order + 2);
            next.Clicked += Next;
            panelBorder = PixelRect.Create(transform, "PanelBorder", art.Pixel, Palette.WallWarning, Order + 3);
            panelFill = PixelRect.Create(transform, "PanelFill", art.Pixel, RunUi.Panel, Order + 4);
            var go = new GameObject("Portrait");
            go.transform.SetParent(transform, false);
            portrait = go.AddComponent<SpriteRenderer>();
            portrait.sortingOrder = Order + 6;
            portrait.sprite = GuideArt.Get(GuideArt.Face.Idle);
            nameText = PixelText.Create(transform, "Name", art.Font, Vector2.zero, PixelText.Align.Center, 1, Order + 6);
            for (var i = 0; i < MaxLines; i++)
                lines[i] = PixelText.Create(transform, "Line" + i, art.Font, Vector2.zero, PixelText.Align.Left, 1, Order + 6);
            counter = PixelText.Create(transform, "Counter", art.Font, Vector2.zero, PixelText.Align.Left, 1, Order + 6);
            tapHint = PixelText.Create(transform, "TapHint", art.Font, Vector2.zero, PixelText.Align.Right, 1, Order + 6);
            // SKIP sits just outside the box: above it when the box is at the bottom, below it when the box is at the top.
            skipAbove = PixelButton.Create(transform, "GuideSkip", art, new Rect(BottomPanel.xMax - 70, BottomPanel.yMax + 3, 70, 18), "SKIP GUIDE", 1,
                Palette.Floor, Palette.TextDim, Palette.Text, Order + 7);
            skipBelow = PixelButton.Create(transform, "GuideSkipBelow", art, new Rect(TopPanel.xMax - 70, TopPanel.yMin - 21, 70, 18), "SKIP GUIDE", 1,
                Palette.Floor, Palette.TextDim, Palette.Text, Order + 7);
            skipAbove.Clicked += Skip;
            skipBelow.Clicked += Skip;
        }

        // ---- Seen scenes ----

        public static bool Seen(string id) => Array.IndexOf(SeenList(), id) >= 0;
        static string[] SeenList() => PlayerPrefs.GetString(SeenKey, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        public static void MarkSeen(string id)
        {
            if (string.IsNullOrEmpty(id) || Seen(id)) return;
            var s = PlayerPrefs.GetString(SeenKey, "");
            PlayerPrefs.SetString(SeenKey, s.Length == 0 ? id : s + "," + id);
            PlayerPrefs.Save();
        }
        /// <summary>Settings > REPLAY GUIDE: every scene plays again the next time its screen shows.</summary>
        public static void ResetSeen()
        {
            PlayerPrefs.DeleteKey(SeenKey);
            PlayerPrefs.Save();
        }

        // ---- Showing ----

        /// <summary>Start <paramref name="scene"/> unless the guide is off, busy, or the scene was already seen.</summary>
        public bool Offer(GuideScene scene)
        {
            if (!Enabled || scene == null || Showing || Seen(scene.Id)) return false;
            Current = scene;
            gameObject.SetActive(true);
            DisableOthers();
            ShowLine(0);
            PixelButton.NoteShownAll(transform);   // the tap that opened this screen is not a "next"
            return true;
        }

        void ShowLine(int index)
        {
            LineIndex = index;
            var line = Current.Lines[index];
            var focus = line.Focus;
            panel = focus.HasValue && focus.Value.Overlaps(BottomPanel) ? TopPanel : BottomPanel;
            Layout(focus);
            wrapped.Clear();
            wrapped.AddRange(TextWrap.SplitToFit(line.Text(StringTable.Current), TextWidth));
            while (wrapped.Count > MaxLines) { wrapped[MaxLines - 1] += " " + wrapped[MaxLines]; wrapped.RemoveAt(MaxLines); }
            totalChars = 0;
            foreach (var w in wrapped) totalChars += w.Length;
            reveal = 0f;
            counter.Set($"{index + 1}/{Current.Lines.Length}", Palette.TextDim);
            DrawText();
        }

        void Layout(Rect? focus)
        {
            if (focus.HasValue)
            {
                var r = focus.Value;
                PixelRect.Set(shade[0], -200, -300, 200, r.yMin - 2);
                PixelRect.Set(shade[1], -200, r.yMax + 2, 200, 300);
                PixelRect.Set(shade[2], -200, r.yMin - 2, r.xMin - 2, r.yMax + 2);
                PixelRect.Set(shade[3], r.xMax + 2, r.yMin - 2, 200, r.yMax + 2);
                PixelRect.Set(frame[0], r.xMin - 2, r.yMin - 2, r.xMax + 2, r.yMin - 1);
                PixelRect.Set(frame[1], r.xMin - 2, r.yMax + 1, r.xMax + 2, r.yMax + 2);
                PixelRect.Set(frame[2], r.xMin - 2, r.yMin - 2, r.xMin - 1, r.yMax + 2);
                PixelRect.Set(frame[3], r.xMax + 1, r.yMin - 2, r.xMax + 2, r.yMax + 2);
                for (var i = 1; i < 4; i++) shade[i].enabled = true;
                foreach (var f in frame) f.enabled = true;
            }
            else
            {
                PixelRect.Set(shade[0], -200, -300, 200, 300);
                for (var i = 1; i < 4; i++) shade[i].enabled = false;
                foreach (var f in frame) f.enabled = false;
            }
            PixelRect.Set(panelBorder, panel.xMin, panel.yMin, panel.xMax, panel.yMax);
            PixelRect.Set(panelFill, panel.xMin + 1, panel.yMin + 1, panel.xMax - 1, panel.yMax - 1);
            portrait.transform.localPosition = new Vector3(panel.xMin + 29, panel.yMax - 34, 0);
            nameText.transform.localPosition = new Vector3(panel.xMin + 29, panel.yMin + 6, 0);
            nameText.Set(Name, Palette.WallWarning);
            for (var i = 0; i < MaxLines; i++)
                lines[i].transform.localPosition = new Vector3(TextLeft, panel.yMax - 8 - PixelFont.CellHeight - i * 15, 0);
            counter.transform.localPosition = new Vector3(TextLeft, panel.yMin + 5, 0);
            tapHint.transform.localPosition = new Vector3(panel.xMax - 7, panel.yMin + 5, 0);
            skipAbove.gameObject.SetActive(panel.yMin < 0);
            skipBelow.gameObject.SetActive(panel.yMin > 0);
        }

        void DrawText()
        {
            var left = Mathf.FloorToInt(reveal);
            for (var i = 0; i < MaxLines; i++)
            {
                var text = i < wrapped.Count ? wrapped[i] : "";
                var shown = Mathf.Clamp(left, 0, text.Length);
                lines[i].Set(text.Substring(0, shown), Palette.Text);
                left -= text.Length;
            }
        }

        void Next()
        {
            if (!Showing || skipClicked == Time.frameCount) return;
            if (!LineComplete) { reveal = totalChars; DrawText(); return; }   // first tap: show the whole line
            if (LineIndex + 1 < Current.Lines.Length) { ShowLine(LineIndex + 1); return; }
            Finish();
        }

        int skipClicked = -1;

        void Skip()
        {
            if (!Showing) return;
            skipClicked = Time.frameCount;
            Finish();
        }

        void Finish()
        {
            var id = Current.Id;
            MarkSeen(id);
            Current = null;
            foreach (var b in disabled) if (b != null) b.enabled = true;   // OnEnable: NoteShown, so this tap is not a click there
            disabled.Clear();
            gameObject.SetActive(false);
            Finished?.Invoke(id);
        }

        /// <summary>Close without marking the scene seen (its screen went away).</summary>
        public void Cancel()
        {
            if (!Showing) return;
            Current = null;
            foreach (var b in disabled) if (b != null) b.enabled = true;
            disabled.Clear();
            gameObject.SetActive(false);
        }

        void DisableOthers()
        {
            foreach (var b in FindObjectsByType<PixelButton>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!b.enabled || b.transform.IsChildOf(transform)) continue;
                b.enabled = false;
                disabled.Add(b);
            }
        }

        // ---- Tests / automation ----
        public void PressNext() => Next();
        public void PressSkip() => Skip();

        void Update()
        {
            if (!Showing) return;
            DisableOthers();   // a screen that redrew under the guide turns its buttons back on
            var dt = Time.unscaledDeltaTime;
            clock += dt;
            if (!LineComplete)
            {
                reveal = Mathf.Min(totalChars, reveal + dt * CharsPerSecond);
                DrawText();
            }
            blinkClock += dt;
            if (blinkClock > 3.2f) blinkClock = 0f;
            var face = !LineComplete && (clock % 0.24f) < 0.12f ? GuideArt.Face.Talk : blinkClock > 3.05f ? GuideArt.Face.Blink : GuideArt.Face.Idle;
            portrait.sprite = GuideArt.Get(face);
            var bob = Mathf.Round(Mathf.Sin(clock * 3f));
            portrait.transform.localPosition = new Vector3(panel.xMin + 29, panel.yMax - 34 + bob, 0);
            tapHint.Set(LineComplete && (clock % 1f) < 0.7f ? "TAP TO CONTINUE" : "", Palette.WallWarning);
            var on = (clock % 0.8f) < 0.55f;
            if (frame[0].enabled != on && Current.Lines[LineIndex].Focus.HasValue) foreach (var f in frame) f.enabled = on;
        }
    }
}
