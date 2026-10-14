using System;
using BallBattle.Sim.Skills;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>Eight browsable weapon/skill entries, one readable page per ball.</summary>
    public sealed class SkillCodexView : MonoBehaviour
    {
        public event Action Closed;
        PixelText name, signature, summary, branchA, branchB;
        WeaponProfileView profile;
        int index;
        public static SkillCodexView Create(Transform parent, ArtLibrary art)
        {
            var root = RunUi.Root(parent, "Codex", art);
            var view = root.gameObject.AddComponent<SkillCodexView>();
            RunUi.Text(root, art, 0, 190, 2, initial: "CODEX");
            view.name = RunUi.Text(root, art, 0, 156, 2);
            view.profile = WeaponProfileView.Create(root, art, new Vector2(0, 127), 190, false);
            view.signature = RunUi.Text(root, art, 0, 34, 2);
            view.summary = RunUi.Text(root, art, 0, 14, 1);
            view.branchA = RunUi.Text(root, art, 0, -12, 1);
            view.branchB = RunUi.Text(root, art, 0, -38, 1);
            RunUi.Button(root, art, new Rect(-120, -110, 90, 24), "PREV", 1, Palette.TextDim, () => { view.index = (view.index + 7) % 8; view.Refresh(); });
            RunUi.Button(root, art, new Rect(30, -110, 90, 24), "NEXT", 1, Palette.TextDim, () => { view.index = (view.index + 1) % 8; view.Refresh(); });
            RunUi.Button(root, art, new Rect(-60, -210, 120, 30), "BACK", 2, Palette.TextDim, () => view.Closed?.Invoke());
            view.Hide(); return view;
        }
        void Refresh()
        {
            var data = SkillDefinition.All[index];
            name.Set(WeaponNames.Of(data.WeaponId), Palette.Look(data.WeaponId).Body);
            profile.Show(data.WeaponId); signature.Set(data.Name, Palette.Text);
            summary.Set(data.Summary, Palette.TextDim); branchA.Set("A " + data.SummaryA, Palette.Text); branchB.Set("B " + data.SummaryB, Palette.Text);
        }
        public void Show() { gameObject.SetActive(true); Refresh(); }
        public void Hide() => gameObject.SetActive(false);
    }
}
