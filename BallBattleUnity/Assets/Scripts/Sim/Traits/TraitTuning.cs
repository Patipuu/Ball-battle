namespace BallBattle.Sim.Traits
{
    /// <summary>Trait numbers in one place. Index [level - 1]. Tests read expectations from here.</summary>
    public static class TraitTuning
    {
        // Heavy: bigger, harder to push, slower, a little tougher.
        public static readonly Fix[] HeavyRadiusScale = { Fix.Ratio(6, 5), Fix.Ratio(27, 20) };
        public static readonly Fix[] HeavySpeedPct = { 0, 0 };
        public static readonly Fix[] HeavyKnockbackResist = { Fix.Ratio(2, 5), Fix.Ratio(3, 5) };
        public static readonly Fix[] HeavyMaxHpPct = { Fix.Ratio(7, 20), Fix.Ratio(7, 10) };
        /// <summary>Heavy never grows a ball past this radius (a boss stays 24), so two big balls still fit the shrunk 110 px arena.</summary>
        public static readonly Fix[] HeavyMaxRadius = { 24, 24 };

        // Vampire: heals a share of every hit it lands.
        public static readonly Fix[] VampireHealPct = { Fix.Ratio(1, 5), Fix.Ratio(7, 25) };

        // Spiky: body contact hurts the other ball.
        public static readonly Fix[] SpikyContactDamage = { 1, Fix.Ratio(3, 2) };
        /// <summary>Ticks between spike hits (body bounces can repeat many times a second).</summary>
        public const int SpikyCooldownTicks = 120;

        // Thorns: share of melee damage taken that is sent back.
        public static readonly Fix[] ThornsReflectPct = { Fix.Ratio(3, 20), Fix.Ratio(11, 50) };

        // Second Wind: once, at or below the threshold fraction of max HP (or instead of dying).
        public static readonly Fix SecondWindThreshold = Fix.Ratio(3, 10);
        public static readonly Fix[] SecondWindHeal = { 20, 28 };

        // Glass Cannon: damage dealt up, max HP down (never below the floor).
        public static readonly Fix[] GlassCannonDamageMul = { Fix.Ratio(31, 20), Fix.Ratio(19, 10) };   // BB-6c: L1 1.5 -> 1.55
        public static readonly Fix[] GlassCannonHpLoss = { 25, 35 };
        public static readonly Fix GlassCannonMinHp = 20;

        // Parry Master: the weapon grows on every Nth parry.
        public static readonly int[] ParryMasterEvery = { 2, 1 };

        // Bubble: ticks between shield charges.
        public static readonly int[] BubbleIntervalTicks = { 25 * MatchConfig.TicksPerSecond, 18 * MatchConfig.TicksPerSecond };

        // Poison Tip: stack strength (per second) and length (whole seconds).
        public static readonly Fix[] PoisonTipDps = { Fix.Ratio(7, 10), Fix.Ratio(4, 5) };
        /// <summary>Most poison stacks this trait keeps on one target (fast hitters would otherwise stack the whole cap).</summary>
        public static readonly int[] PoisonTipMaxStacks = { 1, 2 };
        public static readonly int[] PoisonTipSeconds = { 3, 4 };

        // Twin Blade: second blade length as a fraction of the weapon's blade.
        public static readonly Fix[] TwinBladeScale = { Fix.Ratio(3, 10), Fix.Ratio(2, 5) };
        /// <summary>Damage multiplier on every hit: the price of the extra reach.</summary>
        public static readonly Fix[] TwinBladeDamageMul = { Fix.Ratio(9, 20), Fix.Ratio(9, 20) };

        /// <summary>Mixed into the match hash so a tuning change can never silently match an old replay.</summary>
        public static ulong Fingerprint()
        {
            var h = SimHash.Seed;
            foreach (var arr in new[] { HeavyRadiusScale, HeavySpeedPct, HeavyKnockbackResist, HeavyMaxHpPct, HeavyMaxRadius, VampireHealPct, SpikyContactDamage, ThornsReflectPct, SecondWindHeal, GlassCannonDamageMul, GlassCannonHpLoss, PoisonTipDps, TwinBladeScale, TwinBladeDamageMul })
                foreach (var v in arr) h = SimHash.Mix(h, v);
            foreach (var arr in new[] { ParryMasterEvery, PoisonTipMaxStacks, BubbleIntervalTicks, PoisonTipSeconds })
                foreach (var v in arr) h = SimHash.Mix(h, v);
            foreach (var v in new[] { SecondWindThreshold, GlassCannonMinHp })
                h = SimHash.Mix(h, v);
            h = SimHash.Mix(h, SpikyCooldownTicks);
            return h;
        }
    }
}
