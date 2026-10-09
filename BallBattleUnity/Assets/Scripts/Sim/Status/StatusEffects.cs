namespace BallBattle.Sim
{
    public enum StatusKind : byte
    {
        Poison,
        Shield,
        Regen
    }

    /// <summary>
    /// Status effects on one ball, in fixed-size storage (no allocation per tick).
    /// Poison stacks and regen each pulse every PulseTicks after they were applied (own timer, so the amount
    /// never depends on when they landed): a 3 s effect always pulses exactly 3 times; a 1.5 s effect once.
    /// Shield charges each block one Weapon/Projectile hit completely. MatchSim drives the timers.
    /// </summary>
    public sealed class StatusEffects
    {
        public const int MaxPoisonStacks = 8;
        public const int PulseTicks = MatchConfig.TicksPerSecond;

        public struct PoisonStack
        {
            public float Dps;
            public int TicksLeft;
            /// <summary>Ticks until this stack's next pulse.</summary>
            public int PulseIn;
            /// <summary>Ball index that applied it (-1 = none), credited for a poison kill.</summary>
            public int Source;
        }

        public readonly PoisonStack[] Poison = new PoisonStack[MaxPoisonStacks];
        public int ShieldCharges;
        public float RegenPerSecond;
        public int RegenTicksLeft;
        public int RegenPulseIn;

        public int PoisonStackCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < MaxPoisonStacks; i++) if (Poison[i].TicksLeft > 0) n++;
                return n;
            }
        }

        /// <summary>Adds a stack in the first free slot; when full, replaces the stack with the fewest ticks left (lowest index on ties).</summary>
        public void AddPoison(float dps, int ticks, int source)
        {
            var slot = 0;
            for (var i = 0; i < MaxPoisonStacks; i++)
            {
                if (Poison[i].TicksLeft <= 0) { slot = i; break; }
                if (Poison[i].TicksLeft < Poison[slot].TicksLeft) slot = i;
            }
            Poison[slot] = new PoisonStack { Dps = dps, TicksLeft = ticks, PulseIn = PulseTicks, Source = source };
        }

        /// <summary>Starts (or replaces) regen.</summary>
        public void SetRegen(float perSecond, int ticks)
        {
            RegenPerSecond = perSecond;
            RegenTicksLeft = ticks;
            RegenPulseIn = PulseTicks;
        }

        /// <summary>Uses one shield charge if any. True = the hit is blocked.</summary>
        public bool TryConsumeShield()
        {
            if (ShieldCharges <= 0) return false;
            ShieldCharges--;
            return true;
        }

        /// <summary>Advances poison one tick. Returns total damage of stacks pulsing now; <paramref name="mainSource"/> = source of the strongest of them.</summary>
        public float TickPoison(out int mainSource)
        {
            var total = 0f;
            var mainDps = 0f;
            mainSource = -1;
            for (var i = 0; i < MaxPoisonStacks; i++)
            {
                ref var p = ref Poison[i];
                if (p.TicksLeft <= 0) continue;
                p.TicksLeft--;
                if (--p.PulseIn > 0) continue;
                p.PulseIn = PulseTicks;
                total += p.Dps;
                if (p.Dps > mainDps) { mainDps = p.Dps; mainSource = p.Source; }
            }
            return total;
        }

        /// <summary>Advances regen one tick. Returns the heal due now (0 between pulses).</summary>
        public float TickRegen()
        {
            if (RegenTicksLeft <= 0) return 0f;
            RegenTicksLeft--;
            if (--RegenPulseIn > 0) return 0f;
            RegenPulseIn = PulseTicks;
            return RegenPerSecond;
        }

        public ulong HashInto(ulong h)
        {
            for (var i = 0; i < MaxPoisonStacks; i++)
            {
                h = SimHash.Mix(h, Poison[i].Dps);
                h = SimHash.Mix(h, Poison[i].TicksLeft);
                h = SimHash.Mix(h, Poison[i].PulseIn);
                h = SimHash.Mix(h, Poison[i].Source);
            }
            h = SimHash.Mix(h, ShieldCharges);
            h = SimHash.Mix(h, RegenPerSecond);
            h = SimHash.Mix(h, RegenTicksLeft);
            return SimHash.Mix(h, RegenPulseIn);
        }
    }
}
