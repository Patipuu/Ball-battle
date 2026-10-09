using BallBattle.Sim;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Status marks around each ball: poison = lime dots orbiting (one per stack), shield = white ring
    /// (a second ring at 2+ charges). One color and one shape per effect. Fixed pools.
    /// </summary>
    public sealed class StatusView : MonoBehaviour
    {
        public const int Order = 25;
        const int MaxBalls = 2;
        const int RingDots = 12;
        static readonly Color32 PoisonColor = new Color32(96, 255, 64, 255);
        static readonly Color32 ShieldColor = new Color32(250, 250, 255, 255);

        SpriteRenderer[] poison;   // [ball * MaxPoison + i]
        SpriteRenderer[] ring;     // [ball * 2 * RingDots + ring * RingDots + i]

        public static StatusView Create(Transform parent, Sprite pixel)
        {
            var go = new GameObject("Status");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<StatusView>();
            v.poison = new SpriteRenderer[MaxBalls * StatusEffects.MaxPoisonStacks];
            v.ring = new SpriteRenderer[MaxBalls * 2 * RingDots];
            for (var i = 0; i < v.poison.Length; i++) v.poison[i] = Make(go.transform, pixel, PoisonColor);
            for (var i = 0; i < v.ring.Length; i++) v.ring[i] = Make(go.transform, pixel, ShieldColor);
            return v;
        }

        static SpriteRenderer Make(Transform parent, Sprite pixel, Color32 color)
        {
            var sr = PixelRect.Create(parent, "S", pixel, color, Order);
            sr.enabled = false;
            return sr;
        }

        static void Dot(SpriteRenderer sr, Vector2 center, float angleDeg, float radius, int size)
        {
            var a = angleDeg * Mathf.Deg2Rad;
            var x = Mathf.Round(center.x + Mathf.Cos(a) * radius);
            var y = Mathf.Round(center.y + Mathf.Sin(a) * radius);
            sr.enabled = true;
            PixelRect.Set(sr, x, y, x + size, y + size);
        }

        /// <summary>Draw marks for ball i around its (interpolated) centre.</summary>
        public void Render(int i, BallState ball, Vector2 center, int tick)
        {
            if (i >= MaxBalls) return;
            var stacks = ball.Alive ? ball.Status.PoisonStackCount : 0;
            for (var k = 0; k < StatusEffects.MaxPoisonStacks; k++)
            {
                var sr = poison[i * StatusEffects.MaxPoisonStacks + k];
                if (k >= stacks) { sr.enabled = false; continue; }
                Dot(sr, center, tick * 4f + k * 360f / stacks, ball.Radius + 3f, 2);
            }

            var rings = !ball.Alive ? 0 : (ball.Status.ShieldCharges >= 2 ? 2 : (ball.Status.ShieldCharges == 1 ? 1 : 0));
            for (var r = 0; r < 2; r++)
                for (var k = 0; k < RingDots; k++)
                {
                    var sr = ring[(i * 2 + r) * RingDots + k];
                    if (r >= rings) { sr.enabled = false; continue; }
                    Dot(sr, center, k * 360f / RingDots + r * 15f, ball.Radius + 5f + r * 3f, 1);
                }
        }
    }
}