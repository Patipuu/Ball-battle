namespace BallBattle.Sim.Weapons
{
    /// <summary>BB-12 v2 prototype parameters. Applies only to the player in this copied harness.</summary>
    static class TreeLabParams
    {
        public static int GrowthRank;
        public static bool StartEnabled;
        public static char Keystone;

        public static bool Player(BallState ball) => ball != null && ball.Index == 0;

        public static void ApplyStart(BallState ball)
        {
            if (ball.Weapon is BrawlerRule brawlerBaseline) brawlerBaseline.ApplyRunBaseline();
            if (ball.Weapon is AegisRule aegisBaseline) aegisBaseline.ApplyRunBaseline();
            if (!StartEnabled) return;
            switch (ball.Weapon)
            {
                case BladeRule blade: blade.ApplyTreeStart(); break;
                case FangRule fang: fang.ApplyTreeStart(); break;
                case PikeRule pike: pike.ApplyTreeStart(); break;
                case BrawlerRule brawler: brawler.ApplyTreeStart(); break;
                case VolleyRule volley: volley.ApplyTreeStart(); break;
                case VenomRule venom: venom.ApplyTreeStart(); break;
                case AegisRule aegis: aegis.ApplyTreeStart(); break;
                case RigRule rig: rig.ApplyTreeStart(); break;
            }
        }

        public static void ApplyKeystone(BallState ball)
        {
            if (Keystone == '-') return;
            switch (ball.Weapon)
            {
                case BladeRule blade: blade.ApplyTreeKeystone(); break;
                case FangRule fang: fang.ApplyTreeKeystone(); break;
                case PikeRule pike: pike.ApplyTreeKeystone(); break;
                case BrawlerRule brawler: brawler.ApplyTreeKeystone(); break;
                case VolleyRule volley: volley.ApplyTreeKeystone(); break;
                case VenomRule venom: venom.ApplyTreeKeystone(); break;
                case AegisRule aegis: aegis.ApplyTreeKeystone(); break;
                case RigRule rig: rig.ApplyTreeKeystone(); break;
            }
        }
    }
}
