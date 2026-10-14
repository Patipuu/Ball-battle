using System.Collections.Generic;
using BallBattle.Sim;
using BallBattle.Sim.Boss;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// BB-04 boss telegraphs, drawn from the sim's boss rule state every frame (never from events, so a skipped frame never loses
    /// one). No boss art exists yet (E:\ball-battle-art-hold\boss\ is absent), so everything is a placeholder made of 2x2 pixel
    /// dots: SHELL KING = blinking ring while warning, the 120-degree shell arc outline while ON; MAGNET = pulsing ring while
    /// warning, rings closing in while pulling; WARDEN = blinking outline of the next walls while warning. MIMIC has none.
    /// </summary>
    public sealed class BossTelegraphView : MonoBehaviour
    {
        public const int Order = 27;   // above balls and blades, under the outside mask (30)
        const float Dot = 2f;
        public static readonly Color32 ShellColor = new Color32(170, 220, 255, 255);
        public static readonly Color32 WarnColor = new Color32(255, 200, 60, 255);
        public static readonly Color32 MagnetColor = new Color32(220, 90, 255, 255);

        Sprite pixel;
        readonly List<SpriteRenderer> dots = new List<SpriteRenderer>();
        int used;

        /// <summary>Dots drawn by the last Render (tests).</summary>
        public int DotsShown => used;
        /// <summary>What the last Render drew: "", "shell-warn", "shell-on", "magnet-warn", "magnet-pull", "warden-warn" (tests).</summary>
        public string Shown { get; private set; } = "";

        public static BossTelegraphView Create(Transform parent, Sprite pixel)
        {
            var go = new GameObject("BossTelegraph");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<BossTelegraphView>();
            v.pixel = pixel;
            return v;
        }

        /// <summary><paramref name="bossPos"/> = the boss ball as drawn this frame (interpolated).</summary>
        public void Render(MatchSim sim, Vector2 bossPos)
        {
            used = 0;
            Shown = "";
            var boss = sim?.Boss;
            if (boss != null)
            {
                var r = BossRadius(sim, boss);
                var blink = (sim.ActiveTick / 6) % 2 == 0;
                switch (boss)
                {
                    case ShellKingRule shell:
                        if (shell.ShellOn)
                        {
                            Shown = "shell-on";
                            var f = shell.Facing;
                            var facing = Mathf.Atan2(f.Y.ToFloat(), f.X.ToFloat()) * Mathf.Rad2Deg;
                            Arc(bossPos, r + 3f, facing - BossTuning.ShellArcDeg * 0.5f, facing + BossTuning.ShellArcDeg * 0.5f, ShellColor);
                            Arc(bossPos, r + 5f, facing - BossTuning.ShellArcDeg * 0.5f, facing + BossTuning.ShellArcDeg * 0.5f, ShellColor);
                        }
                        else if (shell.Warning)
                        {
                            Shown = "shell-warn";
                            if (blink) Arc(bossPos, r + 4f, 0f, 360f, WarnColor);
                        }
                        break;
                    case MagnetRule magnet:
                        if (magnet.Pulling)
                        {
                            Shown = "magnet-pull";
                            var k = (sim.ActiveTick * 2) % 24;
                            Arc(bossPos, r + 28f - k, 0f, 360f, MagnetColor);
                            Arc(bossPos, r + 16f - (k % 12), 0f, 360f, MagnetColor);
                        }
                        else if (magnet.Warning)
                        {
                            Shown = "magnet-warn";
                            Arc(bossPos, r + 5f + 3f * Mathf.Abs(Mathf.Sin(sim.ActiveTick * 0.25f)), 0f, 360f, MagnetColor);
                        }
                        break;
                    case WardenRule warden:
                        if (warden.Warning)
                        {
                            Shown = "warden-warn";
                            if (blink)
                            {
                                var w = WardenRule.SizeAfter(sim.Config.ArenaWidth, warden.Steps + 1).ToFloat();
                                var h = WardenRule.SizeAfter(sim.Config.ArenaHeight, warden.Steps + 1).ToFloat();
                                w = Mathf.Min(w, sim.Arena.Width.ToFloat());
                                h = Mathf.Min(h, sim.Arena.Height.ToFloat());
                                Outline(-w * 0.5f, -h * 0.5f, w * 0.5f, h * 0.5f, WarnColor);
                            }
                        }
                        break;
                }
            }
            for (var i = used; i < dots.Count; i++)
                if (dots[i].enabled) dots[i].enabled = false;
        }

        static float BossRadius(MatchSim sim, BossRule boss) => sim.Balls[sim.BossBallIndex].Radius.ToFloat();

        void Arc(Vector2 c, float radius, float fromDeg, float toDeg, Color32 color)
        {
            var steps = Mathf.Max(6, Mathf.CeilToInt(Mathf.Abs(toDeg - fromDeg) * Mathf.Deg2Rad * radius / 3f));
            for (var i = 0; i <= steps; i++)
            {
                var a = Mathf.Lerp(fromDeg, toDeg, i / (float)steps) * Mathf.Deg2Rad;
                Put(new Vector2(c.x + Mathf.Cos(a) * radius, c.y + Mathf.Sin(a) * radius), color);
            }
        }

        void Outline(float l, float b, float r, float t, Color32 color)
        {
            for (var x = l; x <= r; x += 4f) { Put(new Vector2(x, b), color); Put(new Vector2(x, t), color); }
            for (var y = b; y <= t; y += 4f) { Put(new Vector2(l, y), color); Put(new Vector2(r, y), color); }
        }

        void Put(Vector2 p, Color32 color)
        {
            if (used == dots.Count) dots.Add(PixelRect.Create(transform, "Dot", pixel, color, Order));
            var d = dots[used++];
            d.enabled = true;
            d.color = color;
            PixelRect.Set(d, p.x - Dot * 0.5f, p.y - Dot * 0.5f, p.x + Dot * 0.5f, p.y + Dot * 0.5f);
        }
    }
}
