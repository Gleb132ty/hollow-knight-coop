using System.Collections.Generic;

namespace Hkmp.Game.Client;

/// <summary>
/// Registry that keeps track of which players are currently downed and waiting for a revive. This is shared
/// between the revive manager and the animation manager so that animation updates coming from downed players
/// can be ignored.
/// </summary>
internal static class DownedPlayerRegistry {
    /// <summary>
    /// Lock object for thread-safe access to the registry.
    /// </summary>
    private static readonly object Lock = new();

    /// <summary>
    /// Set of IDs of remote players that are downed.
    /// </summary>
    private static readonly HashSet<ushort> DownedPlayerIds = [];

    /// <summary>
    /// Whether the local player is downed.
    /// </summary>
    private static bool _localDowned;

    /// <summary>
    /// Check whether the player with the given ID is downed.
    /// </summary>
    /// <param name="id">The ID of the player.</param>
    /// <returns>Whether the player is downed.</returns>
    public static bool IsDowned(ushort id) {
        lock (Lock) {
            return DownedPlayerIds.Contains(id);
        }
    }

    /// <summary>
    /// Mark the player with the given ID as downed.
    /// </summary>
    /// <param name="id">The ID of the player.</param>
    public static void Add(ushort id) {
        lock (Lock) {
            DownedPlayerIds.Add(id);
        }
    }

    /// <summary>
    /// Mark the player with the given ID as no longer downed.
    /// </summary>
    /// <param name="id">The ID of the player.</param>
    public static void Remove(ushort id) {
        lock (Lock) {
            DownedPlayerIds.Remove(id);
        }
    }

    /// <summary>
    /// Whether the local player is downed.
    /// </summary>
    public static bool IsLocalDowned {
        get {
            lock (Lock) {
                return _localDowned;
            }
        }
    }

    /// <summary>
    /// Set whether the local player is downed.
    /// </summary>
    /// <param name="downed">Whether the local player is downed.</param>
    public static void SetLocalDowned(bool downed) {
        lock (Lock) {
            _localDowned = downed;
        }
    }

    /// <summary>
    /// Clear all tracked downed states, both remote and local. Used on disconnect and scene changes.
    /// </summary>
    public static void Clear() {
        lock (Lock) {
            DownedPlayerIds.Clear();
            _localDowned = false;
        }
    }

    /// <summary>
    /// Clear only the tracked remote downed states. Used when the local player changes scenes, since remote
    /// avatars are recycled and the server resends the downed state for players in the new scene.
    /// </summary>
    public static void ClearRemote() {
        lock (Lock) {
            DownedPlayerIds.Clear();
        }
    }
}
