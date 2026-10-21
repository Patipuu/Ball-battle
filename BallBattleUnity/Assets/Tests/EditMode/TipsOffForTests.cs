using BallBattle.View;
using NUnit.Framework;
using UnityEngine;

namespace BallBattle.Tests
{
    /// <summary>
    /// UI-TUT1: first-run tips block the screen's buttons until OK. The older flow tests start from fresh saves and tap
    /// through the run, so tips are off for the whole assembly; BbTut1Tests switch them on for themselves.
    /// 21/10: the same goes for BI the guide (GuideView) and the first-launch ACCOUNT screen; AccountAndGuideTests switch them on.
    /// </summary>
    [SetUpFixture]
    public class TipsOffForTests
    {
        Lang previousLanguage;
        bool hadSavedLanguage;
        int savedLanguage;

        [OneTimeSetUp]
        public void Off()
        {
            TutorialCoach.Enabled = false;
            GuideView.Enabled = false;
            AccountView.ShowOnLaunch = false;
            HudView.PlayerName = () => "";   // HUD asserts read "YOU EMBER", whatever account this machine is signed in to
            previousLanguage = StringTable.Current;
            hadSavedLanguage = PlayerPrefs.HasKey("language_vi");
            savedLanguage = PlayerPrefs.GetInt("language_vi", 1);
            StringTable.Current = Lang.En; // legacy UI assertions use the English option
            PlayerPrefs.SetInt("language_vi", 0);
        }

        [OneTimeTearDown]
        public void Restore()
        {
            TutorialCoach.Enabled = !DebugOptions.NoTips;
            GuideView.Enabled = !DebugOptions.NoTips;
            AccountView.ShowOnLaunch = !DebugOptions.NoTips;
            HudView.PlayerName = () => AccountSession.Name;
            StringTable.Current = previousLanguage;
            if (hadSavedLanguage) PlayerPrefs.SetInt("language_vi", savedLanguage);
            else PlayerPrefs.DeleteKey("language_vi");
        }
    }
}
