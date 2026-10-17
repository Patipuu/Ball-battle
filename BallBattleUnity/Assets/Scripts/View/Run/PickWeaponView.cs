using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Start of a run: every unlocked ball. UI-UX1: a tap selects a ball and shows what it does (how it grows, good at, weak to,
    /// its skill and both branches) in the panel below; PICK (or a second tap on the same ball) starts the run with it.
    /// </summary>
    public sealed class PickWeaponView : MonoBehaviour
    {
        const int Tile = 48;
        const int Gap = 3;
        const int TileHeight = 84;
        const int Columns = 5;
        const float LineX = -117f;
        /// <summary>Skill row: 14 px icon, then the text 17 px right of the line start.</summary>
        public const float SkillIconGap = 17f;
        /// <summary>Tile skill icon centre y (between the stat bars and the skill name).</summary>
        public const float TileSkillIconY = -37f;
        /// <summary>Detail panel and PICK button (tests).</summary>
        public static readonly Rect PanelArea = new Rect(-127, -238, 254, 140);
        public static readonly Rect PickArea = new Rect(-70, -234, 140, 28);

        public event Action<string> WeaponPicked;

        ArtLibrary art;
        Transform tilesRoot;
        readonly List<(string id, PixelButton button)> tiles = new List<(string, PixelButton)>();
        PixelText hint, header, grows, good, weak, skill, branchA, branchB;
        SpriteRenderer skillIcon;   // UI-UX1: the selected ball's skill icon at the start of the skill row
        PixelButton pickButton;
        string selected;

        /// <summary>Selected weapon id, or null (tests).</summary>
        public string Selected => selected;
        public PixelButton PickButton => pickButton;
        public SpriteRenderer SkillIcon => skillIcon;
        public string DetailText => string.Join("|", new[] { header, grows, good, weak, skill, branchA, branchB }.Select(t => t.Text));

        public static PickWeaponView Create(Transform parent, ArtLibrary art)
        {
            var root = RunUi.Root(parent, "PickWeapon", art);
            var v = root.gameObject.AddComponent<PickWeaponView>();
            v.art = art;
            RunUi.Text(root, art, 0, 150, 2, initial: "CHOOSE YOUR WEAPON");
            RunUi.Box(root, art, PanelArea.xMin, PanelArea.yMin, PanelArea.xMax, PanelArea.yMax);
            v.hint = RunUi.Text(root, art, 0, -175, 1, initial: "", color: Palette.TextDim);
            // Rows of the panel, 13 px cells: name+role, grows, good, weak, then the skill and its two branches.
            v.header = RunUi.Text(root, art, LineX, -114, 1, PixelText.Align.Left);
            v.grows = RunUi.Text(root, art, LineX, -128, 1, PixelText.Align.Left);
            v.good = RunUi.Text(root, art, LineX, -141, 1, PixelText.Align.Left);
            v.weak = RunUi.Text(root, art, LineX, -154, 1, PixelText.Align.Left);
            v.skill = RunUi.Text(root, art, LineX + SkillIconGap, -168, 1, PixelText.Align.Left);
            v.skillIcon = RunUi.Icon(root, new Vector2(LineX + 7, -161), RunUi.Order + 4);
            v.branchA = RunUi.Text(root, art, LineX, -181, 1, PixelText.Align.Left);
            v.branchB = RunUi.Text(root, art, LineX, -194, 1, PixelText.Align.Left);
            v.pickButton = RunUi.Button(root, art, PickArea, "PICK", 2, Palette.WallWarning, () => { if (v.selected != null) v.WeaponPicked?.Invoke(v.selected); });
            v.ShowDetails(null);
            return v;
        }

        public void Show(IReadOnlyList<string> choices)
        {
            gameObject.SetActive(true);
            if (tilesRoot != null)
            {
                tilesRoot.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(tilesRoot.gameObject);
                else DestroyImmediate(tilesRoot.gameObject);   // EditMode tests and captures show it more than once
            }
            tilesRoot = new GameObject("Tiles").transform;
            tilesRoot.SetParent(transform, false);
            tiles.Clear();

            for (var i = 0; i < choices.Count; i++)
            {
                var id = choices[i];
                var col = i % Columns;
                var row = i / Columns;
                var area = new Rect(-126 + col * (Tile + Gap), 8 - row * 91, Tile, TileHeight);
                var b = PixelButton.Create(tilesRoot, "Pick_" + id, art, area, null, 1, Palette.Floor, Palette.Look(id).Body, Palette.Text, RunUi.Order + 2);
                RunUi.Icon(b.transform, new Vector2(area.center.x, area.yMax - 24), b.IconOrder).sprite = art.Get(id).Ball;
                var name = PixelText.Create(b.transform, "Name", art.Font, new Vector2(area.center.x, area.yMin + 21), PixelText.Align.Center, 1, b.IconOrder);
                name.Set(RunUi.WeaponName(id), Palette.Look(id).Body);
                var tileIcon = RunUi.Icon(b.transform, new Vector2(area.center.x, area.yMin + 12), b.IconOrder);
                tileIcon.name = "SkillIcon";
                tileIcon.sprite = art.SkillIcon(id);
                b.Clicked += () => Select(id);
                tiles.Add((id, b));
            }
            ShowDetails(choices.Count > 0 ? choices[0] : null);   // UI-CLEAN1: the first ball is explained at once (no empty panel, no hint line)
        }

        /// <summary>First tap selects and explains a ball; a second tap on the same ball picks it (like PICK).</summary>
        public void Select(string id)
        {
            if (id != null && id == selected) { WeaponPicked?.Invoke(id); return; }
            ShowDetails(id);
        }

        void ShowDetails(string id)
        {
            selected = id;
            foreach (var (tileId, button) in tiles)
                button.SetColors(Palette.Floor, tileId == id ? (Color32)Palette.WallWarning : Palette.Look(tileId).Body);
            var guide = WeaponGuide.For(id);
            var on = id != null;
            hint.gameObject.SetActive(!on);
            pickButton.gameObject.SetActive(on);
            if (!on)
            {
                foreach (var t in new[] { header, grows, good, weak, skill, branchA, branchB }) t.Set("", Palette.Text);
                skillIcon.enabled = false;
                return;
            }
            var data = BallBattle.Sim.Skills.SkillDefinition.ForWeapon(id);
            header.Set(RunUi.WeaponName(id) + "  " + UiVietnamese.TranslateRole(data.Role), Palette.Look(id).Body);
            grows.Set(guide != null ? "HOW: " + guide.Grows : "", Palette.Text);
            good.Set(guide != null ? "GOOD: " + guide.Good : "", WeaponGuide.GoodColor);
            weak.Set(guide != null ? "WEAK: " + guide.Weak : "", WeaponGuide.WeakColor);
            skill.Set(data.Name + ": " + data.Summary, Palette.WallWarning);
            skillIcon.sprite = art.SkillIcon(id);
            skillIcon.enabled = skillIcon.sprite != null;
            branchA.Set((StringTable.Current == Lang.Vi ? "CẤP 2A " : "LV2 A ") + UiVietnamese.Translate(data.SummaryA), Palette.TextDim);
            branchB.Set((StringTable.Current == Lang.Vi ? "CẤP 2B " : "LV2 B ") + UiVietnamese.Translate(data.SummaryB), Palette.TextDim);
            pickButton.SetText("PICK " + RunUi.WeaponName(id), Palette.WallWarning);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
