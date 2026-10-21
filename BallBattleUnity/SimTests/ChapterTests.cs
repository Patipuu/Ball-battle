using System;
using System.Collections.Generic;
using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Boss;
using BallBattle.Sim.Meta;
using BallBattle.Sim.Pvp;
using BallBattle.Sim.Run;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>PvE chapters (21/10) and the Run-only tribute ball BGLGXLQ.</summary>
    public class ChapterTests
    {
        static RunState Start(uint seed, int chapter, string weapon = null)
        {
            var p = new ProgressData { RunsPlayed = 1 };
            var choices = RunState.StartChoices(seed, UnlockRules.Weapons(p));
            return RunState.Start(seed, weapon != null && choices.Contains(weapon) ? weapon : choices[0], p, chapter: chapter);
        }

        [Test]
        public void EachChapterHasItsOwnBossPairBallsAndArenas()
        {
            Assert.That(Chapters.Count, Is.EqualTo(4));
            var bosses = new HashSet<string>();
            for (var c = 0; c < Chapters.Count; c++)
            {
                var ch = Chapters.Get(c);
                Assert.That(BossIds.SlotOf(ch.Bosses[0]), Is.EqualTo(0), ch.Name);
                Assert.That(BossIds.SlotOf(ch.Bosses[1]), Is.EqualTo(1), ch.Name);
                bosses.Add(ch.Bosses[0]); bosses.Add(ch.Bosses[1]);
                for (uint seed = 1; seed <= 20; seed++)
                {
                    var run = Start(seed, c);
                    Assert.That(run.Chapter, Is.EqualTo(c));
                    Assert.That(run.BossIds, Is.EqualTo(ch.Bosses));
                    Assert.That(run.EnemyWeapons, Is.SubsetOf(ch.EnemyWeapons));
                    for (var f = 0; f < RunTuning.Fights; f++)
                    {
                        run.FightIndex = f;
                        Assert.That(ch.Arenas, Does.Contain(run.ArenaId), $"chapter {c} fight {f}");
                        var e = run.Enemy;
                        if (!run.IsBossFight) Assert.That(ch.EnemyWeapons, Does.Contain(e.WeaponId));
                    }
                }
            }
            Assert.That(bosses.Count, Is.EqualTo(8), "the four chapters use all eight bosses once");
        }

        [Test]
        public void LaterChaptersAreTougher()
        {
            for (var c = 1; c < Chapters.Count; c++)
            {
                Assert.That(Chapters.Get(c).HpPct, Is.GreaterThan(Chapters.Get(c - 1).HpPct));
                Assert.That(Chapters.Get(c).DamagePct, Is.GreaterThan(Chapters.Get(c - 1).DamagePct));
            }
            var a = Start(3, 0); var b = Start(3, 3);
            a.FightIndex = b.FightIndex = 0;
            Assert.That(b.Enemy.MaxHp, Is.GreaterThan(a.Enemy.MaxHp));
        }

        [Test]
        public void BglgxlqIsNeverABot()
        {
            for (var c = 0; c < Chapters.Count; c++)
                Assert.That(Chapters.Get(c).EnemyWeapons, Does.Not.Contain(ChorusRule.WeaponId));
            Assert.That(WeaponRegistry.IsPlayerOnly(ChorusRule.WeaponId), Is.True);
            Assert.That(WeaponRegistry.IsPlayerOnly(SerpentRule.WeaponId), Is.False, "only BGLGXLQ is Run-only");

            // MIMIC copies the player's ball, except BGLGXLQ: then it keeps a ball of its own.
            for (uint seed = 1; seed <= 40; seed++)
            {
                var run = Start(seed, 0, ChorusRule.WeaponId);
                if (run.Build.WeaponId != ChorusRule.WeaponId) continue;
                run.FightIndex = RunTuning.BossFights[1];
                Assert.That(run.CurrentBossId, Is.EqualTo(BossIds.Mimic));
                Assert.That(run.Enemy.WeaponId, Is.Not.EqualTo(ChorusRule.WeaponId));
                return;
            }
            Assert.Inconclusive("no seed offered BGLGXLQ at the start");
        }

        [Test]
        public void WinningAChapterOpensTheNextOne()
        {
            var p = new ProgressData();
            Assert.That(Chapters.Unlocked(p), Is.EqualTo(0));
            var run = Start(1, 0);
            run.Over = true; run.Won = true;
            p.RunsWon = 1;
            Chapters.RecordWin(p, run);
            Assert.That(Chapters.Unlocked(p), Is.EqualTo(1));
            Assert.That(run.Achievements, Does.Contain(Chapters.Mark + "1"));
            Assert.That(Chapters.Cleared(p, 0), Is.True);
            Assert.That(Chapters.Cleared(p, 1), Is.False);

            var replay = Start(2, 0);   // clearing chapter 1 again changes nothing
            replay.Over = true; replay.Won = true;
            p.RunsWon = 2;
            Chapters.RecordWin(p, replay);
            Assert.That(Chapters.Unlocked(p), Is.EqualTo(1));
            Assert.That(replay.Achievements.Any(x => x.StartsWith(Chapters.Mark)), Is.False);

            var old = new ProgressData { RunsWon = 3 };   // a save from before chapters, with wins: chapter 2 is open
            Assert.That(Chapters.Unlocked(old), Is.EqualTo(1));
            Assert.That(Chapters.Unlocked(new ProgressData { ChaptersCleared = 9 }), Is.EqualTo(Chapters.Count - 1));
        }

        [Test]
        public void TutorialRunIsChapterOne()
        {
            var p = new ProgressData();
            var choices = RunState.StartChoices(RunState.TutorialSeed, UnlockRules.Weapons(p), true);
            var run = RunState.Start(RunState.TutorialSeed, choices[0], p, tutorial: true, chapter: 3);
            Assert.That(run.Chapter, Is.EqualTo(0));
        }

        [Test]
        public void PvpKeepsBglgxlqOut()
        {
            Assert.Throws<ArgumentException>(() => new PvpBan(ChorusRule.WeaponId, ArenaRegistry.Classic));
            var balls = WeaponRegistry.All.Select(w => new PvpBallSnapshot(new BallProgress { Id = w.Id })).ToList();
            var defense = new[] { ChorusRule.WeaponId, FangRule.WeaponId, PikeRule.WeaponId }
                .Select(id => new PvpBallSnapshot(new BallProgress { Id = id })).ToList();
            var draft = PvpDraft.Resolve(new PvpBan(VolleyRule.WeaponId, ArenaRegistry.Classic), new PvpBan(VolleyRule.WeaponId, ArenaRegistry.Classic),
                BladeRule.WeaponId, balls, defense, 7);
            Assert.That(draft.Defender.Id, Is.EqualTo(FangRule.WeaponId), "a stored BGLGXLQ defender is skipped");
            Assert.Throws<ArgumentException>(() => PvpDraft.Resolve(new PvpBan(VolleyRule.WeaponId, ArenaRegistry.Classic),
                new PvpBan(VolleyRule.WeaponId, ArenaRegistry.Classic), ChorusRule.WeaponId, balls, defense, 7));
        }
    }
}
