using System;
using System.Collections.Generic;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>Start of a run: tap one of up to 3 offered weapons.</summary>
    public sealed class PickWeaponView : MonoBehaviour
    {
        const int Tile = 70;
        const int Gap = 8;

        public event Action<string> WeaponPicked;

        ArtLibrary art;
        Transform tilesRoot;

        public static PickWeaponView Create(Transform parent, ArtLibrary art)
        {
            var root = RunUi.Root(parent, "PickWeapon", art);
            var v = root.gameObject.AddComponent<PickWeaponView>();
            v.art = art;
            RunUi.Text(root, art, 0, 150, 2, initial: "CHOOSE YOUR WEAPON");
            RunUi.Text(root, art, 0, 134, 1, initial: "ONE SIGNATURE SKILL. TWO BRANCHES", color: Palette.TextDim);
            return v;
        }

        public void Show(IReadOnlyList<string> choices)
        {
            gameObject.SetActive(true);
            if (tilesRoot != null)
            {
                if (Application.isPlaying) Destroy(tilesRoot.gameObject);
                else DestroyImmediate(tilesRoot.gameObject);   // EditMode tests and captures show it more than once
            }
            tilesRoot = new GameObject("Tiles").transform;
            tilesRoot.SetParent(transform, false);

            var total = choices.Count * Tile + (choices.Count - 1) * Gap;
            for (var i = 0; i < choices.Count; i++)
            {
                var id = choices[i];
                var area = new Rect(-total / 2 + i * (Tile + Gap), -90, Tile, 190);
                var b = PixelButton.Create(tilesRoot, "Pick_" + id, art, area, null, 1, Palette.Floor, Palette.Look(id).Body, Palette.Text, RunUi.Order + 2);
                RunUi.Icon(b.transform, new Vector2(area.center.x, 73), b.IconOrder).sprite = art.Get(id).Ball;
                var name = PixelText.Create(b.transform, "Name", art.Font, new Vector2(area.center.x, 42), PixelText.Align.Center, 1, b.IconOrder);
                name.Set(RunUi.WeaponName(id), Palette.Look(id).Body);
                WeaponProfileView.Create(b.transform, art, new Vector2(area.center.x, 27), Tile, false).Show(id);
                PixelText.Create(b.transform, "Signature", art.Font, new Vector2(area.center.x, -58), PixelText.Align.Center, 1, b.IconOrder)
                    .Set(BallBattle.Sim.Skills.SkillDefinition.ForWeapon(id).Name, Palette.Text);
                b.Clicked += () => WeaponPicked?.Invoke(id);
            }
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
