
namespace BallBattle.Sim
{
    /// <summary>BB-19b: skill effect entities (fire trail, cloud, bees, hook, afterimage, coil) and their per-tick rules.</summary>
    public sealed partial class MatchSim
    {
        readonly FxPool fx = new FxPool();
        public FxPool Fx => fx;

        internal int SpawnFx(in FxItem item) => Ongoing ? fx.Spawn(item) : -1;

        internal void EmitFx(SkillFxEvent ev, int owner, int target, Fix value, FixVec2 point) =>
            Emit(SimEventType.SkillFx, owner, target, value, point, (byte)ev);

        void UpdateFx()
        {
            if (!Ongoing || fx.ActiveCount == 0) return;
            for (var i = 0; i < FxPool.Capacity; i++)
            {
                ref var f = ref fx.At(i);
                if (!f.Active) continue;
                var owner = balls[f.Owner];
                if (!Active(owner)) { fx.Remove(i); continue; }
                f.Age++;
                switch (f.Kind)
                {
                    case FxKind.FireTrail: FxTrail(ref f, owner); break;
                    case FxKind.Cloud: FxCloud(ref f, owner); break;
                    case FxKind.Bee: FxBee(ref f, owner); break;
                    case FxKind.Ghost: FxGhost(ref f, owner); break;
                    case FxKind.Hook: if (FxHook(ref f, i, owner)) continue; break;
                    case FxKind.Coil: if (FxCoil(ref f, owner)) { fx.Remove(i); continue; } break;
                }
                if (f.Active && --f.Life <= 0) fx.Remove(i);
            }
            ResolveDeaths();
        }

        bool Inside(FixVec2 centre, Fix radius, BallState b) => (b.Pos - centre).LengthSq <= FixMath.Sq(radius + b.Radius);

        void FxTrail(ref FxItem f, BallState owner)
        {
            if (f.Age % Skills.SkillTuning.EmberTrailPulse != Skills.SkillTuning.EmberTrailPulse / 2) return;
            foreach (var b in balls)
            {
                if (b == owner || !Active(b) || !Inside(f.Pos, f.Radius, b)) continue;
                DealDamage(owner, b, f.Power, DamageKind.Ability);
                EmitFx(SkillFxEvent.TrailBurn, owner.Index, b.Index, f.Power, f.Pos);
                if (owner.Skill != null && owner.Skill.Act is Skills.EmberTrail ember) ember.Ignite(b, f.Power);
            }
        }

        void FxGhost(ref FxItem f, BallState owner)
        {
            if (f.Power <= Fix.Zero || f.Age != 12) return;
            foreach (var b in balls)
            {
                if (b == owner || !Active(b) || !Inside(f.Pos, f.Radius, b)) continue;
                DealDamage(owner, b, f.Power, DamageKind.Ability);
                EmitFx(SkillFxEvent.MantisSlash, owner.Index, b.Index, f.Power, f.Pos);
            }
        }

        void FxCloud(ref FxItem f, BallState owner)
        {
            if (f.Aux2 == 1 && f.Life == 1)   // OUTBREAK: a small cloud stays where this one ended
                SpawnFx(new FxItem
                {
                    Kind = FxKind.Cloud, Owner = f.Owner, Target = -1, Pos = f.Pos, Radius = f.Radius * Fix.Ratio(9, 20),
                    Power = f.Power, Extra = Fix.Zero, Life = 150, Aux = f.Aux
                });
            if (f.Age % 30 != 15) return;
            var r = FxPool.CloudRadius(f);
            var any = false;
            foreach (var b in balls)
            {
                if (!Active(b) || !Inside(f.Pos, r, b)) continue;
                if (b == owner)
                {
                    if (f.Extra > Fix.Zero) Heal(owner, f.Extra);
                    continue;
                }
                any = true;
                ApplyPoison(b, owner, f.Power, 3 * MatchConfig.TicksPerSecond * (f.Aux2 == 2 ? 3 : 2) / 2);   // LINGER: +50%
                b.SlowTicks = 40;
                b.SlowFactor = Fix.Ratio(f.Aux, 100);
            }
            if (any) EmitFx(SkillFxEvent.CloudPulse, owner.Index, -1, r, f.Pos);
        }

        void FxBee(ref FxItem f, BallState owner)
        {
            var foe = NearestEnemy(owner);
            if (foe == null) return;
            var orbit = FixVec2.FromAngleDeg(FixMath.Wrap(f.Age * 9 + f.Aux * 60, 360)) * (foe.Radius + 5);
            var want = (foe.Pos + orbit - f.Pos).NormalizedOr(FixVec2.Right) * Skills.SkillTuning.BeeSpeed;
            f.Vel = f.Vel + (want - f.Vel) * Fix.Ratio(1, 5);
            f.Pos += f.Vel;
            if (f.Aux2 > 0) { f.Aux2--; return; }
            if ((f.Pos - foe.Pos).LengthSq > FixMath.Sq(foe.Radius + f.Radius + 3)) return;
            DealDamage(owner, foe, f.Power, DamageKind.Ability);
            EmitFx(SkillFxEvent.BeeSting, owner.Index, foe.Index, f.Power, f.Pos);
            f.Aux2 = Skills.SkillTuning.BeeStingGap;
        }

        /// <summary>True when the hook was removed.</summary>
        bool FxHook(ref FxItem f, int slot, BallState owner)
        {
            if (f.Aux == 0)
            {
                f.Pos += f.Vel;
                var a = Arena;
                if (f.Pos.X < a.Left || f.Pos.X > a.Right || f.Pos.Y < a.Bottom || f.Pos.Y > a.Top || f.Life <= 1)
                {
                    EmitFx(SkillFxEvent.HookMiss, owner.Index, -1, Fix.Zero, f.Pos);
                    fx.Remove(slot);
                    return true;
                }
                foreach (var b in balls)
                {
                    if (b == owner || !Active(b) || !Inside(f.Pos, f.Radius, b)) continue;
                    DealDamage(owner, b, f.Power, DamageKind.Ability);
                    f.Aux = 1; f.Target = b.Index; f.Life = 90;
                    EmitFx(SkillFxEvent.HookHit, owner.Index, b.Index, f.Power, f.Pos);
                    return false;
                }
                return false;
            }
            var t = balls[f.Target];
            if (!Active(t)) { fx.Remove(slot); return true; }
            f.Pos = t.Pos;
            t.Vel = FixVec2.Zero;
            t.StunTicks = FxPool.Max(t.StunTicks, 3);
            var to = owner.Pos - t.Pos;
            var stop = owner.Radius + t.Radius + 6;
            if (to.Length <= stop + f.Extra)
            {
                t.StunTicks = f.Aux2;
                EmitFx(SkillFxEvent.HookReeled, owner.Index, t.Index, Fix.Zero, t.Pos);
                fx.Remove(slot);
                return true;
            }
            t.Pos += to.NormalizedOr(FixVec2.Right) * f.Extra;
            return false;
        }

        /// <summary>True when the coil is over.</summary>
        bool FxCoil(ref FxItem f, BallState owner)
        {
            var t = balls[f.Target];
            if (!Active(t)) return true;
            f.Pos = t.Pos;
            t.Vel = FixVec2.Zero; owner.Vel = FixVec2.Zero;
            t.StunTicks = FxPool.Max(t.StunTicks, 2); owner.StunTicks = FxPool.Max(owner.StunTicks, 2);
            var total = f.Age + f.Life;
            var gap = FxPool.Max(1, total / (f.Aux + 1));
            if (f.Age % gap == 0 && f.Age < total)
            {
                var dealt = DealDamage(owner, t, f.Power, DamageKind.Ability);
                if (f.Extra > Fix.Zero) Heal(owner, dealt * f.Extra);
                if (owner.Weapon is Weapons.SerpentRule serpent) serpent.HydraHeal();   // HYDRA: coil bites heal too
                EmitFx(SkillFxEvent.CoilBite, owner.Index, t.Index, dealt, t.Pos);
            }
            if (f.Life > 1) return false;
            if (f.Aux2 > 0) { t.SlowTicks = f.Aux2; t.SlowFactor = Fix.Half; }
            EmitFx(SkillFxEvent.CoilEnd, owner.Index, t.Index, Fix.Zero, t.Pos);
            if (owner.Skill != null && owner.Skill.Act is Skills.HunterCoil coil) coil.CoilEnded();
            return true;
        }
    }
}
