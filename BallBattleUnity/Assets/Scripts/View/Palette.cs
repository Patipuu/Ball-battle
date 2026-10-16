using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Placeholder palette and per-weapon visuals. Used by the art generator and at runtime (HUD, tints),
    /// so swapping in real art later only touches PNGs and this table.
    /// </summary>
    public static class Palette
    {
        public static readonly Color32 Background = new Color32(16, 16, 26, 255);
        public static readonly Color32 Floor = new Color32(30, 30, 46, 255);
        public static readonly Color32 Wall = new Color32(96, 96, 140, 255);
        public static readonly Color32 WallWarning = new Color32(232, 180, 60, 255);
        public static readonly Color32 Outline = new Color32(20, 18, 28, 255);
        public static readonly Color32 Text = new Color32(232, 232, 240, 255);
        /// <summary>Secondary text. UI-UX2: was 140,140,168 (4.3-5.8:1 on the dark panels, hard to read at 1x); now about 7-8:1.</summary>
        public static readonly Color32 TextDim = new Color32(170, 170, 196, 255);
        public static readonly Color32 HpBack = new Color32(48, 40, 56, 255);
        public static readonly Color32 HpLost = new Color32(120, 40, 48, 255);
        public static readonly Color32 Hot = new Color32(255, 70, 50, 255);
        /// <summary>Primary button fill #F4C542 with dark text (Settings BACK).</summary>
        public static readonly Color32 PrimaryFill = new Color32(0xF4, 0xC5, 0x42, 255);
        public static readonly Color32 PrimaryText = new Color32(30, 30, 46, 255);

        public static readonly Color32 Metal = new Color32(200, 205, 220, 255);
        public static readonly Color32 MetalDark = new Color32(120, 126, 150, 255);
        public static readonly Color32 Wood = new Color32(140, 92, 56, 255);
        public static readonly Color32 WoodDark = new Color32(92, 58, 36, 255);

        public struct WeaponLook
        {
            public string Id;
            public Color32 Body;
            /// <summary>Stat value at which the weapon tint reaches full "hot" red.</summary>
            public float HotAt;
            /// <summary>Stat value at start (tint is neutral here).</summary>
            public float ColdAt;
        }

        public static readonly WeaponLook[] Weapons =
        {
            new WeaponLook { Id = "blade", Body = new Color32(232, 72, 72, 255), ColdAt = 1f, HotAt = 20f },
            new WeaponLook { Id = "fang", Body = new Color32(140, 224, 80, 255), ColdAt = 8f, HotAt = 40f },
            new WeaponLook { Id = "pike", Body = new Color32(64, 208, 232, 255), ColdAt = 26f, HotAt = 90f },
            new WeaponLook { Id = "brawler", Body = new Color32(176, 176, 192, 255), ColdAt = 7f, HotAt = 14f },
            new WeaponLook { Id = "volley", Body = new Color32(240, 200, 64, 255), ColdAt = 1f, HotAt = 8f },
            new WeaponLook { Id = "venom", Body = new Color32(176, 96, 224, 255), ColdAt = 1f, HotAt = 8f },
            new WeaponLook { Id = "aegis", Body = new Color32(80, 128, 232, 255), ColdAt = 7f, HotAt = 12f },
            new WeaponLook { Id = "rig", Body = new Color32(232, 144, 64, 255), ColdAt = 0f, HotAt = 6f },
            new WeaponLook { Id = "chorus", Body = new Color32(255, 128, 196, 255), ColdAt = 0f, HotAt = 6f },     // BB-14 BGLGXLQ (rings)
            new WeaponLook { Id = "serpent", Body = new Color32(52, 196, 150, 255), ColdAt = 0f, HotAt = 8f },     // BB-14 the snake (length)
        };

        public static WeaponLook Look(string weaponId)
        {
            foreach (var w in Weapons)
                if (w.Id == weaponId) return w;
            return new WeaponLook { Id = weaponId, Body = Text, ColdAt = 0f, HotAt = 1f };
        }

        /// <summary>0 at the starting stat, 1 at HotAt.</summary>
        public static float Heat(string weaponId, float statValue)
        {
            var l = Look(weaponId);
            return Mathf.Clamp01((statValue - l.ColdAt) / Mathf.Max(0.0001f, l.HotAt - l.ColdAt));
        }
    }
}
