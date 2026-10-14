using System.Collections.Generic;
using BallBattle.Sim;
using BallBattle.Sim.Traits;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;

namespace BallBattle.Tests
{
    public class ReflectSourceClassifierTests
    {
        const int Tick = 42;
        static readonly FixVec2 BPos = TFix.V(30f, 0f);

        sealed class Owners : ReflectSourceClassifier.IOwners
        {
            public bool Aegis, Thorns, Spiky;   // what ball 0 (the reflecting ball) owns
            public bool UsesAegis(int ball) => ball == 0 && Aegis;
            public bool HasThorns(int ball) => ball == 0 && Thorns;
            public bool HasSpiky(int ball) => ball == 0 && Spiky;
            public FixVec2 Center(int ball) => ball == 0 ? TFix.V(10f, 4f) : BPos;
        }

        static SimEvent Ev(SimEventType type, int a, int b, float value = 0f, FixVec2 point = default, DamageKind kind = DamageKind.Weapon) =>
            new SimEvent { Type = type, Tick = Tick, A = a, B = b, Value = TFix.X(value), Point = point, Variant = type == SimEventType.Damage ? (byte)kind : (byte)0 };

        static SimEvent Reflect(float value = 2f) => Ev(SimEventType.Damage, 0, 1, value, BPos, DamageKind.Reflect);

        static List<ReflectFx> Run(Owners owners, params SimEvent[] tick)
        {
            var output = new List<ReflectFx>();
            new ReflectSourceClassifier().Classify(tick, owners, output);
            return output;
        }

        [Test]
        public void ParryEmittedAfterTheDamageIsAegisAtTheParryPoint()
        {
            var parryPoint = TFix.V(20f, 1f);
            var fx = Run(new Owners { Aegis = true }, Reflect(), Ev(SimEventType.Parry, 1, 0, 0f, parryPoint));
            Assert.That(fx.Count, Is.EqualTo(1));
            Assert.That(fx[0].Source, Is.EqualTo(ReflectSource.Aegis));
            Assert.That(fx[0].Point, Is.EqualTo(parryPoint));
        }

        [Test]
        public void HitOnTheReflectorIsThornsAtTheHitPoint()
        {
            var hitPoint = TFix.V(18f, 2f);
            var fx = Run(new Owners { Thorns = true }, Ev(SimEventType.Hit, 1, 0, 6f, hitPoint), Reflect(0.9f));
            Assert.That(fx.Count, Is.EqualTo(1));
            Assert.That(fx[0].Source, Is.EqualTo(ReflectSource.Thorns));
            Assert.That(fx[0].Point, Is.EqualTo(hitPoint));
        }

        [Test]
        public void BodyHitOnAnAegisBallIsAegisBodyReflect()
        {
            var hitPoint = TFix.V(18f, 2f);
            var fx = Run(new Owners { Aegis = true }, Ev(SimEventType.Hit, 1, 0, 4f, hitPoint), Reflect());
            Assert.That(fx.Count, Is.EqualTo(1));
            Assert.That(fx[0].Source, Is.EqualTo(ReflectSource.Aegis));
            Assert.That(fx[0].Point, Is.EqualTo(hitPoint));
        }

        [Test]
        public void BallBounceIsSpikyAtTheMidpointOfTheCentres()
        {
            var fx = Run(new Owners { Spiky = true }, Ev(SimEventType.BallBounce, 0, 1, 3f, TFix.V(99f, 99f)), Reflect(1f));
            Assert.That(fx.Count, Is.EqualTo(1));
            Assert.That(fx[0].Source, Is.EqualTo(ReflectSource.Spiky));
            Assert.That(fx[0].Point, Is.EqualTo(TFix.V(20f, 2f)));
        }

        [Test]
        public void BallWithThornsAndSpikyGetsOneOfEachFromABodyBlow()
        {
            var owners = new Owners { Thorns = true, Spiky = true };
            var hitPoint = TFix.V(18f, 2f);
            var fx = Run(owners, Ev(SimEventType.Hit, 1, 0, 5f, hitPoint), Reflect(0.75f), Ev(SimEventType.BallBounce, 0, 1, 3f), Reflect(1f));
            Assert.That(fx.Count, Is.EqualTo(2));
            Assert.That(fx[0].Source, Is.EqualTo(ReflectSource.Thorns));
            Assert.That(fx[0].Point, Is.EqualTo(hitPoint));
            Assert.That(fx[1].Source, Is.EqualTo(ReflectSource.Spiky));
            Assert.That(fx[1].Damage.Value.ToFloat(), Is.EqualTo(1f));

            // Only a bump (no hit): the same ball's reflect is Spiky, not Thorns.
            var bumpOnly = Run(owners, Ev(SimEventType.BallBounce, 1, 0, 3f), Reflect(1f));
            Assert.That(bumpOnly.Count, Is.EqualTo(1));
            Assert.That(bumpOnly[0].Source, Is.EqualTo(ReflectSource.Spiky));
        }

        [Test]
        public void NoEvidenceFallsBackAegisThenThornsThenSpikyAtTheDamagePoint()
        {
            var fx = Run(new Owners { Thorns = true, Spiky = true }, Reflect());
            Assert.That(fx[0].Source, Is.EqualTo(ReflectSource.Thorns));
            Assert.That(fx[0].Point, Is.EqualTo(BPos));
            Assert.That(Run(new Owners { Aegis = true, Thorns = true }, Reflect())[0].Source, Is.EqualTo(ReflectSource.Aegis));
            Assert.That(Run(new Owners { Spiky = true }, Reflect())[0].Source, Is.EqualTo(ReflectSource.Spiky));
        }

        [Test]
        public void OnlyReflectDamageIsClassified()
        {
            var fx = Run(new Owners { Thorns = true },
                Ev(SimEventType.Damage, 0, 1, 2f, BPos, DamageKind.Hazard),
                Ev(SimEventType.Damage, -1, 1, 2f, BPos, DamageKind.Reflect),
                Ev(SimEventType.Hit, 1, 0, 5f));
            Assert.That(fx, Is.Empty);
        }

        /// <summary>The real sim, Aegis vs Blade: every reflect is a parry reflect (Blade has a blade), and its Parry is
        /// emitted after the Damage. Each one must be read as Aegis at a Parry point of that tick, not the fallback.</summary>
        [Test]
        public void RealAegisParryReflectsUseTheParryEmittedLater()
        {
            var classifier = new ReflectSourceClassifier();
            var owners = new LiveOwners();
            var output = new List<ReflectFx>();
            var seen = 0;
            for (uint seed = 1; seed <= 6; seed++)
            {
                var sim = new MatchSim(new MatchConfig(), seed, new[] { WeaponRegistry.Create(AegisRule.WeaponId), WeaponRegistry.Create(BladeRule.WeaponId) });
                owners.Match = sim;
                for (var t = 0; t < 60 * 60 && sim.Outcome == MatchOutcome.Ongoing; t++)
                {
                    sim.Step();
                    output.Clear();
                    classifier.Classify(sim.Events, owners, output);
                    foreach (var r in output)
                    {
                        seen++;
                        Assert.That(r.Source, Is.EqualTo(ReflectSource.Aegis), $"seed {seed} tick {t}");
                        var damageAt = sim.Events.FindIndex(e => ReflectSourceClassifier.IsReflect(e));
                        var parryAt = sim.Events.FindIndex(e => e.Type == SimEventType.Parry && e.Point.Equals(r.Point));
                        Assert.That(parryAt, Is.GreaterThan(damageAt), $"seed {seed} tick {t}: point must come from the later Parry");
                    }
                }
            }
            Assert.That(seen, Is.GreaterThan(0), "no Aegis reflect happened: the check would prove nothing");
        }

        sealed class LiveOwners : ReflectSourceClassifier.IOwners
        {
            public MatchSim Match;
            public bool UsesAegis(int ball) => Match.Balls[ball].Weapon is AegisRule;
            public bool HasThorns(int ball) => false;
            public bool HasSpiky(int ball) => false;
            public FixVec2 Center(int ball) => Match.Balls[ball].Pos;
        }
    }}
