using System;
using System.Collections.Generic;
using Npgsql;

namespace BallBattle.PvpServer
{
    /// <summary>
    /// Render + Neon hosting (Phu 21/10): Render's free disk is wiped on every restart and spin-down, so the whole server state
    /// lives in one Postgres row instead of a file. Same model as the JSON file (one process, saved after every change).
    /// Table: pvp_state(id int primary key, data text, updated_utc timestamptz), created on first start.
    /// </summary>
    public sealed class PgPersistence : IPvpPersistence
    {
        readonly string connectionString;

        public PgPersistence(string databaseUrl)
        {
            connectionString = ToConnectionString(databaseUrl);
            using var conn = Open();
            using var cmd = new NpgsqlCommand(
                "CREATE TABLE IF NOT EXISTS pvp_state (id int PRIMARY KEY, data text NOT NULL, updated_utc timestamptz NOT NULL DEFAULT now())", conn);
            cmd.ExecuteNonQuery();
        }

        NpgsqlConnection Open()
        {
            var conn = new NpgsqlConnection(connectionString);
            conn.Open();
            return conn;
        }

        public string Load()
        {
            using var conn = Open();
            using var cmd = new NpgsqlCommand("SELECT data FROM pvp_state WHERE id = 1", conn);
            return cmd.ExecuteScalar() as string;
        }

        public void Save(string json)
        {
            using var conn = Open();
            using var cmd = new NpgsqlCommand(
                "INSERT INTO pvp_state (id, data, updated_utc) VALUES (1, @d, now()) ON CONFLICT (id) DO UPDATE SET data = EXCLUDED.data, updated_utc = now()", conn);
            cmd.Parameters.AddWithValue("d", json);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Neon / Render give a URL ("postgresql://user:pass@host/db?sslmode=require"); Npgsql wants key=value pairs.
        /// A string that is already key=value passes through.
        /// </summary>
        public static string ToConnectionString(string databaseUrl)
        {
            if (string.IsNullOrWhiteSpace(databaseUrl)) throw new ArgumentException("DATABASE_URL is empty.");
            databaseUrl = databaseUrl.Trim();
            if (!databaseUrl.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
                !databaseUrl.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)) return databaseUrl;
            var uri = new Uri(databaseUrl);
            var user = uri.UserInfo.Split(new[] { ':' }, 2);
            var b = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 5432,
                Username = Uri.UnescapeDataString(user[0]),
                Password = user.Length > 1 ? Uri.UnescapeDataString(user[1]) : null,
                Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
                SslMode = SslMode.Require,
                Pooling = true,
                Timeout = 30,   // Neon wakes a suspended database in about a second; a cold Render start adds more
            };
            foreach (var pair in Query(uri.Query))
                if (pair.Key.Equals("sslmode", StringComparison.OrdinalIgnoreCase) && Enum.TryParse<SslMode>(pair.Value.Replace("-", ""), true, out var mode))
                    b.SslMode = mode;
            return b.ConnectionString;
        }

        static IEnumerable<KeyValuePair<string, string>> Query(string query)
        {
            foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=', 2);
                yield return new KeyValuePair<string, string>(Uri.UnescapeDataString(kv[0]), kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "");
            }
        }
    }
}
