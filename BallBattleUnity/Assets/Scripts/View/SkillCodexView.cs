using System;
using BallBattle.Sim.Skills;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// CODEX (SỔ TAY): one page per ball, laid out like a champion page in LoL / Liên Quân. Top: a strip of all ten balls (tap one to
    /// jump to it). Then the ball: epithet over its name, sprite, role and four stat bars; a three-line intro (CodexText); the skill
    /// (icon, name, what the ball does, branch A and B); a PLAY TIP and a COUNTER TIP. PREV / NEXT, then BACK.
    /// </summary>
    public sealed class SkillCodexView : MonoBehaviour
    {
        public event Action Closed;

        const int Left = -121;   // text column (scale 1 lines are at most 242 px)
        const int StripY = 188, StripCell = 24;   // 24x24 tap targets, ball drawn at 16 px
        static readonly Color32 Header = Palette.WallWarning;

        PixelText page, title, ballName, skillHead, tipHead;
        readonly PixelText[] intro = new PixelText[6], skill = new PixelText[3], tip = new PixelText[2];
        WeaponProfileView profile;
        SpriteRenderer ballSprite, skillIcon;
        readonly PixelButton[] strip = new PixelButton[SkillDefinition.All.Length];
        public SpriteRenderer SkillIcon => skillIcon;
        /// <summary>The skill name next to its icon (tests).</summary>
        public PixelText SkillHeader => skillHead;
        /// <summary>Page shown (index into SkillDefinition.All).</summary>
        public int Index => index;
        int index;
        ArtLibrary art;

        public static readonly Rect PrevArea = new Rect(-120, -160, 90, 24), NextArea = new Rect(30, -160, 90, 24);
        const int Line = 13, Gap = 5, TextTop = 57;

        public static SkillCodexView Create(Transform parent, ArtLibrary art)
        {
            var root = RunUi.Root(parent, "Codex", art);
            var view = root.gameObject.AddComponent<SkillCodexView>();
            view.art = art;
            RunUi.Text(root, art, 0, 214, 2, initial: "CODEX");                                    // 214..240

            // Ball strip: all balls, tap to jump.
            var n = SkillDefinition.All.Length;
            var stripW = n * StripCell;
            for (var i = 0; i < n; i++)
            {
                var id = SkillDefinition.All[i].WeaponId;
                var area = new Rect(-stripW / 2 + i * StripCell, StripY, StripCell, StripCell);
                var b = PixelButton.Create(root, "Pick_" + id, art, area, null, 1, Palette.Background, Palette.Background, Palette.Text, RunUi.Order + 2);
                var icon = new GameObject("Icon");
                icon.transform.SetParent(b.transform, false);
                icon.transform.localPosition = new Vector3(area.center.x, area.center.y, 0f);
                icon.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                var sr = icon.AddComponent<SpriteRenderer>();
                sr.sprite = art.Get(id).Ball;
                sr.sortingOrder = b.IconOrder;
                var k = i;
                b.Clicked += () => { view.index = k; view.Refresh(); };
                view.strip[i] = b;
            }

            // The ball: epithet over the name, the sprite, then role and the four bars.
            view.ballSprite = RunUi.Icon(root, new Vector2(-96, 158), RunUi.Order + 4);
            view.title = RunUi.Text(root, art, -66, 167, 1, PixelText.Align.Left);                  // 167..180
            view.page = RunUi.Text(root, art, 121, 167, 1, PixelText.Align.Right);
            view.ballName = RunUi.Text(root, art, -66, 140, 2, PixelText.Align.Left);               // 140..166
            view.profile = WeaponProfileView.Create(root, art, new Vector2(18, 118), 206, false);  // role 118..131, bars 65..117

            // Intro, skill (icon + name + what it does), one play tip. Rows are placed in Refresh, after the intro's real length.
            for (var i = 0; i < view.intro.Length; i++) view.intro[i] = RunUi.Text(root, art, Left, 0, 1, PixelText.Align.Left);
            view.skillIcon = RunUi.Icon(root, Vector2.zero, RunUi.Order + 4);
            view.skillHead = RunUi.Text(root, art, Left + 18, 0, 1, PixelText.Align.Left);
            for (var i = 0; i < view.skill.Length; i++) view.skill[i] = RunUi.Text(root, art, Left, 0, 1, PixelText.Align.Left);
            view.tipHead = RunUi.Text(root, art, Left, 0, 1, PixelText.Align.Left, "PLAY TIP", Header);
            for (var i = 0; i < view.tip.Length; i++) view.tip[i] = RunUi.Text(root, art, Left, 0, 1, PixelText.Align.Left);

            RunUi.Button(root, art, PrevArea, "PREV", 1, Palette.TextDim, () => { view.index = (view.index + n - 1) % n; view.Refresh(); });
            RunUi.Button(root, art, NextArea, "NEXT", 1, Palette.TextDim, () => { view.index = (view.index + 1) % n; view.Refresh(); });
            RunUi.Button(root, art, new Rect(-60, -210, 120, 30), "BACK", 2, Palette.TextDim, () => view.Closed?.Invoke());
            view.Hide();
            return view;
        }

        /// <summary>Translated now and cut to the screen.</summary>
        static string Fit(string s) => TextWrap.Cut(UiVietnamese.Translate(s ?? ""), TextWrap.MaxWidthOnScreen(1) - 2);

        /// <summary>Fills <paramref name="rows"/> top-down from <paramref name="y"/> (top edge); returns the y under the last used row.</summary>
        static float Fill(PixelText[] rows, string[] lines, float y, Color32 color)
        {
            for (var i = 0; i < rows.Length; i++)
            {
                var used = lines != null && i < lines.Length;
                rows[i].Set(used ? Fit(lines[i]) : "", color);
                if (!used) continue;
                y -= Line;
                rows[i].transform.localPosition = new Vector3(Left, y, 0);
            }
            return y;
        }

        void Refresh()
        {
            var data = SkillDefinition.All[index];
            var id = data.WeaponId;
            var look = Palette.Look(id);
            for (var i = 0; i < strip.Length; i++)
            {
                var on = i == index;
                strip[i].SetColors(on ? Palette.HpBack : Palette.Background, on ? (Color32)Palette.Look(SkillDefinition.All[i].WeaponId).Body : Palette.Background);
            }
            page.Set($"{index + 1}/{SkillDefinition.All.Length}", Palette.TextDim);

            var w = art.Get(id);
            ballSprite.sprite = w.Ball48 != null ? w.Ball48 : w.Ball;
            ballSprite.transform.localScale = w.Ball48 != null ? Vector3.one : new Vector3(1.5f, 1.5f, 1f);
            ballName.Set(WeaponNames.Of(id), look.Body);
            profile.Show(id);

            var text = CodexText.For(id);
            title.Set(text != null ? CodexText.Title(text) : "", Palette.TextDim);
            var y = Fill(intro, text != null ? CodexText.Intro(text) : null, TextTop, Palette.Text) - Gap;

            skillIcon.sprite = art.SkillIcon(id);
            skillIcon.enabled = skillIcon.sprite != null;
            y -= Line;
            skillIcon.transform.localPosition = new Vector3(Left + 7, y + 6, 0);
            skillHead.transform.localPosition = new Vector3(skillIcon.enabled ? Left + 18 : Left, y, 0);
            skillHead.Set(UiVietnamese.Translate(data.Name), Header);
            y = Fill(skill, text != null ? CodexText.Skill(text) : new[] { data.Summary }, y, Palette.Text) - Gap;

            y -= Line;
            tipHead.transform.localPosition = new Vector3(Left, y, 0);
            Fill(tip, text != null ? CodexText.Tip(text) : null, y, Palette.Text);
        }

        public void Show() { gameObject.SetActive(true); Refresh(); }
        public void Hide() => gameObject.SetActive(false);

        /// <summary>Automation: open the page of <paramref name="weaponId"/>.</summary>
        public void ShowBall(string weaponId)
        {
            for (var i = 0; i < SkillDefinition.All.Length; i++) if (SkillDefinition.All[i].WeaponId == weaponId) index = i;
            Show();
        }
    }
}
