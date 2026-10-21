using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Typing a short text (account name, password) on every platform the game ships on:
    /// - WebGL (itch.io): an HTML input box over the canvas (BallBattleSync.jslib), so phones get their own keyboard;
    /// - Android / iOS: the system keyboard (TouchScreenKeyboard), secure for passwords;
    /// - Editor / desktop: keys typed while the field is open (Enter = OK, Esc = cancel), shown live through <see cref="Typing"/>.
    /// One request at a time; the callback gets the text, or null when cancelled.
    /// </summary>
    public sealed class TextEntry : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void BB_OpenTextInput(string objectName, string title, string value, int password, int maxLength);
#endif
        public const string ObjectName = "BallBattleTextEntry";

        static TextEntry instance;
        Action<string> done;
        TouchScreenKeyboard keyboard;
        bool desktop;
        int maxLength;
        string typed = "";

        /// <summary>Test hook: answers every request at once (null = cancel).</summary>
        public static Func<string, string> TestAnswer;
        /// <summary>Desktop typing in progress (the field shows it); null when no desktop entry is open.</summary>
        public static string Typing => instance == null || instance.done == null ? null
            : instance.desktop ? instance.typed : instance.keyboard?.text;
        public static bool Open => instance != null && instance.done != null;

        static TextEntry Instance
        {
            get
            {
                if (instance != null) return instance;
                var go = new GameObject(ObjectName);
                if (Application.isPlaying) DontDestroyOnLoad(go);
                return instance = go.AddComponent<TextEntry>();
            }
        }

        public static void Request(string title, string current, bool password, int maxLength, Action<string> onDone)
        {
            if (TestAnswer != null) { onDone?.Invoke(TestAnswer(title)); return; }
            var e = Instance;
            CommitPending();   // a field still being typed into keeps its text when the player moves to another field
            e.done = onDone;
            e.maxLength = maxLength;
            e.typed = current ?? "";
            e.desktop = false;
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = false;   // let the HTML box get the keys
            BB_OpenTextInput(ObjectName, UiVietnamese.Translate(title), current ?? "", password ? 1 : 0, maxLength);
#elif UNITY_EDITOR
            e.desktop = true;   // the Editor (Device Simulator included) has no phone keyboard: type on the PC keyboard
#else
            if (TouchScreenKeyboard.isSupported && Application.isMobilePlatform)
                e.keyboard = TouchScreenKeyboard.Open(current ?? "", TouchScreenKeyboardType.Default, false, false, password, false, UiVietnamese.Translate(title), maxLength);
            else e.desktop = true;
#endif
        }

        /// <summary>Tests: keys typed on the desktop path (as if read from Input.inputString).</summary>
        public static void SimulateTyping(string text) { if (instance != null && instance.desktop) instance.typed += text; }

        public static void Cancel() { if (instance != null) instance.Finish(null); }

        /// <summary>
        /// Close an open entry keeping what was typed so far (desktop keys or phone keyboard). Called before another field opens
        /// and before REGISTER / LOG IN, so the player does not have to press Enter first. The WebGL box confirms with its own OK.
        /// </summary>
        public static void CommitPending()
        {
            if (instance == null || instance.done == null) return;
            if (instance.desktop) instance.Finish(instance.typed);
            else if (instance.keyboard != null) instance.Finish(instance.keyboard.text ?? "");
            else instance.Finish(null);
        }

        // Called from the WebGL plugin (SendMessage).
        void OnTextEntry(string value) => Finish(value ?? "");
        void OnTextEntryCancel(string _) => Finish(null);

        void Finish(string value)
        {
            var callback = done;
            done = null;
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = true;
#endif
            keyboard = null;
            desktop = false;
            callback?.Invoke(value);
        }

        void Update()
        {
            if (done == null) return;
            if (keyboard != null)
            {
                switch (keyboard.status)
                {
                    case TouchScreenKeyboard.Status.Done: Finish(keyboard.text ?? ""); break;
                    // Tapping the game (e.g. the next field) closes the phone keyboard with LostFocus: keep what was typed.
                    case TouchScreenKeyboard.Status.LostFocus: Finish(keyboard.text ?? ""); break;
                    case TouchScreenKeyboard.Status.Canceled: Finish(null); break;
                }
                return;
            }
            if (!desktop) return;
            foreach (var c in Input.inputString)
            {
                if (c == '\b') { if (typed.Length > 0) typed = typed.Substring(0, typed.Length - 1); }
                else if (c == '\n' || c == '\r') { Finish(typed); return; }
                else if (!char.IsControl(c) && typed.Length < maxLength) typed += c;
            }
            if (Input.GetKeyDown(KeyCode.Escape)) Finish(null);
        }
    }
}
