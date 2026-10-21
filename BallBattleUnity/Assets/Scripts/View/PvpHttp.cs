using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace BallBattle.View
{
    /// <summary>
    /// Calls to the Ball Battle server (accounts, cloud save). Same rules as PvpView: HTTPS only (or loopback), and a server that
    /// is asleep (Render free plan, about a minute to wake) is waited for and retried; <c>waking</c> fires once when it takes long.
    /// </summary>
    public static class PvpHttp
    {
        public const int WakeTimeoutSeconds = 90;
        public const float WakeNoticeSeconds = 4f;

        [Serializable] sealed class ErrorReply { public string error; }

        /// <summary>Server base URL: PlayerPrefs pvp_server_url, else Resources/pvp-server.txt, else the local dev server.</summary>
        public static string ServerUrl()
        {
            var configured = PlayerPrefs.GetString("pvp_server_url", "");
            if (string.IsNullOrWhiteSpace(configured)) configured = Resources.Load<TextAsset>("pvp-server")?.text?.Trim();
            if (string.IsNullOrWhiteSpace(configured)) configured = "http://127.0.0.1:5088";
            return configured.TrimEnd('/');
        }

        public static bool UrlAllowed(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var endpoint) &&
            (endpoint.Scheme == Uri.UriSchemeHttps || endpoint.Scheme == Uri.UriSchemeHttp && endpoint.IsLoopback);

        /// <summary>Result of one call: HTTP code (0 = no answer), body, and the server's error line (upper case) when it failed.</summary>
        public sealed class Reply
        {
            public long Code;
            public string Text = "";
            public string Error = "";
            public bool Ok => Code >= 200 && Code < 300;
        }

        /// <summary>Test hook: answers every call without the network (route, body) => reply.</summary>
        public static Func<string, string, string, Reply> Fake;

        public static IEnumerator Send(string method, string route, string body, string token, Action<Reply> done, Action waking = null)
        {
            if (Fake != null) { done?.Invoke(Fake(method, route, body)); yield break; }
            var url = ServerUrl();
            if (!UrlAllowed(url)) { done?.Invoke(new Reply { Error = "PVP NEEDS HTTPS SERVER" }); yield break; }
            var started = Time.realtimeSinceStartup;
            var warned = false;
            for (var attempt = 0; ; attempt++)
            using (var request = new UnityWebRequest(url + route, method))
            {
                if (body != null) request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", "Bearer " + token);
                request.timeout = WakeTimeoutSeconds;
                var op = request.SendWebRequest();
                while (!op.isDone)
                {
                    if (!warned && Time.realtimeSinceStartup - started > WakeNoticeSeconds) { warned = true; waking?.Invoke(); }
                    yield return null;
                }
                var code = request.responseCode;
                var gateway = request.result == UnityWebRequest.Result.ConnectionError || code == 502 || code == 503 || code == 504;
                if (gateway && attempt < 3 && Time.realtimeSinceStartup - started < WakeTimeoutSeconds)
                {
                    if (!warned) { warned = true; waking?.Invoke(); }
                    var until = Time.realtimeSinceStartup + 3f;
                    while (Time.realtimeSinceStartup < until) yield return null;
                    continue;
                }
                var reply = new Reply { Code = code, Text = request.downloadHandler?.text ?? "" };
                if (request.result != UnityWebRequest.Result.Success)
                {
                    ErrorReply error = null;
                    try { error = JsonUtility.FromJson<ErrorReply>(reply.Text); }
                    catch (ArgumentException) { }
                    reply.Error = error != null && !string.IsNullOrEmpty(error.error) ? error.error.ToUpperInvariant()
                        : code == 429 ? "TOO MANY TRIES - WAIT A FEW MINUTES" : "SERVER UNAVAILABLE";
                }
                done?.Invoke(reply);
                yield break;
            }
        }
    }
}
