using System.IO;
using BallBattle.View;
using UnityEditor;
using UnityEngine;

namespace BallBattle.EditorTools
{
    /// <summary>
    /// Writes placeholder WAVs into Assets/Audio/Generated and the SfxLibrary asset. Idempotent.
    /// Menu: BallBattle/Generate Placeholder Sfx (also run by Build Scenes).
    /// </summary>
    public static class PlaceholderSfxGenerator
    {
        public const string Folder = "Assets/Audio/Generated";
        public const string LibraryPath = "Assets/Audio/SfxLibrary.asset";

        [MenuItem("BallBattle/Generate Placeholder Sfx")]
        public static void Run()
        {
            Directory.CreateDirectory(Folder);
            Write("hit", PlaceholderSfxSynth.Hit());
            Write("parry", PlaceholderSfxSynth.Parry());
            Write("wall", PlaceholderSfxSynth.Wall());
            Write("death", PlaceholderSfxSynth.Death());
            Write("win", PlaceholderSfxSynth.Win());
            AssetDatabase.Refresh();

            foreach (var n in new[] { "hit", "parry", "wall", "death", "win" }) Configure($"{Folder}/{n}.wav");

            var lib = AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<SfxLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            lib.Hit = Load("hit");
            lib.Parry = Load("parry");
            lib.Wall = Load("wall");
            lib.Death = Load("death");
            lib.Win = Load("win");
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("[BallBattle] Placeholder sfx generated");
        }

        static void Write(string name, float[] samples) => File.WriteAllBytes($"{Folder}/{name}.wav", PlaceholderSfxSynth.ToWav(samples));

        static AudioClip Load(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Folder}/{name}.wav");

        /// <summary>Short one-shots: decompress on load, preload, mono.</summary>
        static void Configure(string path)
        {
            var imp = (AudioImporter)AssetImporter.GetAtPath(path);
            imp.forceToMono = true;
            imp.loadInBackground = false;
            var s = imp.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.PCM;
            s.preloadAudioData = true;
            imp.defaultSampleSettings = s;
            imp.SaveAndReimport();
        }
    }
}
