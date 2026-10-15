using System;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>All sprites the match view needs. Built by the Editor generator; replace sprites here to swap art.</summary>
    [CreateAssetMenu(menuName = "BallBattle/Art Library")]
    public sealed class ArtLibrary : ScriptableObject
    {
        [Serializable]
        public struct WeaponArt
        {
            public string Id;
            public Sprite Ball;
            /// <summary>Horizontal, pivot at the inner end, 9-sliced so it can stretch to any blade length. Null for bladeless weapons.</summary>
            public Sprite Blade;
            /// <summary>The ball pre-drawn at x1.2 / x1.35 / x1.5 (ball_sizes/ball_&lt;id&gt;_38|43|48.png); null when missing.</summary>
            public Sprite Ball38, Ball43, Ball48;
        }

        /// <summary>Trait icon (14x14), by TraitId.</summary>
        [Serializable]
        public struct TraitArt
        {
            public string Id;
            public Sprite Icon;
        }

        /// <summary>Arena floor tile (10x10, repeats) and its floor / wall colors (arena_colors.json), by arena id.</summary>
        [Serializable]
        public struct ArenaArt
        {
            public string Id;
            public Sprite Floor;
            public Color32 FloorColor;
            public Color32 WallColor;
        }

        public enum CardFrame { Common, Uncommon, Rare, Counter }

        /// <summary>Mimic's looks: idle, then the damage kind it adapted to (weapon, shot, poison, reflect).</summary>
        public enum MimicLook { Idle, Weapon, Shot, Poison, Reflect }

        public Sprite Pixel;
        /// <summary>White ball silhouette drawn over a ball for the hit flash.</summary>
        public Sprite BallFlash;
        public Texture2D Font;
        public WeaponArt[] Weapons;

        [Header("Art v1: HUD (retry tokens: wired in v4)")]
        public Sprite RetryTokenOn;
        public Sprite RetryTokenOff;
        public Sprite IconAntidote;

        [Header("Art v1: cards and traits")]
        public TraitArt[] Traits = new TraitArt[0];
        /// <summary>16x16 frames, 9-slice border 4: common, uncommon, rare, counter (dashed).</summary>
        public Sprite[] CardFrames = new Sprite[4];
        public Sprite CardAntidote;
        /// <summary>Stat card icons (14x14): SPEED CAP, MAX HP, DAMAGE. Also the HUD badges of those cards.</summary>
        public Sprite CardSpeedCap, CardMaxHp, CardDamage;
        /// <summary>HEAL and SWAP card icons (14x14).</summary>
        public Sprite CardHeal, CardSwap;

        [Header("Art v1: bosses (64x64, body centre (32,34) from the top-left, body radius 24)")]
        public Sprite ShellKing;
        public Sprite ShellKingGlow;
        /// <summary>Indexed by MimicLook.</summary>
        public Sprite[] Mimic = new Sprite[5];

        [Header("ART-2 boss v2 overlays and bodies")]
        public const string BossSpriteFolder = "Assets/Art/Sprites/boss/v2";
        public Sprite MagnetBody, WardenBody, ShellWarn, ShellOn, MagnetWarn, MagnetPull;
        public Sprite[] MimicFlashes = new Sprite[3];
        public Sprite WardenSpikeLeftOn, WardenSpikeLeftFade, WardenSpikeRightOn, WardenSpikeRightFade, WardenSpikeTopOn, WardenSpikeTopFade;
        public Sprite WardenWallflash, BumperRings, SpikeWallsThumbnail;
        [Serializable] public struct RoleArt { public string Role; public Sprite Icon; }
        public RoleArt[] Roles = new RoleArt[0];
        public Sprite RoleIcon(string role)
        {
            foreach (var entry in Roles) if (entry.Role == role) return entry.Icon;
            return null;
        }
        public Sprite BossBody(string id) => id == "shellking" ? ShellKing : id == "magnet" ? MagnetBody :
            id == "warden" ? WardenBody : id == "mimic" ? MimicSprite(MimicLook.Idle) : null;

        [Header("Art v1: arenas")]
        public ArenaArt[] Arenas = new ArenaArt[0];
        public Color32 ClosingWallColor = new Color32(232, 180, 60, 255);
        /// <summary>6x4 tooth pointing down (top wall); rotated for the other walls.</summary>
        public Sprite SpikeTooth;

        /// <summary>Radius of a boss body in the 64x64 boss sprites (drawn at scale 1: sim boss radius is 24).</summary>
        public const float BossBodyRadius = 24f;

        public WeaponArt Get(string id)
        {
            foreach (var w in Weapons)
                if (w.Id == id) return w;
            throw new ArgumentException($"No art for weapon '{id}'", nameof(id));
        }

        /// <summary>Size factors (vs the 32 px ball) that have a pre-drawn sprite, and that sprite's size in px.</summary>
        public static readonly (float factor, int pixels)[] SizedBalls = { (1.2f, 38), (1.35f, 43), (1.5f, 48) };
        const float SizeTolerance = 0.02f;

        /// <summary>
        /// The ball sprite for a size <paramref name="factor"/> (sim radius / default radius): the pre-drawn _38/_43/_48 for
        /// x1.2 / x1.35 / x1.5 when present (<paramref name="preSized"/> = true: draw at scale 1), else the base sprite to be
        /// scaled up as before.
        /// </summary>
        public Sprite BallForSize(string weaponId, float factor, out bool preSized)
        {
            var w = Get(weaponId);
            Sprite sized = null;
            if (Mathf.Abs(factor - 1.2f) < SizeTolerance) sized = w.Ball38;
            else if (Mathf.Abs(factor - 1.35f) < SizeTolerance) sized = w.Ball43;
            else if (Mathf.Abs(factor - 1.5f) < SizeTolerance) sized = w.Ball48;
            preSized = sized != null;
            return preSized ? sized : w.Ball;
        }

        /// <summary>
        /// Icon of a card: the trait's icon, or the card icons (DAMAGE, SPEED CAP, MAX HP, HEAL, SWAP).
        /// </summary>
        public Sprite CardIcon(BallBattle.Sim.Run.Card card)
        {
            switch (card.Kind)
            {
                case BallBattle.Sim.Run.CardKind.Trait: return TraitIcon(card.Id);
                case BallBattle.Sim.Run.CardKind.DamageUp: return CardDamage;
                case BallBattle.Sim.Run.CardKind.SpeedUp: return CardSpeedCap;
                case BallBattle.Sim.Run.CardKind.MaxHp: return CardMaxHp;
                case BallBattle.Sim.Run.CardKind.Heal: return CardHeal;
                case BallBattle.Sim.Run.CardKind.SwapWeapon: return CardSwap;
                case BallBattle.Sim.Run.CardKind.SkillUp: return CardSwap; // placeholder upgrade icon, no new asset required
                default: return null;
            }
        }
        /// <summary>Trait icon, or null when the art has none (callers fall back to the drawn badge).</summary>
        public Sprite TraitIcon(string traitId)
        {
            if (Traits == null) return null;
            foreach (var t in Traits)
                if (t.Id == traitId) return t.Icon;
            return null;
        }

        public Sprite Frame(CardFrame frame) => CardFrames != null && (int)frame < CardFrames.Length ? CardFrames[(int)frame] : null;

        public Sprite MimicSprite(MimicLook look) => Mimic != null && (int)look < Mimic.Length ? Mimic[(int)look] : null;

        /// <summary>Arena art, or false when the arena has none (callers keep the plain colors).</summary>
        public bool TryArena(string arenaId, out ArenaArt arena)
        {
            if (Arenas != null)
                foreach (var a in Arenas)
                    if (a.Id == arenaId) { arena = a; return true; }
            arena = default;
            return false;
        }
    }
}
