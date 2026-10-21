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
        // 10: BB-10a: one signature skill per weapon, Run skill branches
        // 11: BB-FIX2: SHELL KING arc judged at the hit to 0.1 degree (60.0 blocks); boss fights: walls bury obstacles
        // 12: BB-BAL1: combined balance pass after skills (values only)
        // 13: BB-BAL3: the Run DAMAGE card also scales SPORE's poison and MIRROR's reflects (Versus unchanged)
        // 14: BB-12g: Run skill tree (ball level points), SKILL UP card removed, Run-only baselines (Versus unchanged)
        // 15: BB-14: two tribute balls (chorus, serpent) in Run pools
        // 16: BB-15: WRECKER Run baseline and BGLGXLQ Run-only CHORUS heal
        // 17: BB-18: BGLGXLQ orbs light on touch, any order
        // 18: BB-18 Versus-only BGLGXLQ damage and orb guard profile
        // 19: BB-19: the Run player's tap presses the skill
        // 21: Eight authored bosses, boss-only attacks and Mimic adaptation.
        // 22: Chorus touch refresh and 15-second fade; Mimic echo blast and Warden crossfire.
        // 23: Run enemies use their active skill (same as the player's) through BallLoadout.AutoSkill. Versus and PvP unchanged.
        // 24: PvP is played live: the challenger's taps press the skill, the defender's bot presses its own; PvP-only HP/damage table.
        public const int Rules = 24;
    }
}
