using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BallBattle.Tests
{
    public class ProjectileArtTests
    {
        [Test]
        public void SparklerBoltIsVisibleAndAimsAlongItsVelocity()
        {
            var art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Art/ArtLibrary.asset");
            var root = new GameObject("SparklerProjectileArt");
            try
            {
                var m = new MatchSim(new MatchConfig(), 5, new[]
                {
                    new BallLoadout(new VolleyRule()), new BallLoadout(new BrawlerRule())
                });
                var slot = m.FireProjectile(m.Balls[0], new FixVec2(12, 18), new FixVec2(0, 5),
                    new ProjectileSpec { Radius = Fix.Ratio(3, 2), Damage = 1 });
                Assert.That(slot, Is.GreaterThanOrEqualTo(0));
                var view = ProjectileView.Create(root.transform, art.Pixel);
                view.Render(m);
                var bolt = view.GetComponentsInChildren<SpriteRenderer>().Single(s => s.enabled);
                Assert.That(bolt.sprite, Is.SameAs(ProceduralSprites.SparklerBolt));
                Assert.That(bolt.bounds.size.x, Is.GreaterThan(0f));
                Assert.That(bolt.bounds.size.y, Is.GreaterThan(0f));
                Assert.That(bolt.transform.localPosition, Is.EqualTo(new Vector3(12, 18, 0)));
                Assert.That(Mathf.DeltaAngle(bolt.transform.localEulerAngles.z, 90f), Is.EqualTo(0f).Within(0.1f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void HiveOpeningTurretUsesAGunSpriteAtItsActualPosition()
        {
            var art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Art/ArtLibrary.asset");
            var root = new GameObject("HiveTurretArt");
            try
            {
                var rig = new RigRule();
                var m = new MatchSim(new MatchConfig(), 7, new[]
                {
                    new BallLoadout(rig) { Tree = new TreeLoadout { Start = true } },
                    new BallLoadout(new BladeRule())
                });
                m.ApplySpawnHooks();
                Assert.That(rig.TurretCount, Is.EqualTo(1));
                var view = ProjectileView.Create(root.transform, art.Pixel);
                view.Render(m);
                var gun = view.GetComponentsInChildren<SpriteRenderer>().Single(s => s.enabled);
                Assert.That(gun.sprite, Is.SameAs(ProceduralSprites.HiveTurret));
                Assert.That(gun.bounds.size.x, Is.GreaterThan(0f));
                Assert.That(gun.bounds.size.y, Is.GreaterThan(0f));
                var at = rig.GetTurret(0);
                Assert.That(gun.transform.localPosition.x, Is.EqualTo(Mathf.Round(at.X.ToFloat())));
                Assert.That(gun.transform.localPosition.y, Is.EqualTo(Mathf.Round(at.Y.ToFloat())));
                var tex = gun.sprite.texture;
                Assert.That(tex.GetPixels32().Count(c => c.a > 0), Is.LessThan(tex.width * tex.height),
                    "the gun has a silhouette instead of a solid square");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
