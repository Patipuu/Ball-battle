namespace BallBattle.Sim
{
    /// <summary>BB-19b: what a skill effect entity is. The view draws them from MatchSim.Fx.</summary>
    public enum FxKind : byte { None, FireTrail, Cloud, Bee, Hook, Ghost, Coil }

    /// <summary>BB-19b: one-shot skill events (SimEventType.SkillFx, Variant = this). A=owner, B=target or -1, Value, Point.</summary>
    public enum SkillFxEvent : byte
    {
        None, EmberDash, TrailBurn, MantisBlink, MantisSlash, MantisDodge, HookThrown, HookHit, HookMiss, HookReeled,
        WreckCharge, WreckLaunch, WreckBackfire, WreckImpact, BoltFired, CloudOpen, CloudPulse, ParryOpen, ParryHit, ParryMiss,
        BeesReleased, BeeSting, UnityRelease, CoilLunge, CoilCatch, CoilBite, CoilEnd, FinaleProc
    }

    /// <summary>One skill effect entity (fire trail piece, cloud, bee, hook, afterimage, coil). Inactive slots are free.</summary>
    public struct FxItem
    {
        public bool Active;
        public FxKind Kind;
        public int Owner;
        /// <summary>Ball index of the victim for hooks and coils, else -1.</summary>
        public int Target;
        public FixVec2 Pos;
        public FixVec2 Vel;
        public Fix Radius;
        /// <summary>Damage per pulse / sting / bite (cloud: poison dps).</summary>
        public Fix Power;
        /// <summary>Kind-specific: hook pull speed, cloud heal per pulse, coil heal fraction.</summary>
        public Fix Extra;
        public int Life;
        public int Age;
        /// <summary>Kind-specific: hook 0 flying / 1 reeling, cloud slow percent, coil bite count, bee number.</summary>
        public int Aux;
        /// <summary>Kind-specific: hook stun ticks, coil slow ticks after release, bee sting cooldown, cloud finale (1 outbreak, 2 linger).</summary>
        public int Aux2;

        public ulong HashInto(ulong h)
        {
            h = SimHash.Mix(h, Active);
            if (!Active) return h;
            h = SimHash.Mix(SimHash.Mix(SimHash.Mix(h, (int)Kind), Owner), Target);
            h = SimHash.Mix(SimHash.Mix(h, Pos), Vel);
            h = SimHash.Mix(SimHash.Mix(SimHash.Mix(h, Radius), Power), Extra);
            h = SimHash.Mix(SimHash.Mix(h, Life), Age);
            return SimHash.Mix(SimHash.Mix(h, Aux), Aux2);
        }
    }

    /// <summary>Fixed-capacity effect storage; lowest free slot first, so reuse is deterministic. Read-only outside the Sim.</summary>
    public sealed class FxPool
    {
        public const int Capacity = 48;
        /// <summary>int max without System.Math (the Sim keeps clear of it).</summary>
        internal static int Max(int a, int b) => a > b ? a : b;
        readonly FxItem[] items = new FxItem[Capacity];
        public int ActiveCount { get; private set; }
        /// <summary>True once anything was ever spawned: a match without skill effects hashes exactly as before BB-19b.</summary>
        public bool Used { get; private set; }

        public ref readonly FxItem Get(int index) => ref items[index];
        internal ref FxItem At(int index) => ref items[index];

        internal int Spawn(in FxItem item)
        {
            for (var i = 0; i < Capacity; i++)
            {
                if (items[i].Active) continue;
                items[i] = item;
                items[i].Active = true;
                ActiveCount++;
                Used = true;
                return i;
            }
            return -1;
        }

        internal void Remove(int index)
        {
            if (!items[index].Active) return;
            items[index].Active = false;
            ActiveCount--;
        }

        /// <summary>Cloud radius now: grows in 0.5 s, shrinks in the last second.</summary>
        public static Fix CloudRadius(in FxItem f)
        {
            if (f.Age < 30) return f.Radius * Fix.Ratio(f.Age, 30);
            if (f.Life < 60) return f.Radius * Fix.Ratio(f.Life < 0 ? 0 : f.Life, 60);
            return f.Radius;
        }

        public ulong HashInto(ulong h)
        {
            for (var i = 0; i < Capacity; i++) h = items[i].HashInto(h);
            return h;
        }
    }
}
