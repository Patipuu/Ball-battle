using System;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// After every Run fight, before the next Prepare (or the summary): WIN / LOST / DRAW, how, HP left, damage dealt
    /// and taken, coins, what it means for the run, and the next fight (enemy + arena). Numbers come from a FightReport
    /// built from the finished match; this screen only formats them. Layout fits the 270x480 frame.
    /// </summary>
    public sealed class RunResultView : MonoBehaviour
    {
        public static readonly Rect ContinueArea = new Rect(-80, -150, 160, 32);
        /// <summary>The tap that closed the fight banner must not press CONTINUE.</summary>
        public const float EntryInputBlockSeconds = 0.8f;

        const float RowLeft = -110f;
        /// <summary>HP LEFT, DMG DEALT, DMG TAKEN, COINS, one 13 px cell each; the FOE HEALED row sits between DEALT and TAKEN.</summary>
        static readonly float[] RowY = { 145f, 132f, 106f, 93f };
        const float FoeHealedY = 119f;
        static readonly Color32 HealColor = new Color32(140, 240, 220, 255);
        PixelText foeHealed;
        const float RowRight = 110f;

        public event Action ContinueRequested;

        ArtLibrary art;
        PixelText fight, title, how;
        PixelText[] labels, values;
        PixelText consequence, carry, runLine;
        PixelText tutorialProgress;
        SpriteRenderer tutorialBarBack;
        SpriteRenderer tutorialBar;
        float tutorialReveal;
        bool tutorialVisible;
        PixelText traitTriggered;
        /// <summary>BB-FIX4: after a lost boss fight, the boss name and its Prepare hint (BossText.NameAndHint), under the NEXT box.</summary>
        readonly PixelText[] bossHint = new PixelText[3];
        static readonly float[] BossHintY = { -62f, -75f, -88f };   // cells -62..-49, -75..-62, -88..-75: NEXT box ends -38, NEXT FIGHT starts -118
        GameObject nextBox;
        PixelText nextHeader, nextName, nextTraits, nextTraits2, nextStats;
        /// <summary>Next enemy's traits lines: from x -74 to 120.</summary>
        const int NextTraitsWidth = 194;
        SpriteRenderer nextIcon;
        PixelButton continueButton;
        bool onScreen;

        public static RunResultView Create(Transform parent, ArtLibrary art)
        {
            var root = RunUi.Root(parent, "RunResult", art);
            var v = root.gameObject.AddComponent<RunResultView>();
            v.art = art;
            v.Build(root);
            return v;
        }

        void Build(Transform root)
        {
            fight = RunUi.Text(root, art, 0, 218, 1);     // cell 218..231
            title = RunUi.Text(root, art, 0, 177, 3);     // x4 (52 px) no longer fits: x3, 177..216
            how = RunUi.Text(root, art, 0, 163, 1);       // 163..176

            RunUi.Box(root, art, -120, 90, 120, 160);
            labels = new PixelText[4];
            values = new PixelText[4];
            string[] names = { "HP LEFT", "DMG DEALT", "DMG TAKEN", "COINS" };
            for (var i = 0; i < 4; i++)
            {
                var y = RowY[i];
                labels[i] = RunUi.Text(root, art, RowLeft, y, 1, PixelText.Align.Left, names[i], Palette.TextDim);
                values[i] = RunUi.Text(root, art, RowRight, y, 1, PixelText.Align.Right);
            }

            // Small line under DMG DEALT, only when the enemy healed (Aegis regen, Vampire...): explains a DMG DEALT above its max HP.
            foeHealed = RunUi.Text(root, art, RowLeft + 8, FoeHealedY, 1, PixelText.Align.Left);
            consequence = RunUi.Text(root, art, 0, 75, 1);      // 75..88
            carry = RunUi.Text(root, art, 0, 62, 1);            // 62..75
            traitTriggered = RunUi.Text(root, art, 0, 49, 1);   // 49..62
            runLine = RunUi.Text(root, art, 0, 14, 2);          // only when the run is over (no next box then): x3 -> x2, 14..40

            nextBox = new GameObject("Next");
            nextBox.transform.SetParent(root, false);
            var n = nextBox.transform;
            RunUi.Box(n, art, -120, -38, 120, 46);
            nextHeader = RunUi.Text(n, art, -112, 31, 1, PixelText.Align.Left);   // 31..44
            nextIcon = RunUi.Icon(n, new Vector2(-96, 10), RunUi.Order + 3);
            nextIcon.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
            nextName = RunUi.Text(n, art, -74, 4, 2, PixelText.Align.Left);       // 4..30
            nextTraits = RunUi.Text(n, art, -74, -9, 1, PixelText.Align.Left);    // -9..4
            nextTraits2 = RunUi.Text(n, art, -74, -22, 1, PixelText.Align.Left);  // -22..-9 (only when the traits need two lines)
            nextStats = RunUi.Text(n, art, -112, -35, 1, PixelText.Align.Left);   // -35..-22

            for (var i = 0; i < bossHint.Length; i++) bossHint[i] = RunUi.Text(root, art, 0, BossHintY[i], 1);

            continueButton = RunUi.Button(root, art, ContinueArea, "NEXT FIGHT", 2, Palette.WallWarning, () => ContinueRequested?.Invoke());
            tutorialProgress = RunUi.Text(root, art, 0, -177, 1);
            tutorialBarBack = PixelRect.Create(root, "TutorialProgressBack", art.Pixel, Palette.HpBack, RunUi.Order + 2);
            PixelRect.Set(tutorialBarBack, -96, -191, 96, -187);
            tutorialBar = PixelRect.Create(root, "TutorialProgress", art.Pixel, Palette.WallWarning, RunUi.Order + 3);
        }

        public void Show(FightReport r, bool tutorial = false)
        {
            if (r == null) return;
            var entering = !onScreen || !gameObject.activeSelf;
            onScreen = true;
            gameObject.SetActive(true);
            if (entering)
            {
                PixelInput.BlockFor(EntryInputBlockSeconds);
                PixelButton.NoteShownAll(transform);
            }

            var win = r.Outcome == FightOutcome.Win;
            var accent = win ? Palette.WallWarning : Palette.HpLost;
            // BB-VIEW3: the boss's real name ("GIANT BOSS" only for an old report without a boss id).
            var boss = !string.IsNullOrEmpty(r.BossId) ? "  " + BossText.Name(r.BossId) : r.WasBoss ? "  GIANT BOSS" : "";
            fight.Set($"FIGHT {r.FightNumber}/{Sim.Run.RunTuning.Fights}" + boss + "  VS " + RunUi.WeaponName(r.EnemyWeaponId), Palette.TextDim);
            title.Set(r.Title, accent);
            how.Set(r.HowLine, Palette.TextDim);

            values[0].Set(HpText.Pair(r.HpLeft, r.MaxHpInFight), r.HpLeft > 0f ? Palette.Text : Palette.HpLost);
            values[1].Set(HpText.Floor(r.DamageDealt).ToString(), Palette.Text);   // whole numbers, rounded down like HP
            values[2].Set(HpText.Floor(r.DamageTaken).ToString(), Palette.Text);
            foeHealed.Set(FoeHealedText(r), HealColor);
            values[3].Set($"+{r.CoinsGained}  TOTAL {r.CoinsTotal}", Palette.WallWarning);

            consequence.Set(r.ConsequenceLine, accent);
            carry.Set(r.CarryLine, Palette.Text);
            traitTriggered.Set(r.PlayerSecondWindTriggered ? "2ND WIND USED" : "", HealColor);
            runLine.Set(r.RunLine, r.RunWon ? Palette.WallWarning : Palette.HpLost);

            nextBox.SetActive(!r.RunOver);
            if (!r.RunOver)
            {
                nextHeader.Set(r.NextHeader, r.NextIsBoss ? Palette.WallWarning : Palette.TextDim);
                nextIcon.sprite = art.Get(r.NextWeaponId).Ball;
                nextName.Set(r.NextWeaponName, Palette.Look(r.NextWeaponId).Body);
                var lines = r.NextTraitNames != null && r.NextTraitNames.Count > 0
                    ? TextWrap.Items(r.NextTraitNames, "", " ", NextTraitsWidth)
                    : TextWrap.SplitToFit(TraitNames.List(r.NextTraits ?? ""), NextTraitsWidth);
                nextTraits.Set(lines.Count > 0 ? lines[0] : "", Palette.Text);
                nextTraits2.Set(lines.Count > 1 ? string.Join(" ", lines.GetRange(1, lines.Count - 1)) : "", Palette.Text);
                nextStats.Set(r.NextStats, Palette.Text);
            }
            var hint = BossHint(r);
            for (var i = 0; i < bossHint.Length; i++)
                bossHint[i].Set(i < hint.Count ? hint[i] : "", i == 0 ? Palette.WallWarning : Palette.Text);   // Prepare: name gold, hint white
            continueButton.SetText(r.RunOver ? "SUMMARY" : "NEXT FIGHT", Palette.WallWarning);
            tutorialVisible = tutorial && r.FightNumber == 1;
            if (entering) tutorialReveal = 0f;
            tutorialProgress.Set(tutorialVisible ? "WIN 8 FIGHTS  -  1 RETRY" : "", Palette.TextDim);
            tutorialBarBack.enabled = tutorialVisible;
            tutorialBar.enabled = tutorialVisible;
            DrawTutorialBar();
        }

        public void Tick(float dt)
        {
            if (!tutorialVisible || !gameObject.activeSelf) return;
            tutorialReveal = Mathf.Min(1f, tutorialReveal + Mathf.Max(0f, dt) / 0.4f);
            DrawTutorialBar();
        }

        void DrawTutorialBar()
        {
            if (tutorialVisible) PixelRect.Set(tutorialBar, -96, -191, -96 + 192f / Sim.Run.RunTuning.Fights * tutorialReveal, -187);
        }

        public void Hide()
        {
            onScreen = false;
            gameObject.SetActive(false);
        }

        // ---- Tests / automation ----
        public void PressContinue() => ContinueRequested?.Invoke();
        public PixelButton ContinueButton => continueButton;
        public string TitleText => title.Text;
        public string Value(int row) => values[row].Text;
        public PixelText FoeHealedLine => foeHealed;

        /// <summary>BB-FIX4: boss name + hint lines on this screen: only after a boss fight that was not won; empty otherwise.</summary>
        public static System.Collections.Generic.List<string> BossHint(FightReport r) =>
            r == null || r.Outcome == FightOutcome.Win ? new System.Collections.Generic.List<string>() : BossText.NameAndHint(r.BossId);
        /// <summary>The boss hint lines as drawn (tests).</summary>
        public PixelText BossHintLine(int i) => bossHint[i];

        /// <summary>"FOE HEALED +x" (x rounded down), or "" when the enemy did not heal this fight.</summary>
        public static string FoeHealedText(FightReport r) => r != null && r.EnemyHealed > 0f ? "FOE HEALED +" + HpText.Floor(r.EnemyHealed) : "";
        public string ConsequenceText => consequence.Text;
        public string CarryText => carry.Text;
        public string NextHeaderText => nextBox.activeSelf ? nextHeader.Text : "";
        public string NextNameText => nextBox.activeSelf ? nextName.Text : "";
        /// <summary>Top line "FIGHT n/8 ... VS NAME" (tests).</summary>
        public string FightLineText => fight.Text;
        public string NextStatsText => nextBox.activeSelf ? nextStats.Text : "";
    }
}
