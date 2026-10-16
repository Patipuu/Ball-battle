using System;
using BallBattle.Sim.Skills;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>Eight browsable weapon/skill entries, one readable page per ball.</summary>
    public sealed class SkillCodexView : MonoBehaviour
    {
        public event Action Closed;
        PixelText name, signature, summary, branchA, branchB, how, good, weak;
        WeaponProfileView profile;
        SpriteRenderer skillIcon;   // UI-UX1: 2x skill icon left of the skill name
        public SpriteRenderer SkillIcon => skillIcon;
        int index;
        ArtLibrary art;
        public static SkillCodexView Create(Transform parent, ArtLibrary art)
        {
            var root = RunUi.Root(parent, "Codex", art);
            var view = root.gameObject.AddComponent<SkillCodexView>();
            RunUi.Text(root, art, 0, 190, 2, initial: "CODEX");
            view.name = RunUi.Text(root, art, 0, 156, 2);
            view.profile = WeaponProfileView.Create(root, art, new Vector2(0, 127), 190, false);
            view.signature = RunUi.Text(root, art, 0, 34, 2);
            view.skillIcon = RunUi.Icon(root, new Vector2(0, 47), RunUi.Order + 4);
            view.skillIcon.transform.localScale = new Vector3(2, 2, 1);
            view.art = art;
            view.summary = RunUi.Text(root, art, 0, 14, 1);
            view.branchA = RunUi.Text(root, art, 0, -12, 1);
            view.branchB = RunUi.Text(root, art, 0, -38, 1);
            // UI-UX1: how the ball plays (WeaponGuide), between the branches and PREV/NEXT.
            view.how = RunUi.Text(root, art, 0, -58, 1);
            view.good = RunUi.Text(root, art, 0, -71, 1);
            view.weak = RunUi.Text(root, art, 0, -84, 1);
            RunUi.Button(root, art, new Rect(-120, -124, 90, 24), "PREV", 1, Palette.TextDim, () => { view.index = (view.index + SkillDefinition.All.Length - 1) % SkillDefinition.All.Length; view.Refresh(); });
            RunUi.Button(root, art, new Rect(30, -124, 90, 24), "NEXT", 1, Palette.TextDim, () => { view.index = (view.index + 1) % SkillDefinition.All.Length; view.Refresh(); });
            RunUi.Button(root, art, new Rect(-60, -210, 120, 30), "BACK", 2, Palette.TextDim, () => view.Closed?.Invoke());
            view.Hide(); return view;
        }
        void Refresh()
        {
            var data = SkillDefinition.All[index];
            name.Set(WeaponNames.Of(data.WeaponId), Palette.Look(data.WeaponId).Body);
            profile.Show(data.WeaponId); signature.Set(data.Name, Palette.Text);
            skillIcon.sprite = art != null ? art.SkillIcon(data.WeaponId) : null;
            skillIcon.enabled = skillIcon.sprite != null;
            // 28 px icon, 6 px gap, then the name: the pair is centred together.
            var w = signature.Bounds.width;
            var shift = skillIcon.enabled ? 17f : 0f;
            signature.transform.localPosition = new Vector3(Mathf.Round(shift), 34, 0);
            skillIcon.transform.localPosition = new Vector3(Mathf.Round(shift - w / 2f - 6 - 14), 47, 0);
            summary.Set(data.Summary, Palette.TextDim); branchA.Set("A " + data.SummaryA, Palette.Text); branchB.Set("B " + data.SummaryB, Palette.Text);
            var guide = WeaponGuide.For(data.WeaponId);
            how.Set(guide != null ? "HOW: " + guide.Grows : "", Palette.Text);
            good.Set(guide != null ? "GOOD: " + guide.Good : "", WeaponGuide.GoodColor);
            weak.Set(guide != null ? "WEAK: " + guide.Weak : "", WeaponGuide.WeakColor);
        }
        public void Show() { gameObject.SetActive(true); Refresh(); }
        public void Hide() => gameObject.SetActive(false);
    }
}
