using System;
using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// In-match overlay: round/score line, seed, 3-2-1-GO countdown, round result banner,
    /// and the series Result panel (REMATCH same seed / NEW MATCH / MENU). Driven by MatchFlow state each frame.
    /// </summary>
    public sealed class OverlayView : MonoBehaviour
    {
        public const int Order = 50;
        public const int ResultOrder = 70;
        const float GoSeconds = 0.6f;

        public event Action RematchRequested;
        public event Action NewMatchRequested;
        public event Action MenuRequested;

        PixelText score, seed, countdown, banner;
        SpriteRenderer bannerBand;
        GameObject resultPanel;
        PixelText resultTitle, resultScore, resultSeed;
        float goTimer;
        MatchFlow.State lastState;
        int lastCountdown = -1;
        int lastScoreKey = int.MinValue;

        public static OverlayView Create(Transform parent, ArtLibrary art)
        {
            var go = new GameObject("Overlay");
            go.transform.SetParent(parent, false);
            var o = go.AddComponent<OverlayView>();
            o.Build(art);
            return o;
        }

        void Build(ArtLibrary art)
        {
            score = PixelText.Create(transform, "Score", art.Font, new Vector2(0, 128), PixelText.Align.Center, 1, Order);
            seed = PixelText.Create(transform, "Seed", art.Font, new Vector2(-131, -236), PixelText.Align.Left, 1, Order);
            countdown = PixelText.Create(transform, "Countdown", art.Font, new Vector2(0, -15), PixelText.Align.Center, 6, Order + 2);
            bannerBand = PixelRect.Create(transform, "BannerBand", art.Pixel, new Color32(16, 16, 26, 220), Order);
            PixelRect.Set(bannerBand, -115, -12, 115, 12);
            banner = PixelText.Create(transform, "Banner", art.Font, new Vector2(0, -5), PixelText.Align.Center, 2, Order + 1);

            resultPanel = new GameObject("Result");
            resultPanel.transform.SetParent(transform, false);
            var p = resultPanel.transform;
            PixelRect.Set(PixelRect.Create(p, "Dim", art.Pixel, new Color32(16, 16, 26, 230), ResultOrder), -200, -300, 200, 300);
            resultTitle = PixelText.Create(p, "Title", art.Font, new Vector2(0, 120), PixelText.Align.Center, 3, ResultOrder + 4);
            resultScore = PixelText.Create(p, "ScoreBig", art.Font, new Vector2(0, 70), PixelText.Align.Center, 4, ResultOrder + 4);
            resultSeed = PixelText.Create(p, "Seed", art.Font, new Vector2(0, 45), PixelText.Align.Center, 1, ResultOrder + 4);
            Button(art, p, "Rematch", new Rect(-80, -10, 160, 28), "REMATCH", Palette.WallWarning, () => RematchRequested?.Invoke());
            Button(art, p, "NewMatch", new Rect(-80, -50, 160, 28), "NEW MATCH", Palette.Text, () => NewMatchRequested?.Invoke());
            Button(art, p, "MenuBtn", new Rect(-80, -90, 160, 28), "MENU", Palette.TextDim, () => MenuRequested?.Invoke());
            Text(p, art, "REMATCH REPLAYS THE SAME FIGHT", -120, Palette.TextDim);   // font has no = or ,

            HideAll();
        }

        static void Button(ArtLibrary art, Transform parent, string name, Rect r, string label, Color32 accent, Action onClick)
        {
            var b = PixelButton.Create(parent, name, art, r, label, 2, Palette.Floor, accent, accent, ResultOrder + 2);
            b.Clicked += onClick;
        }

        static void Text(Transform parent, ArtLibrary art, string s, float y, Color32 c)
        {
            var t = PixelText.Create(parent, s, art.Font, new Vector2(0, y), PixelText.Align.Center, 1, ResultOrder + 4);
            t.Set(s, c);
        }

        public void HideAll()
        {
            score.Set("", default);
            seed.Set("", default);
            countdown.Set("", default);
            banner.Set("", default);
            bannerBand.enabled = false;
            resultPanel.SetActive(false);
            lastScoreKey = int.MinValue;
            lastCountdown = -1;
        }

        public void Render(MatchFlow flow, float dt)
        {
            var state = flow.Current;
            if (state == MatchFlow.State.Menu) { if (lastState != state) HideAll(); lastState = state; return; }

            var s = flow.Series;
            if (state != lastState)
            {
                if (state == MatchFlow.State.Playing) goTimer = GoSeconds;
                resultPanel.SetActive(state == MatchFlow.State.Result);
                if (state == MatchFlow.State.Result) FillResult(s);
                seed.Set("SEED " + s.MatchSeed, Palette.TextDim);
                lastState = state;
            }

            // Rebuild the score string only when it changes (no per-frame garbage).
            var scoreKey = state == MatchFlow.State.Result ? -1 : s.DisplayRound * 10000 + s.WinsA * 100 + s.WinsB;
            if (scoreKey != lastScoreKey)
            {
                lastScoreKey = scoreKey;
                score.Set(scoreKey < 0 ? "" : $"ROUND {s.DisplayRound}   {s.WinsA} - {s.WinsB}", Palette.Text);
            }

            // Countdown 3-2-1 then GO.
            var n = flow.CountdownNumber;
            if (state == MatchFlow.State.Countdown && n != lastCountdown) { lastCountdown = n; countdown.Set(n.ToString(), Palette.WallWarning); }
            if (state == MatchFlow.State.Playing)
            {
                if (goTimer > 0f)
                {
                    goTimer -= dt;
                    if (lastCountdown != 0) { lastCountdown = 0; countdown.Set("GO!", Palette.WallWarning); }
                    if (goTimer <= 0f) countdown.Set("", default);
                }
            }
            else if (state != MatchFlow.State.Countdown && lastCountdown != -1) { lastCountdown = -1; countdown.Set("", default); }

            // Round banner during the pause after a round.
            var showBanner = state == MatchFlow.State.RoundOver;
            if (bannerBand.enabled != showBanner)
            {
                bannerBand.enabled = showBanner;
                banner.Set(showBanner ? RoundText(s, flow.LastRoundWinner) : "", showBanner ? BannerColor(s, flow.LastRoundWinner) : default);
            }
        }

        static string WeaponOf(Series s, int side) => side == 0 ? s.WeaponA : s.WeaponB;

        static string RoundText(Series s, int winner) =>
            winner < 0 ? "DRAW - REPLAY" : WeaponRegistry.Get(WeaponOf(s, winner)).DisplayName + " WINS";

        static Color32 BannerColor(Series s, int winner) =>
            winner < 0 ? Palette.Text : Palette.Look(WeaponOf(s, winner)).Body;

        void FillResult(Series s)
        {
            var w = s.Winner;
            resultTitle.Set(WeaponRegistry.Get(WeaponOf(s, w)).DisplayName + " WINS", Palette.Look(WeaponOf(s, w)).Body);
            resultScore.Set($"{s.WinsA} - {s.WinsB}", Palette.Text);
            resultSeed.Set("SEED " + s.MatchSeed, Palette.TextDim);
        }
    }
}
