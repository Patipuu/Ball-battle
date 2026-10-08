using System;

namespace TeamNet.Multiplayer.Core
{
    /// <summary>Where the game persists the last room id so a player can rejoin after an app kill.</summary>
    public interface IRoomHistoryStore
    {
        bool HasLastRoomId();
        string GetLastRoomId();
        void SetLastRoomId(string lobbyId);
        void ClearLastRoomId();
    }

    /// <summary>
    /// Diagnostic output. Every template takes an optional <c>Action&lt;string&gt;</c> log; adapt a sink
    /// with <see cref="DiagnosticsSinkExtensions.AsLogAction"/>. Implement it over UnityEngine.Debug,
    /// a file, or your telemetry pipeline in the game assembly.
    /// </summary>
    public interface IDiagnosticsSink
    {
        void Info(string message);
        void Warning(string message);
        void Error(string message);
    }

    public static class DiagnosticsSinkExtensions
    {
        /// <summary>Adapts a sink to the <c>Action&lt;string&gt;</c> the templates accept (null sink = silent).</summary>
        public static Action<string> AsLogAction(this IDiagnosticsSink sink) =>
            sink == null ? null : sink.Info;
    }
}
