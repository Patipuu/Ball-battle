using System;
using System.Collections.Generic;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Pointer input in native pixel coordinates (origin at screen centre, y up — same as world space).
    /// The conversion mirrors PixelPerfectCamera "Windowbox": largest integer zoom that fits, image centred.
    ///
    /// Clicks are an ordered queue of press/release events, dispatched one by one to the buttons on screen
    /// (PixelButton). Polling the mouse once per frame lost clicks: two taps landing in the same frame (fast
    /// fingers, a low frame rate, automated testers) collapsed into one at the last position, and every button
    /// read the frame in its own Update order (so "BLADE then START" could start the match before BLADE applied).
    /// Events come from IMGUI (exact order and position of every press/release); until IMGUI has delivered a
    /// mouse event (and on platforms where it never does) the legacy mouse API is polled instead.
    ///
    /// Each event carries a sequence number: a button only reacts to events that happened after it was shown,
    /// so the tap that closes one screen can never also hit the screen that replaces it. BlockFor() drops every
    /// event for a short time (screen entry guard).
    /// </summary>
    public static class PixelInput
    {
        public const int NativeWidth = 270;
        public const int NativeHeight = 480;

        public struct PointerEvent
        {
            public bool Down;
            public Vector2 Native;
            public long Seq;
            public float Time;
        }

        static readonly List<PointerEvent> queue = new List<PointerEvent>();
        static readonly List<PixelButton> live = new List<PixelButton>();
        static readonly List<PixelButton> snapshot = new List<PixelButton>();
        static long nextSeq = 1;
        static float blockedUntil = float.NegativeInfinity;
        static float? testClock;
        static bool guiMode;
        static float guiScale = 1f;
        static bool pointerDown;
        static PixelInputPump pump;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            queue.Clear();
            live.Clear();
            nextSeq = 1;
            blockedUntil = float.NegativeInfinity;
            testClock = null;
            guiMode = false;
            guiScale = 1f;
            pointerDown = false;
            captureDown = false;
            polledFrame = int.MinValue;
            polledDowns = polledUps = 0;
            pump = null;
        }

        /// <summary>Integer zoom used for a given screen size (at least 1).</summary>
        public static int Zoom(int screenWidth, int screenHeight) =>
            Mathf.Max(1, Mathf.Min(screenWidth / NativeWidth, screenHeight / NativeHeight));

        /// <summary>Screen pixel (origin bottom-left) → native pixel (origin centre).</summary>
        public static Vector2 ScreenToNative(Vector2 screen, int screenWidth, int screenHeight)
        {
            var zoom = Zoom(screenWidth, screenHeight);
            var offsetX = (screenWidth - NativeWidth * zoom) * 0.5f;
            var offsetY = (screenHeight - NativeHeight * zoom) * 0.5f;
            return new Vector2(
                (screen.x - offsetX) / zoom - NativeWidth * 0.5f,
                (screen.y - offsetY) / zoom - NativeHeight * 0.5f);
        }

        /// <summary>Unscaled clock used for event times and the entry block (tests can drive it).</summary>
        public static float Now => testClock ?? Time.unscaledTime;

        /// <summary>Sequence number the next event will get (a button shown now ignores everything before it).</summary>
        public static long NextSeq => nextSeq;

        /// <summary>True while a press is down (from the events dispatched so far).</summary>
        public static bool Held => pointerDown;

        public static Vector2 Current => ScreenToNative(Input.mousePosition, Screen.width, Screen.height);

        /// <summary>Drop every event for <paramref name="seconds"/> from now (also anything still queued from before).</summary>
        public static void BlockFor(float seconds) => blockedUntil = Mathf.Max(blockedUntil, Now + seconds);

        public static bool Blocked => Now < blockedUntil;

        // ---- Buttons on screen ----

        internal static void Register(PixelButton b)
        {
            if (!live.Contains(b)) live.Add(b);
            if (Application.isPlaying) EnsurePump();
        }

        internal static void Unregister(PixelButton b) => live.Remove(b);

        static void EnsurePump()
        {
            if (pump != null) return;
            var go = new GameObject("PixelInputPump") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            pump = go.AddComponent<PixelInputPump>();
        }

        // ---- Capture ----

        /// <summary>Queue a press (down) or release at a native-pixel point. Used by capture, tests and automation.</summary>
        public static void Inject(bool down, Vector2 native)
        {
            queue.Add(new PointerEvent { Down = down, Native = native, Seq = nextSeq++, Time = Now });
        }

        // Capture state, shared by both sources.
        /// <summary>The pointer is down as far as capture knows (any source). A release with no press seen first gets one.</summary>
        static bool captureDown;
        /// <summary>Frame of the last polling pass and how many presses / releases it captured (IMGUI duplicates of those are skipped).</summary>
        static int polledFrame = int.MinValue;
        static int polledDowns, polledUps;

        /// <summary>Called by the pump every frame before any other script: poll (until IMGUI events arrive), then dispatch.</summary>
        internal static void PumpFrame()
        {
            PollFrame(Time.frameCount, Input.GetMouseButtonDown(0), Input.GetMouseButtonUp(0), Current);
            Dispatch();
        }

        /// <summary>
        /// One polling pass (legacy mouse API) for <paramref name="frame"/>: press then release, in that order, even when both
        /// happened in the same frame. A release whose press the API never reported (press and release inside one frame on a
        /// slow frame, or the click that focuses the window) still makes a click: the press is added right before it.
        /// Ignored once IMGUI events are the source. Public for tests / automation.
        /// </summary>
        public static void PollFrame(int frame, bool pressed, bool released, Vector2 native)
        {
            if (guiMode) return;
            polledFrame = frame;
            polledDowns = 0;
            polledUps = 0;
            if (pressed) { CaptureDown(native); polledDowns++; }
            if (released) { CaptureUp(native); polledUps++; }
        }

        /// <summary>Called by the pump's OnGUI for every IMGUI event.</summary>
        internal static void CaptureGui(Event e)
        {
            if (e == null || e.button != 0 || (e.type != EventType.MouseDown && e.type != EventType.MouseUp)) return;
            if (!guiMode)
            {
                // IMGUI may measure in points: calibrate against the polled pixel position on the first event.
                var polled = (Vector2)Input.mousePosition;
                if (e.mousePosition.x > 4f && polled.x > 4f)
                {
                    var s = polled.x / e.mousePosition.x;
                    guiScale = Mathf.Abs(s - 1f) < 0.05f ? 1f : Mathf.Clamp(s, 0.5f, 4f);
                }
            }
            var screen = new Vector2(e.mousePosition.x * guiScale, Screen.height - e.mousePosition.y * guiScale);
            GuiEvent(Time.frameCount, e.type == EventType.MouseDown, ScreenToNative(screen, Screen.width, Screen.height));
        }

        /// <summary>
        /// One IMGUI mouse event (exact order and position of every press / release) of <paramref name="frame"/>. The first
        /// one switches the source from polling to IMGUI. An event that the polling pass of the SAME frame already captured
        /// (same kind, not yet matched) is skipped, so a click is never counted twice; anything polling did not see - the
        /// window-focus click, a press and release inside one frame - is taken from IMGUI. Public for tests / automation.
        /// </summary>
        public static void GuiEvent(int frame, bool down, Vector2 native)
        {
            guiMode = true;
            if (frame == polledFrame)
            {
                if (down && polledDowns > 0) { polledDowns--; return; }
                if (!down && polledUps > 0) { polledUps--; return; }
            }
            if (down) CaptureDown(native);
            else CaptureUp(native);
        }

        static void CaptureDown(Vector2 native)
        {
            Inject(true, native);
            captureDown = true;
        }

        static void CaptureUp(Vector2 native)
        {
            if (!captureDown) Inject(true, native);   // the source reported only the release: the press happened in the same frame
            Inject(false, native);
            captureDown = false;
        }

        /// <summary>True once IMGUI events are the input source (tests).</summary>
        public static bool UsesGuiEvents => guiMode;

        /// <summary>Back to the start state: polling source, nothing pressed, nothing queued (tests).</summary>
        public static void ResetCapture()
        {
            guiMode = false;
            guiScale = 1f;
            captureDown = false;
            polledFrame = int.MinValue;
            polledDowns = polledUps = 0;
            queue.Clear();
            pointerDown = false;
        }
        // ---- Dispatch ----

        /// <summary>Deliver the queued events, in order, to the buttons on screen.</summary>
        public static void Dispatch() => Dispatch(null);

        /// <summary>
        /// Deliver the queued events, in order. <paramref name="buttons"/> lists the buttons on screen, asked again
        /// before every event (a click can open or close screens); null = the buttons registered while enabled.
        /// </summary>
        public static void Dispatch(Func<IEnumerable<PixelButton>> buttons)
        {
            if (queue.Count == 0) return;
            var events = queue.ToArray();
            queue.Clear();
            foreach (var e in events)
            {
                if (e.Time < blockedUntil)
                {
                    if (!e.Down) pointerDown = false;
                    continue;   // dropped: a press here never starts a click, a release here never ends one
                }
                pointerDown = e.Down;
                snapshot.Clear();
                if (buttons != null) snapshot.AddRange(buttons());
                else snapshot.AddRange(live);
                foreach (var b in snapshot)
                    if (b != null && b.isActiveAndEnabled) b.Handle(e);
            }
            snapshot.Clear();
        }

        // ---- Tests ----

        /// <summary>Drive time by hand (null = real clock). Also clears the queue and any block.</summary>
        public static void SetTestClock(float? seconds)
        {
            testClock = seconds;
            queue.Clear();
            blockedUntil = float.NegativeInfinity;
            pointerDown = false;
        }

        public static void AdvanceTestClock(float seconds)
        {
            if (testClock.HasValue) testClock += seconds;
        }

        /// <summary>A full tap (press + release) at one point, queued.</summary>
        public static void InjectTap(Vector2 native)
        {
            Inject(true, native);
            Inject(false, native);
        }
    }

    /// <summary>Runs first every frame: feeds PixelInput (polling or IMGUI) and dispatches its events.</summary>
    [DefaultExecutionOrder(-10000)]
    sealed class PixelInputPump : MonoBehaviour
    {
        void Awake() => useGUILayout = false;
        void Update() => PixelInput.PumpFrame();
        void OnGUI() => PixelInput.CaptureGui(Event.current);
    }
}
