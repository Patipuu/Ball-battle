using System;
using System.Collections;
using System.Collections.Generic;
using BallBattle.Sim;
using BallBattle.Sim.Meta;
using BallBattle.Sim.Run;
using BallBattle.Sim.Pvp;
using BallBattle.Sim.Weapons;
using UnityEngine;
using UnityEngine.Networking;

namespace BallBattle.View
{
    /// <summary>Online, server-settled ranked challenge screen. The service URL is read from pvp_server_url PlayerPrefs.</summary>
    public sealed class PvpView : MonoBehaviour
    {
        [Serializable] sealed class AccountReply { public string token; public Profile account; }
        [Serializable] sealed class Profile { public string id, name, banBall, banArena, activeChallengeId; public string[] roster; public int mmr, wins, losses; public bool ready; }
        [Serializable] sealed class OfferReply { public string offerId; public int refreshSeconds; public Opponent[] opponents; }
        [Serializable] sealed class Opponent { public string id, name; public int mmr, averageLevel, winDelta, lossDelta; }
        [Serializable] sealed class ChallengeReply { public string challengeId, state, opponentName; public string[] legalBalls; public string challengerBanBall, challengerBanArena, defenderBanBall, defenderBanArena; }
        [Serializable] sealed class ResultReply { public string state, arenaId, challengerBall, defenderBall, opponentName; public bool autoPicked; public int winner, challengerWins, defenderWins, delta, mmrAfter; public uint[] roundSeeds; public string[] roundHashes; public WireBall challengerProgress, defenderProgress; public string[] challengerTeam, defenderTeam; public int[] roundPairs, roundWinners; }
        /// <summary>
        /// A ball build as the server sends it. ASP.NET writes camelCase ("id", "level"), and JsonUtility only matches exact field
        /// names, so BallProgress (Id, Level) would come back empty and the replay could not start.
        /// </summary>
        [Serializable] sealed class WireBall
        {
            public string id; public int level, vit, pow, growth, skillUps, branch; public bool start, finale;
            public bool Valid => !string.IsNullOrEmpty(id);
            public BallProgress ToProgress() => new BallProgress
            { Id = id, Level = level, Vit = vit, Pow = pow, Growth = growth, Start = start, SkillUps = skillUps, Branch = branch, Finale = finale };
        }
        [Serializable] sealed class ErrorReply { public string error; }
        /// <summary>21/10 live PvP: the resolved draft the client plays (state "fight").</summary>
        [Serializable] sealed class FightReply { public string challengeId, state, opponentName, arenaId; public string[] challengerTeam, defenderTeam; public uint seed; public int rulesVersion, secondsLeft; public WireBall[] challengerProgress, defenderProgress; }
        [Serializable] sealed class FightBody { public int rulesVersion; public int[] taps; }
        [Serializable] sealed class HistoryReply { public ResultReply[] items; }
        [Serializable] sealed class NameAndId { public string offerId, opponentId; }
        [Serializable] sealed class BanBody { public string ballId, arenaId; }
        [Serializable] sealed class PickBody { public string[] ballIds; }
        [Serializable] sealed class PublishBody { public List<BallProgress> balls; public string[] roster; public string banBall, banArena; }

        enum Phase { Loading, Setup, Offers, Ban, Pick, Result, History }
        Phase phase;
        readonly string[] roster = new string[3];
        readonly PixelButton[] cards = new PixelButton[3];
        PixelButton ballButton, arenaButton, actionButton, setupButton, historyButton, skipButton;
        PixelText title, status, rating, details, banner;
        Transform panel;
        ArtLibrary art;
        // 21/10 UI pass: every card carries art (ball icon or avatar), a colour stripe and two text lines a side.
        sealed class CardDeco
        {
            public SpriteRenderer Stripe, Icon, Avatar;
            public PixelText Avatar1, Line1, Line2, Right1, Right2;
        }
        readonly CardDeco[] deco = new CardDeco[3];
        PixelText tierText, mmrText;
        SpriteRenderer tierBadge, banBallIcon, arenaSwatch, arenaSwatchWall;
        GameObject pickRoot, resultRoot;
        SpriteRenderer pickIcon, resultYouIcon, resultThemIcon;
        readonly SpriteRenderer[] rowMine = new SpriteRenderer[3], rowTheirs = new SpriteRenderer[3];
        readonly PixelText[] rowMineName = new PixelText[3], rowTheirName = new PixelText[3], rowResult = new PixelText[3];
        PixelText pickName, pickLevel, resultYouLabel, resultThemLabel, resultYouBall, resultThemBall, resultVs, resultScore, resultDelta, resultArena;
        ArenaView arena;
        ResultReply result;
        // 21/10 live PvP fight state.
        FightReply fight;
        PvpDraft fightDraft;
        int fightRound, fightPair, fightSteps, myIndex, myWins, theirWins;
        bool fighting, roundLive;
        float roundIntro;
        readonly List<int> fightTaps = new List<int>();
        readonly List<string> fightHashes = new List<string>();
        PixelText roundText, scoreText;
        SpriteRenderer roundBand;
        /// <summary>The Run's tap input (ring, PERFECT words); GameController hands it over. Null in tests without one.</summary>
        public TapView Tap { get; set; }
        public const float RoundIntroSeconds = 1.2f;
        ISaveStore store;
        string[] ids, arenas, legal;
        int[] rosterIndex = { 0, 1, 2 };
        int banBallIndex = 3, banArenaIndex, historyPage, offerRefreshSeconds;
        /// <summary>21/10 teams of three: the picked balls, as indexes into <see cref="legal"/>, in fighting order.</summary>
        readonly int[] team = { 0, 1, 2 };
        float offersLoadedAt;
        ResultReply[] history;
        Phase beforeHistory;
        string token, offerId, challengeId, opponentName;
        Opponent[] opponents;
        Profile profile;
        bool busy, offline;
        long lastStatus;
        public event Action Closed;
        /// <summary>21/10: PvP needs a named account. Raised (and the page closes) when there is none or the server forgot it.</summary>
        public event Action AccountNeeded;
        /// <summary>Screen the guide explains: "Setup", "Offers", "Ban", "Pick", "Result", "History"; "" while loading or replaying.</summary>
        public string GuidePhase => !IsOpen || busy ? "" : fighting ? "Fight" : IsReplaying || phase == Phase.Loading ? "" : phase.ToString();
        /// <summary>A live series is being played (tests).</summary>
        public bool Fighting => fighting;
        public IReadOnlyList<int> FightTaps => fightTaps;
        public bool IsOpen => gameObject.activeSelf;
        public bool IsReplaying => IsOpen && !panel.gameObject.activeSelf;
        public bool IsResult => IsOpen && phase == Phase.Result && !IsReplaying;

        public static PvpView Create(Transform parent, ArtLibrary art, ISaveStore save, ArenaView arenaView)
        {
            var go = new GameObject("PvpPage");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<PvpView>();
            view.store = save;
            view.arena = arenaView;
            view.Build(art);
            go.SetActive(false);
            return view;
        }

        void Build(ArtLibrary art)
        {
            panel = new GameObject("Panel").transform;
            panel.SetParent(transform, false);
            var ballIds = new List<string>();
            foreach (var b in WeaponRegistry.All) if (!WeaponRegistry.IsPlayerOnly(b.Id)) ballIds.Add(b.Id);   // BGLGXLQ is Run-only
            ids = ballIds.ToArray();
            var arenaIds = new List<string>();
            foreach (var a in ArenaRegistry.All) arenaIds.Add(a.Id);
            arenas = arenaIds.ToArray();
            this.art = art;
            var bg = PixelRect.Create(panel, "Background", art.Pixel, Palette.Background, MenuView.Order + 5);
            PixelRect.Set(bg, -135, -240, 135, 240);
            MenuBackdrop.Create(panel, art, MenuView.Order + 6, 23);   // the menu's drifting balls, dimmed, behind everything
            // Header band (title + status) and the profile strip under it.
            PixelRect.Set(PixelRect.Create(panel, "HeaderBand", art.Pixel, HeaderFill, MenuView.Order + 7), -135, 155, 135, 240);
            PixelRect.Set(PixelRect.Create(panel, "HeaderLine", art.Pixel, Palette.WallWarning, MenuView.Order + 7), -135, 154, 135, 156);
            PixelRect.Set(PixelRect.Create(panel, "StripBorder", art.Pixel, StripBorder, MenuView.Order + 7), -112, 128, 112, 151);
            PixelRect.Set(PixelRect.Create(panel, "StripFill", art.Pixel, StripFill, MenuView.Order + 8), -111, 129, 111, 150);
            tierBadge = PixelRect.Create(panel, "TierBadge", art.Pixel, Palette.Text, MenuView.Order + 9);
            PixelRect.Set(tierBadge, -106, 134, -96, 145);
            title = Text(art, "Title", 0, 195, 2);
            status = Text(art, "Status", 0, 168, 1);
            tierText = PixelText.Create(panel, "Tier", art.Font, new Vector2(-92, 133), PixelText.Align.Left, 1, MenuView.Order + 9);
            rating = Text(art, "Rating", 6, 133, 1);
            mmrText = PixelText.Create(panel, "Mmr", art.Font, new Vector2(107, 133), PixelText.Align.Right, 1, MenuView.Order + 9);
            details = Text(art, "Details", 0, -184, 1);
            banner = Text(art, "Banner", 0, 80, 3);   // the series result, big, in the space the cards leave on the result screen
            for (var i = 0; i < 3; i++)
            {
                var index = i;
                cards[i] = Button(art, "Card" + i, CardRect(i), "", () => Card(index));
                deco[i] = MakeDeco(cards[i], CardRect(i));
            }
            BuildPick();
            BuildResult();
            ballButton = Button(art, "BanBall", new Rect(-112, -58, 224, 26), "", () =>
            {
                if (busy) return;
                banBallIndex = (banBallIndex + 1) % ids.Length;
                Refresh();
            });
            arenaButton = Button(art, "BanArena", new Rect(-112, -90, 224, 26), "", () => { if (busy) return; banArenaIndex = (banArenaIndex + 1) % arenas.Length; Refresh(); });
            banBallIcon = RunUi.Icon(ballButton.transform, new Vector2(-96, -45), ballButton.IconOrder + 2);
            banBallIcon.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            arenaSwatchWall = PixelRect.Create(arenaButton.transform, "SwatchWall", art.Pixel, Palette.Wall, arenaButton.IconOrder + 2);
            PixelRect.Set(arenaSwatchWall, -105, -86, -87, -68);
            arenaSwatch = PixelRect.Create(arenaButton.transform, "Swatch", art.Pixel, Palette.Floor, arenaButton.IconOrder + 3);
            PixelRect.Set(arenaSwatch, -103, -84, -89, -70);
            setupButton = Button(art, "Setup", new Rect(-112, -124, 100, 27), "DEFENSE", () => { if (!busy) { phase = Phase.Setup; status.Set("SET YOUR DEFENSE", Palette.Text); Refresh(); } });
            actionButton = Button(art, "Action", new Rect(12, -124, 100, 27), "", Action);
            actionButton.SetColors(Palette.PrimaryFill, Palette.PrimaryFill);   // the one main action of each step
            historyButton = Button(art, "History", new Rect(-112, -158, 224, 26), "HISTORY", ToggleHistory);
            Button(art, "Back", new Rect(-60, -221, 120, 28), "BACK", Close).ClickCue = AudioIds.UiBack;
            // Outside the panel: the only control during the replay (a best-of-three can run for minutes).
            skipButton = PixelButton.Create(transform, "LeaveFight", art, new Rect(-40, -239, 80, 16), "LEAVE FIGHT", 1,
                Palette.Floor, Palette.WallWarning, Palette.Text, MenuView.Order + 6);
            skipButton.Clicked += () => { if (fighting) EndFight(); };
            skipButton.gameObject.SetActive(false);
            // Round banner over the arena while a live series plays (outside the panel, which is hidden then).
            roundBand = PixelRect.Create(transform, "RoundBand", art.Pixel, new Color32(16, 16, 26, 220), MenuView.Order + 7);
            PixelRect.Set(roundBand, -115, -18, 115, 30);
            roundBand.enabled = false;
            roundText = PixelText.Create(transform, "Round", art.Font, new Vector2(0, -12), PixelText.Align.Center, 3, MenuView.Order + 8);
            scoreText = PixelText.Create(transform, "Score", art.Font, new Vector2(0, 128), PixelText.Align.Center, 1, MenuView.Order + 8);
        }

        static readonly Color32 HeaderFill = new Color32(34, 26, 60, 255), StripFill = new Color32(26, 22, 44, 255),
            StripBorder = new Color32(90, 70, 140, 255), WinColor = new Color32(110, 220, 120, 255), LossColor = new Color32(240, 90, 80, 255),
            EvenColor = new Color32(244, 197, 66, 255);
        static readonly Color32[] AvatarColors =
        {
            new Color32(232, 72, 72, 255), new Color32(64, 208, 232, 255), new Color32(140, 224, 80, 255), new Color32(176, 96, 224, 255),
            new Color32(240, 200, 64, 255), new Color32(80, 128, 232, 255), new Color32(255, 128, 196, 255), new Color32(232, 144, 64, 255),
        };

        public const float CardHeight = 40f;
        /// <summary>Card i: 224x40, from y 86 down by 44 (cards span -2..126, under the profile strip).</summary>
        public static Rect CardRect(int i) => new Rect(-112, 86 - i * 44, 224, CardHeight);

        CardDeco MakeDeco(PixelButton card, Rect r)
        {
            var o = card.IconOrder + 1;
            var d = new CardDeco
            {
                Stripe = PixelRect.Create(card.transform, "Stripe", art.Pixel, Palette.Text, o),
                Avatar = PixelRect.Create(card.transform, "Avatar", art.Pixel, Palette.Text, o),
                Icon = RunUi.Icon(card.transform, new Vector2(r.xMin + 22, r.center.y), o + 1),
                Avatar1 = PixelText.Create(card.transform, "AvatarLetter", art.Font, new Vector2(r.xMin + 22, r.yMin + 14), PixelText.Align.Center, 1, o + 2),
                Line1 = PixelText.Create(card.transform, "Line1", art.Font, new Vector2(r.xMin + 44, r.yMin + 21), PixelText.Align.Left, 1, o + 2),
                Line2 = PixelText.Create(card.transform, "Line2", art.Font, new Vector2(r.xMin + 44, r.yMin + 6), PixelText.Align.Left, 1, o + 2),
                Right1 = PixelText.Create(card.transform, "Right1", art.Font, new Vector2(r.xMax - 6, r.yMin + 21), PixelText.Align.Right, 1, o + 2),
                Right2 = PixelText.Create(card.transform, "Right2", art.Font, new Vector2(r.xMax - 6, r.yMin + 6), PixelText.Align.Right, 1, o + 2),
            };
            PixelRect.Set(d.Stripe, r.xMin + 1, r.yMin + 2, r.xMin + 4, r.yMax - 2);
            PixelRect.Set(d.Avatar, r.xMin + 9, r.yMin + 5, r.xMin + 35, r.yMax - 5);
            return d;
        }

        void BuildPick()
        {
            pickRoot = new GameObject("Pick");
            pickRoot.transform.SetParent(panel, false);
            var t = pickRoot.transform;
            var o = MenuView.Order + 9;
            PixelRect.Set(PixelRect.Create(t, "Ring", art.Pixel, StripBorder, MenuView.Order + 7), -44, 34, 44, 112);
            PixelRect.Set(PixelRect.Create(t, "RingFill", art.Pixel, StripFill, MenuView.Order + 8), -43, 35, 43, 111);
            pickIcon = RunUi.Icon(t, new Vector2(0, 73), o);
            pickIcon.transform.localScale = new Vector3(2f, 2f, 1f);
            pickName = PixelText.Create(t, "Name", art.Font, new Vector2(0, 2), PixelText.Align.Center, 2, o);
            pickLevel = PixelText.Create(t, "Level", art.Font, new Vector2(0, -14), PixelText.Align.Center, 1, o);
            pickRoot.SetActive(false);
        }

        void BuildResult()
        {
            resultRoot = new GameObject("Result");
            resultRoot.transform.SetParent(panel, false);
            var t = resultRoot.transform;
            var o = MenuView.Order + 9;
            PixelRect.Set(PixelRect.Create(t, "Border", art.Pixel, StripBorder, MenuView.Order + 7), -112, -76, 112, 124);
            PixelRect.Set(PixelRect.Create(t, "Fill", art.Pixel, StripFill, MenuView.Order + 8), -111, -75, 111, 123);
            resultYouLabel = PixelText.Create(t, "YouLabel", art.Font, new Vector2(-104, 64), PixelText.Align.Left, 1, o);
            resultThemLabel = PixelText.Create(t, "ThemLabel", art.Font, new Vector2(104, 64), PixelText.Align.Right, 1, o);
            // 21/10 teams: one row per pair, my ball | result | their ball.
            for (var r = 0; r < PvpTuning.TeamSize; r++)
            {
                var y = 50 - r * 20;
                rowMine[r] = RunUi.Icon(t, new Vector2(-96, y), o);
                rowMine[r].transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                rowTheirs[r] = RunUi.Icon(t, new Vector2(96, y), o);
                rowTheirs[r].transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                rowMineName[r] = PixelText.Create(t, "MineName" + r, art.Font, new Vector2(-84, y - 6), PixelText.Align.Left, 1, o);
                rowTheirName[r] = PixelText.Create(t, "TheirName" + r, art.Font, new Vector2(84, y - 6), PixelText.Align.Right, 1, o);
                rowResult[r] = PixelText.Create(t, "Row" + r, art.Font, new Vector2(0, y - 6), PixelText.Align.Center, 1, o);
            }
            resultVs = PixelText.Create(t, "Vs", art.Font, new Vector2(0, 64), PixelText.Align.Center, 1, o);
            resultScore = PixelText.Create(t, "Score", art.Font, new Vector2(0, -38), PixelText.Align.Center, 3, o);
            resultDelta = PixelText.Create(t, "Delta", art.Font, new Vector2(0, -54), PixelText.Align.Center, 1, o);
            resultArena = PixelText.Create(t, "Arena", art.Font, new Vector2(0, -70), PixelText.Align.Center, 1, o);
            resultRoot.SetActive(false);
        }

        /// <summary>Rank tier for an MMR: name and colour (BRONZE under 1000, SILVER, GOLD from 1200, DIAMOND from 1400).</summary>
        public static (string name, Color32 color) Tier(int mmr) =>
            mmr >= 1400 ? ("DIAMOND", new Color32(120, 210, 255, 255)) : mmr >= 1200 ? ("GOLD", new Color32(244, 197, 66, 255)) :
            mmr >= 1000 ? ("SILVER", new Color32(200, 205, 220, 255)) : ("BRONZE", new Color32(205, 127, 70, 255));

        static Color32 Dim(Color32 c) => new Color32((byte)(Palette.Floor.r + (c.r - Palette.Floor.r) * 0.2f),
            (byte)(Palette.Floor.g + (c.g - Palette.Floor.g) * 0.2f), (byte)(Palette.Floor.b + (c.b - Palette.Floor.b) * 0.2f), 255);

        static string Tr(string s) => UiVietnamese.Translate(s);

        /// <summary>Fill card i: accent colour (stripe, border, tint), a ball icon or a lettered avatar, and up to four texts.</summary>
        void SetCard(int i, Color32 accent, string ballId, string avatar, string line1, Color32 c1, string line2, Color32 c2,
            string right1, Color32 r1, string right2, Color32 r2)
        {
            var d = deco[i];
            cards[i].SetText("", Palette.Text);
            cards[i].SetColors(Dim(accent), accent);
            d.Stripe.color = accent;
            d.Icon.sprite = ballId != null ? art.Get(ballId).Ball : null;
            d.Avatar.enabled = avatar != null;
            d.Avatar.color = accent;
            d.Avatar1.Set(avatar ?? "", Palette.PrimaryText);
            d.Line1.Set(line1, c1);
            d.Line2.Set(line2, c2);
            d.Right1.Set(right1, r1);
            d.Right2.Set(right2, r2);
        }

        static string Initial(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            var n = name.StartsWith("BOT ") && name.Length > 4 ? name.Substring(4) : name;
            return n.Substring(0, 1);
        }

        static Color32 AvatarColor(string name)
        {
            var h = 0;
            foreach (var ch in name ?? "") h = h * 31 + ch;
            return AvatarColors[(h & 0x7fffffff) % AvatarColors.Length];
        }

        /// <summary>Player-facing ball name (EMBER, HUNTER...) for a weapon id.</summary>
        static string BallName(string id) => string.IsNullOrEmpty(id) ? "-" : WeaponNames.Of(id);
        /// <summary>Player-facing arena name (English source; the ui-vi templates around it translate it).</summary>
        static string ArenaName(string id)
        {
            foreach (var a in ArenaRegistry.All) if (a.Id == id) return a.DisplayName;
            return "-";
        }

        PixelText Text(ArtLibrary art, string name, int x, int y, int scale) =>
            PixelText.Create(panel, name, art.Font, new Vector2(x, y), PixelText.Align.Center, scale, MenuView.Order + 9);

        PixelButton Button(ArtLibrary art, string name, Rect rect, string label, Action click)
        {
            // PixelButton only builds a label when created with text; these labels are all set later by Refresh.
            var button = PixelButton.Create(panel, name, art, rect, string.IsNullOrEmpty(label) ? " " : label, 1, Palette.Floor, Palette.WallWarning,
                Palette.Text, MenuView.Order + 7);
            button.Clicked += click;
            return button;
        }

        public void Open()
        {
            if (!AccountSession.HasAccount) { AccountNeeded?.Invoke(); return; }
            gameObject.SetActive(true);
            panel.gameObject.SetActive(true);
            token = PlayerPrefs.GetString("pvp_token", "");
            phase = Phase.Loading;
            status.Set("CONNECTING...", Palette.Text);
            Refresh();
            StartCoroutine(Begin());
            PixelButton.NoteShownAll(panel);
        }

        public void Close()
        {
            StopAllCoroutines();
            UnhookFight();   // a fight left like this settles on the server as "no more presses"
            arena.Paused = true;
            busy = false;
            skipButton.gameObject.SetActive(false);
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        IEnumerator Begin()
        {
            busy = true;
            offline = false;
            profile = null;
            status.Set("CONNECTING...", Palette.Text);
            Refresh();
            if (!string.IsNullOrEmpty(token))
            {
                yield return Request("GET", "/v1/me", null, response => profile = JsonUtility.FromJson<Profile>(response));
                if (profile == null && lastStatus == 401)
                {
                    // The server no longer knows this token (signed out elsewhere, data reset): sign in again.
                    token = "";
                    AccountSession.SignOutLocal();
                }
            }
            if (string.IsNullOrEmpty(token))
            {
                busy = false;
                Close();
                AccountNeeded?.Invoke();
                yield break;
            }
            busy = false;
            if (profile == null) yield break;
            if (profile.roster != null && profile.roster.Length == 3)
                for (var i = 0; i < 3; i++) { var index = Array.IndexOf(ids, profile.roster[i]); if (index >= 0) rosterIndex[i] = index; }
            var ballBan = Array.IndexOf(ids, profile.banBall);
            var arenaBan = Array.IndexOf(arenas, profile.banArena);
            if (ballBan >= 0) banBallIndex = ballBan;
            if (arenaBan >= 0) banArenaIndex = arenaBan;
            if (!string.IsNullOrEmpty(profile.activeChallengeId)) yield return RestoreChallenge(profile.activeChallengeId);
            else if (profile.ready) yield return LoadOffers();
            else { phase = Phase.Setup; status.Set("SET YOUR DEFENSE", Palette.Text); Refresh(); }
        }

        IEnumerator RestoreChallenge(string id)
        {
            challengeId = id;
            busy = true;
            yield return Request("GET", "/v1/challenges/" + id, null, response =>
            {
                var reply = JsonUtility.FromJson<ChallengeReply>(response);
                if (reply.state == "pick") ApplyReveal(reply);
                else if (reply.state == "fight") { fight = JsonUtility.FromJson<FightReply>(response); phase = Phase.Pick; }
                else if (reply.state == "done") ShowResult(JsonUtility.FromJson<ResultReply>(response));
                else if (reply.state == "ban") { opponentName = reply.opponentName; phase = Phase.Ban; status.Set("BLIND BAN: 1 BALL + 1 ARENA", Palette.Text); }
                else phase = Phase.Loading;   // expired: start over with offers
            });
            busy = false;
            if (phase == Phase.Loading) { yield return LoadOffers(); yield break; }
            // A fight that was left (app closed, page closed): it is settled as it stands, never replayed from the start.
            if (fight != null && fight.challengeId == challengeId) { fightTaps.Clear(); fightHashes.Clear(); yield return SendFight(); yield break; }
            Refresh();
        }

        IEnumerator LoadOffers()
        {
            busy = true;
            phase = Phase.Loading;
            Refresh();
            yield return Request("GET", "/v1/offers", null, response =>
            {
                var offers = JsonUtility.FromJson<OfferReply>(response);
                offerId = offers.offerId;
                opponents = offers.opponents;
                offerRefreshSeconds = offers.refreshSeconds;
                offersLoadedAt = Time.realtimeSinceStartup;
                phase = Phase.Offers;
                status.Set(opponents != null && opponents.Length == 3 ? "CHOOSE 1 OF 3 PLAYERS" : "NEED 3 REAL PLAYERS", Palette.Text);
            });
            busy = false;
            Refresh();
        }

        void Card(int i)
        {
            if (busy) return;
            if (phase == Phase.Setup)
            {
                var used = new HashSet<string>();
                for (var n = 0; n < 3; n++) if (n != i) used.Add(ids[rosterIndex[n]]);
                for (var n = 1; n <= ids.Length; n++)
                {
                    var next = (rosterIndex[i] + n) % ids.Length;
                    if (used.Contains(ids[next])) continue;
                    rosterIndex[i] = next;
                    break;
                }
                Refresh();
            }
            else if (phase == Phase.Offers && opponents != null && i < opponents.Length)
                StartCoroutine(Choose(opponents[i]));
            else if (phase == Phase.Pick && legal != null && legal.Length >= PvpTuning.TeamSize)
            {
                // Next legal ball that no other slot holds.
                for (var n = 1; n <= legal.Length; n++)
                {
                    var next = (team[i] + n) % legal.Length;
                    if (next == team[(i + 1) % 3] || next == team[(i + 2) % 3]) continue;
                    team[i] = next;
                    break;
                }
                Refresh();
            }
            else if (phase == Phase.History && history != null && historyPage * 3 + i < history.Length)
                ShowResult(history[historyPage * 3 + i]);
        }

        void Action()
        {
            if (busy) return;
            if (phase == Phase.Loading && offline) StartCoroutine(Begin());
            else if (phase == Phase.Setup) StartCoroutine(Publish());
            else if (phase == Phase.Offers) StartCoroutine(LoadOffers());
            else if (phase == Phase.Ban) StartCoroutine(Ban());
            else if (phase == Phase.Pick && legal != null && legal.Length >= PvpTuning.TeamSize)
                StartCoroutine(Pick(new[] { legal[team[0]], legal[team[1]], legal[team[2]] }));
            else if (phase == Phase.Result) StartCoroutine(LoadOffers());
            else if (phase == Phase.History && history != null && history.Length > 3)
            {
                historyPage = (historyPage + 1) % ((history.Length + 2) / 3);
                Refresh();
            }
        }

        IEnumerator Publish()
        {
            busy = true;
            var progress = store.Load().Progress;
            var balls = new List<BallProgress>();
            foreach (var id in ids) balls.Add(MetaRules.Ball(progress, id));
            var body = new PublishBody { balls = balls, roster = new[] { ids[rosterIndex[0]], ids[rosterIndex[1]], ids[rosterIndex[2]] },
                banBall = ids[banBallIndex], banArena = arenas[banArenaIndex] };
            yield return Request("PUT", "/v1/me/defense", JsonUtility.ToJson(body), response => profile = JsonUtility.FromJson<Profile>(response));
            busy = false;
            if (profile != null && profile.ready) yield return LoadOffers();
        }

        IEnumerator Choose(Opponent opponent)
        {
            busy = true;
            var body = new NameAndId { offerId = offerId, opponentId = opponent.id };
            yield return Request("POST", "/v1/challenges", JsonUtility.ToJson(body), response =>
            {
                challengeId = JsonUtility.FromJson<ChallengeReply>(response).challengeId;
                opponentName = opponent.name;
                phase = Phase.Ban;
                status.Set("BLIND BAN: 1 BALL + 1 ARENA", Palette.Text);
            });
            busy = false;
            Refresh();
        }

        IEnumerator Ban()
        {
            busy = true;
            var body = new BanBody { ballId = ids[banBallIndex], arenaId = arenas[banArenaIndex] };
            yield return Request("POST", "/v1/challenges/" + challengeId + "/ban", JsonUtility.ToJson(body), response =>
            {
                ApplyReveal(JsonUtility.FromJson<ChallengeReply>(response));
            });
            busy = false;
            Refresh();
        }

        void ApplyReveal(ChallengeReply reply)
        {
            legal = reply.legalBalls;
            BestTeam();
            phase = Phase.Pick;
            status.Set("PICK YOUR TEAM OF 3", Palette.Text);
            details.Set(string.Format("THEIR BANS: {0} / {1}", BallName(reply.defenderBanBall), ArenaName(reply.defenderBanArena)), LossColor);
        }

        IEnumerator Pick(string[] ballIds)
        {
            busy = true;
            result = null;
            fight = null;
            yield return Request("POST", "/v1/challenges/" + challengeId + "/pick", JsonUtility.ToJson(new PickBody { ballIds = ballIds }), response =>
            {
                var state = JsonUtility.FromJson<ChallengeReply>(response).state;
                if (state == "done") Settled(JsonUtility.FromJson<ResultReply>(response));
                else fight = JsonUtility.FromJson<FightReply>(response);
            });
            busy = false;
            Refresh();
            if (fight != null) BeginFight();
        }

        /// <summary>A settled series from the server: result screen, and the strip's MMR and record move with it.</summary>
        void Settled(ResultReply reply)
        {
            if (profile != null)
            {
                profile.mmr = reply.mmrAfter;
                if (reply.winner == 0) profile.wins++;
                else if (reply.winner == 1) profile.losses++;
            }
            ShowResult(reply);   // refreshes the strip too
        }

        /// <summary>Result screen for a settled series (just played, restored after a restart, or opened from history).</summary>
        void ShowResult(ResultReply reply)
        {
            result = reply;
            phase = Phase.Result;
            var outcome = reply.winner == 0 ? "VICTORY" : reply.winner == 1 ? "DEFEAT" : "DRAW";
            var outcomeColor = reply.winner == 0 ? WinColor : reply.winner == 1 ? LossColor : EvenColor;
            banner.Set(outcome, outcomeColor);
            status.Set(reply.autoPicked ? "AUTO: " + outcome : "", outcomeColor);
            details.Set("", Palette.Text);
            resultYouLabel.Set(string.IsNullOrEmpty(AccountSession.Name) ? "YOU" : Short(AccountSession.Name), Palette.TextDim);
            resultThemLabel.Set(Short(reply.opponentName ?? opponentName), Palette.TextDim);
            resultVs.Set("VS", Palette.WallWarning);
            var mine = reply.challengerTeam != null && reply.challengerTeam.Length > 0 ? reply.challengerTeam : new[] { reply.challengerBall };
            var theirs = reply.defenderTeam != null && reply.defenderTeam.Length > 0 ? reply.defenderTeam : new[] { reply.defenderBall };
            for (var r = 0; r < PvpTuning.TeamSize; r++)
            {
                var a = r < mine.Length ? mine[r] : null;
                var b = r < theirs.Length ? theirs[r] : null;
                rowMine[r].sprite = string.IsNullOrEmpty(a) ? null : art.Get(a).Ball;
                rowTheirs[r].sprite = string.IsNullOrEmpty(b) ? null : art.Get(b).Ball;
                rowMineName[r].Set(string.IsNullOrEmpty(a) ? "" : BallName(a), string.IsNullOrEmpty(a) ? Palette.Text : Palette.Look(a).Body);
                rowTheirName[r].Set(string.IsNullOrEmpty(b) ? "" : BallName(b), string.IsNullOrEmpty(b) ? Palette.Text : Palette.Look(b).Body);
                // The pair's last decided round (a draw replays the pair); not reached: dim dash.
                var outcomeOfPair = -2;
                if (reply.roundPairs != null && reply.roundWinners != null)
                    for (var k = 0; k < reply.roundPairs.Length && k < reply.roundWinners.Length; k++)
                        if (reply.roundPairs[k] == r) outcomeOfPair = reply.roundWinners[k];
                rowResult[r].Set(outcomeOfPair == 0 ? "WON" : outcomeOfPair == 1 ? "LOST" : outcomeOfPair == -1 ? "DRAW" : "-",
                    outcomeOfPair == 0 ? WinColor : outcomeOfPair == 1 ? LossColor : outcomeOfPair == -1 ? EvenColor : Palette.TextDim);
            }
            resultScore.Set(reply.challengerWins + " - " + reply.defenderWins, outcomeColor);
            resultDelta.Set("MMR " + reply.mmrAfter + "  " + (reply.delta >= 0 ? "+" : "") + reply.delta, reply.delta >= 0 ? WinColor : LossColor);
            resultArena.Set(string.Format("ARENA: {0}", ArenaName(reply.arenaId)), Palette.TextDim);
            Refresh();
        }

        /// <summary>Default team: the three highest-level legal balls, strongest first.</summary>
        void BestTeam()
        {
            if (legal == null || legal.Length < PvpTuning.TeamSize) return;
            var progress = store.Load().Progress;
            var order = new List<int>();
            for (var i = 0; i < legal.Length; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                var c = MetaRules.Ball(progress, legal[b]).Level.CompareTo(MetaRules.Ball(progress, legal[a]).Level);
                return c != 0 ? c : a.CompareTo(b);
            });
            for (var i = 0; i < PvpTuning.TeamSize; i++) team[i] = order[i];
        }

        void ToggleHistory()
        {
            if (busy) return;
            if (phase == Phase.History)
            {
                // Back to where HISTORY was opened, with that screen's own status line.
                if (beforeHistory == Phase.Result && result != null) ShowResult(result);
                else StartCoroutine(LoadOffers());   // the server returns the same live offers
                return;
            }
            StartCoroutine(LoadHistory());
        }

        IEnumerator LoadHistory()
        {
            busy = true;
            var from = phase;
            yield return Request("GET", "/v1/history", null, response =>
            {
                history = JsonUtility.FromJson<HistoryReply>("{\"items\":" + response + "}").items ?? new ResultReply[0];
                historyPage = 0;
                beforeHistory = from;
                phase = Phase.History;
                status.Set(history.Length == 0 ? "NO MATCHES YET" : "LAST MATCHES", Palette.Text);
            });
            busy = false;
            Refresh();
        }

        static string HistoryLine(ResultReply r) =>
            (r.winner == 0 ? "WON: " : r.winner == 1 ? "LOST: " : "DRAW: ") + Short(r.opponentName) + " " +
            BallName(r.challengerBall) + " " + (r.delta >= 0 ? "+" : "") + r.delta;

        static string Short(string name) => string.IsNullOrEmpty(name) ? "?" : name.Length > 10 ? name.Substring(0, 10) : name;

        // ---- 21/10 live PvP ----

        static PvpBallSnapshot[] Snapshots(WireBall[] balls)
        {
            if (balls == null || balls.Length != PvpTuning.TeamSize) throw new ArgumentException("A team needs three balls.");
            var a = new PvpBallSnapshot[balls.Length];
            for (var i = 0; i < a.Length; i++) a[i] = new PvpBallSnapshot(balls[i].ToProgress());
            return a;
        }

        void BeginFight()
        {
            if (fight.rulesVersion != SimVersion.Rules)
            {
                status.Set("UPDATE THE GAME TO PLAY PVP", Palette.WallWarning);
                return;
            }
            try
            {
                fightDraft = PvpDraft.ForFight(fight.arenaId, Snapshots(fight.challengerProgress), Snapshots(fight.defenderProgress));
            }
            catch (ArgumentException e)
            {
                Debug.LogWarning("[PvP] fight not playable: " + e.Message);
                StartCoroutine(SendFight());
                return;
            }
            fightTaps.Clear();
            fightHashes.Clear();
            fightRound = fightPair = myWins = theirWins = 0;
            fighting = true;
            panel.gameObject.SetActive(false);
            skipButton.gameObject.SetActive(true);
            arena.BeforeSimStep += OnFightStep;
            arena.MatchEnded += OnFightRoundEnded;
            if (Tap != null) Tap.Requesting += OnFightTap;
            StartFightRound();
        }

        void StartFightRound()
        {
            if (!PvpSeries.Continues(fightRound, fightPair, myWins, theirWins)) { EndFight(); return; }
            var sim = PvpSeries.CreateRound(fightDraft, fight.seed, fightRound, fightPair, out myIndex);
            fightSteps = 0;
            roundLive = false;
            arena.MarkedBall = myIndex;
            arena.StartMatch(sim, myIndex == 0 ? HudView.PlayerTag : "THEM", myIndex == 0 ? "THEM" : HudView.PlayerTag);
            arena.Paused = true;
            if (Tap != null) Tap.PlayerIndex = myIndex;
            roundIntro = RoundIntroSeconds;
            roundText.Set(string.Format("ROUND {0}", fightRound + 1), Palette.WallWarning);
            roundBand.enabled = true;
            scoreText.Set(ScoreLine(), Palette.Text);
        }

        string ScoreLine() => (string.IsNullOrEmpty(AccountSession.Name) ? Tr("YOU") : Short(AccountSession.Name)) +
            "  " + myWins + " - " + theirWins + "  " + Short(fight.opponentName);

        void Update()
        {
            if (!fighting) return;
            var dt = Time.unscaledDeltaTime;
            if (roundIntro > 0f && !GuideView.Blocking)
            {
                roundIntro -= dt;
                if (roundIntro <= 0f)
                {
                    roundText.Set("", Palette.Text);
                    roundBand.enabled = false;
                    arena.Paused = false;
                    roundLive = true;
                }
            }
            Tap?.TickLive(roundLive && arena.Sim != null && arena.Sim.Outcome == MatchOutcome.Ongoing, dt);
        }

        /// <summary>ArenaView is about to run one step of the round: count it (a press is recorded against the next step).</summary>
        void OnFightStep(MatchSim sim) => fightSteps++;

        /// <summary>A press on the arena: the server will apply it before the same step (taps: round, step, round, step...).</summary>
        void OnFightTap(MatchSim sim)
        {
            if (!fighting || !roundLive || sim != arena.Sim) return;
            if (fightTaps.Count / 2 >= PvpSeries.MaxTaps) return;
            fightTaps.Add(fightRound);
            fightTaps.Add(fightSteps);
        }

        void OnFightRoundEnded(int winner)
        {
            if (!fighting || !roundLive) return;
            roundLive = false;
            arena.Paused = true;
            fightHashes.Add(arena.Sim.ComputeHash().ToString("X16"));
            var w = arena.Sim.WinnerIndex < 0 ? -1 : arena.Sim.WinnerIndex == myIndex ? 0 : 1;
            if (w == 0) myWins++;
            else if (w == 1) theirWins++;
            fightRound++;
            fightPair = PvpSeries.NextPair(fightPair, w);
            scoreText.Set(ScoreLine(), Palette.Text);
            StartCoroutine(NextFightRound());
        }

        IEnumerator NextFightRound()
        {
            yield return new WaitForSecondsRealtime(1.25f);
            if (IsOpen && fighting) StartFightRound();
        }

        void UnhookFight()
        {
            arena.BeforeSimStep -= OnFightStep;
            arena.MatchEnded -= OnFightRoundEnded;
            if (Tap != null) { Tap.Requesting -= OnFightTap; Tap.TickLive(false, 0f); Tap.PlayerIndex = 0; }
            fighting = roundLive = false;
            roundText.Set("", Palette.Text);
            roundBand.enabled = false;
            scoreText.Set("", Palette.Text);
            arena.Paused = true;
            skipButton.gameObject.SetActive(false);
        }

        /// <summary>The series is over (or the player left it): the server simulates it with the recorded presses.</summary>
        void EndFight()
        {
            UnhookFight();
            panel.gameObject.SetActive(true);
            PixelButton.NoteShownAll(panel);
            StartCoroutine(SendFight());
        }

        IEnumerator SendFight()
        {
            busy = true;
            status.Set("CONNECTING...", Palette.Text);
            var body = new FightBody { rulesVersion = SimVersion.Rules, taps = fightTaps.ToArray() };
            ResultReply reply = null;
            yield return Request("POST", "/v1/challenges/" + challengeId + "/fight", JsonUtility.ToJson(body),
                response => reply = JsonUtility.FromJson<ResultReply>(response));
            busy = false;
            fight = null;
            if (reply == null) { Refresh(); yield break; }
            // Same sim, same presses: the server's rounds must match the ones just played.
            for (var i = 0; i < fightHashes.Count; i++)
                if (reply.roundHashes == null || i >= reply.roundHashes.Length || reply.roundHashes[i] != fightHashes[i])
                {
                    Debug.LogWarning($"[PvP] round {i + 1} differs from the server ({fightHashes[i]} vs {(reply.roundHashes != null && i < reply.roundHashes.Length ? reply.roundHashes[i] : "-")})");
                    break;
                }
            Settled(reply);
        }

        IEnumerator Request(string method, string route, string body, Action<string> receive)
        {
            var configured = PlayerPrefs.GetString("pvp_server_url", "");
            if (string.IsNullOrWhiteSpace(configured))
                configured = Resources.Load<TextAsset>("pvp-server")?.text?.Trim();
            if (string.IsNullOrWhiteSpace(configured)) configured = "http://127.0.0.1:5088";
            if (!Uri.TryCreate(configured, UriKind.Absolute, out var endpoint) ||
                (endpoint.Scheme != Uri.UriSchemeHttps && !(endpoint.Scheme == Uri.UriSchemeHttp && endpoint.IsLoopback)))
            {
                status.Set("PVP NEEDS HTTPS SERVER", Palette.WallWarning);
                offline = true;
                Refresh();
                yield break;
            }
            var url = configured.TrimEnd('/') + route;
            // Render's free plan sleeps after 15 idle minutes and takes about a minute to wake: wait long enough, say so,
            // and retry a gateway error (502/503/504 or no answer) while the server is still starting.
            var started = Time.realtimeSinceStartup;
            for (var attempt = 0; ; attempt++)
            using (var request = new UnityWebRequest(url, method))
            {
                if (body != null) request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", "Bearer " + token);
                request.timeout = WakeTimeoutSeconds;
                var operation = request.SendWebRequest();
                var warned = false;
                while (!operation.isDone)
                {
                    if (!warned && Time.realtimeSinceStartup - started > WakeNoticeSeconds)
                    {
                        warned = true;
                        status.Set(ServerWaking, Palette.WallWarning);
                    }
                    yield return null;
                }
                lastStatus = request.responseCode;
                var gateway = request.result == UnityWebRequest.Result.ConnectionError || lastStatus == 502 || lastStatus == 503 || lastStatus == 504;
                if (gateway && attempt < 3 && Time.realtimeSinceStartup - started < WakeTimeoutSeconds)
                {
                    status.Set(ServerWaking, Palette.WallWarning);
                    var until = Time.realtimeSinceStartup + 3f;
                    while (Time.realtimeSinceStartup < until) yield return null;
                    continue;
                }
                if (warned && request.result == UnityWebRequest.Result.Success) status.Set("", Palette.Text);
                if (request.result != UnityWebRequest.Result.Success)
                {
                    ErrorReply error = null;
                    try { error = JsonUtility.FromJson<ErrorReply>(request.downloadHandler.text); }
                    catch (ArgumentException) { }
                    status.Set(error != null && !string.IsNullOrEmpty(error.error) ? error.error.ToUpperInvariant() : "PVP SERVER UNAVAILABLE", Palette.WallWarning);   // the font is upper case
                    offline = phase == Phase.Loading;   // nothing loaded yet: offer RETRY instead of a blank page
                    Refresh();
                    yield break;
                }
                offline = false;
                receive?.Invoke(request.downloadHandler.text);
                yield break;
            }
        }

        /// <summary>Longest wait for one reply: a sleeping Render free server needs about a minute to start.</summary>
        public const int WakeTimeoutSeconds = 90;
        /// <summary>After this long without a reply the status line explains the wait.</summary>
        public const float WakeNoticeSeconds = 4f;
        public const string ServerWaking = "CONNECTING...";

        void Refresh()
        {
            title.Set("PVP RANKED", Palette.WallWarning);
            if (phase != Phase.Result) banner.Set("", Palette.Text);
            // Profile strip: tier badge + tier, name and record, MMR.
            if (profile != null)
            {
                var tier = Tier(profile.mmr);
                tierBadge.enabled = true;
                tierBadge.color = tier.color;
                tierText.Set(tier.name, tier.color);
                rating.Set(Short(profile.name) + "  " + profile.wins + "-" + profile.losses, Palette.Text);
                mmrText.Set("MMR " + profile.mmr, Palette.WallWarning);
            }
            else
            {
                tierBadge.enabled = false;
                tierText.Set("", Palette.Text);
                rating.Set("", Palette.Text);
                mmrText.Set("", Palette.Text);
            }
            if (phase == Phase.Offers && opponents != null && opponents.Length == 3)
            {
                var left = Mathf.Max(0, offerRefreshSeconds - (int)(Time.realtimeSinceStartup - offersLoadedAt));
                details.Set(string.Format("NEW PLAYERS IN {0} MIN", (left + 59) / 60), Palette.TextDim);
            }
            else if (phase == Phase.History)
                details.Set(history != null && history.Length > 3 ? (historyPage + 1) + "/" + ((history.Length + 2) / 3) : "", Palette.TextDim);
            else if (phase == Phase.Setup) details.Set("", Palette.TextDim);
            else if (phase != Phase.Pick && phase != Phase.Result) details.Set("", Palette.Text);
            var progress = phase == Phase.Setup || phase == Phase.Pick ? store.Load().Progress : null;
            for (var i = 0; i < 3; i++)
            {
                var h = historyPage * 3 + i;
                var on = true;
                if (phase == Phase.Setup)
                {
                    var id = ids[rosterIndex[i]];
                    var look = Palette.Look(id).Body;
                    SetCard(i, look, id, null, BallName(id), look, string.Format("LV {0}", MetaRules.Ball(progress, id).Level), Palette.TextDim,
                        Tr("DEFENDER") + " " + (i + 1), Palette.WallWarning, Tr("TAP TO CHANGE"), Palette.TextDim);
                }
                else if (phase == Phase.Offers && opponents != null && i < opponents.Length)
                {
                    var o = opponents[i];
                    var mine = profile != null ? profile.mmr : o.mmr;
                    var gap = o.mmr - mine;
                    var (tag, tagColor) = gap >= 50 ? ("HARD", LossColor) : gap <= -50 ? ("EASY", WinColor) : ("EVEN", EvenColor);
                    var tier = Tier(o.mmr);
                    SetCard(i, tagColor, null, Initial(o.name), Short(o.name), AvatarColor(o.name) , "MMR " + o.mmr + "  " + Tr(tier.name), tier.color,
                        Tr(tag), tagColor, "+" + o.winDelta + " / " + o.lossDelta, Palette.Text);
                    deco[i].Avatar.color = AvatarColor(o.name);
                    deco[i].Line1.Set(Short(o.name), Palette.Text);
                }
                else if (phase == Phase.Pick && legal != null && legal.Length >= PvpTuning.TeamSize)
                {
                    var id = legal[team[i]];
                    var look = Palette.Look(id).Body;
                    SetCard(i, look, id, null, BallName(id), look, string.Format("LV {0}", MetaRules.Ball(progress, id).Level), Palette.TextDim,
                        string.Format("ROUND {0}", i + 1), Palette.WallWarning, Tr("TAP TO CHANGE"), Palette.TextDim);
                }
                else if (phase == Phase.Ban && i == 0 && !string.IsNullOrEmpty(opponentName))
                {
                    SetCard(0, LossColor, null, Initial(opponentName), Short(opponentName), Palette.Text, Tr("OPPONENT"), Palette.TextDim,
                        Tr("BLIND BAN"), LossColor, "", Palette.Text);
                    deco[0].Avatar.color = AvatarColor(opponentName);
                }
                else if (phase == Phase.History && history != null && h < history.Length)
                {
                    var r = history[h];
                    var (word, c) = r.winner == 0 ? ("WON", WinColor) : r.winner == 1 ? ("LOST", LossColor) : ("DRAW", EvenColor);
                    SetCard(i, c, r.challengerBall, null, Tr(word) + "  " + r.challengerWins + "-" + r.defenderWins, c,
                        string.Format("VS {0}", Short(r.opponentName)), Palette.TextDim,
                        (r.delta >= 0 ? "+" : "") + r.delta, r.delta >= 0 ? WinColor : LossColor, BallName(r.challengerBall), Palette.Look(r.challengerBall).Body);
                }
                else on = false;
                cards[i].gameObject.SetActive(on);
            }
            var bansVisible = phase == Phase.Setup || phase == Phase.Ban;
            ballButton.gameObject.SetActive(bansVisible);
            arenaButton.gameObject.SetActive(bansVisible);
            pickRoot.SetActive(false);   // 21/10 teams: the three cards are the pick
            resultRoot.SetActive(phase == Phase.Result);
            if (bansVisible)
            {
                var banned = ids[banBallIndex];
                ballButton.SetColors(Dim(LossColor), LossColor);
                arenaButton.SetColors(Dim(LossColor), LossColor);
                ballButton.SetText(string.Format("BAN BALL: {0}", BallName(banned)), Palette.Text);
                arenaButton.SetText(string.Format("BAN ARENA: {0}", ArenaName(arenas[banArenaIndex])), Palette.Text);
                banBallIcon.enabled = true;
                banBallIcon.sprite = art.Get(banned).Ball;
                var floor = Palette.Floor; var wall = Palette.Wall;
                if (art.TryArena(arenas[banArenaIndex], out var a)) { floor = a.FloorColor; wall = a.WallColor; }
                arenaSwatch.color = floor;
                arenaSwatchWall.color = wall;
            }

            setupButton.gameObject.SetActive(phase == Phase.Offers);
            historyButton.gameObject.SetActive(phase == Phase.Offers || phase == Phase.Result || phase == Phase.History);
            historyButton.SetText(phase == Phase.History ? "BACK TO PVP" : "HISTORY", Palette.Text);
            actionButton.gameObject.SetActive((phase == Phase.Loading && offline) || phase == Phase.Setup || phase == Phase.Offers || phase == Phase.Ban ||
                phase == Phase.Pick || phase == Phase.Result || (phase == Phase.History && history != null && history.Length > 3));
            actionButton.SetText(phase == Phase.Loading ? "TRY AGAIN" : phase == Phase.Setup ? "PUBLISH" : phase == Phase.Offers ? "REFRESH" :
                phase == Phase.Ban ? "LOCK BANS" : phase == Phase.Pick ? "FIGHT" : phase == Phase.History ? "NEXT" : "MORE PVP", Palette.PrimaryText);
        }
    }
}
