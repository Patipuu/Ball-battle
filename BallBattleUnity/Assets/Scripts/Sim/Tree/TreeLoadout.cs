namespace BallBattle.Sim
{
    /// <summary>
    /// BB-12g skill tree in a match: the Run player's tree nodes that act inside the Sim (the root VITALITY / POWER ranks and the
    /// skill level / branch are applied to the RunBuild before the match). A ball with Tree == null (Versus, every enemy) plays and
    /// hashes exactly as before the tree. A Run player always has a Tree (maybe empty): the Run-only baselines (WRECKER, MIRROR,
    /// EMBER A, HIVE A) come with it.
    /// </summary>
    public sealed class TreeLoadout
    {
        /// <summary>INSTINCT (growth node) rank 0..2.</summary>
        public int Growth;
        /// <summary>OPENER (start node) bought.</summary>
        public bool Start;
        /// <summary>FINALE: '-' none, 'A' or 'B' (same branch as the skill).</summary>
        public char Finale = '-';

        public ulong HashInto(ulong h)
        {
            h = SimHash.Mix(h, Growth);
            h = SimHash.Mix(h, Start);
            return SimHash.Mix(h, (int)Finale);
        }
    }

    /// <summary>BB-12g tree numbers that are not inside a weapon rule (docs/design/bb12-persona-web-v2.md, harness v6).</summary>
    public static class TreeTuning
    {
        /// <summary>VITALITY: % max HP per rank; POWER: % damage per rank. Order of WeaponOrder.</summary>
        public static readonly string[] WeaponOrder = { "blade", "fang", "pike", "brawler", "volley", "venom", "aegis", "rig", "chorus", "serpent" };
        public static readonly int[] HpPerRank = { 4, 6, 5, 1, 4, 5, 5, 3, 4, 4 };
        public static readonly int[] DamagePerRank = { 4, 8, 5, 1, 4, 6, 6, 2, 4, 4 };

        static int Index(string weaponId)
        {
            for (var i = 0; i < WeaponOrder.Length; i++) if (WeaponOrder[i] == weaponId) return i;
            return -1;
        }

        public static int HpPct(string weaponId) { var i = Index(weaponId); return i < 0 ? 0 : HpPerRank[i]; }
        public static int DamagePct(string weaponId) { var i = Index(weaponId); return i < 0 ? 0 : DamagePerRank[i]; }

        /// <summary>Spawn: the Run-only baselines, then OPENER, then FINALE (after the ball has its position: HIVE's reserve turret).</summary>
        internal static void ApplyAtSpawn(BallState b)
        {
            if (b.Tree == null) return;
            switch (b.Weapon)
            {
                case Weapons.BrawlerRule brawler: brawler.ApplyRunBaseline(); break;
                case Weapons.AegisRule aegis: aegis.ApplyRunBaseline(); break;
            }
            if (b.Tree.Start)
                switch (b.Weapon)
                {
                    case Weapons.BladeRule w: w.ApplyTreeStart(); break;
                    case Weapons.FangRule w: w.ApplyTreeStart(); break;
                    case Weapons.PikeRule w: w.ApplyTreeStart(); break;
                    case Weapons.BrawlerRule w: w.ApplyTreeStart(); break;
                    case Weapons.VolleyRule w: w.ApplyTreeStart(); break;
                    case Weapons.VenomRule w: w.ApplyTreeStart(); break;
                    case Weapons.AegisRule w: w.ApplyTreeStart(); break;
                    case Weapons.RigRule w: w.ApplyTreeStart(); break;
                    case Weapons.ChorusRule w: w.ApplyTreeStart(); break;    // BB-14 MORNING LIGHT
                    case Weapons.SerpentRule w: w.ApplyTreeStart(); break;   // BB-14 FIRST MEAL
                }
            if (b.Tree.Finale != '-')
                switch (b.Weapon)
                {
                    case Weapons.PikeRule w: w.RefreshLength(); break;
                    case Weapons.BrawlerRule w: w.ApplyTreeKeystone(); break;
                }
        }
    }
}
