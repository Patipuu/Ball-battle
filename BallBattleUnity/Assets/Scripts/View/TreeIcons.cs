using System.Collections.Generic;
using BallBattle.Sim.Meta;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// BB-12h: 16x16 pixel icons of the skill-tree nodes, made at runtime (white body, light highlight, dark outline) and
    /// tinted by the node state through SpriteRenderer.color. Heart = VITALITY, sword = POWER, lightning = INSTINCT,
    /// star = OPENER, gem = skill level, crown = FINALE.
    /// </summary>
    public static class TreeIcons
    {
        public const int Size = 16;
        public enum Kind { Heart, Sword, Bolt, Star, Gem, Crown }

        static readonly Color32 Body = new Color32(205, 205, 210, 255), High = new Color32(255, 255, 255, 255), Line = new Color32(28, 24, 40, 255);

        static readonly Dictionary<Kind, string[]> Art = new Dictionary<Kind, string[]>
        {
            { Kind.Heart, new[] {
                "..oooo....oooo..",
                ".oWWWWo..oWWWWo.",
                "oWHHWWWooWWWWWWo",
                "oWHWWWWWWWWWWWWo",
                "oWWWWWWWWWWWWWWo",
                "oWWWWWWWWWWWWWWo",
                ".oWWWWWWWWWWWWo.",
                "..oWWWWWWWWWWo..",
                "...oWWWWWWWWo...",
                "....oWWWWWWo....",
                ".....oWWWWo.....",
                "......oWWo......",
                ".......oo.......",
            } },
            { Kind.Sword, new[] {
                "............ooo.",
                "...........oHWo.",
                "..........oHWWo.",
                ".........oHWWo..",
                "........oHWWo...",
                ".......oHWWo....",
                "..oo..oHWWo.....",
                "..oWooHWWo......",
                "...oWWWWo.......",
                "....oWWo........",
                "...oWooWo.......",
                "..oWo..oWo......",
                ".oWo....oo......",
                ".oo.............",
            } },
            { Kind.Bolt, new[] {
                ".......ooooo....",
                "......oHWWWo....",
                ".....oHWWWo.....",
                "....oHWWWo......",
                "...oHWWWo.......",
                "..oHWWWWWWWWo...",
                "..ooooooWWWWo...",
                ".......oWWWo....",
                "......oWWWo.....",
                ".....oWWWo......",
                "....oWWWo.......",
                "...oWWo.........",
                "..oWo...........",
                "..oo............",
            } },
            { Kind.Star, new[] {
                ".......oo.......",
                "......oHWo......",
                "......oHWo......",
                ".....oHWWWo.....",
                "oooooHWWWWWooooo",
                "oHHHHWWWWWWWWWWo",
                ".oWWWWWWWWWWWWo.",
                "..oWWWWWWWWWWo..",
                "...oWWWWWWWWo...",
                "...oWWWWWWWWo...",
                "..oWWWWooWWWWo..",
                "..oWWWo..oWWWo..",
                ".oWWo......oWWo.",
                ".ooo........ooo.",
            } },
            { Kind.Gem, new[] {
                "....oooooooo....",
                "...oHHWWWWWWo...",
                "..oHHWWWWWWWWo..",
                ".oooooooooooooo.",
                ".oHWWWWWWWWWWWo.",
                "..oHWWWWWWWWWo..",
                "...oHWWWWWWWo...",
                "....oWWWWWWo....",
                ".....oWWWWo.....",
                "......oWWo......",
                ".......oo.......",
            } },
            { Kind.Crown, new[] {
                ".o.....oo.....o.",
                "oHo...oHWo...oHo",
                "oWWo..oWWo..oWWo",
                "oWWWooWWWWooWWWo",
                "oWWWWWWWWWWWWWWo",
                "oHWWWWWHWWWWWWHo",
                "oWWWWWWWWWWWWWWo",
                "oWWWWWWWWWWWWWWo",
                ".oooooooooooooo.",
                ".oHHHHHHHHHHHHo.",
                ".oooooooooooooo.",
            } },
        };

        static readonly Dictionary<Kind, Sprite> Cache = new Dictionary<Kind, Sprite>();
        static readonly Dictionary<string, Sprite> BallCache = new Dictionary<string, Sprite>();

        /// <summary>Key of a ball's own icon in TreeIconArt: INSTINCT, OPENER, SKILLA/SKILLB, FINA/FINB (null for VITALITY/POWER).</summary>
        public static string BallKey(string weaponId, TreeNode n, char branch)
        {
            switch (n)
            {
                case TreeNode.Instinct: return weaponId + "/INSTINCT";
                case TreeNode.Opener: return weaponId + "/OPENER";
                case TreeNode.SkillA: return weaponId + "/SKILLA";
                case TreeNode.SkillB: return weaponId + "/SKILLB";
                case TreeNode.Finale: return weaponId + (branch == 'B' ? "/FINB" : "/FINA");
                default: return null;
            }
        }

        /// <summary>BB-12i: the ball's own icon for a node, or the shared one (VITALITY, POWER, a ball without art).</summary>
        public static Sprite For(string weaponId, TreeNode n, char branch)
        {
            var key = BallKey(weaponId, n, branch);
            if (key == null || !TreeIconArt.Rows.TryGetValue(key, out var rows)) return Get(KindOf(n));
            if (BallCache.TryGetValue(key, out var s) && s != null) return s;
            s = Make(rows, "tree_icon_" + key.Replace('/', '_'));
            BallCache[key] = s;
            return s;
        }

        public static Kind KindOf(TreeNode n)
        {
            switch (n)
            {
                case TreeNode.Vitality: return Kind.Heart;
                case TreeNode.Power: return Kind.Sword;
                case TreeNode.Instinct: return Kind.Bolt;
                case TreeNode.Opener: return Kind.Star;
                case TreeNode.SkillA:
                case TreeNode.SkillB: return Kind.Gem;
                default: return Kind.Crown;
            }
        }

        /// <summary>The icon (pivot at its centre, 1 unit = 1 px, so scale 2 draws it 32x32).</summary>
        public static Sprite Get(Kind k)
        {
            if (Cache.TryGetValue(k, out var s) && s != null) return s;
            s = Make(Art[k], "tree_icon_" + k);
            Cache[k] = s;
            return s;
        }

        static Sprite Make(string[] rows, string name)
        {
            var t = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[Size * Size];
            var top = (Size - rows.Length) / 2;   // centre the art vertically
            for (var r = 0; r < rows.Length; r++)
                for (var c = 0; c < Size && c < rows[r].Length; c++)
                {
                    var ch = rows[r][c];
                    if (ch == '.') continue;
                    var y = Size - 1 - (top + r);   // texture rows go up
                    px[y * Size + c] = ch == 'o' ? Line : ch == 'H' ? High : Body;
                }
            t.SetPixels32(px);
            return ProceduralSprites.Finish(t, name, new Vector2(0.5f, 0.5f));
        }

        /// <summary>Rows of every icon (tests: all are 16 wide and at most 16 tall).</summary>
        public static IEnumerable<string[]> AllRows => Art.Values;
    }
}
