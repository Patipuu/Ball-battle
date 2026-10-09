using System;
using System.Collections.Generic;

namespace BallBattle.Sim
{
    /// <summary>The seven arena variants. Classic is the plain Step 1 arena (a default MatchConfig).</summary>
    public static class ArenaRegistry
    {
        public const string Classic = "classic";
        public const string Pillar = "pillar";
        public const string Bumpers = "bumpers";
        public const string SpikeWalls = "spike-walls";
        public const string LowGravity = "low-gravity";
        public const string Tight = "tight";
        public const string Wide = "wide";

        public readonly struct Entry
        {
            public readonly string Id;
            public readonly string DisplayName;
            public Entry(string id, string name) { Id = id; DisplayName = name; }
        }

        static readonly Entry[] all =
        {
            new Entry(Classic, "CLASSIC"),
            new Entry(Pillar, "PILLAR"),
            new Entry(Bumpers, "BUMPERS"),
            new Entry(SpikeWalls, "SPIKES"),
            new Entry(LowGravity, "LOW GRAV"),
            new Entry(Tight, "TIGHT"),
            new Entry(Wide, "WIDE"),
        };

        public static IReadOnlyList<Entry> All => all;

        public static Entry Get(string id)
        {
            foreach (var e in all) if (e.Id == id) return e;
            throw new ArgumentException($"Unknown arena '{id}'", nameof(id));
        }

        /// <summary>Arena for a Run fight, chosen by the fight seed (Classic comes up too).</summary>
        public static string ForSeed(uint seed) => all[(int)(seed % (uint)all.Length)].Id;

        /// <summary>A fresh MatchConfig for the arena; null/empty id = Classic.</summary>
        public static MatchConfig Create(string id)
        {
            var c = new MatchConfig();
            switch (string.IsNullOrEmpty(id) ? Classic : id)
            {
                case Classic: break;
                case Pillar:
                    c.Layout = new ArenaLayout(new[] { Obstacle.Circle(new Vec2(0f, 0f), ArenaTuning.PillarRadius) });
                    break;
                case Bumpers:
                    var o = ArenaTuning.BumperOffset;
                    var rad = ArenaTuning.BumperRadius;
                    var boost = ArenaTuning.BumperBoost;
                    c.Layout = new ArenaLayout(new[]
                    {
                        Obstacle.Circle(new Vec2(-o, -o), rad, 0f, boost), Obstacle.Circle(new Vec2(o, -o), rad, 0f, boost),
                        Obstacle.Circle(new Vec2(-o, o), rad, 0f, boost), Obstacle.Circle(new Vec2(o, o), rad, 0f, boost),
                    });
                    break;
                case SpikeWalls:
                    c.Layout = new ArenaLayout(null, ArenaTuning.SpikeWallDamage);
                    break;
                case LowGravity:
                    c.Gravity = ArenaTuning.LowGravity;
                    break;
                case Tight:
                    c.ArenaWidth = c.ArenaHeight = ArenaTuning.TightSize;
                    c.MinArenaWidth = c.MinArenaHeight = ArenaTuning.TightMinSize;
                    break;
                case Wide:
                    c.ArenaWidth = ArenaTuning.WideWidth;
                    c.ArenaHeight = ArenaTuning.WideHeight;
                    break;
                default:
                    throw new ArgumentException($"Unknown arena '{id}'", nameof(id));
            }
            return c;
        }
    }
}