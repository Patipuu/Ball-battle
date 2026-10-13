using System.Collections.Generic;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Sound clips. The five generic match sounds (Hit, Parry, Wall, Death, Win) come from the Editor generator; music and
    /// cues (Assets/Audio/*.wav) are listed in <see cref="Clips"/> by AudioLibraryLinker. Look clips up by id with
    /// <see cref="Get"/>: "hit"/"parry"/"wall"/"death"/"win" for the generic ones, the clip name (AudioIds) for the rest.
    /// </summary>
    [CreateAssetMenu(menuName = "BallBattle/Sfx Library")]
    public sealed class SfxLibrary : ScriptableObject
    {
        public AudioClip Hit;
        public AudioClip Parry;
        public AudioClip Wall;
        public AudioClip Death;
        public AudioClip Win;

        /// <summary>Music and cue clips, found by clip name.</summary>
        public AudioClip[] Clips = new AudioClip[0];

        [System.NonSerialized] Dictionary<string, AudioClip> byId;

        public AudioClip Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (byId == null) Index();
            return byId.TryGetValue(id, out var c) ? c : null;
        }

        public bool Has(string id) => Get(id) != null;

        void Index()
        {
            byId = new Dictionary<string, AudioClip>();
            void Add(string key, AudioClip c)
            {
                if (c != null && !byId.ContainsKey(key)) byId[key] = c;
            }
            Add("hit", Hit);
            Add("parry", Parry);
            Add("wall", Wall);
            Add("death", Death);
            Add("win", Win);
            if (Clips != null)
                foreach (var c in Clips)
                    if (c != null) Add(c.name, c);
        }

        void OnEnable() => byId = null;
        void OnValidate() => byId = null;
    }
}
