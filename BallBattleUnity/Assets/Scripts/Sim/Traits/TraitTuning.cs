namespace BallBattle.Sim.Traits
{
    /// <summary>Trait numbers in one place. Index [level - 1]. Tests read expectations from here.</summary>
    public static class TraitTuning
    {
        // Heavy: bigger, harder to push, slower, a little tougher.
        public static readonly float[] HeavyRadiusScale = { 1.2f, 1.35f };
        public static readonly float[] HeavySpeedPct = { 0f, 0f };
        public static readonly float[] HeavyKnockbackResist = { 0.4f, 0.6f };
        public static readonly float[] HeavyMaxHpPct = { 0.35f, 0.7f };
        /// <summary>Heavy never grows a ball past this radius (a boss stays 24), so two big balls still fit the shrunk 110 px arena.</summary>
        public static readonly float[] HeavyMaxRadius = { 24f, 24f };

        // Vampire: heals a share of every hit it lands.
        public static readonly float[] VampireHealPct = { 0.2f, 0.28f };

        // Spiky: body contact hurts the other ball.
        public static readonly float[] SpikyContactDamage = { 1f, 1.5f };
        /// <summary>Ticks between spike hits (body bounces can repeat many times a second).</summary>
        public const int SpikyCooldownTicks = 120;

        // Thorns: share of melee damage taken that is sent back.
        public static readonly float[] ThornsReflectPct = { 0.15f, 0.22f };

        // Second Wind: once, at or below the threshold fraction of max HP (or instead of dying).
        public const float SecondWindThreshold = 0.3f;
        public static readonly float[] SecondWindHeal = { 20f, 28f };

        // Glass Cannon: damage dealt up, max HP down (never below the floor).
        public static readonly float[] GlassCannonDamageMul = { 1.5f, 1.9f };
        public static readonly float[] GlassCannonHpLoss = { 25f, 35f };
        public const float GlassCannonMinHp = 20f;

        // Parry Master: the weapon grows on every Nth parry.
        public static readonly int[] ParryMasterEvery = { 2, 1 };

        // Bubble: ticks between shield charges.
        public static readonly int[] BubbleIntervalTicks = { 25 * MatchConfig.TicksPerSecond, 18 * MatchConfig.TicksPerSecond };

        // Poison Tip: stack strength (per second) and length (whole seconds).
        public static readonly float[] PoisonTipDps = { 0.7f, 0.8f };
        /// <summary>Most poison stacks this trait keeps on one target (fast hitters would otherwise stack the whole cap).</summary>
        public static readonly int[] PoisonTipMaxStacks = { 1, 2 };
        public static readonly int[] PoisonTipSeconds = { 3, 4 };

        // Twin Blade: second blade length as a fraction of the weapon's blade.
        public static readonly float[] TwinBladeScale = { 0.3f, 0.4f };
        /// <summary>Damage multiplier on every hit: the price of the extra reach.</summary>
        public static readonly float[] TwinBladeDamageMul = { 0.45f, 0.45f };

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
