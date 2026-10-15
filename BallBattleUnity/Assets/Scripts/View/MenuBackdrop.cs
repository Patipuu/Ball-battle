using System.Collections.Generic;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// UI-MENU1: the hub's moving background. A few real ball sprites drift, bounce off the screen edges and off each other,
    /// with a small spark where two meet. Purely decorative View code (no Sim, no randomness after the fixed seed), dimmed so
    /// the buttons stay the focus, drawn above the menu background and below every hub element.
    /// </summary>
    public sealed class MenuBackdrop : MonoBehaviour
    {
        public const int Count = 6;
        public const float Radius = 16f;
        public const float HalfW = 135f, HalfH = 240f;
        /// <summary>Balls are dimmed to this alpha so they read as background.</summary>
        public const float Alpha = 0.26f;
        const float SparkTime = 0.12f;

        sealed class Mover { public SpriteRenderer Sprite; public Vector2 Pos, Vel; public float Angle, Spin; }
        readonly List<Mover> movers = new List<Mover>();
        SpriteRenderer spark;
        float sparkLeft;

        public static MenuBackdrop Create(Transform parent, ArtLibrary art, int order, int seed = 7)
        {
            var go = new GameObject("Backdrop");
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<MenuBackdrop>();
            var rnd = new System.Random(seed);
            var ids = new[] { "blade", "venom", "aegis", "brawler", "volley", "pike", "fang", "rig" };
            for (var i = 0; i < Count; i++)
            {
                var sr = new GameObject("Ball_" + ids[i]).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(go.transform, false);
                sr.sprite = art.Get(ids[i]).Ball;
                sr.sortingOrder = order;
                sr.color = new Color(1f, 1f, 1f, Alpha);
                // Spread over a 2x3 grid so they do not start overlapping; speeds 22..40 px/s in any direction.
                var cell = new Vector2(-70 + (i % 2) * 140, 150 - (i / 2) * 150);
                var a = (float)(rnd.NextDouble() * Mathf.PI * 2);
                var speed = 22f + (float)rnd.NextDouble() * 18f;
                b.movers.Add(new Mover { Sprite = sr, Pos = cell, Vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed, Spin = (rnd.Next(2) == 0 ? -1 : 1) * (40f + speed * 2f) });
            }
            b.spark = new GameObject("Spark").AddComponent<SpriteRenderer>();
            b.spark.transform.SetParent(go.transform, false);
            b.spark.sprite = art.Pixel;
            b.spark.transform.localScale = new Vector3(4, 4, 1);
            b.spark.sortingOrder = order;
            b.spark.color = new Color(1f, 0.95f, 0.7f, 0.8f);
            b.spark.enabled = false;
            b.Place();
            return b;
        }

        void Update() => Step(Mathf.Min(Time.unscaledDeltaTime, 0.05f));

        /// <summary>One step (Update's body; tests drive it).</summary>
        public void Step(float dt)
        {
            foreach (var m in movers)
            {
                m.Pos += m.Vel * dt;
                m.Angle += m.Spin * dt;
                if (m.Pos.x < -HalfW + Radius) { m.Pos.x = -HalfW + Radius; m.Vel.x = Mathf.Abs(m.Vel.x); }
                if (m.Pos.x > HalfW - Radius) { m.Pos.x = HalfW - Radius; m.Vel.x = -Mathf.Abs(m.Vel.x); }
                if (m.Pos.y < -HalfH + Radius) { m.Pos.y = -HalfH + Radius; m.Vel.y = Mathf.Abs(m.Vel.y); }
                if (m.Pos.y > HalfH - Radius) { m.Pos.y = HalfH - Radius; m.Vel.y = -Mathf.Abs(m.Vel.y); }
            }
            for (var i = 0; i < movers.Count; i++)
                for (var j = i + 1; j < movers.Count; j++)
                {
                    var a = movers[i]; var b = movers[j];
                    var d = b.Pos - a.Pos;
                    var dist = d.magnitude;
                    if (dist >= 2f * Radius || dist < 0.001f) continue;
                    var n = d / dist;
                    var approach = Vector2.Dot(a.Vel - b.Vel, n);
                    var push = (2f * Radius - dist) * 0.5f;
                    a.Pos -= n * push; b.Pos += n * push;
                    if (approach <= 0f) continue;
                    a.Vel -= n * approach; b.Vel += n * approach;   // equal masses: swap the normal components
                    spark.transform.localPosition = new Vector3(Mathf.Round((a.Pos.x + b.Pos.x) * 0.5f), Mathf.Round((a.Pos.y + b.Pos.y) * 0.5f), 0f);
                    sparkLeft = SparkTime;
                }
            sparkLeft -= dt;
            spark.enabled = sparkLeft > 0f;
            Place();
        }

        void Place()
        {
            foreach (var m in movers)
            {
                m.Sprite.transform.localPosition = new Vector3(Mathf.Round(m.Pos.x), Mathf.Round(m.Pos.y), 0f);   // whole pixels: crisp
                m.Sprite.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Round(m.Angle / 15f) * 15f);    // 15 degree steps
            }
        }

        public int BallCount => movers.Count;
        public Vector2 PositionOf(int i) => movers[i].Pos;
        public SpriteRenderer SpriteOf(int i) => movers[i].Sprite;
    }
}
