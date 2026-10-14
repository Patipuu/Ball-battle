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
        public const int CurrentVersion = 2;
        public int Version = CurrentVersion;
        public ProgressData Progress = new ProgressData();
        public bool HasRun;
        /// <summary>The run in its save form (doubles, same JSON names as before 6a.6b/c); RunSave.ToRun() gives the Sim RunState.</summary>
        public RunSave Run = new RunSave();
        /// <summary>Music / SFX volume steps 0..10 (BB-03). Older saves have no such fields and load the defaults; no version bump.</summary>
        public int musicVol = VolumeSettings.DefaultMusic;
        public int sfxVol = VolumeSettings.DefaultSfx;
        /// <summary>Set by SaveMigration when it had to drop a run from an older save; the menu shows a one-time notice, OK clears it.</summary>
        public bool oldRunNotice;
    }

    /// <summary>
    /// Upgrades an older save in place (both stores call it on Load). v1 → v2: the run in progress cannot be converted (the sim
    /// changed under it), so it is dropped; as compensation it counts as having reached its current fight (BestFight), it is NOT
    /// counted as played/won/lost, and oldRunNotice is set so the player is told once. Achievements, unlocks and every other
    /// record stay as they were (unlocks come from achievements; the next goal is recomputed from progress on demand).
    /// </summary>
    public static class SaveMigration
    {
        public static SaveData Upgrade(SaveData data)
        {
            if (data == null || data.Version >= SaveData.CurrentVersion) return data;
            if (data.Progress == null) data.Progress = new ProgressData();
            if (data.HasRun && data.Run != null)
            {
                var reached = Math.Min(Math.Max(data.Run.FightIndex + 1, 1), RunTuning.Fights);
                if (reached > data.Progress.BestFight) data.Progress.BestFight = reached;
                data.oldRunNotice = true;
            }
            data.Version = SaveData.CurrentVersion;
            data.HasRun = false;
            data.Run = new RunSave();
            return data;
        }
    }

    /// <summary>The one-time "OLD RUN ENDED BY UPDATE" notice, read and cleared through the save store.</summary>
    public static class OldRunNotice
    {
        public static bool Pending(ISaveStore store) => store?.Load()?.oldRunNotice == true;

        /// <summary>OK: clear the flag and save, so it never shows again.</summary>
        public static void Dismiss(ISaveStore store)
        {
            var d = store?.Load();
            if (d == null || !d.oldRunNotice) return;
            d.oldRunNotice = false;
            store.Save(d);
        }
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
                SaveMigration.Upgrade(data);   // older save: keep progress, drop the run (with compensation + notice)
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

        public SaveData Load() => string.IsNullOrEmpty(Json) ? new SaveData() : SaveMigration.Upgrade(JsonUtility.FromJson<SaveData>(Json));

        public void Save(SaveData data)
        {
            Json = JsonUtility.ToJson(data);
            Saves++;
        }
    }
}
