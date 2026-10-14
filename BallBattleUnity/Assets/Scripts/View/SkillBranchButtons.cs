using System;
using BallBattle.Sim.Run;
using BallBattle.Sim.Skills;
using UnityEngine;

namespace BallBattle.View
{
    public sealed class SkillBranchButtons
    {
        public readonly PixelButton A, B;
        RunState run;
        int index;
        readonly Action<SkillBranch> picked;
        public SkillBranchButtons(Transform parent, ArtLibrary art, Rect area, Action<SkillBranch> onPick)
        {
            picked = onPick;
            A = PixelButton.Create(parent, "SkillA", art, new Rect(area.xMin + 8, area.yMin + 2, 112, 24), "", 1,
                Palette.Floor, Palette.WallWarning, Palette.Text, RunUi.Order + 8);
            B = PixelButton.Create(parent, "SkillB", art, new Rect(area.xMax - 120, area.yMin + 2, 112, 24), "", 1,
                Palette.Floor, Palette.WallWarning, Palette.Text, RunUi.Order + 8);
            A.Clicked += () => Choose(SkillBranch.A); B.Clicked += () => Choose(SkillBranch.B);
            A.ClickCue = B.ClickCue = null;
            Hide();
        }
        void Choose(SkillBranch branch)
        {
            if (run == null || !run.CanPick(index)) return;
            picked(run.EffectiveSkillLevel == 1 ? branch : run.EffectiveSkillBranch);
        }
        public bool Show(RunState state, int slot)
        {
            run = state; index = slot;
            if (state == null || slot >= state.OfferCount || state.OfferCard(slot).Kind != CardKind.SkillUp || !state.CanPick(slot)) { Hide(); return false; }
            var data = SkillDefinition.ForWeapon(state.Build.WeaponId);
            A.gameObject.SetActive(true);
            B.gameObject.SetActive(state.EffectiveSkillLevel == 1);
            A.SetText(state.EffectiveSkillLevel == 1 ? "A " + data.BranchA : data.BranchName(state.EffectiveSkillBranch) + " LV 3", Palette.Text);
            B.SetText("B " + data.BranchB, Palette.Text);
            return true;
        }
        public void Hide() { A.gameObject.SetActive(false); B.gameObject.SetActive(false); }
    }
}
