using System;

namespace BallBattle.View
{
    /// <summary>Screens that decide the music track (AudioIds.TrackFor). None = leave the music as it is.</summary>
    public enum MusicScreen { None, Menu, VersusPick, Settings, RunTitle, PickWeapon, StartPick, Prepare, Fight, BossFight, FightResult, Summary, VersusResult }

    /// <summary>
    /// Clip ids (= file names in Assets/Audio and Assets/Audio/Generated) and the routing rules around them.
    /// Boss cues (boss_*) are BB-04 and deliberately absent.
    /// </summary>
    public static class AudioIds
    {
        public const string MusicMenu = "music_menu", MusicBattle = "music_battle", MusicBoss = "music_boss", MusicResult = "music_result";

        public const string UiClick = "ui_click", UiBack = "ui_back", UiDenied = "ui_denied";
        public const string CardPick = "card_pick", CardReroll = "card_reroll";
        public const string FightStart = "fight_start", FightWin = "fight_win", FightLose = "fight_lose";
        public const string RetryBanner = "retry_banner", Unlock = "unlock", RunWin = "run_win", RunOver = "run_over", SecondWind = "second_wind";

        /// <summary>Generic match sounds (Assets/Audio/Generated, SfxLibrary fields).</summary>
        public const string Hit = "hit", Parry = "parry", Wall = "wall", Death = "death";
        public const string HitPrefix = "hit_";

        /// <summary>Weapon ids with their own hit clip.</summary>
        public static readonly string[] HitWeapons = { "blade", "fang", "pike", "brawler", "volley", "venom", "aegis", "rig" };

        public static readonly string[] Music = { MusicMenu, MusicBattle, MusicBoss, MusicResult };

        /// <summary>Every one-shot linked into the SfxLibrary besides the generic ones.</summary>
        public static readonly string[] Cues =
        {
            UiClick, UiBack, CardPick, CardReroll, UiDenied, FightStart, FightWin, FightLose, RetryBanner, Unlock, RunWin, RunOver, SecondWind,
            "hit_blade", "hit_fang", "hit_pike", "hit_brawler", "hit_volley", "hit_venom", "hit_aegis", "hit_rig",
        };

        /// <summary>"hit_&lt;weaponId&gt;" when that clip exists, else the generic "hit".</summary>
        public static string HitCue(string weaponId, Func<string, bool> hasClip)
        {
            if (string.IsNullOrEmpty(weaponId)) return Hit;
            var id = HitPrefix + weaponId;
            return hasClip != null && hasClip(id) ? id : Hit;
        }

        public static bool IsHit(string id) => id == Hit || (id != null && id.StartsWith(HitPrefix, StringComparison.Ordinal));

        /// <summary>Cues that duck the music by Ducker.DepthDb.</summary>
        public static bool Ducks(string id) =>
            IsHit(id) || id == Parry || id == Death || id == FightStart || id == FightWin || id == RetryBanner || id == RunWin || id == SecondWind;

        /// <summary>Cues that get the small random pitch (never music or UI).</summary>
        public static bool Jittered(string id) => IsHit(id) || id == Parry || id == Wall;

        /// <summary>Result cues that stop the fight music (fade) instead of ducking it.</summary>
        public static bool EndsFightMusic(string id) => id == FightWin || id == FightLose || id == RunWin || id == RunOver;

        public static string TrackFor(MusicScreen s)
        {
            switch (s)
            {
                case MusicScreen.Menu:
                case MusicScreen.VersusPick:
                case MusicScreen.Settings:
                case MusicScreen.RunTitle:
                case MusicScreen.PickWeapon:
                case MusicScreen.StartPick:
                case MusicScreen.Prepare:
                    return MusicMenu;
                case MusicScreen.Fight: return MusicBattle;
                case MusicScreen.BossFight: return MusicBoss;
                case MusicScreen.FightResult:
                case MusicScreen.Summary:
                case MusicScreen.VersusResult:
                    return MusicResult;
                default: return null;
            }
        }

        public static bool IsFightTrack(string track) => track == MusicBattle || track == MusicBoss;
    }
}
