using System;
using System.Collections.Generic;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// UI-TUT1: tips for a player's first run, one at a time, at the moment they matter. A tip dims the screen except the
    /// thing it talks about (yellow frame), explains it in two short lines, and waits for OK. SKIP TIPS closes every tip for
    /// good. While a tip is open the screen's own buttons are off, so the tap that closes it cannot also pick a card.
    /// Tips show only while the player has no finished run (ProgressData.RunsPlayed == 0) and each one only once (SeenTips).
    /// </summary>
    public sealed class TutorialCoach : MonoBehaviour
    {
        public sealed class Tip
        {
            public string Id;
            public RunFlow.State State;
            public Rect Target;
            public string Line1, Line2;
        }

        public const int Order = RunUi.Order + 30;
        /// <summary>Tips on/off for the whole game: off with -bbNoTips (QA) and in the EditMode test assembly (TipsOffForTests).</summary>
        public static bool Enabled = !DebugOptions.NoTips;
        public const float BoxWidth = 240f, BoxHeight = 74f;
        public static readonly Color32 Shade = new Color32(10, 10, 18, 190);

        /// <summary>The first-run tips, in the order they appear. Prepare has three in a row (enemy, cards, FIGHT).</summary>
        public static readonly Tip[] Tips =
        {
            new Tip { Id = "pick", State = RunFlow.State.PickWeapon, Target = new Rect(-114, -91, 228, 192),
                      Line1 = "PICK YOUR BALL", Line2 = "TAP ONE TO READ IT, AGAIN TO PICK" },
            new Tip { Id = "startpick", State = RunFlow.State.StartPick, Target = Union(StartPickView.CardArea(0), StartPickView.CardArea(2)),
                      Line1 = "PICK 1 CARD TO START", Line2 = "CARDS STAY FOR THE WHOLE RUN" },
            new Tip { Id = "enemy", State = RunFlow.State.Prepare, Target = new Rect(-125, 113, 250, 83),
                      Line1 = "YOUR NEXT FOE AND THE ARENA", Line2 = "READ ITS WEAPON AND TRAITS" },
            new Tip { Id = "cards", State = RunFlow.State.Prepare, Target = Union(PrepareView.CardArea(0), PrepareView.CardArea(2)),
                      Line1 = "1 FREE CARD BEFORE EVERY FIGHT", Line2 = "COINS BUY ONE MORE OR A REROLL" },
            new Tip { Id = "fight", State = RunFlow.State.Prepare, Target = PrepareView.FightArea,
                      Line1 = "READY? PRESS FIGHT", Line2 = "IN THE FIGHT, TAP TO USE YOUR SKILL" },
            new Tip { Id = "result", State = RunFlow.State.FightResult, Target = new Rect(-120, 49, 240, 111),
                      Line1 = "HP LEFT CARRIES TO THE NEXT FIGHT", Line2 = "LOSE ONCE: YOUR 1 RETRY IS USED" },
        };

        static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        sealed class Layout { public GameObject Root; public PixelButton Ok, Skip; public PixelText Line1, Line2; }

        RunFlow flow;
        readonly SpriteRenderer[] shade = new SpriteRenderer[4];
        readonly SpriteRenderer[] frame = new SpriteRenderer[4];
        readonly Dictionary<string, Layout> layouts = new Dictionary<string, Layout>();
        Transform screen;
        readonly List<PixelButton> disabled = new List<PixelButton>();
        float blink;

        public Tip Current { get; private set; }
        public bool Showing => Current != null;
        public PixelButton OkButton => Current == null ? null : layouts[Current.Id].Ok;
        public PixelButton SkipButton => Current == null ? null : layouts[Current.Id].Skip;
        public Rect BoxArea => Current == null ? Rect.zero : BoxFor(Current);
        public string Text => Current == null ? "" : layouts[Current.Id].Line1.Text + " " + layouts[Current.Id].Line2.Text;

        /// <summary>The tip box goes on the side of the target with more room, 8 px from it, kept on screen.</summary>
        public static Rect BoxFor(Tip tip)
        {
            var r = tip.Target;
            var below = r.yMin + 240f;
            var above = 240f - r.yMax;
            float top;
            if (above >= BoxHeight + 10 && above >= below) top = Mathf.Min(234f, r.yMax + 8 + BoxHeight);
            else if (below >= BoxHeight + 10) top = Mathf.Max(-234f + BoxHeight, r.yMin - 8);
            else top = BoxHeight / 2f;   // target fills the screen: centre (covers part of it)
            return new Rect(-BoxWidth / 2f, top - BoxHeight, BoxWidth, BoxHeight);
        }

        public static TutorialCoach Create(Transform parent, ArtLibrary art)
        {
            var go = new GameObject("TutorialCoach");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<TutorialCoach>();
            for (var i = 0; i < 4; i++)
            {
                c.shade[i] = PixelRect.Create(go.transform, "Shade", art.Pixel, Shade, Order);
                c.frame[i] = PixelRect.Create(go.transform, "Frame", art.Pixel, Palette.WallWarning, Order + 1);
            }
            for (var n = 0; n < Tips.Length; n++)
            {
                var tip = Tips[n];
                var box = BoxFor(tip);
                var l = new Layout { Root = new GameObject("Tip_" + tip.Id) };
                var t = l.Root.transform;
                t.SetParent(go.transform, false);
                PixelRect.Set(PixelRect.Create(t, "BoxBorder", art.Pixel, Palette.WallWarning, Order + 2), box.xMin, box.yMin, box.xMax, box.yMax);
                PixelRect.Set(PixelRect.Create(t, "BoxFill", art.Pixel, RunUi.Panel, Order + 3), box.xMin + 1, box.yMin + 1, box.xMax - 1, box.yMax - 1);
                l.Line1 = PixelText.Create(t, "Line1", art.Font, new Vector2(0, box.yMax - 4 - PixelFont.CellHeight), PixelText.Align.Center, 1, Order + 5);
                l.Line1.Set(tip.Line1, Palette.Text);
                l.Line2 = PixelText.Create(t, "Line2", art.Font, new Vector2(0, box.yMax - 18 - PixelFont.CellHeight), PixelText.Align.Center, 1, Order + 5);
                l.Line2.Set(tip.Line2, Palette.WallWarning);
                PixelText.Create(t, "Counter", art.Font, new Vector2(box.xMin + 8, box.yMin + 10), PixelText.Align.Left, 1, Order + 5)
                    .Set($"TIP {n + 1}/{Tips.Length}", Palette.TextDim);
                l.Ok = PixelButton.Create(t, "TipOk", art, new Rect(box.xMax - 66, box.yMin + 6, 58, 22), "OK", 1, Palette.Floor, Palette.WallWarning, Palette.WallWarning, Order + 4);
                l.Skip = PixelButton.Create(t, "TipSkip", art, new Rect(box.xMax - 152, box.yMin + 6, 80, 22), "SKIP TIPS", 1, Palette.Floor, Palette.TextDim, Palette.TextDim, Order + 4);
                l.Ok.Clicked += () => c.Close(false);
                l.Skip.Clicked += () => c.Close(true);
                l.Root.SetActive(false);
                c.layouts[tip.Id] = l;
            }
            c.gameObject.SetActive(false);
            return c;
        }

        /// <summary>
        /// After a screen is shown: open the next unseen tip for <paramref name="state"/> (buttons under <paramref name="screenRoot"/>
        /// are switched off while it shows). A tip of another state that is still open is closed without being marked seen.
        /// </summary>
        public void Offer(RunFlow runFlow, RunFlow.State state, Transform screenRoot)
        {
            flow = runFlow;
            if (Showing && Current.State != state) Hide();
            if (Showing) { DisableButtons(); return; }   // the screen redrew under the tip: keep its buttons off
            if (!Enabled || flow == null || flow.Progress == null || flow.Progress.RunsPlayed > 0) return;
            foreach (var t in Tips)
                if (t.State == state && !flow.TipSeen(t.Id)) { Open(t, screenRoot); return; }
        }

        void Open(Tip tip, Transform screenRoot)
        {
            Current = tip;
            screen = screenRoot;
            gameObject.SetActive(true);
            var r = tip.Target;
            // Shade everything but the target: below, above, left, right of it (screen 270x480).
            PixelRect.Set(shade[0], -135, -240, 135, r.yMin - 2);
            PixelRect.Set(shade[1], -135, r.yMax + 2, 135, 240);
            PixelRect.Set(shade[2], -135, r.yMin - 2, r.xMin - 2, r.yMax + 2);
            PixelRect.Set(shade[3], r.xMax + 2, r.yMin - 2, 135, r.yMax + 2);
            // 1 px yellow frame 2 px outside the target.
            PixelRect.Set(frame[0], r.xMin - 2, r.yMin - 2, r.xMax + 2, r.yMin - 1);
            PixelRect.Set(frame[1], r.xMin - 2, r.yMax + 1, r.xMax + 2, r.yMax + 2);
            PixelRect.Set(frame[2], r.xMin - 2, r.yMin - 2, r.xMin - 1, r.yMax + 2);
            PixelRect.Set(frame[3], r.xMax + 1, r.yMin - 2, r.xMax + 2, r.yMax + 2);
            foreach (var l in layouts.Values) l.Root.SetActive(false);
            layouts[tip.Id].Root.SetActive(true);   // OnEnable: its buttons only take taps from now on
            DisableButtons();
            blink = 0f;
        }

        void DisableButtons()
        {
            if (screen == null) return;
            foreach (var b in screen.GetComponentsInChildren<PixelButton>(true))
            {
                if (!b.enabled || b.transform.IsChildOf(transform)) continue;
                b.enabled = false;
                disabled.Add(b);
            }
        }

        void Close(bool skipAll)
        {
            if (Current == null) return;
            if (flow != null)
            {
                if (skipAll) { var ids = new string[Tips.Length]; for (var i = 0; i < Tips.Length; i++) ids[i] = Tips[i].Id; flow.MarkTipsSeen(ids); }
                else flow.MarkTipsSeen(Current.Id);
            }
            var state = Current.State;
            var root = screen;
            Hide();
            Offer(flow, state, root);   // the next tip of the same screen, if any (Prepare: enemy -> cards -> FIGHT)
        }

        /// <summary>Close the tip without marking it seen and give the screen its buttons back.</summary>
        public void Hide()
        {
            foreach (var b in disabled) if (b != null) b.enabled = true;   // OnEnable: NoteShown, so the closing tap is not a click
            disabled.Clear();
            Current = null;
            screen = null;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (!Showing) return;
            blink += Time.unscaledDeltaTime;
            var on = (blink % 0.8f) < 0.55f;
            foreach (var f in frame) f.enabled = on;
        }
    }
}
