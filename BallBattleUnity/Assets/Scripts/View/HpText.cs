namespace BallBattle.View
{
    /// <summary>
    /// The one rule for showing HP anywhere (HUD, Prepare, result screen, HP NEXT FIGHT): round DOWN, but a ball that is
    /// still alive (HP &gt; 0) always shows at least 1. 97.2 → 97, 0.4 → 1, 0 → 0. A tiny epsilon keeps float noise such
    /// as 134.99999 (100 × 1.35) from showing as 134.
    /// </summary>
    public static class HpText
    {
        const float Epsilon = 0.001f;

        public static int Of(float hp)
        {
            if (hp <= 0f) return 0;
            var shown = (int)System.Math.Floor(hp + Epsilon);
            return shown < 1 ? 1 : shown;
        }

        /// <summary>"cur/max" with both values shown by the same rule.</summary>
        public static string Pair(float hp, float maxHp) => Of(hp) + "/" + Of(maxHp);
    }
}
