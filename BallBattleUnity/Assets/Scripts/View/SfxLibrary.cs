using UnityEngine;

namespace BallBattle.View
{
    /// <summary>Sound clips for match events. Built by the Editor generator; replace clips here to swap audio.</summary>
    [CreateAssetMenu(menuName = "BallBattle/Sfx Library")]
    public sealed class SfxLibrary : ScriptableObject
    {
        public AudioClip Hit;
        public AudioClip Parry;
        public AudioClip Wall;
        public AudioClip Death;
        public AudioClip Win;
    }
}
