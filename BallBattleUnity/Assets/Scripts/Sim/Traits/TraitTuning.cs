namespace BallBattle.Sim.Traits
{
    /// <summary>Trait numbers in one place. Index [level - 1]. Tests read expectations from here.</summary>
    public static class TraitTuning
    {
        // Heavy: bigger, harder to push, slower, a little tougher.
        public static readonly float[] HeavyRadiusScale = { 1.2f, 1.35f };
        public static readonly float[] HeavySpeedPct = { -0.1f, -0.1f };
        public static readonly float[] HeavyKnockbackResist = { 0.4f, 0.6f };
        public static readonly float[] HeavyMaxHpPct = { 0.1f, 0.2f };
        /// <summary>Heavy never grows a ball past this radius (a boss stays 24), so two big balls still fit the shrunk 110 px arena.</summary>
        public static readonly float[] HeavyMaxRadius = { 24f, 24f };

        // Vampire: heals a share of every hit it lands.
        public static readonly float[] VampireHealPct = { 0.2f, 0.35f };

        // Spiky: body contact hurts the other ball.
        public static readonly float[] SpikyContactDamage = { 3f, 5f };

        /// <summary>Mixed into the match hash so a tuning change can never silently match an old replay.</summary>
        public static ulong Fingerprint()
        {
            var h = SimHash.Seed;
            foreach (var arr in new[] { HeavyRadiusScale, HeavySpeedPct, HeavyKnockbackResist, HeavyMaxHpPct, HeavyMaxRadius, VampireHealPct, SpikyContactDamage })
                foreach (var v in arr) h = SimHash.Mix(h, v);
            return h;
        }
    }
}
