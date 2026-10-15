using System;
using BallBattle.Sim.Run;
using BallBattle.Sim.Skills;
using UnityEngine;

namespace BallBattle.View
{
    public sealed class SkillBranchButtons
    {
        public readonly PixelButton A, B;
        readonly PixelText effectA, effectB;
        RunState run;
        int index;
        readonly Action<SkillBranch> picked;
        public SkillBranchButtons(Transform parent, ArtLibrary art, Rect area, Action<SkillBranch> onPick)
        {
            picked = onPick;
            A = PixelButton.Create(parent, "SkillA", art, new Rect(area.xMin + 8, area.yMin + 2, 112, 28), " ", 1,
                Palette.Floor, Palette.WallWarning, Palette.Text, RunUi.Order + 8);
            B = PixelButton.Create(parent, "SkillB", art, new Rect(area.xMax - 120, area.yMin + 2, 112, 28), " ", 1,
                Palette.Floor, Palette.WallWarning, Palette.Text, RunUi.Order + 8);
            effectA = Effect(A, art); effectB = Effect(B, art);
            A.Clicked += () => Choose(SkillBranch.A); B.Clicked += () => Choose(SkillBranch.B);
            A.ClickCue = B.ClickCue = null;
            Hide();
        }
        static PixelText Effect(PixelButton button, ArtLibrary art)
        {
            button.transform.Find("Label").localPosition = new Vector3(button.Area.center.x, button.Area.yMin + 14, 0);
            return PixelText.Create(button.transform, "Effect", art.Font, new Vector2(button.Area.center.x, button.Area.yMin + 1),
                PixelText.Align.Center, 1, button.IconOrder + 1);
        }
        static string EffectText(string summary) => summary.Substring(summary.IndexOf(':') + 1).Trim();
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
            effectA.Set(EffectText(state.EffectiveSkillLevel == 1 || state.EffectiveSkillBranch == SkillBranch.A ? data.SummaryA : data.SummaryB), Palette.TextDim);
            effectB.Set(EffectText(data.SummaryB), Palette.TextDim);
            return true;
        }
        public void Hide() { A.gameObject.SetActive(false); B.gameObject.SetActive(false); }
    }
}
