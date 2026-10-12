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
        /// <summary>HP LEFT, DMG DEALT, DMG TAKEN, COINS; the FOE HEALED line sits between DEALT and TAKEN (2 px clear of both).</summary>
        static readonly float[] RowY = { 145f, 134f, 117f, 106f };
        const float FoeHealedY = 126f;
        static readonly Color32 HealColor = new Color32(140, 240, 220, 255);
        PixelText foeHealed;
        const float RowRight = 110f;

        public event Action ContinueRequested;

        ArtLibrary art;
        PixelText fight, title, how;
        PixelText[] labels, values;
        PixelText consequence, carry, runLine;
        PixelText traitTriggered;
        GameObject nextBox;
        PixelText nextHeader, nextName, nextTraits, nextStats;
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
            fight = RunUi.Text(root, art, 0, 206, 1);
            title = RunUi.Text(root, art, 0, 176, 4);
            how = RunUi.Text(root, art, 0, 164, 1);

            RunUi.Box(root, art, -120, 98, 120, 156);
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
            consequence = RunUi.Text(root, art, 0, 84, 1);
            carry = RunUi.Text(root, art, 0, 72, 1);
            traitTriggered = RunUi.Text(root, art, 0, 60, 1);
            runLine = RunUi.Text(root, art, 0, 40, 3);

            nextBox = new GameObject("Next");
            nextBox.transform.SetParent(root, false);
            var n = nextBox.transform;
            RunUi.Box(n, art, -120, -10, 120, 58);
            nextHeader = RunUi.Text(n, art, -112, 48, 1, PixelText.Align.Left);
            nextIcon = RunUi.Icon(n, new Vector2(-96, 22), RunUi.Order + 3);
            nextIcon.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
            nextName = RunUi.Text(n, art, -74, 28, 2, PixelText.Align.Left);
            nextTraits = RunUi.Text(n, art, -74, 16, 1, PixelText.Align.Left);
            nextStats = RunUi.Text(n, art, -112, -2, 1, PixelText.Align.Left);

            continueButton = RunUi.Button(root, art, ContinueArea, "NEXT FIGHT", 2, Palette.WallWarning, () => ContinueRequested?.Invoke());
        }

        public void Show(FightReport r)
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
            fight.Set($"FIGHT {r.FightNumber}/{Sim.Run.RunTuning.Fights}" + (r.WasBoss ? "  GIANT BOSS" : "") + "  VS " + RunUi.WeaponName(r.EnemyWeaponId), Palette.TextDim);
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
                nextTraits.Set(r.NextTraits, Palette.Text);
                nextStats.Set(r.NextStats, Palette.Text);
            }
            continueButton.SetText(r.RunOver ? "SUMMARY" : "NEXT FIGHT", Palette.WallWarning);
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
