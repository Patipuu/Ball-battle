using System;
using System.Collections.Generic;
using BallBattle.Sim;
using BallBattle.Sim.Traits;

namespace BallBattle.View
{
    /// <summary>Which rule sent a Reflect damage back (view only: picks the effect).</summary>
    public enum ReflectSource : byte
    {
        Aegis,
        Thorns,
        Spiky
    }

    /// <summary>One Reflect damage with its guessed source and where the effect should play.</summary>
    public readonly struct ReflectFx
    {
        public readonly SimEvent Damage;
        public readonly ReflectSource Source;
        public readonly FixVec2 Point;

        public ReflectFx(SimEvent damage, ReflectSource source, FixVec2 point)
        {
            Damage = damage;
            Source = source;
            Point = point;
        }
    }

    /// <summary>
    /// The sim only says "Reflect" on a Damage event. This guesses Aegis / Thorns / Spiky from the other events of the
    /// same tick (the whole tick is needed: Parry is emitted after the Damage it caused).
    /// Evidence per Reflect damage, A = reflecting ball, B = ball hurt:
    ///   Parry between A and B, A uses Aegis           → Aegis (parry reflect), point of the Parry.
    ///   Hit from B on A: A uses Aegis                  → Aegis (body reflect), point of the Hit;
    ///                    otherwise A has Thorns        → Thorns, point of the Hit.
    ///   BallBounce between A and B, A has Spiky        → Spiky, midpoint of the two centres.
    ///   Nothing fits                                   → Aegis &gt; Thorns &gt; Spiky (first one A has), point of the Damage.
    /// Each piece of evidence explains one damage only (a Hit once for Aegis and once for Thorns), so a ball with
    /// Thorns and Spiky hit by a body blow gets one Thorns effect and one Spiky effect.
    /// Reuses its buffers: no allocation once warm.
    /// </summary>
    public sealed class ReflectSourceClassifier
    {
        /// <summary>What a ball owns. Tests pass plain values; FxView reads them from the match.</summary>
        public interface IOwners
        {
            bool UsesAegis(int ball);
            bool HasThorns(int ball);
            bool HasSpiky(int ball);
            /// <summary>Ball centre now (for the Spiky midpoint).</summary>
            FixVec2 Center(int ball);
        }

        readonly List<int> parries = new List<int>();
        readonly List<int> hits = new List<int>();
        readonly List<int> bounces = new List<int>();
        readonly List<bool> parryUsed = new List<bool>();
        readonly List<bool> hitAegisUsed = new List<bool>();
        readonly List<bool> hitThornsUsed = new List<bool>();
        readonly List<bool> bounceUsed = new List<bool>();

        public static bool IsReflect(in SimEvent e) => e.Type == SimEventType.Damage && e.Variant == (byte)DamageKind.Reflect && e.A >= 0 && e.B >= 0;

        /// <summary>
        /// Classify every Reflect damage in <paramref name="tick"/> (events of ONE tick, in emit order) and append them,
        /// in order, to <paramref name="output"/>.
        /// </summary>
        public void Classify(IReadOnlyList<SimEvent> tick, IOwners owners, List<ReflectFx> output)
        {
            if (tick == null) throw new ArgumentNullException(nameof(tick));
            if (owners == null) throw new ArgumentNullException(nameof(owners));
            if (output == null) throw new ArgumentNullException(nameof(output));

            parries.Clear(); hits.Clear(); bounces.Clear();
            parryUsed.Clear(); hitAegisUsed.Clear(); hitThornsUsed.Clear(); bounceUsed.Clear();
            var any = false;
            for (var i = 0; i < tick.Count; i++)
            {
                switch (tick[i].Type)
                {
                    case SimEventType.Parry: parries.Add(i); parryUsed.Add(false); break;
                    case SimEventType.Hit: hits.Add(i); hitAegisUsed.Add(false); hitThornsUsed.Add(false); break;
                    case SimEventType.BallBounce: bounces.Add(i); bounceUsed.Add(false); break;
                    case SimEventType.Damage: any |= IsReflect(tick[i]); break;
                }
            }
            if (!any) return;

            for (var i = 0; i < tick.Count; i++)
            {
                var d = tick[i];
                if (!IsReflect(d)) continue;
                output.Add(ClassifyOne(tick, d, owners));
            }
        }

        ReflectFx ClassifyOne(IReadOnlyList<SimEvent> tick, SimEvent d, IOwners owners)
        {
            int a = d.A, b = d.B;
            var aegis = owners.UsesAegis(a);
            var thorns = owners.HasThorns(a);
            var spiky = owners.HasSpiky(a);

            if (aegis)
                for (var k = 0; k < parries.Count; k++)
                {
                    if (parryUsed[k]) continue;
                    var p = tick[parries[k]];
                    if (!SamePair(p, a, b)) continue;
                    parryUsed[k] = true;
                    return new ReflectFx(d, ReflectSource.Aegis, p.Point);
                }

            for (var k = 0; k < hits.Count; k++)
            {
                var h = tick[hits[k]];
                if (h.A != b || h.B != a) continue;
                if (aegis && !hitAegisUsed[k]) { hitAegisUsed[k] = true; return new ReflectFx(d, ReflectSource.Aegis, h.Point); }
                if (thorns && !hitThornsUsed[k]) { hitThornsUsed[k] = true; return new ReflectFx(d, ReflectSource.Thorns, h.Point); }
            }

            if (spiky)
                for (var k = 0; k < bounces.Count; k++)
                {
                    if (bounceUsed[k]) continue;
                    var bb = tick[bounces[k]];
                    if (!SamePair(bb, a, b)) continue;
                    bounceUsed[k] = true;
                    return new ReflectFx(d, ReflectSource.Spiky, (owners.Center(a) + owners.Center(b)) / 2);
                }

            var fallback = aegis ? ReflectSource.Aegis : thorns ? ReflectSource.Thorns : spiky ? ReflectSource.Spiky : ReflectSource.Aegis;
            return new ReflectFx(d, fallback, d.Point);
        }

        static bool SamePair(in SimEvent e, int a, int b) => (e.A == a && e.B == b) || (e.A == b && e.B == a);
    }
}
