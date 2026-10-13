using System.Collections.Generic;
using BallBattle.View;
using UnityEditor;
using UnityEngine;

namespace BallBattle.EditorTools
{
    /// <summary>
    /// BB-03: sets import settings of the music and cue WAVs in Assets/Audio and lists them in SfxLibrary.Clips
    /// (looked up by clip name = AudioIds). Music: Streaming, Vorbis q 0.7. Cues: Decompress On Load, PCM. Mono always.
    /// Idempotent. Menu: BallBattle/Link Audio Library (also run by Generate Placeholder Sfx / Build Scenes).
    /// </summary>
    public static class AudioLibraryLinker
    {
        public const string Folder = "Assets/Audio";

        [MenuItem("BallBattle/Link Audio Library")]
        public static void Link()
        {
            var lib = AssetDatabase.LoadAssetAtPath<SfxLibrary>(PlaceholderSfxGenerator.LibraryPath);
            if (lib == null)
            {
                Debug.LogError("[BallBattle] SfxLibrary missing: run BallBattle/Generate Placeholder Sfx first");
                return;
            }
            var clips = new List<AudioClip>();
            foreach (var id in AudioIds.Music) Add(clips, Configure(id, true));
            foreach (var id in AudioIds.Cues) Add(clips, Configure(id, false));
            lib.Clips = clips.ToArray();
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BallBattle] Audio library linked: {clips.Count} clips");
        }

        /// <summary>Batch entry: -executeMethod BallBattle.EditorTools.AudioLibraryLinker.LinkAndExit</summary>
        public static void LinkAndExit()
        {
            Link();
            EditorApplication.Exit(0);
        }

        static void Add(List<AudioClip> clips, AudioClip c)
        {
            if (c != null) clips.Add(c);
        }

        static AudioClip Configure(string id, bool music)
        {
            var path = $"{Folder}/{id}.wav";
            if (!(AssetImporter.GetAtPath(path) is AudioImporter imp))
            {
                Debug.LogWarning("[BallBattle] Audio clip missing: " + path);
                return null;
            }
            imp.forceToMono = true;
            imp.loadInBackground = music;
            var s = imp.defaultSampleSettings;
            if (music)
            {
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.7f;
                s.preloadAudioData = false;
            }
            else
            {
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.PCM;
                s.preloadAudioData = true;
            }
            imp.defaultSampleSettings = s;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
