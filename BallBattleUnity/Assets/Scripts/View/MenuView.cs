using System;
using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Full-screen pixel menu drawn over the (paused) arena: pick a weapon for ball 1 and ball 2
    /// (mirror allowed), RANDOM, START. Raises StartRequested(weaponA, weaponB).
    /// </summary>
    public sealed class MenuView : MonoBehaviour
    {
        public const int Order = 60;
        const int Cols = 4;
        const int TileW = 62;
        const int TileH = 34;
        const int Gap = 6;

        public event Action<string, string> StartRequested;
        /// <summary>RUN MODE button.</summary>
        public event Action RunRequested;

        readonly string[] selected = new string[2];
        PixelButton[][] tiles;
        PixelText versus;
        PixelText arenaLabel;
        int arenaIndex;

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

            Text(art, "BALL BATTLE", 0, 196, 3, Palette.Text);
            Text(art, "WEAPON BALL AUTO BATTLER", 0, 182, 1, Palette.TextDim);

            ids = new string[WeaponRegistry.All.Count];
            for (var i = 0; i < ids.Length; i++) ids[i] = WeaponRegistry.All[i].Id;
            selected[0] = ids[0];
            selected[1] = ids.Length > 1 ? ids[1] : ids[0];

            tiles = new PixelButton[2][];
            BuildRow(art, 0, "BALL 1", 168, 164);   // label 168..178, tiles 90..164
            BuildRow(art, 1, "BALL 2", 78, 74);    // label 78..88, tiles 0..74

            versus = PixelText.Create(transform, "Versus", art.Font, new Vector2(0, -22), PixelText.Align.Center, 2, Order + 4);

            var arenaBtn = PixelButton.Create(transform, "ArenaPick", art, new Rect(-60, -64, 120, 18), "", 1,
                                              Palette.Floor, Palette.Wall, Palette.Text, Order + 2);
            arenaBtn.Clicked += () => { arenaIndex = (arenaIndex + 1) % ArenaRegistry.All.Count; Refresh(); };
            arenaLabel = PixelText.Create(transform, "ArenaLabel", art.Font, new Vector2(0, -55), PixelText.Align.Center, 1, Order + 4);
            var random = PixelButton.Create(transform, "Random", art, new Rect(-60, -92, 120, 22), "RANDOM", 1,
                                            Palette.Floor, Palette.TextDim, Palette.Text, Order + 2);
            random.Clicked += Randomize;
            var start = PixelButton.Create(transform, "Start", art, new Rect(-80, -136, 160, 32), "START", 3,
                                           Palette.Floor, Palette.WallWarning, Palette.WallWarning, Order + 2);
            start.Clicked += () => StartRequested?.Invoke(selected[0], selected[1]);

            Text(art, "BEST OF 3", 0, -160, 1, Palette.TextDim);

            var run = PixelButton.Create(transform, "RunMode", art, new Rect(-80, -206, 160, 30), "RUN MODE", 2,
                                         Palette.Floor, new Color32(180, 120, 240, 255), new Color32(180, 120, 240, 255), Order + 2);
            run.Clicked += () => RunRequested?.Invoke();
            Text(art, "ROGUELITE: 8 FIGHTS AND CARDS", 0, -218, 1, Palette.TextDim);
            Refresh();
        }

        void Text(ArtLibrary art, string s, float x, float y, int scale, Color32 c)
        {
            var t = PixelText.Create(transform, s, art.Font, new Vector2(x, y), PixelText.Align.Center, scale, Order + 4);
            t.Set(s, c);
        }

        /// <summary>Weapon tiles in a grid of Cols columns: icon on the left, name on the right.</summary>
        void BuildRow(ArtLibrary art, int side, string title, float labelY, float tilesTop)
        {
            var label = PixelText.Create(transform, title, art.Font, new Vector2(-121, labelY), PixelText.Align.Left, 2, Order + 4);
            label.Set(title, Palette.Text);

            tiles[side] = new PixelButton[ids.Length];
            var rows = (ids.Length + Cols - 1) / Cols;
            var x0 = -(Cols * TileW + (Cols - 1) * Gap) / 2f;
            for (var i = 0; i < ids.Length; i++)
            {
                var id = ids[i];
                var col = i % Cols;
                var row = i / Cols;
                var area = new Rect(x0 + col * (TileW + Gap), tilesTop - (row + 1) * TileH - row * Gap, TileW, TileH);
                var b = PixelButton.Create(transform, $"Tile{side}_{id}", art, area, null, 1, Palette.Floor, Palette.Wall, Palette.Text, Order + 2);

                var icon = new GameObject("Icon");
                icon.transform.SetParent(b.transform, false);
                icon.transform.localPosition = new Vector3(area.xMin + 13, area.center.y, 0f);
                icon.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                var sr = icon.AddComponent<SpriteRenderer>();
                sr.sprite = art.Get(id).Ball;
                sr.sortingOrder = b.IconOrder;

                var name = PixelText.Create(b.transform, "Name", art.Font, new Vector2(area.xMin + 26, area.center.y - 2), PixelText.Align.Left, 1, b.IconOrder);
                name.Set(WeaponRegistry.Get(id).DisplayName, Palette.Look(id).Body);

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
            versus.Set($"{WeaponRegistry.Get(selected[0]).DisplayName} VS {WeaponRegistry.Get(selected[1]).DisplayName}", Palette.Text);
        }

        public void Show(bool visible) => gameObject.SetActive(visible);

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

        public void PressRun() => RunRequested?.Invoke();
    }
}
