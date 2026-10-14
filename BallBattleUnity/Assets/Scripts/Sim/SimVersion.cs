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
        public const int Rules = 6;   // 6: whole Sim in Q16.16 fixed point (6a.6b/c), same rules, integer rounding
    }
}
