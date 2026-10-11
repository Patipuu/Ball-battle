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

        /// <summary>Called by the pump every frame before any other script: poll (until IMGUI events arrive), then dispatch.</summary>
        internal static void PumpFrame()
        {
            if (!guiMode)
            {
                if (Input.GetMouseButtonDown(0)) Inject(true, Current);
                if (Input.GetMouseButtonUp(0)) Inject(false, Current);
            }
            Dispatch();
        }

        /// <summary>Called by the pump's OnGUI for every IMGUI event.</summary>
        internal static void CaptureGui(Event e)
        {
            if (e == null || e.button != 0 || (e.type != EventType.MouseDown && e.type != EventType.MouseUp)) return;
            var down = e.type == EventType.MouseDown;
            if (!guiMode)
            {
                // First IMGUI mouse event: from now on IMGUI is the source. This click was already polled this frame,
                // so it is not queued twice. IMGUI may measure in points: calibrate against the polled pixel position.
                guiMode = true;
                var polled = (Vector2)Input.mousePosition;
                if (e.mousePosition.x > 4f && polled.x > 4f)
                {
                    var s = polled.x / e.mousePosition.x;
                    guiScale = Mathf.Abs(s - 1f) < 0.05f ? 1f : Mathf.Clamp(s, 0.5f, 4f);
                }
                return;
            }
            var screen = new Vector2(e.mousePosition.x * guiScale, Screen.height - e.mousePosition.y * guiScale);
            Inject(down, ScreenToNative(screen, Screen.width, Screen.height));
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
