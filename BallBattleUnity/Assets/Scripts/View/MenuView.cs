using System;
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
        const int Tile = 56;
        const int Gap = 6;

        public event Action<string, string> StartRequested;

        readonly string[] selected = new string[2];
        PixelButton[][] tiles;
        PixelText versus;
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
            BuildRow(art, 0, "BALL 1", 160, 90);   // label 160..170, tiles 98..154
            BuildRow(art, 1, "BALL 2", 70, 0);    // label 70..80, tiles 8..64

            versus = PixelText.Create(transform, "Versus", art.Font, new Vector2(0, -38), PixelText.Align.Center, 2, Order + 4);

            var random = PixelButton.Create(transform, "Random", art, new Rect(-60, -86, 120, 22), "RANDOM", 1,
                                            Palette.Floor, Palette.TextDim, Palette.Text, Order + 2);
            random.Clicked += Randomize;
            var start = PixelButton.Create(transform, "Start", art, new Rect(-80, -136, 160, 32), "START", 3,
                                           Palette.Floor, Palette.WallWarning, Palette.WallWarning, Order + 2);
            start.Clicked += () => StartRequested?.Invoke(selected[0], selected[1]);

            Text(art, "BEST OF 3", 0, -160, 1, Palette.TextDim);
            Refresh();
        }

        void Text(ArtLibrary art, string s, float x, float y, int scale, Color32 c)
        {
            var t = PixelText.Create(transform, s, art.Font, new Vector2(x, y), PixelText.Align.Center, scale, Order + 4);
            t.Set(s, c);
        }

        void BuildRow(ArtLibrary art, int side, string title, float labelY, float tileTop)
        {
            var label = PixelText.Create(transform, title, art.Font, new Vector2(-121, labelY), PixelText.Align.Left, 2, Order + 4);
            label.Set(title, Palette.Text);

            tiles[side] = new PixelButton[ids.Length];
            var totalWidth = ids.Length * Tile + (ids.Length - 1) * Gap;
            var x0 = -totalWidth / 2;
            for (var i = 0; i < ids.Length; i++)
            {
                var id = ids[i];
                var area = new Rect(x0 + i * (Tile + Gap), tileTop + 8, Tile, Tile);
                var b = PixelButton.Create(transform, $"Tile{side}_{id}", art, area, null, 1, Palette.Floor, Palette.Wall, Palette.Text, Order + 2);

                var icon = new GameObject("Icon");
                icon.transform.SetParent(b.transform, false);
                icon.transform.localPosition = new Vector3(area.center.x, area.yMin + 34, 0f);
                var sr = icon.AddComponent<SpriteRenderer>();
                sr.sprite = art.Get(id).Ball;
                sr.sortingOrder = b.IconOrder;

                var name = PixelText.Create(b.transform, "Name", art.Font, new Vector2(area.center.x, area.yMin + 5), PixelText.Align.Center, 1, b.IconOrder);
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
            versus.Set($"{WeaponRegistry.Get(selected[0]).DisplayName} VS {WeaponRegistry.Get(selected[1]).DisplayName}", Palette.Text);
        }

        public void Show(bool visible) => gameObject.SetActive(visible);

        /// <summary>Automation hooks (tests, MCP playthroughs).</summary>
        public void Select(int side, string weaponId)
        {
            selected[side] = weaponId;
            Refresh();
        }

        public void PressStart() => StartRequested?.Invoke(selected[0], selected[1]);
    }
}
