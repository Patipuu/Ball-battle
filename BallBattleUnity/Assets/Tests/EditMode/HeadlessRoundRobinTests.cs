using System.IO;
using BallBattle.Sim.Tools;
using BallBattle.View;
using NUnit.Framework;

namespace BallBattle.Tests
{
    /// <summary>6a.6b/c part C: the player's -roundrobin mode (files, exit codes; same bytes for the same arguments).</summary>
    public class HeadlessRoundRobinTests
    {
        string dir;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "bb-rr-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }

        [Test]
        public void WritesBothCsvsAndExitsZeroSameBytesTwice()
        {
            var a = Path.Combine(dir, "a.csv");
            var b = Path.Combine(dir, "b.csv");
            var log = new StringWriter();
            Assert.That(HeadlessRoundRobin.Execute(new[] { "x", "-batchmode", "-nographics", "-roundrobin", "-seeds", "2", "-arena", "classic", "-out", a }, log, log), Is.Zero, log.ToString());
            Assert.That(HeadlessRoundRobin.Execute(new[] { "-roundrobin", "-out", b, "-arena", "classic", "-seeds", "2" }, log, log), Is.Zero, log.ToString());
            Assert.That(File.ReadAllBytes(b), Is.EqualTo(File.ReadAllBytes(a)));
            Assert.That(File.ReadAllBytes(RoundRobin.WeaponPath(b)), Is.EqualTo(File.ReadAllBytes(RoundRobin.WeaponPath(a))));
            StringAssert.StartsWith(RoundRobin.MainHeader + "\n", File.ReadAllText(a));
            StringAssert.StartsWith(RoundRobin.WeaponHeader + "\n", File.ReadAllText(RoundRobin.WeaponPath(a)));
        }

        [Test]
        public void BadArgumentsExitNonZero()
        {
            var log = new StringWriter();
            Assert.That(HeadlessRoundRobin.Execute(new[] { "-roundrobin", "-arena", "nope", "-out", Path.Combine(dir, "c.csv") }, log, log), Is.EqualTo(1));
            Assert.That(HeadlessRoundRobin.Execute(new[] { "-roundrobin", "-seeds", "-3" }, log, log), Is.EqualTo(1));
            StringAssert.Contains("round-robin failed", log.ToString());
        }
    }
}
