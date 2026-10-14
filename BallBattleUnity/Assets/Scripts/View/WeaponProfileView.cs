using BallBattle.Sim.Skills;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>Full role names and four independently labelled, five-cell stat bars.</summary>
    public sealed class WeaponProfileView : MonoBehaviour
    {
        PixelText role, skill;
        readonly SpriteRenderer[,] cells = new SpriteRenderer[4, 5];
        public string RoleText => role.Text;
        public static WeaponProfileView Create(Transform parent, ArtLibrary art, Vector2 at, int width, bool showSkill = true)
        {
            var go = new GameObject("WeaponProfile"); go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(at.x, at.y, 0);
            var view = go.AddComponent<WeaponProfileView>();
            view.role = PixelText.Create(go.transform, "Role", art.Font, new Vector2(0, 0), PixelText.Align.Center, 1, MenuView.Order + 8);
            var labels = width < 110 ? new[] { "DMG", "RNG", "DEF", "DIFF" } : new[] { "DAMAGE", "RANGE", "DEFENSE", "DIFFICULTY" };
            for (var row = 0; row < 4; row++)
            {
                var y = -14 - row * 13;
                PixelText.Create(go.transform, "Stat_" + row, art.Font, new Vector2(-width / 2 + 2, y), PixelText.Align.Left, 1, MenuView.Order + 8).Set(labels[row], Palette.TextDim);
                for (var col = 0; col < 5; col++)
                {
                    var sr = PixelRect.Create(go.transform, "Cell_" + row + "_" + col, art.Pixel, Palette.Wall, MenuView.Order + 8);
                    var x = width / 2 - 29 + col * 5;
                    PixelRect.Set(sr, x, y + 2, x + 3, y + 7);
                    view.cells[row, col] = sr;
                }
            }
            view.skill = PixelText.Create(go.transform, "Skill", art.Font, new Vector2(0, -67), PixelText.Align.Center, 1, MenuView.Order + 8);
            view.skill.gameObject.SetActive(showSkill);
            return view;
        }
        public void Show(string id)
        {
            var data = SkillDefinition.ForWeapon(id);
            role.Set(data.Role, Palette.Look(id).Body);
            skill.Set(SkillText.Line(id, 1, SkillBranch.None), Palette.Text);
            var stats = new[] { data.Damage, data.Range, data.Defense, data.Difficulty };
            for (var row = 0; row < 4; row++) for (var col = 0; col < 5; col++)
                cells[row, col].color = col < stats[row] ? Palette.Look(id).Body : Palette.Wall;
        }
    }
}
