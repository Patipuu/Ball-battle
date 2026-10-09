using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Pointer input in native pixel coordinates (origin at screen centre, y up — same as world space).
    /// The conversion mirrors PixelPerfectCamera "Windowbox": largest integer zoom that fits, image centred.
    /// Uses the legacy Input Manager mouse API, which also reports the first touch on mobile.
    /// </summary>
    public static class PixelInput
    {
        public const int NativeWidth = 270;
        public const int NativeHeight = 480;

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

        public static bool PressedThisFrame(out Vector2 native)
        {
            native = default;
            if (!Input.GetMouseButtonDown(0)) return false;
            native = ScreenToNative(Input.mousePosition, Screen.width, Screen.height);
            return true;
        }

        public static bool ReleasedThisFrame(out Vector2 native)
        {
            native = default;
            if (!Input.GetMouseButtonUp(0)) return false;
            native = ScreenToNative(Input.mousePosition, Screen.width, Screen.height);
            return true;
        }

        public static bool Held => Input.GetMouseButton(0);

        public static Vector2 Current => ScreenToNative(Input.mousePosition, Screen.width, Screen.height);
    }
}
