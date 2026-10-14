using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Projectiles (2-3 px squares in the owner's weapon color) and Engineer turrets (dark-rimmed squares in the
    /// owner's color). Fixed pools, no allocation per frame. Sits above the blades, under the arena mask.
    /// </summary>
    public sealed class ProjectileView : MonoBehaviour
    {
        public const int Order = 24;
        const int TurretSize = 5;
        const int MaxTurretsPerBall = 8;

        SpriteRenderer[] shots;
        SpriteRenderer[] turretRims;
        SpriteRenderer[] turretBodies;

        public static ProjectileView Create(Transform parent, Sprite pixel)
        {
            var go = new GameObject("Projectiles");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<ProjectileView>();
            v.shots = new SpriteRenderer[ProjectilePool.Capacity];
            for (var i = 0; i < v.shots.Length; i++) v.shots[i] = Make(go.transform, pixel, Palette.Text, Order + 1);
            var n = 2 * MaxTurretsPerBall;
            v.turretRims = new SpriteRenderer[n];
            v.turretBodies = new SpriteRenderer[n];
            for (var i = 0; i < n; i++)
            {
                v.turretRims[i] = Make(go.transform, pixel, Palette.Outline, Order);
                v.turretBodies[i] = Make(go.transform, pixel, Palette.Text, Order + 1);
            }
            return v;
        }

        static SpriteRenderer Make(Transform parent, Sprite pixel, Color32 color, int order)
        {
            var sr = PixelRect.Create(parent, "P", pixel, color, order);
            sr.enabled = false;
            return sr;
        }

        public void Render(MatchSim sim)
        {
            var pool = sim.Projectiles;
            for (var i = 0; i < ProjectilePool.Capacity; i++)
            {
                var sr = shots[i];
                ref readonly var p = ref pool.Get(i);
                if (!p.Active || p.Owner < 0 || p.Owner >= sim.Balls.Count) { sr.enabled = false; continue; }
                var size = Mathf.Max(2, Mathf.RoundToInt(p.Radius.ToFloat() * 2f));
                var x = Mathf.RoundToInt(p.Pos.X.ToFloat());
                var y = Mathf.RoundToInt(p.Pos.Y.ToFloat());
                sr.enabled = true;
                sr.color = Palette.Look(sim.Balls[p.Owner].Weapon.Id).Body;
                var left = x - size / 2;   // whole-pixel edges: the size never depends on the position`r`n                var bottom = y - size / 2;`r`n                PixelRect.Set(sr, left, bottom, left + size, bottom + size);
            }

            var slot = 0;
            for (var b = 0; b < sim.Balls.Count && b < 2; b++)
            {
                var rig = sim.Balls[b].Weapon as RigRule;
                var count = rig != null ? Mathf.Min(rig.TurretCount, MaxTurretsPerBall) : 0;
                for (var t = 0; t < MaxTurretsPerBall; t++, slot++)
                {
                    if (t >= count) { turretRims[slot].enabled = false; turretBodies[slot].enabled = false; continue; }
                    var c = rig.GetTurret(t);
                    var x = Mathf.RoundToInt(c.X.ToFloat());
                    var y = Mathf.RoundToInt(c.Y.ToFloat());
                    const int half = TurretSize / 2;   // 5 px body (x-2..x+3), 7 px rim
                    turretRims[slot].enabled = true;
                    turretBodies[slot].enabled = true;
                    turretBodies[slot].color = Palette.Look(rig.Id).Body;
                    PixelRect.Set(turretRims[slot], x - half - 1, y - half - 1, x - half + TurretSize + 1, y - half + TurretSize + 1);
                    PixelRect.Set(turretBodies[slot], x - half, y - half, x - half + TurretSize, y - half + TurretSize);
                }
            }
        }
    }
}