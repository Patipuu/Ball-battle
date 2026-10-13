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
        /// <summary>VERSUS button (64,326) 142x30.</summary>
        public static readonly Rect VersusArea = new Rect(-71, -116, 142, 30);
        public static readonly Rect VersusBackArea = new Rect(-60, -206, 120, 22);

        public event Action<string, string> StartRequested;
        /// <summary>RUN with no saved run: open Run mode on its title screen.</summary>
        public event Action RunRequested;
        /// <summary>CONTINUE RUN: open Run mode and resume the saved run.</summary>
        public event Action ContinueRequested;

        readonly string[] selected = new string[2];
        PixelButton[][] tiles;
        PixelText versus;
        PixelText arenaLabel;
        int arenaIndex;
        Transform hub, versusPage;
        PixelButton runButton;
        PixelText runLabel, continueLabel, continueLine;
        NextGoalBox goal;
        RunState savedRun;

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
            Text(hub, art, "BALL BATTLE", 0, 129, 3, Palette.Text);                  // top 96
            Text(hub, art, "WEAPON BALL AUTO BATTLER", 0, 115, 1, Palette.TextDim);  // top 120

            runButton = PixelButton.Create(hub, "Run", art, RunArea, null, 1, Palette.WallWarning, Palette.WallWarning, Palette.Background, Order + 2);
            runButton.Clicked += PressRun;
            var o = runButton.IconOrder + 1;
            runLabel = PixelText.Create(runButton.transform, "RunLabel", art.Font, new Vector2(0, 48), PixelText.Align.Center, 3, o);           // top 177
            continueLabel = PixelText.Create(runButton.transform, "ContinueLabel", art.Font, new Vector2(0, 63), PixelText.Align.Center, 2, o); // top 167
            continueLine = PixelText.Create(runButton.transform, "ContinueLine", art.Font, new Vector2(0, 41), PixelText.Align.Center, 1, o);   // top 194 = y+34

            Text(hub, art, RunStrings.RulesSummary, 0, 21, 1, Palette.TextDim);   // top 214
            goal = NextGoalBox.Create(hub, art, 8, 244, Order);

            var vs = PixelButton.Create(hub, "Versus", art, VersusArea, RunStrings.Versus, 2, Palette.Floor, Palette.TextDim, Palette.Text, Order + 2);
            vs.Clicked += ShowVersus;
            VersusButton = vs;
        }
        void BuildVersus(ArtLibrary art)
        {
            var p = versusPage;
            Text(p, art, "BALL BATTLE", 0, 196, 3, Palette.Text);
            Text(p, art, "VERSUS: ANY WEAPON  BEST OF 3", 0, 182, 1, Palette.TextDim);

            ids = new string[WeaponRegistry.All.Count];
            for (var i = 0; i < ids.Length; i++) ids[i] = WeaponRegistry.All[i].Id;
            selected[0] = ids[0];
            selected[1] = ids.Length > 1 ? ids[1] : ids[0];

            tiles = new PixelButton[2][];
            BuildRow(art, 0, "BALL 1", 168, 164);   // label 168..178, tiles 90..164
            BuildRow(art, 1, "BALL 2", 78, 74);    // label 78..88, tiles 0..74

            versus = PixelText.Create(p, "Versus", art.Font, new Vector2(0, -22), PixelText.Align.Center, 2, Order + 4);

            var arenaBtn = PixelButton.Create(p, "ArenaPick", art, new Rect(-60, -64, 120, 18), "", 1,
                                              Palette.Floor, Palette.Wall, Palette.Text, Order + 2);
            arenaBtn.Clicked += () => { arenaIndex = (arenaIndex + 1) % ArenaRegistry.All.Count; Refresh(); };
            arenaLabel = PixelText.Create(p, "ArenaLabel", art.Font, new Vector2(0, -55), PixelText.Align.Center, 1, Order + 4);
            var random = PixelButton.Create(p, "Random", art, new Rect(-60, -92, 120, 22), "RANDOM", 1,
                                            Palette.Floor, Palette.TextDim, Palette.Text, Order + 2);
            random.Clicked += Randomize;
            var start = PixelButton.Create(p, "Start", art, new Rect(-80, -136, 160, 32), "START", 3,
                                           Palette.Floor, Palette.WallWarning, Palette.WallWarning, Order + 2);
            start.Clicked += () => StartRequested?.Invoke(selected[0], selected[1]);
            StartButton = start;

            var backBtn = PixelButton.Create(p, "Back", art, VersusBackArea, RunStrings.Back, 1, Palette.Floor, Palette.TextDim, Palette.TextDim, Order + 2);
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
            var label = PixelText.Create(versusPage, title, art.Font, new Vector2(-121, labelY), PixelText.Align.Left, 2, Order + 4);
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
                icon.transform.localPosition = new Vector3(area.xMin + 13, area.center.y, 0f);
                icon.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                var sr = icon.AddComponent<SpriteRenderer>();
                sr.sprite = art.Get(id).Ball;
                sr.sortingOrder = b.IconOrder;

                var name = PixelText.Create(b.transform, "Name", art.Font, new Vector2(area.xMin + 26, area.center.y - 2), PixelText.Align.Left, 1, b.IconOrder);
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
            goal.Show(progress);
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
            versusPage.gameObject.SetActive(false);
            hub.gameObject.SetActive(true);
            PixelButton.NoteShownAll(hub);
        }

        public void ShowVersus()
        {
            hub.gameObject.SetActive(false);
            versusPage.gameObject.SetActive(true);
            PixelButton.NoteShownAll(versusPage);
        }

        public bool VersusOpen => versusPage.gameObject.activeSelf;

        /// <summary>Weapon picked for a side (0 = ball 1, 1 = ball 2).</summary>
        public string Selected(int side) => selected[side];

        /// <summary>Tile button of a weapon on a side (tests).</summary>
        public PixelButton Tile(int side, string weaponId) => tiles[side][Array.IndexOf(ids, weaponId)];

        public PixelButton StartButton { get; private set; }
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