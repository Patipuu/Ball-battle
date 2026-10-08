using FishNet.Connection;
using UnityEngine;

namespace TeamNet.Multiplayer.FishNetEos
{
    /// <summary>
    /// Game-supplied answer to "where does this connection's player spawn?". Return false while the
    /// answer is not known yet (for example the join-order mapping has not replicated): the spawner then
    /// waits, and raises its starvation alarm if that goes on too long. Never invent a fallback pose
    /// here — a wrong early spawn is worse than a short wait.
    /// </summary>
    public interface ISpawnSlotResolver
    {
        bool TryResolve(NetworkConnection connection, out Vector3 position, out Quaternion rotation);
    }
}
