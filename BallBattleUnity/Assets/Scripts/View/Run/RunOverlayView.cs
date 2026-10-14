using System;
using BallBattle.Sim.Run;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>Over a Run fight: fight label, 2-1 countdown, x2 speed toggle, result banner. No background.</summary>
    public sealed class RunOverlayView : MonoBehaviour
    {
        const int Order = 50;
        const float GoSeconds = 0.5f;

        public event Action SpeedToggled;

        PixelText label, countdown, banner, retryCaption, sixtyPercent;
        SpriteRenderer band;
        PixelButton speed;
        SpriteRenderer retryToken;
        SpriteRenderer[] retryBorder;
        GameObject retryBannerRoot;
        PixelButton retryBannerTap;
        float retryBannerLeft, sixtyPercentLeft;
        ArtLibrary art;
        RunFlow.State lastState = RunFlow.State.Closed;
        int lastCountdown = -1;
        float goTimer;

        public static RunOverlayView Create(Transform parent, ArtLibrary art)
        {
            var root = RunUi.Root(parent, "RunOverlay", art, background: false);
            var v = root.gameObject.AddComponent<RunOverlayView>();
            v.art = art;
            v.label = PixelText.Create(root, "Label", art.Font, new Vector2(0, 128), PixelText.Align.Center, 1, Order);
            v.countdown = PixelText.Create(root, "Countdown", art.Font, new Vector2(0, -15), PixelText.Align.Center, 6, Order + 2);
            v.band = PixelRect.Create(root, "Band", art.Pixel, new Color32(16, 16, 26, 220), Order);
            PixelRect.Set(v.band, -115, -14, 115, 14);
            v.banner = PixelText.Create(root, "Banner", art.Font, new Vector2(0, -13), PixelText.Align.Center, 2, Order + 1);   // 26 px cell in the 28 px band
            v.speed = PixelButton.Create(root, "Speed", art, new Rect(-22, -238, 44, 24), "X1", 1, Palette.Floor, Palette.TextDim, Palette.Text, Order + 2);   // 24 px tap target (was 18)
            v.speed.Clicked += () => v.SpeedToggled?.Invoke();
            v.retryToken = RunUi.Icon(root, new Vector2(115, 229), Order + 2);   // above the x2 HP number's ink (5x7 font: 197..211)
            v.retryCaption = PixelText.Create(root, "RetryCaption", art.Font, new Vector2(87, 158), PixelText.Align.Right, 1, Order + 2);
            v.sixtyPercent = PixelText.Create(root, "SixtyPercent", art.Font, new Vector2(23, 185), PixelText.Align.Center, 1, Order + 2);
            v.retryBorder = new[]
            {
                PixelRect.Create(root, "RetryBorderTop", art.Pixel, Palette.WallWarning, Order + 1),
                PixelRect.Create(root, "RetryBorderBottom", art.Pixel, Palette.WallWarning, Order + 1),
                PixelRect.Create(root, "RetryBorderLeft", art.Pixel, Palette.WallWarning, Order + 1),
                PixelRect.Create(root, "RetryBorderRight", art.Pixel, Palette.WallWarning, Order + 1),
            };
            PixelRect.Set(v.retryBorder[0], -125, 217, 125, 218);
            PixelRect.Set(v.retryBorder[1], -125, 154, 125, 155);
            PixelRect.Set(v.retryBorder[2], -125, 155, -124, 217);
            PixelRect.Set(v.retryBorder[3], 124, 155, 125, 217);

            v.retryBannerRoot = new GameObject("RetryBanner");
            v.retryBannerRoot.transform.SetParent(root, false);
            var bannerRoot = v.retryBannerRoot.transform;
            // BB-FONT1: 160x76 panel; RETRY x3 cell 0..39, the two lines -14..-1 and -28..-15.
            PixelRect.Set(PixelRect.Create(bannerRoot, "BannerBorder", art.Pixel, Palette.WallWarning, Order + 5), -80, -33, 80, 43);
            PixelRect.Set(PixelRect.Create(bannerRoot, "BannerFill", art.Pixel, Palette.Background, Order + 6), -79, -32, 79, 42);
            PixelText.Create(bannerRoot, "RetryTitle", art.Font, new Vector2(0, 0), PixelText.Align.Center, 3, Order + 7).Set("RETRY", Palette.WallWarning);
            PixelText.Create(bannerRoot, "SameFoe", art.Font, new Vector2(0, -14), PixelText.Align.Center, 1, Order + 7).Set("SAME FOE  SAME ARENA", Palette.Text);
            PixelText.Create(bannerRoot, "LastChance", art.Font, new Vector2(0, -28), PixelText.Align.Center, 1, Order + 7).Set("HP 60%  LAST CHANCE", Palette.WallWarning);
            v.retryBannerTap = PixelButton.Create(bannerRoot, "DismissRetry", art, new Rect(-135, -240, 270, 480), null, 1,
                new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0), Palette.Text, Order + 8);
            v.retryBannerTap.Clicked += v.DismissRetryBanner;
            v.retryBannerRoot.SetActive(false);
            v.Hide();
            return v;
        }

        public void SetSpeed(float timeScale) => speed.SetText(timeScale > 1f ? "X2" : "X1", timeScale > 1f ? Palette.WallWarning : Palette.Text);

        public void Hide()
        {
            gameObject.SetActive(false);
            lastState = RunFlow.State.Closed;
            retryBannerLeft = 0f;
            retryBannerRoot.SetActive(false);
        }

        public bool RetryBannerActive => retryBannerLeft > 0f;

        public void ShowRetryBanner()
        {
            retryBannerLeft = 1.5f;
            retryBannerRoot.SetActive(true);
            retryBannerTap.NoteShown();
        }

        public void DismissRetryBanner()
        {
            retryBannerLeft = 0f;
            retryBannerRoot.SetActive(false);
        }

        /// <summary>Hold the countdown while the retry notice is readable.</summary>
        public bool TickRetryBanner(float dt)
        {
            if (!RetryBannerActive) return false;
            retryBannerLeft -= dt;
            if (retryBannerLeft <= 0f) DismissRetryBanner();
            return true;
        }

        public void Render(RunFlow flow, float dt)
        {
            var state = flow.Current;
            var inFight = state == RunFlow.State.Countdown || state == RunFlow.State.Playing || state == RunFlow.State.FightOver;
            if (!inFight) { if (gameObject.activeSelf) Hide(); return; }
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            retryToken.sprite = flow.Run.RetryCount == 0 ? art.RetryTokenOn : art.RetryTokenOff;
            retryToken.enabled = retryToken.sprite != null;
            retryCaption.Set(flow.Run.RetryCount == 0 ? "RETRY 1" : "", Palette.WallWarning);
            var retry = flow.Run.IsRetry;
            foreach (var edge in retryBorder) edge.enabled = retry;
            if (!retry) sixtyPercent.Set("", Palette.WallWarning);

            if (state != lastState)
            {
                var run = flow.Run;
                if (state == RunFlow.State.Countdown)
                    label.Set($"FIGHT {run.FightIndex + 1}/{RunTuning.Fights}" + (run.IsBossFight ? "  " + BossText.Name(run.CurrentBossId) : ""), run.IsBossFight ? Palette.WallWarning : Palette.Text);
                if (state == RunFlow.State.Playing)
                {
                    goTimer = GoSeconds;
                    sixtyPercentLeft = retry ? 3f : 0f;
                }
                var over = state == RunFlow.State.FightOver;
                band.enabled = over;
                banner.Set(over ? (flow.LastFightWon ? $"WIN!  +{RunTuning.WinCoins} COINS" : flow.Run.RetryCount == 0 ? "LOST  RETRY" : "RUN OVER") : "",
                           flow.LastFightWon ? Palette.WallWarning : Palette.HpLost);
                lastState = state;
            }

            var n = flow.CountdownNumber;
            if (state == RunFlow.State.Countdown && RetryBannerActive)
            {
                lastCountdown = -1;              // the countdown is held under the RETRY notice: no digit behind it
                countdown.Set("", default);
            }
            else if (state == RunFlow.State.Countdown && n != lastCountdown) { lastCountdown = n; countdown.Set(n.ToString(), Palette.WallWarning); }
            else if (state == RunFlow.State.Playing && goTimer > 0f)
            {
                goTimer -= dt;
                if (lastCountdown != 0) { lastCountdown = 0; countdown.Set("GO!", Palette.WallWarning); }
                if (goTimer <= 0f) countdown.Set("", default);
            }
            else if (state == RunFlow.State.FightOver && lastCountdown != -1) { lastCountdown = -1; countdown.Set("", default); }
            if (state == RunFlow.State.Playing && retry && sixtyPercentLeft > 0f)
            {
                sixtyPercent.Set("60%", Palette.WallWarning);
                sixtyPercentLeft -= dt;
                if (sixtyPercentLeft <= 0f) sixtyPercent.Set("", Palette.WallWarning);
            }
            else if (state != RunFlow.State.Playing) sixtyPercent.Set("", Palette.WallWarning);
        }
    }
}
