using System;

namespace TeamNet.Multiplayer.Core
{
    /// <summary>
    /// Ports Godot <c>MultiplayerManagerEOS</c>'s stale-operation guard
    /// (<c>_is_lobby_operation_stale</c>, MultiplayerManagerEOS.gd:47-57). An async
    /// lobby op captures a generation token when it begins; when its result lands it
    /// must be DROPPED if its generation was superseded, recoverable teardown/scene transition is
    /// active, or its owning lifecycle was terminally invalidated:
    /// <list type="number">
    ///   <item>its generation no longer matches the current one (a newer op superseded it),</item>
    ///   <item>a lobby teardown is active, or</item>
    ///   <item>a scene transition is in progress.</item>
    /// </list>
    /// Pure logic with no Unity/EOS dependency (logging is injected) so it is fully
    /// unit-testable per R4. Wired into <c>EosLobbyService</c> so every lobby op passes
    /// through it.
    /// </summary>
    public sealed class GenerationGuard
    {
        private readonly Action<string> _log;
        private int _generation;
        private bool _teardownActive;
        private bool _sceneTransitioning;
        private bool _invalidated;

        /// <param name="log">Optional diagnostic sink (mirrors Godot Utils.attention_log). Null = silent.</param>
        public GenerationGuard(Action<string> log = null) => _log = log;

        public int Generation => _generation;
        public bool TeardownActive => _teardownActive;
        public bool SceneTransitioning => _sceneTransitioning;
        public bool Invalidated => _invalidated;

        /// <summary>
        /// Begin a lobby operation: clears the teardown flag and bumps the generation.
        /// Returns the token the caller passes back to <see cref="IsStale"/> when its
        /// async result lands. (Godot <c>_begin_lobby_operation</c>:44-46)
        /// </summary>
        public int BeginOperation(string reason)
        {
            if (_invalidated)
            {
                _log?.Invoke($"EOS: refusing operation on invalidated lifecycle ({reason})");
                return _generation;
            }
            _teardownActive = false;
            return BumpGeneration(reason);
        }

        /// <summary>
        /// Increment and return the current generation. (Godot <c>_bump_lobby_generation</c>:39-42)
        /// </summary>
        public int BumpGeneration(string reason)
        {
            _generation++;
            _log?.Invoke($"EOS lobby generation {_generation} ({reason})");
            return _generation;
        }

        /// <summary>
        /// True if an operation carrying <paramref name="generation"/> must be ignored.
        /// Extends Godot's three recoverable stale conditions with terminal lifecycle invalidation.
        /// </summary>
        public bool IsStale(int generation, string context)
        {
            if (generation != _generation)
            {
                _log?.Invoke($"EOS: Ignoring stale lobby operation {context} generation={generation} current={_generation}");
                return true;
            }
            if (_invalidated)
            {
                _log?.Invoke($"EOS: Ignoring lobby operation after lifecycle invalidation: {context}");
                return true;
            }
            if (_teardownActive)
            {
                _log?.Invoke($"EOS: Ignoring lobby operation during teardown: {context}");
                return true;
            }
            if (_sceneTransitioning)
            {
                _log?.Invoke($"EOS: Ignoring lobby operation during scene transition: {context}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Mark a teardown active so in-flight ops are dropped until the next
        /// <see cref="BeginOperation"/>. (Godot sets <c>_is_lobby_teardown_active</c>.)
        /// </summary>
        public void BeginTeardown() => _teardownActive = true;

        /// <summary>
        /// Terminal lifecycle invalidation. Unlike <see cref="BeginTeardown"/>, a later
        /// <see cref="BeginOperation"/> cannot reopen this guard.
        /// </summary>
        public void Invalidate(string reason)
        {
            if (_invalidated) return;
            _invalidated = true;
            _teardownActive = true;
            BumpGeneration(reason);
        }

        /// <summary>
        /// Enter a scene transition: idempotent set + a generation bump so ops issued
        /// before the transition are superseded too. (Godot <c>prepare_for_scene_transition</c>:70-74)
        /// </summary>
        public void PrepareSceneTransition(string reason = "scene_transition")
        {
            if (_sceneTransitioning)
                return;
            _sceneTransitioning = true;
            BumpGeneration(reason);
        }

        /// <summary>
        /// Exit a scene transition: idempotent clear. Returns true only if a transition
        /// was actually in progress, so the caller can resume a deferred lobby refresh
        /// as Godot does. (Godot <c>complete_scene_transition</c>:76-82)
        /// </summary>
        public bool CompleteSceneTransition(string reason = "scene_transition_complete")
        {
            if (!_sceneTransitioning)
                return false;
            _sceneTransitioning = false;
            _log?.Invoke($"EOS: Scene transition complete ({reason})");
            return true;
        }
    }
}
