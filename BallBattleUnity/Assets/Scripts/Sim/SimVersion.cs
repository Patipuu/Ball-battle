namespace BallBattle.Sim
{
    /// <summary>
    /// Version of the simulation rules. Bump whenever a rule change alters match outcomes,
    /// so stored seeds/replays from an older rule set are never compared against new results.
    /// Pure C#: nothing in BallBattle.Sim may reference UnityEngine (asmdef noEngineReferences).
    /// </summary>
    public static class SimVersion
    {
        public const int Rules = 3;
    }
}
