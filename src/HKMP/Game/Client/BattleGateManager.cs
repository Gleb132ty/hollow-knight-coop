using System.Collections.Generic;
using Hkmp.Game.Client.Entity;
using Hkmp.Game.Settings;
using Hkmp.Networking.Packet;
using Hkmp.Networking.Packet.Data;
using Hkmp.Util;
using UnityEngine;
using Logger = Hkmp.Logging.Logger;
using EntityInstance = Hkmp.Game.Client.Entity.Entity;

namespace Hkmp.Game.Client;

/// <summary>
/// Manager that keeps battle gates open for a scene client until that client has entered the arena.
/// Host FSM updates for the gate are buffered while the local player is outside and replayed once the
/// local player is inside, so the gate does not close in front of the player.
/// </summary>
internal class BattleGateManager {
    /// <summary>
    /// How often the position of the local player is checked, in seconds.
    /// </summary>
    private const float EvaluationInterval = 0.25f;

    /// <summary>
    /// How much closer to the arena reference the player has to be to enter, in world units.
    /// </summary>
    private const float EnterMargin = 1f;

    /// <summary>
    /// How much farther the player can move before being considered outside again, in world units.
    /// </summary>
    private const float ExitMargin = 1.5f;

    /// <summary>
    /// Maximum distance of an enemy to the gate for it to be used as an arena reference.
    /// </summary>
    private const float EnemyReferenceRadius = 40f;

    /// <summary>
    /// Maximum distance of a remote player to the gate for them to be used as an arena reference.
    /// </summary>
    private const float RemotePlayerReferenceRadius = 30f;

    /// <summary>
    /// Maximum time updates are held while the player cannot be located, in seconds.
    /// </summary>
    private const float HoldTimeout = 25f;

    /// <summary>
    /// The entity manager used to apply buffered updates.
    /// </summary>
    private readonly EntityManager _entityManager;

    /// <summary>
    /// Mapping of remote player IDs to their client player data.
    /// </summary>
    private readonly Dictionary<ushort, ClientPlayerData> _playerData;

    /// <summary>
    /// The mod settings instance.
    /// </summary>
    private readonly ModSettings _modSettings;

    /// <summary>
    /// Buffered state per gate entity ID.
    /// </summary>
    private readonly Dictionary<ushort, GateState> _states = [];

    /// <summary>
    /// Whether the update hook of this manager is registered.
    /// </summary>
    private bool _hooksRegistered;

    public BattleGateManager(
        EntityManager entityManager,
        Dictionary<ushort, ClientPlayerData> playerData,
        ModSettings modSettings
    ) {
        _entityManager = entityManager;
        _playerData = playerData;
        _modSettings = modSettings;
    }

    /// <summary>
    /// Register the update hook used to release buffered gate updates.
    /// </summary>
    public void RegisterHooks() {
        if (_hooksRegistered) {
            return;
        }

        _hooksRegistered = true;

        if (MonoBehaviourUtil.Instance != null) {
            MonoBehaviourUtil.Instance.OnUpdateEvent += OnUpdate;
        }
    }

    /// <summary>
    /// Deregister the update hook of this manager.
    /// </summary>
    public void DeregisterHooks() {
        if (!_hooksRegistered) {
            return;
        }

        _hooksRegistered = false;

        if (MonoBehaviourUtil.Instance != null) {
            MonoBehaviourUtil.Instance.OnUpdateEvent -= OnUpdate;
        }

        Clear();
    }

    /// <summary>
    /// Clear buffered gate state. Called when the scene changes or the client disconnects.
    /// </summary>
    public void OnSceneChanged() {
        Clear();
    }

    /// <summary>
    /// Clear buffered gate state on disconnect.
    /// </summary>
    public void OnDisconnected() {
        Clear();
    }

    /// <summary>
    /// Check whether a reliable gate update should be buffered instead of applied immediately.
    /// </summary>
    /// <param name="entity">The gate entity the update belongs to.</param>
    /// <param name="update">The update to check.</param>
    /// <returns>Whether the update should be buffered.</returns>
    public bool ShouldBuffer(EntityInstance entity, ReliableEntityUpdate update) {
        if (!IsEnabled() || entity == null || entity.Object.Client == null) {
            return false;
        }

        if (HeroController.instance == null) {
            return false;
        }

        if (_states.TryGetValue(entity.Id, out var existing)) {
            if (Time.time - existing.FirstBufferedTime > HoldTimeout) {
                Flush(existing);
                return false;
            }

            return true;
        }

        if (!TryGetReference(entity, out var reference)) {
            return false;
        }

        var state = GetOrCreateState(entity);
        if (IsInside(state, entity, true, reference)) {
            _states.Remove(entity.Id);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Buffer a reliable gate update and disable the solid colliders of the gate while holding.
    /// </summary>
    /// <param name="entity">The gate entity the update belongs to.</param>
    /// <param name="update">The update to buffer.</param>
    /// <param name="alreadyInSceneUpdate">Whether the update came from an already in scene packet.</param>
    public void Buffer(EntityInstance entity, ReliableEntityUpdate update, bool alreadyInSceneUpdate) {
        if (!IsEnabled() || entity == null || entity.Object.Client == null) {
            return;
        }

        var state = GetOrCreateState(entity);
        if (state.Updates.Count == 0) {
            state.FirstBufferedTime = Time.time;
        }

        state.Updates.Add(new BufferedUpdate {
            Update = Clone(update),
            AlreadyInSceneUpdate = alreadyInSceneUpdate
        });

        DisableSolidColliders(state);
    }

    /// <summary>
    /// Whether the gate hold feature is active for the local client.
    /// </summary>
    /// <returns>Whether a scene client should hold gate updates.</returns>
    private bool IsEnabled() {
        return _modSettings != null
               && _modSettings.GateHoldOpen
               && _entityManager != null
               && _entityManager.IsSceneHostDetermined
               && !_entityManager.IsSceneHost;
    }

    /// <summary>
    /// Callback for the Unity update tick, used to release gates once the local player is inside.
    /// </summary>
    private void OnUpdate() {
        if (_states.Count == 0) {
            return;
        }

        if (!IsEnabled() || HeroController.instance == null) {
            FlushAll();
            return;
        }

        var now = Time.time;

        foreach (var state in new List<GateState>(_states.Values)) {
            if (state.Entity == null || state.Entity.Object.Client == null) {
                RemoveState(state);
                continue;
            }

            if (now - state.FirstBufferedTime > HoldTimeout) {
                Flush(state);
                continue;
            }

            if (now < state.NextCheckTime) {
                continue;
            }

            state.NextCheckTime = now + EvaluationInterval;

            if (!TryGetReference(state.Entity, out var reference)) {
                continue;
            }

            if (IsInside(state, state.Entity, true, reference)) {
                Flush(state);
            } else {
                DisableSolidColliders(state);
            }
        }
    }

    /// <summary>
    /// Check whether the local player is inside the arena for the given gate.
    /// </summary>
    /// <param name="state">The buffered gate state.</param>
    /// <param name="gate">The gate entity.</param>
    /// <param name="haveReference">Whether a valid reference position was found.</param>
    /// <param name="reference">The reference position inside the arena.</param>
    /// <returns>Whether the local player is inside the arena.</returns>
    private bool IsInside(GateState state, EntityInstance gate, bool haveReference, Vector3 reference) {
        var hero = HeroController.instance;
        if (hero == null) {
            return true;
        }

        var playerPosition = (Vector2) hero.transform.position;
        var gatePosition = (Vector2) GetGatePosition(gate);

        bool inside;
        if (haveReference) {
            var reference2D = (Vector2) reference;
            var playerDistance = Vector2.Distance(playerPosition, reference2D);
            var gateDistance = Vector2.Distance(gatePosition, reference2D);

            if (state.Inside) {
                inside = playerDistance <= gateDistance + ExitMargin;
            } else {
                inside = playerDistance + EnterMargin <= gateDistance;
            }
        } else {
            inside = state.Inside;
        }

        state.Inside = inside;

        return inside;
    }

    /// <summary>
    /// Find a position that represents the inside of the arena for the given gate. Prefers living bosses,
    /// then other living enemies, and finally remote players that are likely already inside.
    /// </summary>
    /// <param name="gate">The gate entity.</param>
    /// <param name="reference">The reference position.</param>
    /// <returns>Whether a reference position was found.</returns>
    private bool TryGetReference(EntityInstance gate, out Vector3 reference) {
        reference = Vector3.zero;

        var gatePosition = GetGatePosition(gate);

        var bossSceneController = BossSceneController.Instance;
        if (bossSceneController != null && bossSceneController.bosses != null) {
            HealthManager nearestBoss = null;
            var nearestDistance = float.MaxValue;

            foreach (var boss in bossSceneController.bosses) {
                if (boss == null || boss.isDead) {
                    continue;
                }

                var distance = ((Vector2) boss.transform.position - (Vector2) gatePosition).sqrMagnitude;
                if (distance < nearestDistance) {
                    nearestDistance = distance;
                    nearestBoss = boss;
                }
            }

            if (nearestBoss != null) {
                reference = nearestBoss.transform.position;
                return true;
            }
        }

        HealthManager nearestEnemy = null;
        var nearestEnemyDistance = EnemyReferenceRadius * EnemyReferenceRadius;

        foreach (var healthManager in EnemyHealthManager.TrackedHealthManagers) {
            if (healthManager == null || healthManager.isDead || !healthManager.isActiveAndEnabled) {
                continue;
            }

            var distance = ((Vector2) healthManager.transform.position - (Vector2) gatePosition).sqrMagnitude;
            if (distance < nearestEnemyDistance) {
                nearestEnemyDistance = distance;
                nearestEnemy = healthManager;
            }
        }

        if (nearestEnemy != null) {
            reference = nearestEnemy.transform.position;
            return true;
        }

        PlayerCandidate nearestPlayer = null;
        var nearestPlayerDistance = RemotePlayerReferenceRadius * RemotePlayerReferenceRadius;

        foreach (var playerData in _playerData.Values) {
            if (!playerData.IsInLocalScene) {
                continue;
            }

            var playerObject = playerData.PlayerObject;
            if (playerObject == null || !playerObject.activeInHierarchy) {
                continue;
            }

            var distance = ((Vector2) playerObject.transform.position - (Vector2) gatePosition).sqrMagnitude;
            if (distance < nearestPlayerDistance) {
                nearestPlayerDistance = distance;
                nearestPlayer = new PlayerCandidate(playerObject.transform.position);
            }
        }

        if (nearestPlayer != null) {
            reference = nearestPlayer.Position;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get the position of the given gate.
    /// </summary>
    /// <param name="gate">The gate entity.</param>
    /// <returns>The position of the gate.</returns>
    private static Vector3 GetGatePosition(EntityInstance gate) {
        if (gate.Object.Client != null) {
            return gate.Object.Client.transform.position;
        }

        return gate.Object.Host != null ? gate.Object.Host.transform.position : Vector3.zero;
    }

    /// <summary>
    /// Disable the non-trigger colliders of the gate while it is held open.
    /// </summary>
    /// <param name="state">The buffered gate state.</param>
    private static void DisableSolidColliders(GateState state) {
        if (state.Entity == null || state.Entity.Object.Client == null) {
            return;
        }

        var colliders = state.Entity.Object.Client.GetComponentsInChildren<Collider2D>(true);
        foreach (var collider in colliders) {
            if (collider == null || collider.isTrigger || !collider.enabled) {
                continue;
            }

            if (!state.DisabledColliders.Contains(collider)) {
                state.DisabledColliders.Add(collider);
            }

            collider.enabled = false;
        }
    }

    /// <summary>
    /// Restore colliders that were disabled while holding the gate open.
    /// </summary>
    /// <param name="state">The buffered gate state.</param>
    private static void RestoreColliders(GateState state) {
        foreach (var collider in state.DisabledColliders) {
            if (collider != null) {
                collider.enabled = true;
            }
        }

        state.DisabledColliders.Clear();
    }

    /// <summary>
    /// Apply all buffered updates for the given gate.
    /// </summary>
    /// <param name="state">The buffered gate state.</param>
    private void Flush(GateState state) {
        if (state.Entity == null) {
            RemoveState(state);
            return;
        }

        RestoreColliders(state);

        _states.Remove(state.Entity.Id);

        var entity = state.Entity;
        var updates = state.Updates;

        Logger.Debug($"Applying {updates.Count} buffered battle gate updates for entity {entity.Id}");

        foreach (var bufferedUpdate in updates) {
            _entityManager.ApplyReliableEntityUpdate(
                entity,
                bufferedUpdate.Update,
                bufferedUpdate.AlreadyInSceneUpdate
            );
        }
    }

    /// <summary>
    /// Apply all buffered updates for all gates.
    /// </summary>
    private void FlushAll() {
        foreach (var state in new List<GateState>(_states.Values)) {
            Flush(state);
        }
    }

    /// <summary>
    /// Get or create the buffered state for the given gate entity.
    /// </summary>
    /// <param name="entity">The gate entity.</param>
    /// <returns>The buffered gate state.</returns>
    private GateState GetOrCreateState(EntityInstance entity) {
        if (!_states.TryGetValue(entity.Id, out var state)) {
            state = new GateState { Entity = entity };
            _states[entity.Id] = state;
        }

        return state;
    }

    /// <summary>
    /// Remove the given state and restore its colliders.
    /// </summary>
    /// <param name="state">The state to remove.</param>
    private void RemoveState(GateState state) {
        RestoreColliders(state);

        if (state.Entity != null) {
            _states.Remove(state.Entity.Id);
        }
    }

    /// <summary>
    /// Clear all buffered state and restore colliders.
    /// </summary>
    private void Clear() {
        foreach (var state in _states.Values) {
            RestoreColliders(state);
        }

        _states.Clear();
    }

    /// <summary>
    /// Create a deep copy of a reliable entity update so it can be applied later.
    /// </summary>
    /// <param name="update">The update to copy.</param>
    /// <returns>The copied update.</returns>
    private static ReliableEntityUpdate Clone(ReliableEntityUpdate update) {
        var clone = new ReliableEntityUpdate {
            Id = update.Id,
            IsActive = update.IsActive
        };

        foreach (var updateType in update.UpdateTypes) {
            clone.UpdateTypes.Add(updateType);
        }

        foreach (var pair in update.HostFsmData) {
            var data = new EntityHostFsmData();
            data.MergeData(pair.Value);
            clone.HostFsmData[pair.Key] = data;
        }

        foreach (var data in update.GenericData) {
            clone.GenericData.Add(new EntityNetworkData {
                Type = data.Type,
                Packet = new Packet(data.Packet.ToArray())
            });
        }

        return clone;
    }

    /// <summary>
    /// Buffered update for a battle gate.
    /// </summary>
    private class BufferedUpdate {
        /// <summary>
        /// The copied update.
        /// </summary>
        public ReliableEntityUpdate Update { get; set; }

        /// <summary>
        /// Whether the update came from an already in scene packet.
        /// </summary>
        public bool AlreadyInSceneUpdate { get; set; }
    }

    /// <summary>
    /// Buffered state for a single battle gate.
    /// </summary>
    private class GateState {
        /// <summary>
        /// The gate entity.
        /// </summary>
        public EntityInstance Entity { get; set; }

        /// <summary>
        /// The buffered updates, in the order they were received.
        /// </summary>
        public List<BufferedUpdate> Updates { get; } = [];

        /// <summary>
        /// Colliders that were disabled while holding the gate open.
        /// </summary>
        public List<Collider2D> DisabledColliders { get; } = [];

        /// <summary>
        /// Whether the local player was last considered inside the arena.
        /// </summary>
        public bool Inside { get; set; }

        /// <summary>
        /// The time the first update was buffered.
        /// </summary>
        public float FirstBufferedTime { get; set; }

        /// <summary>
        /// The next time the player position is checked.
        /// </summary>
        public float NextCheckTime { get; set; }
    }

    /// <summary>
    /// Minimal player candidate used for the fallback reference position.
    /// </summary>
    private class PlayerCandidate {
        /// <summary>
        /// The position of the player.
        /// </summary>
        public Vector3 Position { get; }

        public PlayerCandidate(Vector3 position) {
            Position = position;
        }
    }
}
