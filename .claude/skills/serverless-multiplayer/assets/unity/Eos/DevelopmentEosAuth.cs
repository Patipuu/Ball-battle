using System;
using System.Globalization;
using Epic.OnlineServices;
using Epic.OnlineServices.Auth;
using FishNet.Plugins.FishyEOS.Util;
using UnityEngine;

namespace TeamNet.Multiplayer.Eos
{
    /// <summary>
    /// One auth policy for boot and lobby reconnect, driving the Epic Dev Auth Tool so several local
    /// players (Editor + clones, or standalone dev builds) get distinct EOS identities.
    /// Fail-closed: an opt-in with an invalid endpoint or credential label returns an error and never
    /// falls back to DeviceID. Compiled only for the Editor and standalone DEVELOPMENT_BUILDs, so it is
    /// inert in mobile/release players.
    /// </summary>
    public static class DevelopmentEosAuth
    {
        /// <summary>Command-line switches. Both are configurable; set them before the first <see cref="TryResolve"/>.</summary>
        public static string EndpointArg = "-eosDevAuth=";
        public static string CredentialArg = "-eosCredential=";

        /// <summary>
        /// Prefix of the project-scoped EditorPrefs keys. Set to something unique to your project
        /// (for example your product name) so two projects on one machine do not share settings.
        /// </summary>
        public static string EditorPreferencePrefix = "TeamNet.EosDevAuth.";

        /// <summary>
        /// True when this player is a clone (e.g. a Multiplayer Play Mode virtual player) that must use the
        /// second credential. Supplied by the game; the default treats every instance as the main player.
        /// </summary>
        public static Func<bool> IsVirtualPlayerProvider = () => false;

        static bool captured;
        static string endpoint;
        static string credential;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession()
        {
            captured = false;
            endpoint = credential = null;
        }

        /// <summary>Snapshot once per Play session; changing settings requires stopping all players.</summary>
        public static bool TryResolve(AuthData original, out AuthData auth, out string error)
        {
            auth = original;
            error = null;
#if UNITY_EDITOR || (DEVELOPMENT_BUILD && UNITY_STANDALONE)
            if (!captured)
            {
                var args = Environment.GetCommandLineArgs();
                endpoint = ReadArgument(args, EndpointArg);
                credential = ReadArgument(args, CredentialArg);
#if UNITY_EDITOR
                if (endpoint == null && credential == null && EditorEnabled
                    && !UsesDeviceId(IsVirtualPlayerProvider(), EditorMainUsesDeviceId))
                {
                    endpoint = "localhost:" + EditorPort.ToString(CultureInfo.InvariantCulture);
                    credential = ResolveEditorCredential(IsVirtualPlayerProvider(), args,
                        EditorMainCredential, EditorSecondCredential);
                }
#endif
                captured = true;
            }
            if (endpoint != null || credential != null)
            {
                // Never modify serialized transport data, including with scene reload disabled.
                auth = new AuthData
                {
                    displayName = original?.displayName ?? "FishyEOS",
                    timeout = original?.timeout ?? 30f,
                };
                return TryApply(auth, endpoint, credential, out error);
            }
#endif
            return true;
        }

        /// <summary>Validate before mutating: an invalid opt-in must never fall back to DeviceID.</summary>
        public static bool TryApply(AuthData auth, string address, string label, out string error)
        {
            error = null;
            string[] parts = (address ?? string.Empty).Split(':');
            if (parts.Length != 2 || (parts[0] != "localhost" && parts[0] != "127.0.0.1") ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int port) ||
                port < 1 || port > 65535)
            {
                error = "eos-dev-auth-invalid-endpoint: use localhost:PORT (1-65535)";
                return false;
            }
            if (string.IsNullOrWhiteSpace(label) || label.Length > 64 ||
                label != label.Trim() || ContainsControlCharacter(label))
            {
                error = "eos-dev-auth-missing-or-invalid-credential: configure a distinct Dev Auth Tool label per player";
                return false;
            }
            if (auth == null)
            {
                error = "eos-dev-auth-missing-auth-data";
                return false;
            }
            auth.loginCredentialType = LoginCredentialType.Developer;
            // FishyEOS Developer flow copies an Auth ACCESS token, not an ID token.
            auth.externalCredentialType = ExternalCredentialType.Epic;
            auth.id = address;
            auth.token = label; // Tool credential NAME only. Never an Epic password.
            auth.authScopeFlags = AuthScopeFlags.BasicProfile;
            auth.automaticallyCreateDeviceId = false;
            auth.automaticallyCreateConnectAccount = true;
            return true;
        }

        static bool ContainsControlCharacter(string value)
        {
            foreach (char c in value) if (char.IsControl(c)) return true;
            return false;
        }

        static string ReadArgument(string[] args, string prefix)
        {
            foreach (string arg in args)
                if (arg != null && arg.StartsWith(prefix, StringComparison.Ordinal))
                    return arg.Substring(prefix.Length);
            return null;
        }

        /// <summary>
        /// Mixed identities (FishyEOS README: "use the same device to log into both if they are
        /// different sign in providers"): the main Editor keeps the anonymous DeviceID login while
        /// MPPM players use the Dev Auth Tool, so ONE Epic account yields two distinct PUIDs.
        /// </summary>
        public static bool UsesDeviceId(bool isVirtualPlayer, bool mainUsesDeviceId)
            => mainUsesDeviceId && !isVirtualPlayer;

        /// <summary>MPPM 2.0.2 supplies '-name Player N'. Unknown clone names fail closed.</summary>
        public static string ResolveEditorCredential(bool isVirtualPlayer, string[] args,
            string mainCredential = "Player1", string secondCredential = "Player2")
        {
            if (!isVirtualPlayer) return mainCredential;
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] != "-name") continue;
                string name = args[i + 1];
                if (name != null && name.StartsWith("Player ", StringComparison.Ordinal) &&
                    int.TryParse(name.Substring(7), NumberStyles.None, CultureInfo.InvariantCulture, out int number) &&
                    number >= 2)
                    return number == 2 ? secondCredential : "Player" + number.ToString(CultureInfo.InvariantCulture);
            }
            return null;
        }

#if UNITY_EDITOR
        // Main Editor and Library/VP clones share this project-scoped preference.
        public static string EditorPreferenceKey => PreferenceKeyForDataPath(Application.dataPath);

        public static string PreferenceKeyForDataPath(string dataPath)
        {
            string path = dataPath.Replace('\\', '/');
            int clone = path.IndexOf("/Library/VP/", StringComparison.Ordinal);
            string root = clone >= 0 ? path.Substring(0, clone) : System.IO.Path.GetDirectoryName(path);
            return EditorPreferencePrefix + root;
        }

        public static bool EditorEnabled => UnityEditor.EditorPrefs.GetBool(EditorPreferenceKey + ".enabled", false);
        public static int EditorPort => UnityEditor.EditorPrefs.GetInt(EditorPreferenceKey + ".port", 8888);
        public static string EditorMainCredential => UnityEditor.EditorPrefs.GetString(EditorPreferenceKey + ".mainCredential", "Player1");
        public static string EditorSecondCredential => UnityEditor.EditorPrefs.GetString(EditorPreferenceKey + ".secondCredential", "Player2");
        /// <summary>Main Editor logs in with the anonymous DeviceID; only MPPM players use the tool.</summary>
        public static bool EditorMainUsesDeviceId => UnityEditor.EditorPrefs.GetBool(EditorPreferenceKey + ".mainDeviceId", false);

        public static void SetEditorSettings(bool enabled, int port)
            => SetEditorSettings(enabled, port, EditorMainCredential, EditorSecondCredential, EditorMainUsesDeviceId);

        public static void SetEditorSettings(bool enabled, int port, string mainCredential, string secondCredential)
            => SetEditorSettings(enabled, port, mainCredential, secondCredential, EditorMainUsesDeviceId);

        public static void SetEditorSettings(bool enabled, int port, string mainCredential, string secondCredential,
            bool mainUsesDeviceId)
        {
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop all players before changing EOS Dev Auth settings.");
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            string address = "localhost:" + port.ToString(CultureInfo.InvariantCulture);
            // With the main Editor on DeviceID its label is unused (kept as typed, not validated).
            if ((!mainUsesDeviceId && !TryApply(new AuthData(), address, mainCredential, out string error)) ||
                !TryApply(new AuthData(), address, secondCredential, out error))
                throw new ArgumentException(error);
            if (!mainUsesDeviceId && mainCredential == secondCredential)
                throw new ArgumentException("Use distinct Dev Auth Tool credential labels for the two players.");
            UnityEditor.EditorPrefs.SetString(EditorPreferenceKey + ".mainCredential", mainCredential);
            UnityEditor.EditorPrefs.SetString(EditorPreferenceKey + ".secondCredential", secondCredential);
            UnityEditor.EditorPrefs.SetInt(EditorPreferenceKey + ".port", port);
            UnityEditor.EditorPrefs.SetBool(EditorPreferenceKey + ".mainDeviceId", mainUsesDeviceId);
            UnityEditor.EditorPrefs.SetBool(EditorPreferenceKey + ".enabled", enabled);
            ResetSession();
        }
#endif
    }
}
