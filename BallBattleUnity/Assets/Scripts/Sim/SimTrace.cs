using System.Globalization;
using System.Text;

namespace BallBattle.Sim
{
    /// <summary>Read-only per-tick fingerprint of six fields on every ball (Fix raw values).</summary>
    public static class SimTrace
    {
        public static ulong Hash(MatchSim sim)
        {
            var h = SimHash.Mix(SimHash.Seed, sim.Balls.Count);
            foreach (var b in sim.Balls)
            {
                h = SimHash.Mix(h, b.Pos.X);
                h = SimHash.Mix(h, b.Pos.Y);
                h = SimHash.Mix(h, b.Vel.X);
                h = SimHash.Mix(h, b.Vel.Y);
                h = SimHash.Mix(h, b.WeaponAngleDeg);
                h = SimHash.Mix(h, b.Hp);
            }
            return h;
        }

        public static string Line(MatchSim sim) => sim.Tick.ToString(CultureInfo.InvariantCulture) + " " + Hash(sim).ToString("X16", CultureInfo.InvariantCulture);

        /// <summary>Q16.16 raw bits as hex.</summary>
        public static string Bits(Fix value) => "0x" + unchecked((uint)value.Raw).ToString("X8", CultureInfo.InvariantCulture);

        /// <summary>Companion detail line, same tick as Line, for locating the first changed field.</summary>
        public static string FieldsLine(MatchSim sim)
        {
            var s = new StringBuilder(sim.Tick.ToString(CultureInfo.InvariantCulture));
            foreach (var b in sim.Balls)
            {
                var prefix = b.Index == 0 ? " A." : b.Index == 1 ? " B." : " ball" + b.Index + ".";
                Add(s, prefix, "pos.x", b.Pos.X);
                Add(s, prefix, "pos.y", b.Pos.Y);
                Add(s, prefix, "vel.x", b.Vel.X);
                Add(s, prefix, "vel.y", b.Vel.Y);
                Add(s, prefix, "angle", b.WeaponAngleDeg);
                Add(s, prefix, "hp", b.Hp);
            }
            return s.ToString();
        }

        static void Add(StringBuilder s, string prefix, string name, Fix value) => s.Append(prefix).Append(name).Append('=').Append(Bits(value));
    }
}
