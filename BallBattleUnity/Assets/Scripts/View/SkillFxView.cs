using BallBattle.Sim;
using BallBattle.Sim.Skills;
using BallBattle.Sim.Weapons;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// BB-19b visuals of the tapped skills. Persistent effects (fire trail, spore cloud, bees, hook and rope, afterimage, coil) are
    /// drawn from MatchSim.Fx with a pool of discs; one-shot bursts come from SimEventType.SkillFx; the wind-up ring, parry arc and
    /// the ricochet bolt's glow are read from the player's skill. View only: reads the Sim, never changes it.
    /// </summary>
    public sealed class SkillFxView : MonoBehaviour
    {
        public const int DotOrder = 27;
        const int DotCount = 240;
        static readonly Color32 Fire = new Color32(255, 110, 30, 255), FireCore = new Color32(255, 225, 90, 255);
        static readonly Color32 Spore = new Color32(110, 230, 90, 255), Bee = new Color32(255, 214, 40, 255);
        static readonly Color32 Steel = new Color32(215, 225, 240, 255), Rope = new Color32(190, 225, 255, 255);
        static readonly Color32 Glass = new Color32(200, 245, 255, 255), Bolt = new Color32(90, 220, 255, 255);
        static readonly Color32 Dust = new Color32(190, 180, 160, 255), Smoke = new Color32(70, 70, 80, 255);

        ArenaView arena;
        FxView fx;
        Sprite disc;
        SpriteRenderer[] dots;
        int shown;
        PixelParticles particles;
        readonly int[] lastBounces = new int[ProjectilePool.Capacity];
        float clock;

        /// <summary>Dots drawn last frame (tests).</summary>
        public int DotsShown => shown;

        public static SkillFxView Create(Transform parent, ArenaView arena, Sprite disc, Sprite pixel)
        {
            var go = new GameObject("SkillFx");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<SkillFxView>();
            v.arena = arena;
            v.disc = disc;
            v.dots = new SpriteRenderer[DotCount];
            for (var i = 0; i < DotCount; i++)
            {
                var d = new GameObject("d");
                d.transform.SetParent(go.transform, false);
                var sr = d.AddComponent<SpriteRenderer>();
                sr.sprite = disc;
                sr.sortingOrder = DotOrder;
                sr.enabled = false;
                v.dots[i] = sr;
            }
            v.particles = PixelParticles.Create(go.transform, pixel, 160);
            for (var i = 0; i < v.lastBounces.Length; i++) v.lastBounces[i] = -1;
            return v;
        }

        public void Clear()
        {
            particles.Clear();
            for (var i = 0; i < lastBounces.Length; i++) lastBounces[i] = -1;
        }

        static Vector2 V(FixVec2 p) => new Vector2(p.X.ToFloat(), p.Y.ToFloat());
        static Color32 A(Color32 c, float alpha) => new Color32(c.r, c.g, c.b, (byte)Mathf.Clamp(alpha * 255f, 0f, 255f));
        static Color32 Body(BallState b) => Palette.Look(b.Weapon.Id).Body;

        void Dot(Vector2 centre, float diameter, Color32 color)
        {
            if (shown >= dots.Length || diameter <= 0.2f) return;
            var sr = dots[shown++];
            sr.enabled = true;
            sr.color = color;
            sr.transform.localPosition = new Vector3(Mathf.Round(centre.x), Mathf.Round(centre.y), 0f);
            var s = diameter / 32f;   // the disc sprite is 32 px across
            sr.transform.localScale = new Vector3(s, s, 1f);
        }

        void Ring(Vector2 centre, float radius, int n, float diameter, Color32 color, float spinDeg)
        {
            for (var i = 0; i < n; i++)
            {
                var a = (spinDeg + i * 360f / n) * Mathf.Deg2Rad;
                Dot(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, diameter, color);
            }
        }

        void Kick(float amp, float seconds)
        {
            if (fx == null && arena != null) fx = arena.GetComponent<FxView>();
            fx?.Kick(amp, seconds);
        }

        void Word(Vector2 at, string text, Color32 color)
        {
            if (fx == null && arena != null) fx = arena.GetComponent<FxView>();
            if (fx != null && fx.Popups != null && arena.Sim != null) fx.Popups.ShowText(at, text, color, arena.Sim.Arena, 2);
        }

        /// <summary>One-shot bursts for the SkillFx events.</summary>
        public void OnEvent(SimEvent e, MatchSim sim)
        {
            if (e.Type != SimEventType.SkillFx || e.A < 0 || e.A >= sim.Balls.Count) return;
            var owner = sim.Balls[e.A];
            var body = Body(owner);
            var at = V(e.Point);
            var foe = e.B >= 0 && e.B < sim.Balls.Count ? sim.Balls[e.B] : null;
            switch ((SkillFxEvent)e.Variant)
            {
                case SkillFxEvent.EmberDash:
                    particles.Burst(at, 14, Fire, 50f, 130f, 0.4f, 2); particles.Burst(at, 8, FireCore, 30f, 90f, 0.3f); Kick(2f, 0.1f); break;
                case SkillFxEvent.TrailBurn:
                    if (foe != null) { particles.Burst(V(foe.Pos), 8, Fire, 30f, 80f, 0.35f, 2); particles.Burst(V(foe.Pos), 4, FireCore, 20f, 60f, 0.25f); }
                    break;
                case SkillFxEvent.MantisBlink:
                    particles.Burst(at, 12, Color.white, 40f, 120f, 0.35f); particles.Burst(at, 8, A(body, 1f), 20f, 70f, 0.3f, 2);
                    particles.Burst(V(owner.Pos), 12, Color.white, 40f, 120f, 0.35f); break;
                case SkillFxEvent.MantisSlash:
                    if (foe != null) Slash(V(foe.Pos)); Kick(3f, 0.12f); break;
                case SkillFxEvent.MantisDodge:
                    particles.Burst(at, 16, Glass, 60f, 150f, 0.4f, 2); Word(new Vector2(at.x, at.y + owner.Radius.ToFloat() + 14f), "DODGE!", Glass); Kick(3f, 0.12f); break;
                case SkillFxEvent.HookThrown:
                    particles.Burst(at, 5, Steel, 30f, 80f, 0.2f); break;
                case SkillFxEvent.HookHit:
                    particles.Burst(at, 14, Steel, 60f, 160f, 0.35f, 2); particles.Burst(at, 8, Color.white, 40f, 110f, 0.25f); Kick(3f, 0.12f); break;
                case SkillFxEvent.HookMiss:
                    particles.Burst(at, 6, Dust, 20f, 60f, 0.4f); break;
                case SkillFxEvent.HookReeled:
                    if (foe != null) { particles.Burst(V(foe.Pos), 16, Dust, 40f, 110f, 0.45f, 2); }
                    Kick(3f, 0.15f); break;
                case SkillFxEvent.WreckCharge:
                    particles.Burst(at, 10, Dust, 30f, 80f, 0.4f, 2); break;
                case SkillFxEvent.WreckLaunch:
                    particles.Burst(at, 18, Dust, 60f, 170f, 0.45f, 2); particles.Burst(at, 8, Fire, 50f, 140f, 0.3f); Kick(2f + e.Value.ToFloat() * 4f, 0.15f); break;
                case SkillFxEvent.WreckBackfire:
                    particles.Burst(at, 22, Smoke, 30f, 110f, 0.7f, 2); particles.Burst(at, 8, Fire, 50f, 130f, 0.3f); Kick(4f, 0.2f); break;
                case SkillFxEvent.WreckImpact:
                    particles.Burst(at, 24, FireCore, 80f, 220f, 0.5f, 2); particles.Burst(at, 14, Color.white, 60f, 160f, 0.35f); Kick(3f + Mathf.Min(4f, e.Value.ToFloat()), 0.2f); break;
                case SkillFxEvent.BoltFired:
                    particles.Burst(at, 8, Bolt, 40f, 110f, 0.25f); particles.Burst(at, 4, Color.white, 30f, 80f, 0.2f); break;
                case SkillFxEvent.CloudOpen:
                    particles.Burst(at, 22, Spore, 20f, 80f, 0.8f, 2); break;
                case SkillFxEvent.CloudPulse:
                    if (foe != null) particles.Burst(V(foe.Pos), 6, Spore, 20f, 60f, 0.5f); particles.Burst(at, 10, Spore, 15f, 50f, 0.7f); break;
                case SkillFxEvent.ParryOpen:
                    particles.Burst(at, 8, Glass, 30f, 90f, 0.25f); break;
                case SkillFxEvent.ParryHit:
                    particles.Burst(at, 22, Color.white, 80f, 200f, 0.4f, 2); particles.Burst(at, 10, Glass, 40f, 130f, 0.35f);
                    Word(new Vector2(at.x, at.y + owner.Radius.ToFloat() + 14f), "PARRY!", Glass); Kick(4f, 0.18f); break;
                case SkillFxEvent.ParryMiss:
                    particles.Burst(at, 12, Glass, 20f, 70f, 0.6f); break;
                case SkillFxEvent.BeesReleased:
                    particles.Burst(at, 14, Bee, 40f, 110f, 0.4f, 2); break;
                case SkillFxEvent.BeeSting:
                    particles.Burst(at, 3, Bee, 20f, 60f, 0.2f); break;
                case SkillFxEvent.UnityRelease:
                    particles.Burst(at, 20, Color.white, 60f, 170f, 0.5f, 2); Kick(2f + Mathf.Min(3f, e.Value.ToFloat() * 2f), 0.18f); break;
                case SkillFxEvent.CoilLunge:
                    particles.Burst(at, 6, A(body, 1f), 30f, 90f, 0.3f, 2); break;
                case SkillFxEvent.CoilCatch:
                    particles.Burst(at, 18, A(body, 1f), 50f, 130f, 0.45f, 2); Kick(3f, 0.15f); break;
                case SkillFxEvent.CoilBite:
                    particles.Burst(at, 10, new Color32(240, 70, 60, 255), 40f, 120f, 0.35f, 2); particles.Burst(at, 5, Color.white, 30f, 90f, 0.25f); Kick(2f, 0.1f); break;
                case SkillFxEvent.CoilEnd:
                    particles.Burst(at, 10, Dust, 30f, 80f, 0.4f); break;
                case SkillFxEvent.FinaleProc:   // BB-19c: a finale effect just triggered
                    particles.Burst(at, 12, A(body, 1f), 40f, 110f, 0.4f, 2); particles.Burst(at, 6, Color.white, 30f, 80f, 0.3f); break;
            }
        }

        void Slash(Vector2 at)
        {
            for (var i = -5; i <= 5; i++)
                particles.Emit(at + new Vector2(i * 2.2f, i * 2.2f), new Vector2(i * 4f, i * 4f), Color.white, 0.25f, 2);
        }

        /// <param name="pos">Interpolated ball positions (as drawn this frame).</param>
        public void Render(MatchSim sim, Vector2[] pos, float dt)
        {
            clock += dt;
            var before = shown;
            shown = 0;
            particles.Tick(dt);
            for (var i = 0; i < FxPool.Capacity; i++)
            {
                if (!sim.Fx.Get(i).Active) continue;
                DrawFx(sim, sim.Fx.Get(i), pos);
            }
            for (var b = 0; b < sim.Balls.Count && b < pos.Length; b++) DrawBall(sim, sim.Balls[b], pos[b]);
            DrawBolts(sim);
            for (var i = shown; i < before; i++) dots[i].enabled = false;
        }

        void DrawFx(MatchSim sim, in FxItem f, Vector2[] pos)
        {
            var owner = sim.Balls[f.Owner];
            var c = V(f.Pos);
            switch (f.Kind)
            {
                case FxKind.FireTrail:
                {
                    var life = Mathf.Clamp01(f.Life / 60f);
                    var r = f.Radius.ToFloat() * (0.55f + 0.45f * life) * (1f + 0.12f * Mathf.Sin(clock * 18f + f.Age * 0.7f));
                    Dot(c, r * 2f, A(Fire, 0.55f * life + 0.15f));
                    Dot(c, r * 1.1f, A(FireCore, 0.85f * life));
                    if (Random.value < 0.05f * life) particles.Emit(c + Random.insideUnitCircle * r * 0.6f, new Vector2(Random.Range(-6f, 6f), Random.Range(18f, 34f)), Fire, 0.5f);
                    break;
                }
                case FxKind.Cloud:
                {
                    var r = FxPool.CloudRadius(f).ToFloat();
                    var fade = Mathf.Clamp01(f.Life / 60f);
                    Dot(c, r * 1.3f, A(Spore, 0.22f * fade));
                    for (var i = 0; i < 8; i++)
                    {
                        var a = (clock * 25f + i * 45f) * Mathf.Deg2Rad;
                        Dot(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 0.6f, r * 0.9f, A(Spore, 0.17f * fade));
                    }
                    if (Random.value < 0.18f) particles.Emit(c + Random.insideUnitCircle * r * 0.8f, new Vector2(Random.Range(-4f, 4f), Random.Range(10f, 22f)), Spore, 0.8f);
                    break;
                }
                case FxKind.Bee:
                {
                    var flap = Mathf.Sin(clock * 50f + f.Aux) > 0f ? 2f : 1f;
                    Dot(c, 5f, Bee);
                    Dot(c + new Vector2(-2.5f, 2f), flap, A(Color.white, 0.8f));
                    Dot(c + new Vector2(2.5f, 2f), flap, A(Color.white, 0.8f));
                    break;
                }
                case FxKind.Hook:
                {
                    var from = pos[f.Owner];
                    var to = c;
                    var dist = Vector2.Distance(from, to);
                    var n = Mathf.Clamp(Mathf.CeilToInt(dist / 5f), 1, 48);
                    for (var i = 1; i < n; i++) Dot(Vector2.Lerp(from, to, i / (float)n), 2f, Rope);
                    Dot(c, f.Radius.ToFloat() * 2f, Steel);
                    Dot(c, f.Radius.ToFloat() * 1.0f, new Color32(60, 70, 90, 255));
                    break;
                }
                case FxKind.Ghost:
                {
                    var life = Mathf.Clamp01(f.Life / 30f);
                    var body = Body(owner);
                    Dot(c, (f.Radius.ToFloat() - 6f) * 2f, A(body, 0.5f * life));
                    Ring(c, f.Radius.ToFloat(), 12, 2f, A(Color.white, 0.7f * life), clock * 90f);
                    break;
                }
                case FxKind.Coil:
                {
                    var t = f.Target >= 0 && f.Target < sim.Balls.Count ? sim.Balls[f.Target] : null;
                    if (t == null) break;
                    var body = Body(owner);
                    var tp = pos[t.Index];
                    var pulse = 1f + 0.12f * Mathf.Sin(clock * 14f);
                    Ring(tp, (t.Radius.ToFloat() + 2f) * pulse, 16, 4f, A(body, 1f), clock * 200f);
                    Ring(tp, (t.Radius.ToFloat() + 5f) * pulse, 16, 3f, A(body, 0.7f), -clock * 160f);
                    break;
                }
            }
        }

        void DrawBall(MatchSim sim, BallState b, Vector2 at)
        {
            var act = b.Skill != null ? b.Skill.Act : null;
            if (act == null || !b.Alive) return;
            var radius = b.Radius.ToFloat();
            var foe = Nearest(sim, b);
            if (act is WreckerCharge wreck && wreck.Charging)
            {
                var charge = wreck.Charge.ToFloat();
                var color = Color32.Lerp(FireCore, new Color32(240, 60, 40, 255), charge);
                var r = Mathf.Lerp(radius + 30f, radius + 6f, charge);
                Ring(at, r, 16, 3f, color, clock * 120f);
                if (Random.value < 0.4f) particles.Emit(at + new Vector2(Random.Range(-10f, 10f), -radius), new Vector2(Random.Range(-14f, 14f), Random.Range(6f, 24f)), Dust, 0.45f, 2);
                if (charge > 0.6f) Kick(0.6f + charge, 0.05f);
                return;
            }
            if (act is MirrorParry && act.Running)
            {
                var face = foe != null ? Mathf.Atan2(pos2(foe).y - at.y, pos2(foe).x - at.x) * Mathf.Rad2Deg : 90f;
                var pulse = 0.65f + 0.35f * Mathf.Sin(clock * 30f);
                for (var i = -5; i <= 5; i++)
                {
                    var a = (face + i * 8f) * Mathf.Deg2Rad;
                    Dot(at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (radius + 9f), i == 0 ? 4f : 3f, A(Glass, pulse));
                }
                return;
            }
            if (act is MantisPhantom && act.Running) { Ring(at, radius + 3f, 10, 2f, A(Color.white, 0.55f), clock * 240f); return; }
            if (act is EmberTrail && act.Running)
            {
                var back = -V(b.Vel).normalized;
                particles.Emit(at + back * radius, back * 40f + Random.insideUnitCircle * 20f, Random.value < 0.5f ? Fire : FireCore, 0.35f, 2);
                return;
            }
            if (act is HunterCoil && act.Running)
                particles.Emit(at - V(b.Vel).normalized * radius, Random.insideUnitCircle * 25f, Dust, 0.3f, 2);
        }

        static Vector2 pos2(BallState b) => V(b.Pos);

        static BallState Nearest(MatchSim sim, BallState self)
        {
            BallState best = null;
            var bestD = long.MaxValue;
            for (var i = 0; i < sim.Balls.Count; i++)
            {
                var o = sim.Balls[i];
                if (o == self || !o.Alive) continue;
                var d = (o.Pos - self.Pos).LengthSq;
                if (d < bestD) { bestD = d; best = o; }
            }
            return best;
        }

        void DrawBolts(MatchSim sim)
        {
            for (var i = 0; i < ProjectilePool.Capacity; i++)
            {
                var p = sim.Projectiles.Get(i);
                if (!p.Active) { lastBounces[i] = -1; continue; }
                if (p.Radius.ToFloat() < 3.4f || p.Owner < 0 || p.Owner >= sim.Balls.Count || !(sim.Balls[p.Owner].Weapon is VolleyRule)) continue;
                var c = V(p.Pos);
                Dot(c, p.Radius.ToFloat() * 2f + 6f, A(Bolt, 0.35f));
                Dot(c, p.Radius.ToFloat() * 1.6f, Color.white);
                particles.Emit(c, Random.insideUnitCircle * 14f, Bolt, 0.3f, 2);
                if (lastBounces[i] >= 0 && p.BouncesLeft < lastBounces[i])
                {
                    particles.Burst(c, 14, Bolt, 60f, 160f, 0.35f, 2);
                    particles.Burst(c, 6, Color.white, 40f, 100f, 0.25f);
                    Kick(1.5f, 0.08f);
                }
                lastBounces[i] = p.BouncesLeft;
            }
        }
    }
}
