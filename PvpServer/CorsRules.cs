using System;
using System.Linq;

namespace BallBattle.PvpServer
{
    /// <summary>Browser origins allowed to call the service: itch.io's HTML5 game hosts, local tests, and configured extras.</summary>
    public static class CorsRules
    {
        /// <summary>itch.io serves HTML5 games from *.itch.zone (newer, SharedArrayBuffer) or this legacy CDN host.</summary>
        static readonly string[] ItchHosts = { "itch.zone", "v6p9d9t4.ssl.hwcdn.net", "itch.io" };

        public static bool Allowed(string origin, string[] extra)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
            if (extra != null && extra.Any(e => string.Equals(e.TrimEnd('/'), origin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))) return true;
            if (uri.IsLoopback) return true;   // a local WebGL build served by Unity's Build And Run
            if (uri.Scheme != Uri.UriSchemeHttps) return false;
            var host = uri.Host.ToLowerInvariant();
            return ItchHosts.Any(h => host == h || host.EndsWith("." + h, StringComparison.Ordinal));
        }
    }
}
