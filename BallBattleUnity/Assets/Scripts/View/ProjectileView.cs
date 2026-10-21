using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>Draws live shots and Hive turrets without changing projectile or turret simulation.</summary>
    public sealed class ProjectileView : MonoBehaviour
    {
        public const int Order = 24;
        const int MaxTurretsPerBall = 8;

        Sprite pixel;
        SpriteRenderer[] shots;
        SpriteRenderer[] turrets;

        public static ProjectileView Create(Transform parent, Sprite pixel)
        {
            var go = new GameObject("Projectiles");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<ProjectileView>();
            v.pixel = pixel;
            v.shots = new SpriteRenderer[ProjectilePool.Capacity];
            for (var i = 0; i < v.shots.Length; i++) v.shots[i] = Make(go.transform, pixel, Order + 1);
            v.turrets = new SpriteRenderer[2 * MaxTurretsPerBall];
            for (var i = 0; i < v.turrets.Length; i++)
                v.turrets[i] = Make(go.transform, ProceduralSprites.HiveTurret, Order + 1);
            return v;
        }

        static SpriteRenderer Make(Transform parent, Sprite sprite, int order)
        {
            var sr = PixelRect.Create(parent, "P", sprite, Color.white, order);
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
                var x = Mathf.RoundToInt(p.Pos.X.ToFloat());
                var y = Mathf.RoundToInt(p.Pos.Y.ToFloat());
                sr.enabled = true;
                if (sim.Balls[p.Owner].Weapon is VolleyRule)
                {
                    sr.sprite = ProceduralSprites.SparklerBolt;
                    sr.color = Color.white;
                    sr.transform.localPosition = new Vector3(x, y, 0f);
                    var scale = p.Radius.ToFloat() >= 3f ? 2f : 1f;
                    sr.transform.localScale = new Vector3(scale, scale, 1f);
                    sr.transform.localRotation = Quaternion.Euler(0f, 0f,
                        Mathf.Atan2(p.Vel.Y.ToFloat(), p.Vel.X.ToFloat()) * Mathf.Rad2Deg);
                }
                else
                {
                    sr.sprite = pixel;
                    sr.color = Palette.Look(sim.Balls[p.Owner].Weapon.Id).Body;
                    sr.transform.localRotation = Quaternion.identity;
                    var size = Mathf.Max(2, Mathf.RoundToInt(p.Radius.ToFloat() * 2f));
                    var left = x - size / 2;
                    var bottom = y - size / 2;
                    PixelRect.Set(sr, left, bottom, left + size, bottom + size);
                }
            }

            for (var b = 0; b < 2; b++)
            {
                var rig = b < sim.Balls.Count ? sim.Balls[b].Weapon as RigRule : null;
                var count = rig != null ? Mathf.Min(rig.TurretCount, MaxTurretsPerBall) : 0;
                for (var t = 0; t < MaxTurretsPerBall; t++)
                {
                    var sr = turrets[b * MaxTurretsPerBall + t];
                    if (t >= count) { sr.enabled = false; continue; }
                    var c = rig.GetTurret(t);
                    var x = Mathf.RoundToInt(c.X.ToFloat());
                    var y = Mathf.RoundToInt(c.Y.ToFloat());
                    sr.enabled = true;
                    sr.transform.localPosition = new Vector3(x, y, 0f);
                    sr.transform.localScale = Vector3.one;
                    for (var foe = 0; foe < sim.Balls.Count; foe++)
                    {
                        if (foe == b || !sim.Balls[foe].Alive) continue;
                        var d = sim.Balls[foe].Pos - c;
                        sr.transform.localRotation = Quaternion.Euler(0f, 0f,
                            Mathf.Atan2(d.Y.ToFloat(), d.X.ToFloat()) * Mathf.Rad2Deg);
                        break;
                    }
                }
            }
        }
    }
}
