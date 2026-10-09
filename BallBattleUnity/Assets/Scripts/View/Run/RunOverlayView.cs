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

        PixelText label, countdown, banner;
        SpriteRenderer band;
        PixelButton speed;
        RunFlow.State lastState = RunFlow.State.Closed;
        int lastCountdown = -1;
        float goTimer;

        public static RunOverlayView Create(Transform parent, ArtLibrary art)
        {
            var root = RunUi.Root(parent, "RunOverlay", art, background: false);
            var v = root.gameObject.AddComponent<RunOverlayView>();
            v.label = PixelText.Create(root, "Label", art.Font, new Vector2(0, 128), PixelText.Align.Center, 1, Order);
            v.countdown = PixelText.Create(root, "Countdown", art.Font, new Vector2(0, -15), PixelText.Align.Center, 6, Order + 2);
            v.band = PixelRect.Create(root, "Band", art.Pixel, new Color32(16, 16, 26, 220), Order);
            PixelRect.Set(v.band, -115, -12, 115, 12);
            v.banner = PixelText.Create(root, "Banner", art.Font, new Vector2(0, -5), PixelText.Align.Center, 2, Order + 1);
            v.speed = PixelButton.Create(root, "Speed", art, new Rect(-22, -234, 44, 18), "X1", 1, Palette.Floor, Palette.TextDim, Palette.Text, Order + 2);
            v.speed.Clicked += () => v.SpeedToggled?.Invoke();
            v.Hide();
            return v;
        }

        public void SetSpeed(float timeScale) => speed.SetText(timeScale > 1f ? "X2" : "X1", timeScale > 1f ? Palette.WallWarning : Palette.Text);

        public void Hide()
        {
            gameObject.SetActive(false);
            lastState = RunFlow.State.Closed;
        }

        public void Render(RunFlow flow, float dt)
        {
            var state = flow.Current;
            var inFight = state == RunFlow.State.Countdown || state == RunFlow.State.Playing || state == RunFlow.State.FightOver;
            if (!inFight) { if (gameObject.activeSelf) Hide(); return; }
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            if (state != lastState)
            {
                var run = flow.Run;
                if (state == RunFlow.State.Countdown)
                    label.Set($"FIGHT {run.FightIndex + 1}/{RunTuning.Fights}" + (run.IsBossFight ? "  GIANT BOSS" : ""), run.IsBossFight ? Palette.WallWarning : Palette.Text);
                if (state == RunFlow.State.Playing) goTimer = GoSeconds;
                var over = state == RunFlow.State.FightOver;
                band.enabled = over;
                banner.Set(over ? (flow.LastFightWon ? $"WIN!  +{RunTuning.WinCoins} COINS" : flow.LastFightDraw ? "DRAW  -1 LIFE" : "LOST  -1 LIFE") : "",
                           flow.LastFightWon ? Palette.WallWarning : Palette.HpLost);
                lastState = state;
            }

            var n = flow.CountdownNumber;
            if (state == RunFlow.State.Countdown && n != lastCountdown) { lastCountdown = n; countdown.Set(n.ToString(), Palette.WallWarning); }
            else if (state == RunFlow.State.Playing && goTimer > 0f)
            {
                goTimer -= dt;
                if (lastCountdown != 0) { lastCountdown = 0; countdown.Set("GO!", Palette.WallWarning); }
                if (goTimer <= 0f) countdown.Set("", default);
            }
            else if (state == RunFlow.State.FightOver && lastCountdown != -1) { lastCountdown = -1; countdown.Set("", default); }
        }
    }
}
