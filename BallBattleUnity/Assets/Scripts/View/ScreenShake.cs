using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Camera shake in whole native pixels only (never sub-pixel, so the pixel grid stays crisp).
    /// Amplitude capped at 3 px; a new offset is picked every ~1/30 s; decays linearly.
    /// </summary>
    public sealed class ScreenShake
    {
        public const int MaxAmplitude = 3;
        const float Interval = 1f / 30f;

        readonly Transform cam;
        Vector3 basePosition;
        float amplitude;
        float duration;
        float remaining;
        float timer;

        public ScreenShake(Transform camera)
        {
            cam = camera;
            basePosition = camera.position;
        }

        public void Add(float amplitudePx, float seconds)
        {
            // Re-capture the rest position when idle, so camera moves made by other code (menus, transitions) are kept.
            if (remaining <= 0f) basePosition = cam.position;
            amplitude = Mathf.Min(MaxAmplitude, Mathf.Max(amplitude * (remaining / Mathf.Max(duration, 0.0001f)), amplitudePx));
            duration = Mathf.Max(seconds, remaining);
            remaining = duration;
        }

        public void Stop()
        {
            remaining = 0f;
            cam.position = basePosition;
        }

        public void Tick(float dt)
        {
            if (remaining <= 0f) return;
            remaining -= dt;
            if (remaining <= 0f) { Stop(); return; }

            timer -= dt;
            if (timer > 0f) return;
            timer = Interval;
            var a = Mathf.RoundToInt(amplitude * (remaining / duration));
            if (a <= 0) { cam.position = basePosition; return; }
            cam.position = basePosition + new Vector3(Random.Range(-a, a + 1), Random.Range(-a, a + 1), 0f);
        }
    }
}
