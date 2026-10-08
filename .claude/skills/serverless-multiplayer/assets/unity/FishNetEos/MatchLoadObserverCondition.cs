using FishNet.Connection;
using FishNet.Observing;
using UnityEngine;

namespace TeamNet.Multiplayer.FishNetEos
{
    /// <summary>
    /// Server-side visibility gate: a remote peer sees NO networked objects until it reports having
    /// loaded the current match (the same load barrier that gates player spawning). Without this, a
    /// rematch keeps the transport alive across the scene switch — the host loads first and streams the
    /// new scene's objects while the slower client is still loading, FishNet on that client drops the
    /// spawns ("SceneId not found in SceneObjects"), and the player lands in a half-empty scene.
    ///
    /// Timed condition: the loaded flag arrives via an EOS lobby attribute, which triggers no observer
    /// rebuild by itself, so FishNet must re-check on its interval. When the flag flips for a new epoch
    /// the peer briefly loses visibility and is then resynced against the scene it actually has.
    ///
    /// Wiring: create the asset (Assets > Create > TeamNet > Observers), add it to the NetworkManager's
    /// ObserverManager and to every networked prefab/scene object that must be gated, and install an
    /// <see cref="IMatchLoadGate"/> in <see cref="MatchLoadGate.Provider"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "TeamNet/Observers/Match Load Condition",
        fileName = "MatchLoadObserverCondition")]
    public sealed class MatchLoadObserverCondition : ObserverCondition
    {
        public override bool ConditionMet(NetworkConnection connection, bool currentlyAdded, out bool notProcessed)
        {
            notProcessed = false;

            // The host's own client shares the server's scene by definition.
            if (NetworkObject.ClientManager.Connection == connection)
                return true;

            IMatchLoadGate gate = MatchLoadGate.Provider;
            if (gate == null)
                return true; // no session system (gym/dev scene) — never withhold

            // Only multiplayer room matches are gated: a solo room has no slower peer to protect, and
            // withholding there would fight direct-scene-load dev flows.
            if (!gate.IsBarrierActive)
                return true;

            return gate.HasClientLoadedCurrentMatch(connection.ClientId);
        }

        public override ObserverConditionType GetConditionType() => ObserverConditionType.Timed;
    }
}
