using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using BallBattle.View;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BallBattle.EditorTools
{
    /// <summary>
    /// Trailer frames: plays chosen fights through the real ArenaView and writes one 1080x1920 PNG per frame (the 270x480 view at x4,
    /// point-filtered) plus events.csv (frame, event, balls, weapons) so sound can be laid on the cuts afterwards.
    /// Batch: Unity.exe -batchmode -projectPath BallBattleUnity -executeMethod BallBattle.EditorTools.TrailerCapture.CaptureAndExit
    ///        -bbTrailerSpec &lt;spec.txt&gt; -bbTrailerOut &lt;dir&gt;
    /// Spec, one shot per line ('#' comments): name|kind|a|b|seed|arena|anchor|occurrence|leadTicks|frames|ticksPerFrame
    ///   kind vs:   a, b = weapon ids (a Versus match on that arena).
    ///   kind boss: a = player weapon, b = boss id (a fresh Run with that boss forced into fight 1; arena from the run).
    ///   anchor:    a SimEventType name (the occurrence-th one), "start" or "end"; the shot starts leadTicks before it.
    ///   ticksPerFrame: 1 = real time at 60 fps, 0.5 = half-speed slow motion.
    /// Must run without -nographics (the sprite renderer needs a device).
    /// </summary>
    public static class TrailerCapture
    {
        const int Scale = 4, W = 270 * Scale, H = 480 * Scale;

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        public static void CaptureAndExit()
        {
            var code = 0;
            try { Capture(Arg("-bbTrailerSpec"), Arg("-bbTrailerOut")); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        sealed class Shot
        {
            public string Name, Kind, A, B, Arena, Anchor;
            public uint Seed;
            public int Occurrence, Lead, Frames;
            public float TicksPerFrame;
        }

        static List<Shot> Parse(string path)
        {
            var shots = new List<Shot>();
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var f = line.Split('|');
                shots.Add(new Shot
                {
                    Name = f[0], Kind = f[1], A = f[2], B = f[3], Seed = uint.Parse(f[4]), Arena = f[5], Anchor = f[6],
                    Occurrence = int.Parse(f[7]), Lead = int.Parse(f[8]), Frames = int.Parse(f[9]),
                    TicksPerFrame = float.Parse(f[10], CultureInfo.InvariantCulture)
                });
            }
            return shots;
        }

        static string BossName(string id) => id switch
        {
            "shellking" => "SHELL KING", "magnet" => "MAGNET", "mimic" => "MIMIC", "warden" => "WARDEN", _ => id.ToUpperInvariant()
        };

        /// <summary>Never use HUNTER (serpent) in the trailer (Phu, 16/10): its art is not final.</summary>
        static void CheckNoHunter(Shot s)
        {
            if (s.A == "serpent" || s.B == "serpent") throw new Exception(s.Name + ": HUNTER is not allowed in the trailer");
        }

        /// <summary>A fresh match for the shot, its boss look and HUD tags (same every call: matches are deterministic).</summary>
        static MatchSim Build(Shot s, out ArenaView.BossLook look, out string tagA, out string tagB)
        {
            CheckNoHunter(s);
            look = ArenaView.BossLook.None; tagA = null; tagB = null;
            if (s.Kind == "vs")
            {
                var m = new MatchSim(ArenaRegistry.Create(s.Arena), s.Seed,
                    new[] { new BallLoadout(BallBattle.Sim.Weapons.WeaponRegistry.Create(s.A)), new BallLoadout(BallBattle.Sim.Weapons.WeaponRegistry.Create(s.B)) });
                m.ApplySpawnHooks();
                return m;
            }
            var p = new ProgressData { RunsPlayed = 1 };
            p.Achievements.AddRange(new[] { UnlockRules.Boss1, UnlockRules.Win1 });
            var seed = s.Seed;
            while (!RunState.StartChoices(seed, UnlockRules.Weapons(p)).Contains(s.A)) seed++;
            var run = RunState.Start(seed, s.A, p);
            if (run.StartPickPending) run.Pick(0);
            run.ApplyForcedBoss(s.B);
            look = RunController.BossLookFor(run);
            tagA = "YOU"; tagB = BossName(s.B);
            return run.CreateMatch();
        }

        /// <summary>Tick at which the shot's anchor happens.</summary>
        static int AnchorTick(Shot s)
        {
            if (s.Anchor == "start") return 0;
            var m = Build(s, out _, out _, out _);
            var wanted = s.Anchor == "end" ? (SimEventType?)null : (SimEventType)Enum.Parse(typeof(SimEventType), s.Anchor);
            var seen = 0;
            while (m.Outcome == MatchOutcome.Ongoing && m.Tick < 60 * 240)
            {
                m.Step();
                if (wanted == null) continue;
                foreach (var e in m.Events)
                    if (e.Type == wanted.Value && ++seen >= s.Occurrence) return m.Tick;
            }
            if (wanted != null) throw new Exception($"{s.Name}: only {seen} {s.Anchor} event(s) in the match");
            return m.Tick;
        }

        static void Capture(string specPath, string outDir)
        {
            var art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            Directory.CreateDirectory(outDir);
            var log = new StringBuilder("shot,frame,event,a,b,weaponA,weaponB,variant,value\n");
            var root = new GameObject("TrailerRoot");
            var camGo = new GameObject("TrailerCam");
            var rt = new RenderTexture(W, H, 24) { filterMode = FilterMode.Point };
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true; cam.orthographicSize = 240f; cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Palette.Background; camGo.transform.position = new Vector3(0, 0, -10);
                var only = Arg("-bbTrailerOnly");   // capture just this shot (one Unity process per shot keeps GPU memory low)
                foreach (var s in Parse(specPath))
                {
                    if (!string.IsNullOrEmpty(only) && s.Name != only) continue;
                    int anchor;
                    try { anchor = AnchorTick(s); }
                    catch (Exception e) { Debug.LogWarning("[Trailer] skipped " + e.Message); continue; }
                    var start = Math.Max(0, anchor - s.Lead);
                    var sim = Build(s, out var look, out var tagA, out var tagB);
                    for (var t = 0; t < start && sim.Outcome == MatchOutcome.Ongoing; t++) sim.Step();
                    var go = new GameObject("Arena_" + s.Name);
                    go.transform.SetParent(root.transform, false);
                    var view = go.AddComponent<ArenaView>();
                    view.Art = art;
                    view.AutoRestart = false;
                    view.Boss = look;
                    var frame = 0;
                    var shotName = s.Name;
                    view.SimEventRaised += e =>
                    {
                        var wa = e.A >= 0 && e.A < view.Sim.Balls.Count ? view.Sim.Balls[e.A].Weapon.Id : "";
                        var wb = e.B >= 0 && e.B < view.Sim.Balls.Count ? view.Sim.Balls[e.B].Weapon.Id : "";
                        log.Append(shotName).Append(',').Append(frame).Append(',').Append(e.Type).Append(',').Append(e.A).Append(',').Append(e.B)
                           .Append(',').Append(wa).Append(',').Append(wb).Append(',').Append(e.Variant).Append(',')
                           .Append(e.Value.ToFloat().ToString("0.###", CultureInfo.InvariantCulture)).Append('\n');
                    };
                    view.StartMatch(sim, tagA, tagB);
                    var dir = Path.Combine(outDir, s.Name);
                    Directory.CreateDirectory(dir);
                    for (frame = 0; frame < s.Frames; frame++)
                    {
                        view.Advance(s.TicksPerFrame / 60f);
                        cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
                        File.WriteAllBytes(Path.Combine(dir, $"{frame:0000}.png"), tex.EncodeToPNG());
                        RenderTexture.active = null; cam.targetTexture = null;
                        GL.Flush();
                        if (frame % 60 == 59) { Resources.UnloadUnusedAssets(); GC.Collect(); }
                    }
                    Debug.Log($"[Trailer] {s.Name}: anchor tick {anchor}, start {start}, {s.Frames} frames");
                    Object.DestroyImmediate(go);
                }
                File.WriteAllText(Path.Combine(outDir, string.IsNullOrEmpty(only) ? "events.csv" : $"events-{only}.csv"), log.ToString());
            }
            finally
            {
                Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
                Object.DestroyImmediate(camGo); Object.DestroyImmediate(root);
            }
        }
    }
}
