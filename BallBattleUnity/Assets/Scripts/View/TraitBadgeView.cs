using BallBattle.Sim;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Trait badges on the HUD stat row: a small colored box with a 2-character code per trait, gold rim at level 2.
    /// Built once per match (traits never change during a fight).
    /// </summary>
    public sealed class TraitBadgeView : MonoBehaviour
    {
        const int Order = HudView.Order + 3;
        const int BoxWidth = 13;
        const int BoxHeight = 9;
        const int Spacing = 2;
        /// <summary>Trait icons (art v1) are 14x14.</summary>
        public const int IconSize = 14;
        static readonly Color32 Gold = new Color32(250, 210, 70, 255);

        struct Style
        {
            public string Id, Code;
            public Color32 Color;
        }

        static readonly Style[] Styles =
        {
            new Style { Id = "heavy", Code = "HV", Color = new Color32(120, 120, 150, 255) },
            new Style { Id = "vampire", Code = "VM", Color = new Color32(170, 50, 90, 255) },
            new Style { Id = "spiky", Code = "SP", Color = new Color32(190, 90, 50, 255) },
            new Style { Id = "thorns", Code = "TH", Color = new Color32(70, 140, 70, 255) },
            new Style { Id = "second-wind", Code = "2W", Color = new Color32(60, 150, 170, 255) },
            new Style { Id = "glass-cannon", Code = "GC", Color = new Color32(170, 80, 170, 255) },
            new Style { Id = "parry-master", Code = "PM", Color = new Color32(60, 100, 190, 255) },
            new Style { Id = "bubble", Code = "BB", Color = new Color32(90, 160, 210, 255) },
            new Style { Id = "twin-blade", Code = "TB", Color = new Color32(180, 60, 60, 255) },
            new Style { Id = "poison-tip", Code = "PT", Color = new Color32(80, 160, 60, 255) },
        };

        /// <summary>A spent badge (once-per-fight trait already used) shows at this brightness, crossed out.</summary>
        public const float SpentBrightness = 0.4f;

        sealed class Badge
        {
            public int Side;
            public string Id;
            public SpriteRenderer Box, Rim;
            public PixelText Code;
            public Color32 BoxColor, CodeColor;
            public GameObject Slash;
            public bool Spent;
        }

        readonly System.Collections.Generic.List<Badge> badges = new System.Collections.Generic.List<Badge>();
        ArtLibrary art;
        Transform root;

        static void DestroyNow(GameObject go)

        {

            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);

        }


        public static TraitBadgeView Create(Transform parent, ArtLibrary art)
        {
            var go = new GameObject("TraitBadges");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<TraitBadgeView>();
            v.art = art;
            return v;
        }

        public static bool HasStyle(string id)
        {
            foreach (var s in Styles) if (s.Id == id) return true;
            return false;
        }

        static Style StyleFor(string id)
        {
            foreach (var s in Styles) if (s.Id == id) return s;
            return new Style { Id = id, Code = id.Length >= 2 ? id.Substring(0, 2).ToUpperInvariant() : "??", Color = Palette.Wall };
        }

        /// <summary>
        /// Mark a once-per-fight trait as used on side <paramref name="side"/>: 40% brightness and a diagonal slash.
        /// Badges are rebuilt for every match, so the next fight starts normal again.
        /// </summary>
        public void SetSpent(int side, string traitId, bool spent)
        {
            foreach (var b in badges)
            {
                if (b.Side != side || b.Id != traitId || b.Spent == spent) continue;
                b.Spent = spent;
                var k = spent ? SpentBrightness : 1f;
                b.Box.color = Dim(b.BoxColor, k);
                if (b.Rim != null) b.Rim.color = Dim(Gold, k);
                b.Code?.Set(b.Code.Text, Dim(b.CodeColor, k));
                b.Slash.SetActive(spent);
            }
        }

        public bool IsSpent(int side, string traitId)
        {
            foreach (var b in badges) if (b.Side == side && b.Id == traitId) return b.Spent;
            return false;
        }

        /// <summary>Trait color (badge fallback, card stripe).</summary>
        public static Color32 ColorOf(string traitId) => StyleFor(traitId).Color;

        /// <summary>Icon sprite shown for a badge, or null (tests).</summary>
        public Sprite IconOf(int side, string traitId)
        {
            foreach (var b in badges) if (b.Side == side && b.Id == traitId) return b.Box.sprite == art.Pixel ? null : b.Box.sprite;
            return null;
        }

        /// <summary>Badge box color now (tests).</summary>
        public Color32? BoxColor(int side, string traitId)
        {
            foreach (var b in badges) if (b.Side == side && b.Id == traitId) return (Color32)b.Box.color;
            return null;
        }

        static Color32 Dim(Color32 c, float k) => new Color32((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), c.a);

        /// <summary>Rebuild the badges for a new match. Ball 0 is the top side, ball 1 the bottom.</summary>
        /// <summary>HUD stat row height of a side (ball 0 top, ball 1 bottom).</summary>
        static float BaseY(int side) => side == 0 ? 163f : -219f;

        readonly int[] slotsUsed = new int[2];

        /// <summary>Badge ids of the stat cards (DAMAGE, SPEED CAP, MAX HP) on the HUD.</summary>
        public const string DamageBadge = "card-damage";
        public const string SpeedCapBadge = "card-speed-cap";
        public const string MaxHpBadge = "card-max-hp";

        /// <summary>
        /// Stat card badges after the trait icons of <paramref name="side"/>: one per card type taken (DAMAGE, SPEED CAP,
        /// MAX HP), drawn with the same 14x14 icons as the cards. Call after Bind (Run mode, from the build).
        /// </summary>
        public void SetCardBadges(int side, bool damage, bool speedCap, bool maxHp)
        {
            if (root == null || side < 0 || side > 1) return;
            var cards = new[] { (damage, DamageBadge, art.CardDamage), (speedCap, SpeedCapBadge, art.CardSpeedCap), (maxHp, MaxHpBadge, art.CardMaxHp) };
            foreach (var (on, id, icon) in cards)
            {
                if (!on || icon == null || IconOf(side, id) != null) continue;
                var slot = slotsUsed[side]++;
                var right = 115f - slot * (IconSize + Spacing);
                var left = right - IconSize;
                var bottom = BaseY(side) - 2f;
                var go = new GameObject("Icon_" + id);
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(left + IconSize * 0.5f, bottom + IconSize * 0.5f, 0f);
                var box = go.AddComponent<SpriteRenderer>();
                box.sprite = icon;
                box.sortingOrder = Order + 1;
                var slash = new GameObject("Slash");   // stat cards are never "spent": empty, kept for SetSpent
                slash.transform.SetParent(root, false);
                slash.SetActive(false);
                badges.Add(new Badge { Side = side, Id = id, Box = box, BoxColor = new Color32(255, 255, 255, 255), CodeColor = Palette.Text, Slash = slash });
            }
        }

        public void Bind(MatchSim sim)
        {
            if (root != null) DestroyNow(root.gameObject);
            badges.Clear();
            slotsUsed[0] = slotsUsed[1] = 0;
            root = new GameObject("Badges").transform;
            root.SetParent(transform, false);
            for (var side = 0; side < 2 && side < sim.Balls.Count; side++)
            {
                var traits = sim.Balls[side].Traits;
                var baseY = BaseY(side);
                slotsUsed[side] = traits.Length;
                for (var i = 0; i < traits.Length; i++)
                {
                    var st = StyleFor(traits[i].Id);
                    var icon = art.TraitIcon(traits[i].Id);
                    var w = icon != null ? IconSize : BoxWidth;    // art v1: 14x14 icon; drawn badge only as a fallback
                    var h = icon != null ? IconSize : BoxHeight;
                    var right = 115f - i * (w + Spacing);
                    var left = right - w;
                    var bottom = baseY - 2f;
                    SpriteRenderer rim = null;
                    if (traits[i].Level >= 2)
                    {
                        rim = PixelRect.Create(root, "Rim", art.Pixel, Gold, Order);
                        PixelRect.Set(rim, left - 1f, bottom - 1f, right + 1f, bottom + h + 1f);
                    }
                    SpriteRenderer box;
                    PixelText txt = null;
                    Color32 boxColor;
                    if (icon != null)
                    {
                        var go = new GameObject("Icon_" + traits[i].Id);
                        go.transform.SetParent(root, false);
                        go.transform.localPosition = new Vector3(left + w * 0.5f, bottom + h * 0.5f, 0f);
                        box = go.AddComponent<SpriteRenderer>();
                        box.sprite = icon;
                        box.sortingOrder = Order + 1;
                        boxColor = new Color32(255, 255, 255, 255);
                        box.color = boxColor;
                    }
                    else
                    {
                        box = PixelRect.Create(root, "Box", art.Pixel, st.Color, Order + 1);
                        PixelRect.Set(box, left, bottom, right, bottom + h);
                        boxColor = st.Color;
                        txt = PixelText.Create(root, "Code", art.Font, new Vector2((left + right) * 0.5f, baseY), PixelText.Align.Center, 1, Order + 2);
                        txt.Set(st.Code, Palette.Text);
                    }

                    // Cross-out for a spent badge: a 1 px diagonal from the bottom-left to the top-right corner (hidden until spent).
                    var slash = new GameObject("Slash");
                    slash.transform.SetParent(root, false);
                    for (var k = 0; k < w; k++)
                    {
                        var y = Mathf.Floor(k * (h - 1) / (float)(w - 1));
                        var px = PixelRect.Create(slash.transform, "S", art.Pixel, Palette.Text, Order + 3);
                        PixelRect.Set(px, left + k, bottom + y, left + k + 1, bottom + y + 1);
                    }
                    slash.SetActive(false);
                    badges.Add(new Badge { Side = side, Id = traits[i].Id, Box = box, Rim = rim, Code = txt, BoxColor = boxColor, CodeColor = Palette.Text, Slash = slash });
                }
            }
        }
    }
}