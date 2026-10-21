using System;
using System.Collections.Generic;
using BallBattle.Sim;
using BallBattle.Sim.Boss;
using BallBattle.Sim.Run;
using BallBattle.SimTests;

sealed class Program
{
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "verify") { Verify(); return; }
        var count = args.Length > 0 ? int.Parse(args[0]) : 32;
        var forced = args.Length > 1 ? args[1] : null;
        if (forced == "without-new-attacks")
        {
            BossTuning.MimicEchoDamage = Fix.Zero;
            BossTuning.WardenCrossDamage = Fix.Zero;
            forced = null;
        }
        var overall = new Dictionary<string, int[]>();
        foreach (var id in BossIds.All) overall[id] = new int[5];
        var wins = 0;
        for (uint seed = 1; seed <= count; seed++)
        {
            var result = RunBot.Play(seed, RunBot.Policy.Greedy, (run, match) =>
            {
                var id = run.CurrentBossId;
                if (id == null) return;
                var row = overall[id];
                row[0]++;
                if (match.WinnerIndex == 0) row[1]++;
                if (match.EndReason == MatchEndReason.TimeCap) row[2]++;
                row[3] += match.ActiveTick;
                row[4] += (int)(run.Build.Hp - match.Balls[0].Hp).ToFloat();
            }, forced == null ? (Action<RunState, MatchSim>)null : (run, match) =>
            {
                if (run.FightIndex == 0) run.BossIds[BossIds.SlotOf(forced)] = forced;
            });
            if (result.Won) wins++;
        }
        Console.WriteLine($"Greedy run wins: {wins}/{count} ({100.0*wins/count:0.0}%)");
        foreach (var id in BossIds.All)
        {
            var row = overall[id];
            Console.WriteLine($"{id,-12} fights={row[0],3} wins={row[1],3} win%={(row[0] == 0 ? 0 : 100.0*row[1]/row[0]):0.0} cap={row[2]} avgTicks={(row[0] == 0 ? 0 : row[3]/row[0])} avgHpLost={(row[0] == 0 ? 0 : row[4]/row[0])}");
        }
    }

    static void Verify()
    {
        if (BossIds.All.Length != 8 || BossIds.Slots[0].Length != 4 || BossIds.Slots[1].Length != 4)
            throw new Exception("Boss pool is not 4 + 4");
        foreach (var id in BossIds.All)
        {
            var slot = BossIds.SlotOf(id);
            var enemy = EnemyGenerator.Generate(17, RunTuning.BossFights[slot], id);
            if (enemy.ToLoadout().Boss.Id != id) throw new Exception(id + " does not bind");
            if (id != BossIds.Mimic && enemy.WeaponId != BossCoreRule.WeaponId)
                throw new Exception(id + " still rolls a player weapon");
            var a = CheckFight(id);
            var b = CheckFight(id);
            var events = 0;
            for (var tick = 0; tick < 950; tick++)
            {
                a.Step(); b.Step();
                if (a.ComputeHash() != b.ComputeHash()) throw new Exception(id + " non-deterministic at " + tick);
                foreach (var e in a.Events) if (e.Type == SimEventType.Boss) events++;
            }
            if (events == 0) throw new Exception(id + " never telegraphed");
        }
        var mimic = CheckFight(BossIds.Mimic);
        mimic.DealDamage(mimic.Balls[0], mimic.Balls[1], 1000, DamageKind.Ability);
        while (mimic.ActiveTick < 900) mimic.Step();
        var rule = (MimicRule)mimic.Boss;
        var dealt = mimic.DealDamage(mimic.Balls[0], mimic.Balls[1], 10, DamageKind.Ability);
        if (rule.AdaptedKind != DamageKind.Ability || dealt != (Fix)10 * Fix.Ratio(7, 10))
        {
            Console.WriteLine($"Mimic state: adapted={rule.Adapted} kind={rule.AdaptedKind} active={mimic.ActiveTick} dealt={dealt}");
            throw new Exception("Mimic did not adapt to actual damage");
        }
        Console.WriteLine("boss8 verify OK: 8 loadouts, events, deterministic replay, Mimic adaptation");
    }

    static MatchSim CheckFight(string id)
    {
        var player = new BallLoadout(new BossCoreRule()) { MaxHp = 10000 };
        var boss = new BallLoadout(new BossCoreRule()) { MaxHp = 10000, Boss = BossRegistry.Create(id) };
        return new MatchSim(new MatchConfig { CapTicks = 4000 }, 13, new[] { player, boss }, true);
    }
}
