using System;
using System.Collections.Generic;
using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Run;

// A first-fight pacing probe, not a human retention or skill test. No Unity save is touched.
var samples = args.Length > 0 ? int.Parse(args[0]) : 20;
var useTap = args.Length < 2 || args[1] != "no-tap";
var tutorialHp = args.Length > 2 ? int.Parse(args[2]) : 60;
var fixedTutorialSeed = args.Length > 3 && args[3] == "fixed";
var foeWeapon = args.Length > 4 ? args[4] : null;
var ids = new[] { "blade", "fang", "pike", "brawler", "volley", "venom", "aegis", "rig", "chorus", "serpent" };
foreach (var id in ids)
{
    var rows = new List<(int first, int duration, int gap, int events, int skills, int perfect, int miss, bool won)>();
    for (uint sample = 1; sample <= samples; sample++)
    {
        var seed = fixedTutorialSeed ? RunState.TutorialSeed : sample;
        var progress = new ProgressData();
        var choices = RunState.StartChoices(seed, UnlockRules.Weapons(progress), true);
        var run = RunState.Start(seed, choices[0], progress, tutorial: true);
        run.Build.WeaponId = id;
        PickFirst(run); // first-run start card
        PickFirst(run); // free card before the first fight
        var player = run.Build.ToLoadout();
        player.TapEnabled = RunTuning.TapStrike;
        var enemy = run.Enemy;
        enemy.MaxHp = enemy.Hp = tutorialHp;
        if (foeWeapon != null) enemy.WeaponId = foeWeapon;
        var match = new MatchSim(ArenaRegistry.Create(run.ArenaId), run.FightSeed,
            new[] { player, enemy.ToLoadout() }, true);
        match.ApplySpawnHooks();
        var first = -1;
        var last = -1;
        var longestGap = 0;
        var impacts = 0;
        var skills = 0;
        var perfects = 0;
        var misses = 0;
        while (match.Outcome == MatchOutcome.Ongoing)
        {
            var tap = match.Balls[0].Tap;
            if (useTap && match.ActiveTick >= 5 * MatchConfig.TicksPerSecond && tap != null && tap.Ready)
                match.Tap(0);
            match.Step();
            foreach (var e in match.Events)
            {
                if (e.Type == SimEventType.SkillFired && e.A == 0) skills++;
                if (e.Type == SimEventType.TapPerfect && e.A == 0) perfects++;
                if (e.Type == SimEventType.TapMiss && e.A == 0) misses++;
                var impact = e.Value > Fix.Zero &&
                    (e.Type == SimEventType.Hit || e.Type == SimEventType.ProjectileHit ||
                     e.Type == SimEventType.Damage || e.Type == SimEventType.StatusTick ||
                     e.Type == SimEventType.ObstacleHit || e.Type == SimEventType.WallBounce);
                if (!impact) continue;
                if (first < 0) first = match.ActiveTick;
                if (last >= 0) longestGap = Math.Max(longestGap, match.ActiveTick - last);
                last = match.ActiveTick;
                impacts++;
            }
        }
        rows.Add((first, match.ActiveTick, longestGap, impacts, skills, perfects, misses, match.WinnerIndex == 0));
    }
    Console.WriteLine($"{id,-8} n={samples} first={Median(rows.Select(x => x.first)) / 60.0:0.0}s " +
                      $"fight={Median(rows.Select(x => x.duration)) / 60.0:0.0}s " +
                      $"gap={Median(rows.Select(x => x.gap)) / 60.0:0.0}s " +
                      $"impacts={Median(rows.Select(x => x.events))} skills={Median(rows.Select(x => x.skills))} " +
                      $"perfect={Median(rows.Select(x => x.perfect))} miss={Median(rows.Select(x => x.miss))} " +
                      $"win={100.0 * rows.Count(x => x.won) / samples:0}%");
    if (fixedTutorialSeed && useTap && tutorialHp == 60 && foeWeapon == null &&
        new[] { "blade", "fang", "pike", "brawler" }.Contains(id))
    {
        var actual = rows[0];
        if (actual.first > 2 * MatchConfig.TicksPerSecond ||
            actual.duration > 40 * MatchConfig.TicksPerSecond || !actual.won)
            throw new Exception($"Tutorial pacing regressed for {id}: first={actual.first}, duration={actual.duration}, won={actual.won}");
    }
}

static void PickFirst(RunState run)
{
    for (var i = 0; i < run.OfferCount; i++)
        if (run.CanPick(i)) { run.Pick(i); return; }
    throw new Exception("No pick in first-fight probe");
}

static int Median(IEnumerable<int> values)
{
    var a = values.OrderBy(x => x).ToArray();
    return a[a.Length / 2];
}
