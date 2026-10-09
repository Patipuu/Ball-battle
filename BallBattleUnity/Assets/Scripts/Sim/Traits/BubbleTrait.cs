namespace BallBattle.Sim.Traits
{
    /// <summary>A shield charge every few seconds (never more than one banked). Counters chip damage.</summary>
    public sealed class BubbleTrait : TraitRule
    {
        public const string TraitId = "bubble";
        int timer;
        public BubbleTrait(int level = 1) : base(level) { }
        public override string Id => TraitId;

        public override void OnTick()
        {
            if (Self.Status.ShieldCharges > 0) { timer = 0; return; } // the next one starts counting once this is used up
            if (++timer < TraitTuning.BubbleIntervalTicks[Level - 1]) return;
            timer = 0;
            Sim.AddShield(Self, 1);
        }

        protected override ulong HashState(ulong h) => SimHash.Mix(h, timer);
    }
}