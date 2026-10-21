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
    /// warning, rings closing in while pulling; MIMIC = marked blast circle; WARDEN = closing walls and crossfire lanes.
    /// </summary>
    public sealed class BossTelegraphView : MonoBehaviour
    {
        public const int Order = 27;   // above balls and blades, under the outside mask (30)
        const float Dot = 2f;
        public static readonly Color32 ShellColor = new Color32(170, 220, 255, 255);
        public static readonly Color32 WarnColor = new Color32(255, 200, 60, 255);
        public static readonly Color32 MagnetColor = new Color32(220, 90, 255, 255);
        public static readonly Color32 HunterColor = new Color32(255, 105, 85, 255);
        public static readonly Color32 BellColor = new Color32(255, 224, 90, 255);
        public static readonly Color32 EmberColor = new Color32(255, 110, 45, 255);
        public static readonly Color32 PrismColor = new Color32(110, 220, 255, 255);
        public static readonly Color32 MimicColor = new Color32(255, 130, 220, 255);

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
                            if (blink)
                            {
                                var target = sim.Balls[0];
                                var facing = Mathf.Atan2(target.Pos.Y.ToFloat() - bossPos.y, target.Pos.X.ToFloat() - bossPos.x) * Mathf.Rad2Deg;
                                Arc(bossPos, 95f, facing - BossTuning.ShellArcDeg * .5f,
                                    facing + BossTuning.ShellArcDeg * .5f, WarnColor);
                            }
                        }
                        break;
                    case MagnetRule magnet:
                        if (magnet.Pulling)
                        {
                            Shown = "magnet-pull";
                            var k = (sim.ActiveTick * 2) % 24;
                            Arc(bossPos, r + 28f - k, 0f, 360f, MagnetColor);
                            Arc(bossPos, r + 16f - (k % 12), 0f, 360f, MagnetColor);
                            Arc(bossPos, 92f, 0f, 360f, WarnColor);
                        }
                        else if (magnet.Warning)
                        {
                            Shown = "magnet-warn";
                            Arc(bossPos, r + 5f + 3f * Mathf.Abs(Mathf.Sin(sim.ActiveTick * 0.25f)), 0f, 360f, MagnetColor);
                        }
                        break;
                    case MimicRule mimic:
                        if (mimic.EchoWarning)
                        {
                            Shown = "mimic-echo";
                            var mark = new Vector2(mimic.EchoMark.X.ToFloat(), mimic.EchoMark.Y.ToFloat());
                            Arc(mark, BossTuning.MimicEchoRadius.ToFloat(), 0, 360, MimicColor);
                        }
                        break;
                    case WardenRule warden:
                        if (warden.CrossWarning)
                        {
                            Shown = "warden-cross";
                            var x = warden.CrossMark.X.ToFloat();
                            var y = warden.CrossMark.Y.ToFloat();
                            var half = BossTuning.WardenCrossHalfWidth.ToFloat();
                            Line(new Vector2(x - half, sim.Arena.Bottom.ToFloat()), new Vector2(x - half, sim.Arena.Top.ToFloat()), WarnColor);
                            Line(new Vector2(x + half, sim.Arena.Bottom.ToFloat()), new Vector2(x + half, sim.Arena.Top.ToFloat()), WarnColor);
                            Line(new Vector2(sim.Arena.Left.ToFloat(), y - half), new Vector2(sim.Arena.Right.ToFloat(), y - half), WarnColor);
                            Line(new Vector2(sim.Arena.Left.ToFloat(), y + half), new Vector2(sim.Arena.Right.ToFloat(), y + half), WarnColor);
                        }
                        if (warden.Warning)
                        {
                            if (!warden.CrossWarning) Shown = "warden-warn";
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
                    case HunterRule hunter:
                        if (hunter.Warning || hunter.Charging)
                        {
                            Shown = hunter.Warning ? "hunter-mark" : "hunter-charge";
                            var mark = new Vector2(hunter.Mark.X.ToFloat(), hunter.Mark.Y.ToFloat());
                            Arc(mark, 38, 0, 360, HunterColor);
                            Line(bossPos, mark, HunterColor);
                        }
                        break;
                    case BellRule bell:
                        if (bell.Warning || bell.Waving)
                        {
                            Shown = bell.Warning ? "bell-warn" : "bell-wave";
                            Arc(bossPos, bell.Warning ? 88 : bell.WaveRadius.ToFloat(), 0, 360, BellColor);
                        }
                        break;
                    case EmberRule ember:
                        if (ember.Warning || ember.Burning)
                        {
                            Shown = ember.Warning ? "ember-mark" : "ember-fire";
                            var mark = new Vector2(ember.Mark.X.ToFloat(), ember.Mark.Y.ToFloat());
                            Arc(mark, 42, 0, 360, EmberColor);
                            if (ember.Burning) Arc(mark, 27, 0, 360, EmberColor);
                        }
                        break;
                    case PrismRule prism:
                        if (prism.Warning || prism.Firing)
                        {
                            Shown = prism.Warning ? "prism-warn" : "prism-beam";
                            var lane = prism.Lane.ToFloat();
                            if (prism.Horizontal)
                            {
                                Line(new Vector2(sim.Arena.Left.ToFloat(), lane - 16),
                                    new Vector2(sim.Arena.Right.ToFloat(), lane - 16), PrismColor);
                                Line(new Vector2(sim.Arena.Left.ToFloat(), lane + 16),
                                    new Vector2(sim.Arena.Right.ToFloat(), lane + 16), PrismColor);
                                if (prism.Firing) Line(new Vector2(sim.Arena.Left.ToFloat(), lane),
                                    new Vector2(sim.Arena.Right.ToFloat(), lane), PrismColor);
                            }
                            else
                            {
                                Line(new Vector2(lane - 16, sim.Arena.Bottom.ToFloat()),
                                    new Vector2(lane - 16, sim.Arena.Top.ToFloat()), PrismColor);
                                Line(new Vector2(lane + 16, sim.Arena.Bottom.ToFloat()),
                                    new Vector2(lane + 16, sim.Arena.Top.ToFloat()), PrismColor);
                                if (prism.Firing) Line(new Vector2(lane, sim.Arena.Bottom.ToFloat()),
                                    new Vector2(lane, sim.Arena.Top.ToFloat()), PrismColor);
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

        void Line(Vector2 a, Vector2 b, Color32 color)
        {
            var length = Vector2.Distance(a, b);
            var steps = Mathf.Max(1, Mathf.CeilToInt(length / 4f));
            for (var i = 0; i <= steps; i++) Put(Vector2.Lerp(a, b, i / (float)steps), color);
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
