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
        }

        public Sprite Pixel;
        public Texture2D Font;
        public WeaponArt[] Weapons;

        public WeaponArt Get(string id)
        {
            foreach (var w in Weapons)
                if (w.Id == id) return w;
            throw new ArgumentException($"No art for weapon '{id}'", nameof(id));
        }
    }
}
