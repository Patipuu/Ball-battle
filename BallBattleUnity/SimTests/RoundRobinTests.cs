using System;
using System.IO;
using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Tools;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>6a.6b/c part C: headless round-robin (pure core in Sim/Tools; the player's -roundrobin mode wraps it).</summary>
    public class RoundRobinTests
    {
        [Test]
        public void ParseDefaultsAndValues()
        {
            var d = RoundRobin.Parse(new[] { "-batchmode", "-nographics", "-roundrobin" });
            Assert.That((d.Seeds, d.Arena, d.Out, d.MaxTicks), Is.EqualTo((200, ArenaRegistry.Classic, "results.csv", 0)));
            var o = RoundRobin.Parse(new[] { "-roundrobin", "-out", "x/r.csv", "-seeds", "7", "-arena", "all", "-maxTicks", "500" });
            Assert.That((o.Seeds, o.Arena, o.Out, o.MaxTicks), Is.EqualTo((7, "all", "x/r.csv", 500)));
            Assert.Throws<ArgumentException>(() => RoundRobin.Parse(new[] { "-seeds", "0" }));
            Assert.Throws<ArgumentException>(() => RoundRobin.Parse(new[] { "-seeds", "abc" }));
            Assert.Throws<ArgumentException>(() => RoundRobin.Parse(new[] { "-arena", "nope" }));
            Assert.Throws<ArgumentException>(() => RoundRobin.Parse(new[] { "-out" }));
        }

        [Test]
        public void WeaponFileSitsNextToTheMainFile()
        {
            Assert.That(RoundRobin.WeaponPath("results.csv"), Is.EqualTo("results-weapons.csv"));
            Assert.That(RoundRobin.WeaponPath("out/run.1/results"), Is.EqualTo("out/run.1/results-weapons"));
        }

        [Test]
        public void DecimalIsIntegerOnlyAndRoundsHalfUp()
        {
            Assert.That(RoundRobin.Decimal(1, 2, 4), Is.EqualTo("0.5000"));
            Assert.That(RoundRobin.Decimal(2, 3, 4), Is.EqualTo("0.6667"));
            Assert.That(RoundRobin.Decimal(12345, 10, 1), Is.EqualTo("1234.5"));
            Assert.That(RoundRobin.Decimal(5, 200, 2), Is.EqualTo("0.03"));   // 0.025 → half up
            Assert.That(RoundRobin.Decimal(200, 200, 4), Is.EqualTo("1.0000"));
        }

        [Test]
        public void SeedsDependOnlyOnPairAndIndex()
        {
            Assert.That(RoundRobin.Seed("blade", "fang", 3), Is.EqualTo(RoundRobin.Seed("blade", "fang", 3)));
            Assert.That(RoundRobin.Seed("blade", "fang", 3), Is.Not.EqualTo(RoundRobin.Seed("fang", "blade", 3)));
            Assert.That(RoundRobin.Seed("blade", "fang", 3), Is.Not.EqualTo(RoundRobin.Seed("blade", "fang", 4)));
        }

        [Test]
        public void SameArgumentsGiveByteIdenticalCsv()
        {
            var o = RoundRobin.Parse(new[] { "-seeds", "3", "-arena", "classic" });
            var a = RoundRobin.Run(o, parallel: true);
            var b = RoundRobin.Run(o, parallel: false);
            Assert.That(a.MainCsv, Is.EqualTo(b.MainCsv));
            Assert.That(a.WeaponCsv, Is.EqualTo(b.WeaponCsv));
            var lines = a.MainCsv.TrimEnd('\n').Split('\n');
            var n = WeaponRegistry.All.Count;
            Assert.That(lines[0], Is.EqualTo(RoundRobin.MainHeader));
            Assert.That(lines.Length, Is.EqualTo(1 + n * n), "every ordered pair, mirrors included");
            Assert.That(a.WeaponCsv.TrimEnd('\n').Split('\n')[0], Is.EqualTo(RoundRobin.WeaponHeader));
            foreach (var p in a.Pairs)
            {
                Assert.That(p.WinsA + p.WinsB + p.Draws, Is.EqualTo(p.Matches));
                Assert.That(p.Matches, Is.EqualTo(3));
            }
            // Every match in the table replays to the same result on its own.
            var first = a.Pairs[1];
            var (w, t) = RoundRobin.Play(first.Arena, first.WeaponA, first.WeaponB, RoundRobin.Seed(first.WeaponA, first.WeaponB, 0), 0);
            Assert.That(t, Is.LessThanOrEqualTo(first.MaxTicks));
            Assert.That(w, Is.InRange(-1, 1));
        }

        [Test]
        public void MaxTicksStopsMatchesAsDraws()
        {
            var r = RoundRobin.Run(RoundRobin.Parse(new[] { "-seeds", "2", "-maxTicks", "5" }));
            Assert.That(r.Pairs.All(p => p.MaxTicks <= 5));
            Assert.That(r.Pairs.Sum(p => p.Draws), Is.EqualTo(r.Pairs.Sum(p => p.Matches)), "nobody dies in 5 ticks");
        }

        /// <summary>
        /// dotnet entry for the same tool: BB_RR_ARGS="-seeds 200 -arena classic -out results.csv" dotnet test --filter Name=RoundRobinCli
        /// </summary>
        [Test, Category("Tool"), Explicit("tool: set BB_RR_ARGS")]
        public void RoundRobinCli()
        {
            var args = (Environment.GetEnvironmentVariable("BB_RR_ARGS") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var o = RoundRobin.Parse(args);
            var r = RoundRobin.Run(o);
            var path = Path.GetFullPath(o.Out);
            File.WriteAllText(path, r.MainCsv);
            File.WriteAllText(RoundRobin.WeaponPath(path), r.WeaponCsv);
            TestContext.Progress.WriteLine($"wrote {path} and {RoundRobin.WeaponPath(path)}");
            TestContext.Progress.Write(r.WeaponCsv);
        }
    }
}
