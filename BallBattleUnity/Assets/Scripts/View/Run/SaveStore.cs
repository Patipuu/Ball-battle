using System;
using System.IO;
using BallBattle.Sim.Run;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>Everything saved on the device: long-term progress and the run in progress (if any).</summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;
        public int Version = CurrentVersion;
        public ProgressData Progress = new ProgressData();
        public bool HasRun;
        public RunState Run = new RunState();
    }

    public interface ISaveStore
    {
        SaveData Load();
        void Save(SaveData data);
    }

    /// <summary>JSON file (JsonUtility). Writes a complete temp file first, then copies it over the save (a failed write never touches the old save).</summary>
    public sealed class FileSaveStore : ISaveStore
    {
        public readonly string Path;

        public FileSaveStore(string path) => Path = path;

        public static FileSaveStore Default() => new FileSaveStore(System.IO.Path.Combine(Application.persistentDataPath, "save.json"));

        public SaveData Load()
        {
            if (!File.Exists(Path)) return new SaveData();
            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(Path));
                if (data == null || data.Version > SaveData.CurrentVersion) return Reset("unknown version");
                if (data.Version < SaveData.CurrentVersion)
                {
                    // Older save: keep long-term progress (JsonUtility tolerates new fields), drop the run in progress.
                    data.Version = SaveData.CurrentVersion;
                    data.HasRun = false;
                    data.Run = new RunState();
                }
                if (data.Progress == null) data.Progress = new ProgressData();
                return data;
            }
            catch (Exception e)
            {
                return Reset(e.Message);
            }
        }

        /// <summary>Unreadable save: keep it aside for debugging, start fresh.</summary>
        SaveData Reset(string reason)
        {
            Debug.LogWarning($"Save file unreadable ({reason}); starting fresh. Old file kept as .bad");
            try { File.Copy(Path, Path + ".bad", true); } catch (IOException) { }
            return new SaveData();
        }

        /// <summary>Never throws: a failed write (disk full, storage quirk) is logged and the previous file is kept.</summary>
        public void Save(SaveData data)
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var tmp = Path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data));
                if (File.Exists(Path))
                {
                    File.Copy(tmp, Path, true);   // File.Replace is unreliable on some Android storage
                    File.Delete(tmp);
                }
                else File.Move(tmp, Path);
            }
            catch (Exception e)
            {
                Debug.LogError($"Could not save to {Path}: {e.Message}");
            }
        }
    }

    /// <summary>In-memory store for tests; round-trips through JSON like the real one.</summary>
    public sealed class MemorySaveStore : ISaveStore
    {
        public string Json;
        public int Saves;

        public SaveData Load() => string.IsNullOrEmpty(Json) ? new SaveData() : JsonUtility.FromJson<SaveData>(Json);

        public void Save(SaveData data)
        {
            Json = JsonUtility.ToJson(data);
            Saves++;
        }
    }
}
