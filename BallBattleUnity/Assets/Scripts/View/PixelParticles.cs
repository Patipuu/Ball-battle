using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Pooled square pixel particles (1x1 or 2x2), drawn at whole-pixel positions. Fixed capacity:
    /// when full, the oldest particle is recycled. No allocation after creation.
    /// </summary>
    public sealed class PixelParticles : MonoBehaviour
    {
        public const int Order = 25;
        const float Gravity = -260f;   // px/s², a little arc so sparks fall

        SpriteRenderer[] renderers;
        Vector2[] pos, vel;
        float[] life, maxLife;
        Color32[] color;
        int next;

        public static PixelParticles Create(Transform parent, Sprite pixel, int capacity)
        {
            var go = new GameObject("Particles");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<PixelParticles>();
            p.renderers = new SpriteRenderer[capacity];
            p.pos = new Vector2[capacity];
            p.vel = new Vector2[capacity];
            p.life = new float[capacity];
            p.maxLife = new float[capacity];
            p.color = new Color32[capacity];
            for (var i = 0; i < capacity; i++)
            {
                var c = new GameObject("p");
                c.transform.SetParent(go.transform, false);
                var sr = c.AddComponent<SpriteRenderer>();
                sr.sprite = pixel;
                sr.sortingOrder = Order;
                sr.enabled = false;
                p.renderers[i] = sr;
            }
            return p;
        }

        /// <summary>Burst of <paramref name="count"/> particles flying out in random directions.</summary>
        public void Burst(Vector2 at, int count, Color32 c, float speedMin, float speedMax, float lifeSeconds, int size = 1)
        {
            for (var k = 0; k < count; k++)
            {
                var i = next;
                next = (next + 1) % renderers.Length;
                var dir = Random.insideUnitCircle.normalized;
                if (dir == Vector2.zero) dir = Vector2.up;
                pos[i] = at;
                vel[i] = dir * Random.Range(speedMin, speedMax);
                maxLife[i] = lifeSeconds * Random.Range(0.7f, 1.2f);
                life[i] = maxLife[i];
                color[i] = c;
                var sr = renderers[i];
                sr.transform.localScale = new Vector3(size, size, 1f);
                sr.color = c;
                sr.enabled = true;
            }
        }

        public void Clear()
        {
            for (var i = 0; i < renderers.Length; i++)
            {
                life[i] = 0f;
                renderers[i].enabled = false;
            }
        }

        /// <summary>Advance by <paramref name="dt"/> (already slow-mo scaled by the caller).</summary>
        public void Tick(float dt)
        {
            for (var i = 0; i < renderers.Length; i++)
            {
                if (life[i] <= 0f) continue;
                life[i] -= dt;
                var sr = renderers[i];
                if (life[i] <= 0f) { sr.enabled = false; continue; }

                vel[i].y += Gravity * dt;
                pos[i] += vel[i] * dt;
                sr.transform.localPosition = new Vector3(Mathf.Round(pos[i].x), Mathf.Round(pos[i].y), 0f);

                var c = color[i];
                var t = life[i] / maxLife[i];
                c.a = (byte)(t < 0.3f ? 255f * t / 0.3f : 255f);
                sr.color = c;
            }
        }
    }
}
