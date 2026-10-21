namespace BallBattle.Sim.Boss
{
    /// <summary>Marks where the player stood, then lunges along that line. The mark is fixed during the tell.</summary>
    public sealed class HunterRule : BossRule
    {
        public override string Id => BossIds.Hunter;
        public FixVec2 Mark { get; private set; }
        public bool Warning { get; private set; }
        public bool Charging { get; private set; }
        bool struck;

        public override void OnActiveTick()
        {
            var t = Sim.ActiveTick - 90;
            if (t < 0) return;
            var p = t % 300;
            if (p == 0)
            {
                Mark = Target?.Pos ?? Self.Pos;
                Warning = true;
                struck = false;
                Sim.EmitBoss(BossEvent.HunterMark, Self.Index, Target?.Index ?? -1, Fix.Zero, Mark);
            }
            if (p == 50)
            {
                Warning = false;
                Charging = true;
                Self.Vel = (Mark - Self.Pos).NormalizedOr(FixVec2.Right) * 10;
                Sim.EmitBoss(BossEvent.HunterCharge, Self.Index, Target?.Index ?? -1, Fix.Zero, Mark);
                var target = Target;
                if (target != null && (target.Pos - Mark).LengthSq <= FixMath.Sq(38))
                {
                    Sim.DealDamage(Self, target, 19, DamageKind.Ability);
                    struck = true;
                }
            }
            if (p == 72) Charging = false;
            if (Charging && !struck && Target != null && (Target.Pos - Self.Pos).LengthSq <= FixMath.Sq(34))
            {
                DamageNear(Self.Pos, 34, 19);
                struck = true;
            }
        }

        protected override ulong HashState(ulong h)
        {
            h = SimHash.Mix(h, Mark);
            h = SimHash.Mix(h, Warning);
            h = SimHash.Mix(h, Charging);
            return SimHash.Mix(h, struck);
        }
    }

    /// <summary>A visible ring expands from the boss. The player must cross it or stay outside its reach.</summary>
    public sealed class BellRule : BossRule
    {
        public override string Id => BossIds.Bell;
        public bool Warning { get; private set; }
        public bool Waving { get; private set; }
        public Fix WaveRadius { get; private set; }
        bool struck;

        public override void OnActiveTick()
        {
            var t = Sim.ActiveTick - 110;
            if (t < 0) return;
            var p = t % 360;
            if (p == 0)
            {
                Warning = true;
                struck = false;
                Sim.EmitBoss(BossEvent.BellWarn, Self.Index, Target?.Index ?? -1, Fix.Zero, Self.Pos);
            }
            if (p == 50)
            {
                Warning = false;
                Waving = true;
                Sim.EmitBoss(BossEvent.BellWave, Self.Index, Target?.Index ?? -1, Fix.Zero, Self.Pos);
            }
            if (p == 90) Waving = false;
            if (!Waving) return;
            WaveRadius = 20 + (p - 50) * 2;
            var target = Target;
            if (!struck && target != null && FixMath.Abs((target.Pos - Self.Pos).Length - WaveRadius) <= 8)
            {
                Sim.DealDamage(Self, target, 13, DamageKind.Ability);
                struck = true;
            }
        }

        protected override ulong HashState(ulong h)
        {
            h = SimHash.Mix(h, Warning);
            h = SimHash.Mix(h, Waving);
            h = SimHash.Mix(h, WaveRadius);
            return SimHash.Mix(h, struck);
        }
    }

    /// <summary>Marks the player's old position, then leaves a short-lived burning circle there.</summary>
    public sealed class EmberRule : BossRule
    {
        public override string Id => BossIds.Ember;
        public FixVec2 Mark { get; private set; }
        public bool Warning { get; private set; }
        public bool Burning { get; private set; }

        public override void OnActiveTick()
        {
            var t = Sim.ActiveTick - 120;
            if (t < 0) return;
            var p = t % 300;
            if (p == 0)
            {
                Mark = Target?.Pos ?? Self.Pos;
                Warning = true;
                Sim.EmitBoss(BossEvent.EmberMark, Self.Index, Target?.Index ?? -1, Fix.Zero, Mark);
            }
            if (p == 60)
            {
                Warning = false;
                Burning = true;
                Sim.EmitBoss(BossEvent.EmberIgnite, Self.Index, Target?.Index ?? -1, Fix.Zero, Mark);
            }
            if (p == 210) Burning = false;
            if (Burning && p % 20 == 0) DamageNear(Mark, 42, 7);
        }

        protected override ulong HashState(ulong h)
        {
            h = SimHash.Mix(h, Mark);
            h = SimHash.Mix(h, Warning);
            return SimHash.Mix(h, Burning);
        }
    }

    /// <summary>Locks onto a horizontal or vertical lane, then fires across it; the next beam swaps axis.</summary>
    public sealed class PrismRule : BossRule
    {
        public override string Id => BossIds.Prism;
        public Fix Lane { get; private set; }
        public bool Horizontal { get; private set; }
        public bool Warning { get; private set; }
        public bool Firing { get; private set; }
        int shots;

        public override void OnActiveTick()
        {
            var t = Sim.ActiveTick - 100;
            if (t < 0) return;
            var p = t % 360;
            if (p == 0)
            {
                Horizontal = !Horizontal;
                Lane = Horizontal ? (Target?.Pos.Y ?? Fix.Zero) : (Target?.Pos.X ?? Fix.Zero);
                Warning = true;
                Sim.EmitBoss(BossEvent.PrismWarn, Self.Index, Target?.Index ?? -1, Lane, Self.Pos);
            }
            if (p == 60)
            {
                Warning = false;
                Firing = true;
                Sim.EmitBoss(BossEvent.PrismBeam, Self.Index, Target?.Index ?? -1, Lane, Self.Pos);
            }
            if (p == 150) Firing = false;
            if (!Firing || p % 20 != 0) return;
            shots++;
            var target = Target;
            if (target != null && FixMath.Abs((Horizontal ? target.Pos.Y : target.Pos.X) - Lane) <= 16)
                Sim.DealDamage(Self, target, 9, DamageKind.Ability);
        }

        protected override ulong HashState(ulong h)
        {
            h = SimHash.Mix(h, Lane);
            h = SimHash.Mix(h, Horizontal);
            h = SimHash.Mix(h, Warning);
            h = SimHash.Mix(h, Firing);
            return SimHash.Mix(h, shots);
        }
    }
}
