using System;
using System.IO;
using BallBattle.Sim.Tools;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Player command-line mode (6a.6b/c part C), before any scene loads:
    ///   ./BallBattle.x86_64 -batchmode -nographics -roundrobin [-seeds 200] [-arena classic|all|&lt;id&gt;] [-out results.csv] [-maxTicks N]
    /// Writes the pair CSV to -out and the per-weapon CSV next to it (results-weapons.csv), prints a short summary to stdout
    /// and quits with exit code 0, or 1 on any error (bad argument, unknown arena, I/O failure). Without -roundrobin it does nothing.
    /// </summary>
    public static class HeadlessRoundRobin
    {
        public const string Flag = "-roundrobin";

        /// <summary>True once the round-robin mode started (the game scene, if it loads, stays idle).</summary>
        public static bool Active { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Entry()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, Flag) < 0) return;
            Active = true;
            var code = Execute(args, Console.Out, Console.Error);
            Application.Quit(code);
        }

        /// <summary>Runs the mode with these arguments; returns the exit code. Public for tests.</summary>
        public static int Execute(string[] args, TextWriter stdout, TextWriter stderr)
        {
            try
            {
                var o = RoundRobin.Parse(args);
                stdout.WriteLine($"[BallBattle] round-robin: arena {o.Arena}, {o.Seeds} seeds per pair" + (o.MaxTicks > 0 ? $", maxTicks {o.MaxTicks}" : ""));
                var started = DateTime.UtcNow;
                var r = RoundRobin.Run(o);
                var outPath = Path.GetFullPath(o.Out);
                var dir = Path.GetDirectoryName(outPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(outPath, r.MainCsv);
                var weaponsPath = RoundRobin.WeaponPath(outPath);
                File.WriteAllText(weaponsPath, r.WeaponCsv);
                stdout.WriteLine($"[BallBattle] {r.Pairs.Count} pairs, {r.Pairs.Count * o.Seeds} matches in {(DateTime.UtcNow - started).TotalSeconds:0.0} s");
                stdout.WriteLine($"[BallBattle] wrote {outPath}");
                stdout.WriteLine($"[BallBattle] wrote {weaponsPath}");
                stdout.Write(r.WeaponCsv);
                stdout.Flush();
                return 0;
            }
            catch (Exception e)
            {
                stderr.WriteLine($"[BallBattle] round-robin failed: {e.Message}");
                stderr.Flush();
                return 1;
            }
        }
    }
}
