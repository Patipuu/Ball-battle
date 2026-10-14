namespace BallBattle.Sim
{
    public sealed partial class MatchSim
    {
        void SkillsPreTick()
        {
            foreach (var ball in balls) if (Active(ball)) ball.Skill?.Tick();
        }
        internal Fix SkillSpeedCap(BallState ball) => SpeedCap(ball);
        internal void EmitSkill(SimEventType type, BallState ball, Skills.SkillState skill)
        {
            Events.Add(new SimEvent { Type = type, Tick = Tick, ActiveTick = ActiveTick, A = ball.Index, B = -1,
                Point = ball.Pos, SkillId = skill.Definition.Id, SkillLevel = skill.Level, SkillBranch = skill.Branch });
        }
    }
}
