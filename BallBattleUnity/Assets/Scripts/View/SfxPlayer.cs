using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Small pool of 2D AudioSources. The same clip is played at most once per ~30 ms (Fang combos and
    /// multi-substep contacts would otherwise stack into noise); when all voices are busy the oldest is reused.
    /// </summary>
    public sealed class SfxPlayer : MonoBehaviour
    {
        const float SameClipGap = 0.03f;

        AudioSource[] voices;
        float[] startedAt;
        AudioClip lastClip;
        float lastClipTime = -1f;

        public static SfxPlayer Create(Transform parent, int voiceCount)
        {
            var go = new GameObject("Sfx");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<SfxPlayer>();
            p.voices = new AudioSource[voiceCount];
            p.startedAt = new float[voiceCount];
            for (var i = 0; i < voiceCount; i++)
            {
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                p.voices[i] = src;
            }
            return p;
        }

        public void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            var now = Time.unscaledTime;
            if (clip == lastClip && now - lastClipTime < SameClipGap) return;
            lastClip = clip;
            lastClipTime = now;

            var best = 0;
            for (var i = 0; i < voices.Length; i++)
            {
                if (!voices[i].isPlaying) { best = i; break; }
                if (startedAt[i] < startedAt[best]) best = i;
            }
            var v = voices[best];
            v.clip = clip;
            v.volume = volume;
            v.pitch = pitch;
            v.Play();
            startedAt[best] = now;
        }
    }
}
