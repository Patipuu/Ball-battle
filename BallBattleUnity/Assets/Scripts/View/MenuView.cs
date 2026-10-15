using System;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using BallBattle.Sim.Weapons;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Main menu. Hub page: RUN (or CONTINUE RUN when a run is saved) as the main button, the Run rules line,
    /// the NEXT GOAL box and a smaller VERSUS button. VERSUS opens the Versus page: pick a weapon for ball 1 and
    /// ball 2 (mirror allowed), arena, RANDOM, START (raises StartRequested(weaponA, weaponB)), BACK to the hub.
    /// The gear (12x12 at (250,8)) opens the Settings page (SettingsView: volumes, language row, BACK).
    /// Positions in comments are 270x480 screen pixels, origin top-left.
    /// </summary>
    public sealed class MenuView : MonoBehaviour
    {
        public const int Order = 60;
        const int Cols = 4;
        const int TileW = 62;
        const int TileH = 34;
        const int Gap = 6;

        /// <summary>Main button (24,160) 222x48.</summary>
        public static readonly Rect RunArea = new Rect(-111, 32, 222, 48);
        /// <summary>UI-MENU1: one column of same-size buttons under the NEXT GOAL box, each with a one-line explanation under it.</summary>
        /// <summary>BB-12c: BALLS joins the column at the top; the four buttons are 26 px tall, 40 px apart.</summary>
        public static readonly Rect BallsArea = new Rect(-71, -94, 142, 26);
        public static readonly Rect VersusArea = new Rect(-71, -136, 142, 26);
        /// <summary>UI-UX2: CODEX matches VERSUS (same size and label scale).</summary>
        public static readonly Rect CodexArea = new Rect(-71, -178, 142, 26);
        public static readonly Rect HowToArea = new Rect(-71, -222, 142, 26);
        public const string BallsLine = "LEVEL UP WITH CHEST SHARDS";
        public const string VersusLine = "WATCH ANY TWO BALLS FIGHT";
        public const string CodexLine = "WHAT EVERY BALL DOES";
        public const string StartHere = "NEW? START HERE";
        /// <summary>Title letters take the ball colours in turn (space skipped).</summary>
        public static readonly string[] TitleColors = { "blade", "volley", "fang", "pike", "venom", "aegis", "rig", "blade", "volley", "fang" };
        public static readonly Rect VersusBackArea = new Rect(-60, -214, 120, 32);

        public event Action<string, string> StartRequested;
        /// <summary>RUN with no saved run: open Run mode on its title screen.</summary>
        public event Action RunRequested;
        /// <summary>CONTINUE RUN: open Run mode and resume the saved run.</summary>
        public event Action ContinueRequested;
        /// <summary>BB-12b: the OPEN CHEST button (welcome chests waiting in ProgressData.PendingChests).</summary>
        public event Action ChestRequested;
        /// <summary>BB-12b: top-left of the hub, shown only while chests wait.</summary>
        public static readonly Rect ChestArea = new Rect(-127, 206, 76, 24);
        public PixelButton ChestButton { get; private set; }
        /// <summary>BB-12c: the BALLS page and its button ("UP!" badge when a ball can level now).</summary>
        public PixelButton BallsButton { get; private set; }
        public BallsView Balls => balls;
        public bool BallsBadgeShown => ballsBadge != null && ballsBadge.Text.Length > 0;
        /// <summary>Set by GameController: BALLS reads and saves progress through it.</summary>
        public ISaveStore Store { get; set; }
        /// <summary>BALLS closed (GameController refreshes the hub: gold, badge).</summary>
        public event Action BallsClosed;
        BallsView balls;
        PixelText ballsBadge;

        readonly string[] selected = new string[2];
        PixelButton[][] tiles;
        PixelText versus;
        PixelText arenaLabel;
        SpriteRenderer arenaThumbnail;
        Sprite spikeWallsThumbnail;
        int arenaIndex;
        Transform hub, versusPage;
        PixelButton runButton;
        PixelText runLabel, continueLabel, continueLine;
        NextGoalBox goal;
        RunState savedRun;
        SettingsView settings;
        SkillCodexView codex;
        readonly WeaponProfileView[] profiles = new WeaponProfileView[2];
        Transform noticePage;
        Action noticeOk;
        // UI-MENU1: lively hub
        PixelText title;
        MenuBackdrop backdrop;
        Transform startHere;
        SpriteRenderer runShine;
        HowToPlayView howTo;
        public MenuBackdrop Backdrop => backdrop;
        public PixelText Title => title;
        public bool StartHereShown => startHere != null && startHere.gameObject.activeSelf;
        public HowToPlayView HowTo => howTo;
        public SpriteRenderer RunShine => runShine;

        /// <summary>Settings page closed (BACK, Esc, any other page): volumes are saved then, if they changed.</summary>
        public event Action SettingsClosed;

        /// <summary>One-time "OLD RUN ENDED BY UPDATE" panel: box (8,200) 254x64, OK button 80x24 (BB-FONT1: text scale 1).</summary>
        public static readonly Rect NoticeOkArea = new Rect(-40, 240 - 200 - 58, 80, 24);
        /// <summary>Sorting base of the notice: above everything on the hub (which tops out at Order + 5).</summary>
        public const int NoticeOrder = Order + 10;

        /// <summary>Arena picked for Versus (ArenaRegistry id).</summary>
        public string ArenaId => ArenaRegistry.All[arenaIndex].Id;
        string[] ids;

        public static MenuView Create(Transform parent, ArtLibrary art)
        {
            var go = new GameObject("Menu");
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<MenuView>();
            m.Build(art);
            return m;
        }

        void Build(ArtLibrary art)
        {
            var back = PixelRect.Create(transform, "Back", art.Pixel, Palette.Background, Order);
            PixelRect.Set(back, -200, -300, 200, 300);
            hub = Page("Hub");
            versusPage = Page("VersusPage");
            BuildHub(art);
            BuildVersus(art);
            settings = SettingsView.Create(transform, art, Order);
            settings.BackRequested += ShowHub;
            BuildNotice(art);
            codex = SkillCodexView.Create(transform, art);
            codex.Closed += ShowHub;
            balls = BallsView.Create(transform, art);
            balls.Closed += () => { ShowHub(); BallsClosed?.Invoke(); };
            howTo = HowToPlayView.Create(transform, art);
            howTo.Closed += ShowHub;
            howTo.StartRequested += PressRun;
            SetRunInfo(new ProgressData(), null);
            ShowHub();
        }

        Transform Page(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        void BuildHub(ArtLibrary art)
        {
            backdrop = MenuBackdrop.Create(hub, art, Order + 1);   // under every hub element (buttons and boxes start at Order + 2)
            title = PixelText.Create(hub, "BALL BATTLE", art.Font, new Vector2(0, 135), PixelText.Align.Center, 3, Order + 4);
            title.EnableShadow(Palette.Outline, new Vector2(0, -3));
            title.Set("BALL BATTLE", Palette.Text);
            ColorTitle();
            Text(hub, art, "WEAPON BALL AUTO BATTLER", 0, 115, 1, Palette.TextDim);  // top 120

            runButton = PixelButton.Create(hub, "Run", art, RunArea, null, 1, Palette.WallWarning, Palette.WallWarning, Palette.Background, Order + 2);
            runButton.Clicked += PressRun;
            runShine = PixelRect.Create(runButton.transform, "Shine", art.Pixel, new Color32(255, 244, 200, 150), runButton.IconOrder);
            runShine.enabled = false;
            BuildStartHere(art);
            var o = runButton.IconOrder + 1;
            runLabel = PixelText.Create(runButton.transform, "RunLabel", art.Font, new Vector2(0, 36), PixelText.Align.Center, 3, o);           // cell 36..75
            continueLabel = PixelText.Create(runButton.transform, "ContinueLabel", art.Font, new Vector2(0, 52), PixelText.Align.Center, 2, o); // cell 52..78
            continueLine = PixelText.Create(runButton.transform, "ContinueLine", art.Font, new Vector2(0, 37), PixelText.Align.Center, 1, o);   // cell 37..50

            Text(hub, art, RunStrings.RulesSummary, 0, 17, 1, Palette.TextDim);   // cell 17..30, under the RUN button (32)
            goal = NextGoalBox.Create(hub, art, 8, 244, Order);

            BallsButton = PixelButton.Create(hub, "BallsButton", art, BallsArea, "BALLS", 2, Palette.Floor, Palette.WallWarning, Palette.WallWarning, Order + 2);
            BallsButton.Clicked += ShowBalls;
            ballsBadge = PixelText.Create(BallsButton.transform, "Badge", art.Font, new Vector2(BallsArea.xMax - 6, BallsArea.yMin + 7), PixelText.Align.Right, 1, BallsButton.IconOrder + 1);
            Text(hub, art, BallsLine, 0, BallsArea.yMin - 14, 1, Palette.TextDim);
            var vs = PixelButton.Create(hub, "Versus", art, VersusArea, RunStrings.Versus, 2, Palette.Floor, Palette.TextDim, Palette.Text, Order + 2);
            vs.Clicked += ShowVersus;
            VersusButton = vs;
            var codexButton = PixelButton.Create(hub, "CodexButton", art, CodexArea, "CODEX", 2,
                Palette.Floor, Palette.TextDim, Palette.Text, Order + 2);
            codexButton.Clicked += () => { hub.gameObject.SetActive(false); codex.Show(); };
            Text(hub, art, VersusLine, 0, VersusArea.yMin - 14, 1, Palette.TextDim);   // cell under the button
            Text(hub, art, CodexLine, 0, CodexArea.yMin - 14, 1, Palette.TextDim);
            var howButton = PixelButton.Create(hub, "HowToButton", art, HowToArea, "HOW TO PLAY", 2,
                Palette.Floor, Palette.WallWarning, Palette.WallWarning, Order + 2);
            howButton.Clicked += () => { hub.gameObject.SetActive(false); howTo.Show(); };
            BuildGear(art);
            ChestButton = PixelButton.Create(hub, "ChestButton", art, ChestArea, "OPEN CHEST", 1, Palette.Floor, Palette.WallWarning, Palette.WallWarning, Order + 2);
            ChestButton.Clicked += () => ChestRequested?.Invoke();
            ChestButton.gameObject.SetActive(false);
        }

        /// <summary>BB-12b: label of the chest button for <paramref name="waiting"/> chests ("" = hidden).</summary>
        public static string ChestLabel(int waiting) => waiting <= 0 ? "" : waiting == 1 ? "OPEN CHEST" : "CHESTS X" + waiting;
        void ColorTitle()
        {
            var k = 0;
            for (var i = 0; i < title.Text.Length; i++)
            {
                if (title.Text[i] == ' ') continue;
                title.SetGlyphColor(i, Palette.Look(TitleColors[k % TitleColors.Length]).Body);
                k++;
            }
        }

        /// <summary>"NEW? START HERE" with two small down arrows, between the tagline and RUN; only before the first run.</summary>
        void BuildStartHere(ArtLibrary art)
        {
            startHere = new GameObject("StartHere").transform;
            startHere.SetParent(hub, false);
            var t = PixelText.Create(startHere, StartHere, art.Font, new Vector2(0, 86), PixelText.Align.Center, 1, Order + 4);
            t.Set(StartHere, Palette.WallWarning);
            foreach (var x in new[] { -52f, 52f })
                for (var row = 0; row < 3; row++)   // rows 5, 3, 1 px wide from top to bottom: points down at RUN
                    PixelRect.Set(PixelRect.Create(startHere, "Arrow", art.Pixel, Palette.WallWarning, Order + 4), x - 2 + row, 93 - 2 * row, x + 3 - row, 95 - 2 * row);
            startHere.gameObject.SetActive(false);
        }

        void Update()
        {
            if (hub != null && hub.gameObject.activeInHierarchy) Animate(Time.unscaledTime);
        }

        /// <summary>UI-MENU1 hub animation at time <paramref name="t"/> (s): title wave, START HERE bob, a shine across RUN every 3.2 s.</summary>
        public void Animate(float t)
        {
            for (var i = 0; i < title.Text.Length; i++)
                title.SetGlyphOffsetY(i, Mathf.Round(2f * Mathf.Sin(t * 2.4f - i * 0.55f)));
            if (startHere.gameObject.activeSelf)
                startHere.localPosition = new Vector3(0f, Mathf.Round(Mathf.Sin(t * 4f)), 0f);
            var phase = t % ShinePeriod;
            runShine.enabled = phase < ShineSweep;
            if (runShine.enabled)
            {
                var x = Mathf.Round(Mathf.Lerp(RunArea.xMin + 1, RunArea.xMax - 4, phase / ShineSweep));
                PixelRect.Set(runShine, x, RunArea.yMin + 1, x + 2, RunArea.yMax - 1);
            }
        }
        public const float ShinePeriod = 3.2f, ShineSweep = 0.7f;

        /// <summary>12x12 gear: 1 px frame, a 6x6 ring with a 2x2 hole and four teeth (whole pixels).</summary>
        void BuildGear(ArtLibrary art)
        {
            var a = SettingsView.GearArea;
            GearButton = PixelButton.Create(hub, "Gear", art, a, null, 1, Palette.Floor, Palette.TextDim, Palette.TextDim, Order + 2);
            GearButton.Clicked += ShowSettings;
            var o = GearButton.IconOrder;
            void Px(string n, Color32 c, int order, float x0, float y0, float x1, float y1) =>
                PixelRect.Set(PixelRect.Create(GearButton.transform, n, art.Pixel, c, order), a.xMin + x0, a.yMin + y0, a.xMin + x1, a.yMin + y1);
            const float g = 6f;   // the 12x12 gear drawing sits in the middle of the 24x24 tap target
            Px("Ring", Palette.TextDim, o, g + 3, g + 3, g + 9, g + 9);
            Px("ToothN", Palette.TextDim, o, g + 5, g + 9, g + 7, g + 10);
            Px("ToothS", Palette.TextDim, o, g + 5, g + 2, g + 7, g + 3);
            Px("ToothW", Palette.TextDim, o, g + 2, g + 5, g + 3, g + 7);
            Px("ToothE", Palette.TextDim, o, g + 9, g + 5, g + 10, g + 7);
            Px("Hole", Palette.Floor, o + 1, g + 5, g + 5, g + 7, g + 7);
        }

        void BuildVersus(ArtLibrary art)
        {
            var p = versusPage;
            Text(p, art, "BALL BATTLE", 0, 199, 3, Palette.Text);                    // cell 199..238
            Text(p, art, "VERSUS: ANY WEAPON  BEST OF 3", 0, 185, 1, Palette.TextDim);   // 185..198

            ids = new string[WeaponRegistry.All.Count];
            for (var i = 0; i < ids.Length; i++) ids[i] = WeaponRegistry.All[i].Id;
            selected[0] = ids[0];
            selected[1] = ids.Length > 1 ? ids[1] : ids[0];

            tiles = new PixelButton[2][];
            BuildRow(art, 0, "BALL 1", 171, 169);   // label x1 171..184, tiles 95..169
            BuildRow(art, 1, "BALL 2", 80, 78);     // label 80..93, tiles 4..78

            versus = PixelText.Create(p, "Versus", art.Font, new Vector2(0, -24), PixelText.Align.Center, 2, Order + 4);   // -24..2
            profiles[0] = WeaponProfileView.Create(p, art, new Vector2(-65, -39), 123);
            profiles[1] = WeaponProfileView.Create(p, art, new Vector2(65, -39), 123);
            spikeWallsThumbnail = art.SpikeWallsThumbnail;
            var thumb = new GameObject("ArenaThumbnail"); thumb.transform.SetParent(p, false);
            thumb.transform.localPosition = new Vector3(-100, -178, 0);
            arenaThumbnail = thumb.AddComponent<SpriteRenderer>(); arenaThumbnail.sortingOrder = Order + 4;

            var arenaBtn = PixelButton.Create(p, "ArenaPick", art, new Rect(-125, -140, 120, 24), "", 1,
                                              Palette.Floor, Palette.Wall, Palette.Text, Order + 2);
            arenaBtn.Clicked += () => { arenaIndex = (arenaIndex + 1) % ArenaRegistry.All.Count; Refresh(); };
            arenaLabel = PixelText.Create(p, "ArenaLabel", art.Font, new Vector2(-65, -134), PixelText.Align.Center, 1, Order + 4);
            var random = PixelButton.Create(p, "Random", art, new Rect(5, -140, 120, 24), "RANDOM", 1,
                                            Palette.Floor, Palette.TextDim, Palette.Text, Order + 2);
            random.Clicked += Randomize;
            var start = PixelButton.Create(p, "Start", art, new Rect(-70, -180, 140, 32), "START", 2,
                                           Palette.Floor, Palette.WallWarning, Palette.WallWarning, Order + 2);
            start.Clicked += () => StartRequested?.Invoke(selected[0], selected[1]);
            StartButton = start;

            var backBtn = PixelButton.Create(p, "Back", art, VersusBackArea, RunStrings.Back, RunUi.BackTextScale, Palette.Floor, Palette.TextDim, Palette.TextDim, Order + 2);
            backBtn.ClickCue = AudioIds.UiBack;
            VersusBackButton = backBtn;
            backBtn.Clicked += ShowHub;
            Refresh();
        }

        void Text(Transform parent, ArtLibrary art, string s, float x, float y, int scale, Color32 c)
        {
            var t = PixelText.Create(parent, s, art.Font, new Vector2(x, y), PixelText.Align.Center, scale, Order + 4);
            t.Set(s, c);
        }

        /// <summary>Weapon tiles in a grid of Cols columns: icon on the left, name on the right.</summary>
        void BuildRow(ArtLibrary art, int side, string title, float labelY, float tilesTop)
        {
            var label = PixelText.Create(versusPage, title, art.Font, new Vector2(-121, labelY), PixelText.Align.Left, 1, Order + 4);
            label.Set(title, Palette.Text);

            tiles[side] = new PixelButton[ids.Length];
            var x0 = -(Cols * TileW + (Cols - 1) * Gap) / 2f;
            for (var i = 0; i < ids.Length; i++)
            {
                var id = ids[i];
                var col = i % Cols;
                var row = i / Cols;
                var area = new Rect(x0 + col * (TileW + Gap), tilesTop - (row + 1) * TileH - row * Gap, TileW, TileH);
                var b = PixelButton.Create(versusPage, $"Tile{side}_{id}", art, area, null, 1, Palette.Floor, Palette.Wall, Palette.Text, Order + 2);

                var icon = new GameObject("Icon");
                icon.transform.SetParent(b.transform, false);
                icon.transform.localPosition = new Vector3(area.center.x, area.yMax - 10, 0f);   // icon on top, name under it
                icon.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                var sr = icon.AddComponent<SpriteRenderer>();
                sr.sprite = art.Get(id).Ball;
                sr.sortingOrder = b.IconOrder;

                var name = PixelText.Create(b.transform, "Name", art.Font, new Vector2(area.center.x, area.yMin + 2), PixelText.Align.Center, 1, b.IconOrder);   // fits 62 px
                name.Set(WeaponNames.Of(id), Palette.Look(id).Body);

                var s = side;
                b.Clicked += () => { selected[s] = id; Refresh(); };
                tiles[side][i] = b;
            }
        }
        void Randomize()
        {
            selected[0] = ids[UnityEngine.Random.Range(0, ids.Length)];
            selected[1] = ids[UnityEngine.Random.Range(0, ids.Length)];
            Refresh();
        }

        void Refresh()
        {
            for (var side = 0; side < 2; side++)
                for (var i = 0; i < ids.Length; i++)
                {
                    var on = ids[i] == selected[side];
                    tiles[side][i].SetColors(on ? Palette.HpBack : Palette.Floor, on ? (Color32)Palette.Look(ids[i]).Body : Palette.Wall);
                }
            arenaLabel.Set("ARENA: " + ArenaRegistry.All[arenaIndex].DisplayName, Palette.Text);
            arenaThumbnail.sprite = ArenaId == ArenaRegistry.SpikeWalls ? spikeWallsThumbnail : null;
            arenaThumbnail.enabled = arenaThumbnail.sprite != null;
            for (var side = 0; side < 2; side++) profiles[side].Show(selected[side]);
            versus.Set($"{WeaponNames.Of(selected[0])} VS {WeaponNames.Of(selected[1])}", Palette.Text);
        }
        /// <summary>
        /// Progress and the resumable run (null = none) for the hub: RUN vs CONTINUE RUN and the NEXT GOAL box.
        /// GameController calls it each time the menu appears.
        /// </summary>
        public void SetRunInfo(ProgressData progress, RunState resumable)
        {
            savedRun = resumable;
            if (savedRun != null)
            {
                runLabel.Set("", Palette.Background);
                continueLabel.Set(RunStrings.ContinueRun, Palette.Background);
                continueLine.Set(RunStrings.ContinueLine(savedRun), Palette.Background);
            }
            else
            {
                runLabel.Set(RunStrings.Run, Palette.Background);
                continueLabel.Set("", Palette.Background);
                continueLine.Set("", Palette.Background);
            }
            goal.Show(progress, savedRun);   // BB-VIEW3: the saved run's bosses name the boss goals
            startHere.gameObject.SetActive(savedRun == null && (progress == null || progress.RunsPlayed == 0));   // UI-MENU1: brand-new player
            var waiting = progress?.PendingChests?.Count ?? 0;   // BB-12b: welcome chests
            ChestButton.gameObject.SetActive(waiting > 0);
            if (waiting > 0) ChestButton.SetText(ChestLabel(waiting), Palette.WallWarning);
            ballsBadge.Set(AnyUpgrade(progress) ? "UP!" : "", BallsView.UpColor);   // BB-12c
        }

        /// <summary>BB-12c: some unlocked ball can level up now.</summary>
        public static bool AnyUpgrade(ProgressData p)
        {
            if (p == null) return false;
            foreach (var id in UnlockRules.Weapons(p))
                if (BallBattle.Sim.Meta.MetaRules.CanUpgrade(p, id)) return true;
            return false;
        }

        /// <summary>BB-12c: open the BALLS page over the hub.</summary>
        public void ShowBalls()
        {
            hub.gameObject.SetActive(false);
            balls.Show(Store);
        }

        public bool HasSavedRun => savedRun != null;

        /// <summary>Showing the menu always starts on the hub page.</summary>
        public void Show(bool visible)
        {
            var appearing = visible && !gameObject.activeSelf;
            gameObject.SetActive(visible);
            if (appearing) ShowHub();
            if (appearing) PixelButton.NoteShownAll(transform);   // the tap that opened the menu (result MENU) is not a menu tap
        }

        public void ShowHub()
        {
            codex?.Hide();
            howTo?.Hide();
            if (balls != null && balls.IsOpen) balls.gameObject.SetActive(false);
            CloseSettings();
            noticePage.gameObject.SetActive(false);
            SetHubInput(true);
            versusPage.gameObject.SetActive(false);
            hub.gameObject.SetActive(true);
            PixelButton.NoteShownAll(hub);
        }

        public void ShowVersus()
        {
            howTo?.Hide();
            CloseSettings();
            noticePage.gameObject.SetActive(false);
            SetHubInput(true);
            hub.gameObject.SetActive(false);
            versusPage.gameObject.SetActive(true);
            PixelButton.NoteShownAll(versusPage);
        }

        public bool VersusOpen => versusPage.gameObject.activeSelf;

        public void ShowSettings()
        {
            hub.gameObject.SetActive(false);
            versusPage.gameObject.SetActive(false);
            settings.gameObject.SetActive(true);
            PixelButton.NoteShownAll(settings.transform);
        }

        public bool SettingsOpen => settings.gameObject.activeSelf;

        void CloseSettings()
        {
            if (!settings.gameObject.activeSelf) return;
            settings.gameObject.SetActive(false);
            SettingsClosed?.Invoke();
        }

        // ---- one-time notice (old run dropped by a save update) ----

        void BuildNotice(ArtLibrary art)
        {
            var go = new GameObject("NoticePage");
            go.transform.SetParent(transform, false);
            noticePage = go.transform;
            var left = NextGoalBox.LocalX(8);
            var top = NextGoalBox.LocalTop(200);
            PixelRect.Set(PixelRect.Create(noticePage, "NoticeBorder", art.Pixel, Palette.WallWarning, NoticeOrder + 1), left, top - 64, left + NextGoalBox.Width, top);
            PixelRect.Set(PixelRect.Create(noticePage, "NoticeFill", art.Pixel, RunUi.Panel, NoticeOrder + 2), left + 1, top - 63, left + NextGoalBox.Width - 1, top - 1);
            var text = PixelText.Create(noticePage, "NoticeText", art.Font, new Vector2(0, top - 10 - PixelFont.CellHeight), PixelText.Align.Center, 1, NoticeOrder + 4);
            text.Set(RunStrings.OldRunEnded, Palette.Text);
            var ok = PixelButton.Create(noticePage, "NoticeOk", art, NoticeOkArea, RunStrings.Ok, 1, Palette.Floor, Palette.WallWarning, Palette.WallWarning, NoticeOrder + 3);
            ok.Clicked += PressNoticeOk;
            NoticeOkButton = ok;
            go.SetActive(false);
        }

        /// <summary>
        /// Shows the notice over the hub: the hub stays drawn as usual behind it (sorted below NoticeOrder) but its buttons
        /// take no input until OK. OK closes it, returns to the hub and calls <paramref name="onOk"/>.
        /// </summary>
        public void ShowOldRunNotice(Action onOk)
        {
            noticeOk = onOk;
            CloseSettings();
            versusPage.gameObject.SetActive(false);
            hub.gameObject.SetActive(true);
            SetHubInput(false);
            noticePage.gameObject.SetActive(true);
            PixelButton.NoteShownAll(noticePage);
        }

        /// <summary>Hub buttons react to taps (true) or not (false, while the notice covers the hub). Drawing is unaffected.</summary>
        void SetHubInput(bool on)
        {
            foreach (var b in hub.GetComponentsInChildren<PixelButton>(true)) b.enabled = on;
        }

        /// <summary>True when the hub page is drawn (tests).</summary>
        public bool HubVisible => hub.gameObject.activeInHierarchy;
        /// <summary>The notice page (tests).</summary>
        public Transform NoticePage => noticePage;
        /// <summary>The hub page (tests).</summary>
        public Transform Hub => hub;

        public void PressNoticeOk()
        {
            if (!NoticeOpen) return;
            var ok = noticeOk;
            noticeOk = null;
            ShowHub();
            ok?.Invoke();
        }

        public bool NoticeOpen => noticePage.gameObject.activeSelf;
        public PixelButton NoticeOkButton { get; private set; }
        public SettingsView Settings => settings;
        public PixelButton GearButton { get; private set; }

        /// <summary>Weapon picked for a side (0 = ball 1, 1 = ball 2).</summary>
        public string Selected(int side) => selected[side];

        /// <summary>Tile button of a weapon on a side (tests).</summary>
        public PixelButton Tile(int side, string weaponId) => tiles[side][Array.IndexOf(ids, weaponId)];

        public PixelButton StartButton { get; private set; }
        public PixelButton VersusBackButton { get; private set; }
        public PixelButton RunButton => runButton;
        public PixelButton VersusButton { get; private set; }
        public NextGoalBox Goal => goal;

        /// <summary>Text on the main button: "RUN", or "CONTINUE RUN|FIGHT 3/8  RETRY 1" (tests).</summary>
        public string RunButtonText => savedRun != null ? continueLabel.Text + "|" + continueLine.Text : runLabel.Text;

        /// <summary>Automation hooks (tests, MCP playthroughs).</summary>
        public void Select(int side, string weaponId)
        {
            selected[side] = weaponId;
            Refresh();
        }

        public void SelectArena(string id)
        {
            for (var i = 0; i < ArenaRegistry.All.Count; i++) if (ArenaRegistry.All[i].Id == id) arenaIndex = i;
            Refresh();
        }

        public void PressStart() => StartRequested?.Invoke(selected[0], selected[1]);

        /// <summary>The main button: CONTINUE RUN when a run is saved, else RUN.</summary>
        public void PressRun()
        {
            if (savedRun != null) ContinueRequested?.Invoke();
            else RunRequested?.Invoke();
        }
    }
}
