using System;
using System.Collections;
using BallBattle.Sim.Run;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// The player's account on this device (Phu 21/10: a name and a password on first launch identify the player).
    /// The token is the PvP token (PlayerPrefs pvp_token, shared with PvpView); the name and the cloud save version sit beside it.
    /// </summary>
    public static class AccountSession
    {
        public const string TokenKey = "pvp_token", NameKey = "account_name", SaveVersionKey = "account_save_version";

        public static string Token => PlayerPrefs.GetString(TokenKey, "");
        public static string Name => PlayerPrefs.GetString(NameKey, "");
        public static bool HasAccount => !string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(Token);
        /// <summary>Cloud save version this device last read or wrote (0 = none).</summary>
        public static int SaveVersion
        {
            get => PlayerPrefs.GetInt(SaveVersionKey, 0);
            set { PlayerPrefs.SetInt(SaveVersionKey, value); PlayerPrefs.Save(); }
        }
        /// <summary>"Later" was chosen this session (server unreachable): the account screen is not forced again until restart.</summary>
        public static bool SkippedThisSession;

        public static void SignedIn(string token, string name, int saveVersion)
        {
            PlayerPrefs.SetString(TokenKey, token ?? "");
            PlayerPrefs.SetString(NameKey, name ?? "");
            PlayerPrefs.SetInt(SaveVersionKey, saveVersion);
            PlayerPrefs.Save();
        }

        /// <summary>Forget the account on this device (the local game save stays).</summary>
        public static void SignOutLocal()
        {
            PlayerPrefs.DeleteKey(TokenKey);
            PlayerPrefs.DeleteKey(NameKey);
            PlayerPrefs.DeleteKey(SaveVersionKey);
            PlayerPrefs.Save();
        }

        /// <summary>The game save as the server stores it (JsonUtility, same as the local file).</summary>
        public static string SaveJson(SaveData data) => JsonUtility.ToJson(data);

        public static SaveData ParseSave(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return SaveMigration.Upgrade(JsonUtility.FromJson<SaveData>(json)); }
            catch (ArgumentException) { return null; }
        }

        /// <summary>True when this device has nothing worth keeping (fresh install): a cloud save simply replaces it.</summary>
        public static bool LocalIsFresh(SaveData data)
        {
            if (data == null || data.Progress == null) return true;
            var p = data.Progress;
            return p.RunsPlayed == 0 && !data.HasRun && p.Gold == 0 && (p.Balls == null || p.Balls.TrueForAll(b => b.Level <= 1 && b.Shards == 0));
        }

        [Serializable] sealed class SaveBody { public string Data; public int BaseVersion; }
        [Serializable] sealed class SaveReply { public bool ok, conflict; public int version; public string data; }

        /// <summary>Last upload time (realtime) and whether the last try met a newer save from another device.</summary>
        static float lastUpload = -999f;
        public static bool Conflict { get; private set; }
        public const float UploadEverySeconds = 30f;

        /// <summary>
        /// Upload the local save if signed in and the last upload is old enough. Never overwrites a newer cloud save:
        /// a version mismatch sets <see cref="Conflict"/> (Settings > account asks which one to keep).
        /// </summary>
        public static IEnumerator Upload(ISaveStore store, bool force = false)
        {
            if (!HasAccount || store == null) yield break;
            if (!force && Time.realtimeSinceStartup - lastUpload < UploadEverySeconds) yield break;
            lastUpload = Time.realtimeSinceStartup;
            var body = JsonUtility.ToJson(new SaveBody { Data = SaveJson(store.Load()), BaseVersion = SaveVersion });
            PvpHttp.Reply reply = null;
            yield return PvpHttp.Send("PUT", "/v1/me/save", body, Token, r => reply = r);
            if (reply == null || !reply.Ok) yield break;
            var parsed = JsonUtility.FromJson<SaveReply>(reply.Text);
            if (parsed.conflict) { Conflict = true; yield break; }
            Conflict = false;
            SaveVersion = parsed.version;
        }

        /// <summary>Cloud save: (json or "" when none, version). Null reply = network error.</summary>
        public static IEnumerator Download(Action<string, int> done)
        {
            PvpHttp.Reply reply = null;
            yield return PvpHttp.Send("GET", "/v1/me/save", null, Token, r => reply = r);
            if (reply == null || !reply.Ok) { done?.Invoke(null, -1); yield break; }
            var parsed = JsonUtility.FromJson<SaveReply>(reply.Text);
            done?.Invoke(parsed.data ?? "", parsed.version);
        }

        /// <summary>Use the cloud save on this device (written to the local file; the caller reloads the scene).</summary>
        public static bool Adopt(ISaveStore store, string json, int version)
        {
            var data = ParseSave(json);
            if (data == null) return false;
            store.Save(data);
            SaveVersion = version;
            Conflict = false;
            return true;
        }

        /// <summary>Keep this device's save: overwrite the cloud one (version <paramref name="cloudVersion"/>).</summary>
        public static IEnumerator ForceUpload(ISaveStore store, int cloudVersion)
        {
            SaveVersion = cloudVersion;
            yield return Upload(store, true);
        }
    }
}
