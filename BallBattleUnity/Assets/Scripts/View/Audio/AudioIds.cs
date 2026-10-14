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
        /// <summary>Optional clip for a bounce inside a spike zone; when absent the plain wall sound plays.</summary>
        public const string SpikeHit = "spike_hit";

        /// <summary>Weapon ids with their own hit clip.</summary>
        public static readonly string[] HitWeapons = { "blade", "fang", "pike", "brawler", "volley", "venom", "aegis", "rig" };

        public static readonly string[] Music = { MusicMenu, MusicBattle, MusicBoss, MusicResult };

        /// <summary>Every one-shot linked into the SfxLibrary besides the generic ones.</summary>
        public static readonly string[] Cues =
        {
            UiClick, UiBack, CardPick, CardReroll, UiDenied, FightStart, FightWin, FightLose, RetryBanner, Unlock, RunWin, RunOver, SecondWind,
            SpikeHit,   // BB-FIX1b: Assets/Audio/spike_hit.wav (a missing file is skipped by the linker with a warning)
            "hit_blade", "hit_fang", "hit_pike", "hit_brawler", "hit_volley", "hit_venom", "hit_aegis", "hit_rig",
        };

        /// <summary>"hit_&lt;weaponId&gt;" when that clip exists, else the generic "hit".</summary>
        public static string HitCue(string weaponId, Func<string, bool> hasClip)
        {
            if (string.IsNullOrEmpty(weaponId)) return Hit;
            var id = HitPrefix + weaponId;
            return hasClip != null && hasClip(id) ? id : Hit;
        }

        /// <summary>Most a hit cue's pitch rises with the attacker's heat (+12%).</summary>
        public const float HeatPitchCap = 0.12f;
        /// <summary>Lower cap for the rapid-fire weapons (SPARKLER = volley, HIVE = rig): +8%.</summary>
        public const float HeatPitchCapRapid = 0.08f;

        /// <summary>Heat pitch cap of a weapon: +8% for volley and rig, +12% for every other weapon.</summary>
        public static float HeatPitchCapFor(string weaponId) => weaponId == "volley" || weaponId == "rig" ? HeatPitchCapRapid : HeatPitchCap;

        /// <summary>Pitch of a hit cue from the attacker's heat (0 = cold .. 1 = hot, clamped): 1 + cap * heat.</summary>
        public static float HeatPitch(string weaponId, float heat) => 1f + HeatPitchCapFor(weaponId) * Math.Max(0f, Math.Min(1f, heat));

        public static bool IsHit(string id) => id == Hit || (id != null && id.StartsWith(HitPrefix, StringComparison.Ordinal));

        /// <summary>Cues that duck the music by Ducker.DepthDb.</summary>
        public static bool Ducks(string id) =>
            IsHit(id) || id == Parry || id == Death || id == FightStart || id == FightWin || id == RetryBanner || id == RunWin || id == SecondWind;

        /// <summary>Cues that get the small random pitch (never music or UI).</summary>
        public static bool Jittered(string id) => IsHit(id) || id == Parry || id == Wall || id == SpikeHit;

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
