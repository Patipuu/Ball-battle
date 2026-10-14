namespace BallBattle.Sim
{
    /// <summary>
    /// Version of the simulation rules. Bump whenever a rule change alters match outcomes,
    /// so stored seeds/replays from an older rule set are never compared against new results.
    /// Pure C#: nothing in BallBattle.Sim may reference UnityEngine (asmdef noEngineReferences).
    /// </summary>
    public static class SimVersion
    {
        // 5: spike-walls hurt only in corner zones (BB-FIX1a), 3 damage, max 1 per ball per tick
        // 6: whole Sim in Q16.16 fixed point (6a.6b/c), same rules, integer rounding
        // 7: spike re-hit immunity (10 ActiveTicks per ball) + arena balance pass (BB-6c)
        // 8: Run boss pool v1 (BB-04): Shell King, Magnet, Mimic, Warden. Versus/round-robin results unchanged.
        public const int Rules = 10;   // BB-10a: one signature skill per weapon, Run skill branches
    }
}
