using BallBattle.View;
using NUnit.Framework;

namespace BallBattle.Tests
{
    /// <summary>
    /// UI-TUT1: first-run tips block the screen's buttons until OK. The older flow tests start from fresh saves and tap
    /// through the run, so tips are off for the whole assembly; BbTut1Tests switch them on for themselves.
    /// </summary>
    [SetUpFixture]
    public class TipsOffForTests
    {
        [OneTimeSetUp]
        public void Off() => TutorialCoach.Enabled = false;

        [OneTimeTearDown]
        public void Restore() => TutorialCoach.Enabled = !DebugOptions.NoTips;
    }
}
