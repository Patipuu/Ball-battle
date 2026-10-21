using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// World-space HUD in the free bands above and below the square arena. The player's ball (<see cref="BottomBall"/>, set from
    /// ArenaView.MarkedBall) sits at the bottom near the thumb, the opponent on top; with no player (local versus) ball 0 is on top.
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
            public PixelText Skill;
            public SpriteRenderer SkillBack, SkillFill;
            /// <summary>Player marker arrow before "YOU" (Run mode); hidden otherwise.</summary>
            public SpriteRenderer Arrow;
            public Vector3 NameHome;
            public SpriteRenderer BarBack, BarLost, BarFill;
            public float LostFraction = 1f;
            /// <summary>Just bound to a new match: the red "lost" segment starts at the current HP, not at full.</summary>
            public bool Fresh = true;
            public string WeaponId;
            public Color32 Color;
            // Last drawn values: strings are rebuilt only when these change (no per-frame garbage).
            public int ShownHp = int.MinValue;
            public float ShownStat = float.NaN;
        }

        /// <summary>Slot 0 = top band, slot 1 = bottom band.</summary>
        readonly Side[] slots = new Side[2];

        /// <summary>Ball shown in the bottom band (the player's); the other ball goes on top.</summary>
        public int BottomBall { get; set; } = 1;

        Side For(int ball) => slots[ball == BottomBall ? 1 : 0];

        /// <summary>True when ball <paramref name="ball"/> is drawn in the top band (tests).</summary>
        public bool IsTop(int ball) => ball != BottomBall;

        /// <summary>HUD tag of the player's ball in Run mode; it gets the marker arrow in front.</summary>
        public const string PlayerTag = "YOU";
        static int ArrowWidth => ProceduralSprites.ArrowRows[0].Length;
        const int ArrowGap = 2;
        /// <summary>Name shown on the player's side instead of YOU (the signed-in account; tests swap it).</summary>
        public static System.Func<string> PlayerName = () => AccountSession.Name;
        /// <summary>Width kept free for the x2 HP number at the right end of the name row ("1000" plus a gap).</summary>
        const int HpRoom = 54;

        /// <summary>True when side <paramref name="side"/> shows the player arrow (tests).</summary>
        public bool ShowsPlayerArrow(int side) => For(side).Arrow.enabled;

        /// <summary>Arrow and name of a side (tests).</summary>
        public SpriteRenderer PlayerArrow(int side) => For(side).Arrow;
        public PixelText NameText(int side) => For(side).Name;
        public string HpText(int side) => For(side).Hp.Text;
        /// <summary>Width of the red "lost" segment as a fraction of the bar (tests).</summary>
        public float LostFraction(int side) => For(side).LostFraction;

        public static HudView Create(Transform parent, ArtLibrary art)
        {
            var go = new GameObject("Hud");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<HudView>();
            // Top band: arena top is +115; bottom band mirrors it.
            v.slots[0] = v.BuildSide(art, top: true);
            v.slots[1] = v.BuildSide(art, top: false);
            return v;
        }

        Side BuildSide(ArtLibrary art, bool top)
        {
            var s = new Side();
            float y(float fromArenaEdge) => top ? 115f + fromArenaEdge : -115f - fromArenaEdge - 10f;
            const float left = -BarWidth / 2f;
            const float right = BarWidth / 2f;
            var nameY = top ? y(78) : -168f;
            var barY = top ? y(62) : -186f;
            var statY = top ? y(48) : -204f;

            s.Name = PixelText.Create(transform, top ? "NameTop" : "NameBottom", art.Font, new Vector2(left, nameY), PixelText.Align.Left, 2, Order);
            s.Hp = PixelText.Create(transform, top ? "HpTop" : "HpBottom", art.Font, new Vector2(right, nameY), PixelText.Align.Right, 2, Order);
            s.Stat = PixelText.Create(transform, top ? "StatTop" : "StatBottom", art.Font, new Vector2(left, statY), PixelText.Align.Left, 1, Order);
            s.Skill = PixelText.Create(transform, top ? "SkillTop" : "SkillBottom", art.Font, new Vector2(left, statY - 13), PixelText.Align.Left, 1, Order);
            s.SkillBack = Bar(art.Pixel, Palette.Wall, Order, left, statY - 16, BarWidth);
            s.SkillFill = Bar(art.Pixel, Palette.TextDim, Order + 1, left, statY - 16, BarWidth);
            s.SkillBack.transform.localScale = new Vector3(BarWidth, 1, 1);
            s.SkillFill.transform.localScale = new Vector3(BarWidth, 1, 1);
            s.SkillBack.transform.localPosition = new Vector3(0, statY - 15.5f, 0);
            s.SkillFill.transform.localPosition = new Vector3(0, statY - 15.5f, 0);
            s.NameHome = s.Name.transform.localPosition;
            var arrow = new GameObject(top ? "YouArrowTop" : "YouArrowBottom");
            arrow.transform.SetParent(transform, false);
            // Same arrow as over the ball; its 8 px sit inside the 10 px name line, tip column 5 px from the left edge.
            arrow.transform.localPosition = new Vector3(left + ProceduralSprites.ArrowTipColumn, Mathf.Round(nameY + 1f), 0f);
            s.Arrow = arrow.AddComponent<SpriteRenderer>();
            s.Arrow.sprite = ProceduralSprites.DownArrow;
            s.Arrow.sortingOrder = Order + 1;
            s.Arrow.enabled = false;
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
            var s = For(side);
            s.WeaponId = weaponId;
            s.Color = weaponId == BallBattle.Sim.Boss.BossCoreRule.WeaponId ? Palette.WallWarning : Palette.Look(weaponId).Body;
            s.LostFraction = 1f;
            s.Fresh = true;
            s.ShownHp = int.MinValue;
            s.ShownStat = float.NaN;
            s.BarFill.color = s.Color;
            var name = weaponId == BallBattle.Sim.Boss.BossCoreRule.WeaponId ? "" : WeaponNames.Of(weaponId);
            var you = tag == PlayerTag;
            s.Arrow.enabled = you;
            var shownTag = StringTable.Current == Lang.Vi && tag == "FOE" ? "ĐỊCH" : UiVietnamese.Translate(tag);
            // 21/10: the player's side shows their account name (then the ball) instead of YOU / CAO THU.
            var account = PlayerName?.Invoke();
            if (you && !string.IsNullOrEmpty(account)) shownTag = account;
            var text = string.IsNullOrEmpty(tag) ? name : (shownTag + " " + name).Trim();
            // x2 unless it would run into the HP number on the right (a 12-letter name + SPARKLER): then x1, centred on the x2 row.
            var room = BarWidth - (you ? ArrowWidth + ArrowGap : 0) - HpRoom;
            var small = PixelFont.MeasureWidth(PixelFont.Normalize(UiVietnamese.Translate(text))) * 2 > room;
            s.Name.SetScale(small ? 1 : 2);
            s.Name.transform.localPosition = (you ? s.NameHome + new Vector3(ArrowWidth + ArrowGap, 0f, 0f) : s.NameHome) +
                (small ? new Vector3(0f, Mathf.Floor(PixelFont.CellHeight / 2f), 0f) : Vector3.zero);
            s.Name.Set(text, s.Color);
        }

        public void Render(MatchSim sim, float deltaTime)
        {
            for (var i = 0; i < 2 && i < sim.Balls.Count; i++)
            {
                var s = For(i);
                var ball = sim.Balls[i];
                var frac = Mathf.Clamp01(ball.HpFraction.ToFloat());
                if (s.Fresh) { s.LostFraction = frac; s.Fresh = false; }   // a fight that starts hurt shows no red drain
                s.LostFraction = Mathf.Max(frac, s.LostFraction - LostLagPerSecond * deltaTime);

                var left = -BarWidth / 2f;
                var y = s.BarBack.transform.localPosition.y - BarHeight * 0.5f;
                SetBar(s.BarLost, left, y, BarWidth * s.LostFraction);
                SetBar(s.BarFill, left, y, BarWidth * frac);

                var hp = BallBattle.View.HpText.Of(ball.Hp.ToFloat());
                if (hp != s.ShownHp)
                {
                    s.ShownHp = hp;
                    s.Hp.Set(hp.ToString(), Palette.Text);
                }

                var w = ball.Weapon;
                var skill = ball.Skill;
                s.Skill.Set(skill == null ? "" : SkillText.Line(w.Id, skill.Level, skill.Branch), Palette.TextDim);
                s.SkillBack.enabled = skill != null;
                var cooldownWidth = skill == null ? 0 : Mathf.Round(BarWidth * skill.ReadyFraction.ToFloat());
                s.SkillFill.enabled = skill != null && cooldownWidth > 0;
                s.SkillFill.transform.localScale = new Vector3(cooldownWidth, 1, 1);
                s.SkillFill.transform.localPosition = new Vector3(left + cooldownWidth / 2, s.SkillBack.transform.localPosition.y, 0);
                if (!w.StatValue.Equals(s.ShownStat))
                {
                    s.ShownStat = w.StatValue.ToFloat();
                    s.Stat.Set(UiVietnamese.Translate(w.StatLabel) + " " + w.StatText, Palette.TextDim);
                }
            }
        }
    }
}
