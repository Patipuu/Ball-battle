using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// World-space HUD in the free bands above and below the square arena: ball 0 on top, ball 1 at the bottom.
    /// Each side: weapon name (2x), HP number, HP bar with a lagging "lost" segment, growing stat ("DMG 7").
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        public const int Order = 40;
        const int BarWidth = 230;
        const int BarHeight = 6;
        const float LostLagPerSecond = 0.6f;   // fraction of max HP the red segment drains per second

        sealed class Side
        {
            public PixelText Name, Hp, Stat;
            public SpriteRenderer BarBack, BarLost, BarFill;
            public float LostFraction = 1f;
            public string WeaponId;
            public Color32 Color;
            // Last drawn values: strings are rebuilt only when these change (no per-frame garbage).
            public int ShownHp = int.MinValue;
            public float ShownStat = float.NaN;
        }

        readonly Side[] sides = new Side[2];

        public static HudView Create(Transform parent, ArtLibrary art)
        {
            var go = new GameObject("Hud");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<HudView>();
            // Top band: arena top is +115; bottom band mirrors it.
            v.sides[0] = v.BuildSide(art, top: true);
            v.sides[1] = v.BuildSide(art, top: false);
            return v;
        }

        Side BuildSide(ArtLibrary art, bool top)
        {
            var s = new Side();
            float y(float fromArenaEdge) => top ? 115f + fromArenaEdge : -115f - fromArenaEdge - 10f;
            const float left = -BarWidth / 2f;
            const float right = BarWidth / 2f;
            var nameY = top ? y(78) : y(66);
            var barY = top ? y(62) : y(80);
            var statY = top ? y(48) : y(94);

            s.Name = PixelText.Create(transform, top ? "NameTop" : "NameBottom", art.Font, new Vector2(left, nameY), PixelText.Align.Left, 2, Order);
            s.Hp = PixelText.Create(transform, top ? "HpTop" : "HpBottom", art.Font, new Vector2(right, nameY), PixelText.Align.Right, 2, Order);
            s.Stat = PixelText.Create(transform, top ? "StatTop" : "StatBottom", art.Font, new Vector2(left, statY), PixelText.Align.Left, 1, Order);
            s.BarBack = Bar(art.Pixel, Palette.HpBack, Order, left, barY, BarWidth);
            s.BarLost = Bar(art.Pixel, Palette.HpLost, Order + 1, left, barY, BarWidth);
            s.BarFill = Bar(art.Pixel, Palette.Text, Order + 2, left, barY, BarWidth);
            return s;
        }

        SpriteRenderer Bar(Sprite pixel, Color32 color, int order, float left, float y, float width)
        {
            var go = new GameObject("Bar");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = pixel;
            sr.color = color;
            sr.sortingOrder = order;
            SetBar(sr, left, Mathf.Round(y), width);
            return sr;
        }

        static void SetBar(SpriteRenderer sr, float left, float y, float width)
        {
            width = Mathf.Max(0f, Mathf.Round(width));
            sr.enabled = width > 0f;
            sr.transform.localPosition = new Vector3(left + width * 0.5f, y + BarHeight * 0.5f, 0f);
            sr.transform.localScale = new Vector3(width, BarHeight, 1f);
        }

        /// <summary>Show a side's weapon; an optional tag (YOU / BOSS) goes before its name.</summary>
        public void Bind(int side, string weaponId, string tag = null)
        {
            var s = sides[side];
            s.WeaponId = weaponId;
            s.Color = Palette.Look(weaponId).Body;
            s.LostFraction = 1f;
            s.ShownHp = int.MinValue;
            s.ShownStat = float.NaN;
            s.BarFill.color = s.Color;
            var name = WeaponRegistry.Get(weaponId).DisplayName;
            s.Name.Set(string.IsNullOrEmpty(tag) ? name : tag + " " + name, s.Color);
        }

        public void Render(MatchSim sim, float deltaTime)
        {
            for (var i = 0; i < 2 && i < sim.Balls.Count; i++)
            {
                var s = sides[i];
                var ball = sim.Balls[i];
                var frac = Mathf.Clamp01(ball.HpFraction);
                s.LostFraction = Mathf.Max(frac, s.LostFraction - LostLagPerSecond * deltaTime);

                var left = -BarWidth / 2f;
                var y = s.BarBack.transform.localPosition.y - BarHeight * 0.5f;
                SetBar(s.BarLost, left, y, BarWidth * s.LostFraction);
                SetBar(s.BarFill, left, y, BarWidth * frac);

                var hp = Mathf.CeilToInt(ball.Hp);
                if (hp != s.ShownHp)
                {
                    s.ShownHp = hp;
                    s.Hp.Set(hp.ToString(), Palette.Text);
                }

                var w = ball.Weapon;
                if (!w.StatValue.Equals(s.ShownStat))
                {
                    s.ShownStat = w.StatValue;
                    s.Stat.Set(w.StatLabel + " " + w.StatText, Palette.TextDim);
                }
            }
        }
    }
}
