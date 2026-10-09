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

        ArtLibrary art;
        Transform root;

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

        /// <summary>Rebuild the badges for a new match. Ball 0 is the top side, ball 1 the bottom.</summary>
        public void Bind(MatchSim sim)
        {
            if (root != null) Destroy(root.gameObject);
            root = new GameObject("Badges").transform;
            root.SetParent(transform, false);
            for (var side = 0; side < 2 && side < sim.Balls.Count; side++)
            {
                var traits = sim.Balls[side].Traits;
                var baseY = side == 0 ? 163f : -219f;   // HudView stat row
                for (var i = 0; i < traits.Length; i++)
                {
                    var st = StyleFor(traits[i].Id);
                    var right = 115f - i * (BoxWidth + Spacing);
                    var left = right - BoxWidth;
                    var bottom = baseY - 2f;
                    if (traits[i].Level >= 2)
                    {
                        var rim = PixelRect.Create(root, "Rim", art.Pixel, Gold, Order);
                        PixelRect.Set(rim, left - 1f, bottom - 1f, right + 1f, bottom + BoxHeight + 1f);
                    }
                    var box = PixelRect.Create(root, "Box", art.Pixel, st.Color, Order + 1);
                    PixelRect.Set(box, left, bottom, right, bottom + BoxHeight);
                    var txt = PixelText.Create(root, "Code", art.Font, new Vector2((left + right) * 0.5f, baseY), PixelText.Align.Center, 1, Order + 2);
                    txt.Set(st.Code, Palette.Text);
                }
            }
        }
    }
}