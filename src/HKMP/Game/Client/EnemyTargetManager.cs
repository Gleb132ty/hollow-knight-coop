using System.Collections.Generic;
using Hkmp.Game.Client.Entity;
using Hkmp.Game.Settings;
using Modding;
using UnityEngine;
using Logger = Hkmp.Logging.Logger;

namespace Hkmp.Game.Client;

/// <summary>
/// Manager that makes enemies and bosses on the scene host target the closest living player instead of
/// always targeting the local player. Bosses additionally switch their target periodically and enemies
/// briefly focus the player that hit them.
/// </summary>
internal class EnemyTargetManager {
    /// <summary>
    /// How often the target for an enemy is re-evaluated, in seconds.
    /// </summary>
    private const float EvaluationInterval = 0.2f;

    /// <summary>
    /// How long an enemy focuses the player that hit it, in seconds.
    /// </summary>
    private const float AggroDuration = 3.5f;

    /// <summary>
    /// How often a boss switches to another player, in seconds.
    /// </summary>
    private const float BossRetargetInterval = 8f;

    /// <summary>
    /// How much closer another player has to be before a non-boss enemy switches target.
    /// </summary>
    private const float SwitchDistanceRatio = 0.8f;

    /// <summary>
    /// The layer mask used for line of sight ray casts (terrain).
    /// </summary>
    private const int LineOfSightMask = 256;

    /// <summary>
    /// The singleton instance of the enemy target manager.
    /// </summary>
    public static EnemyTargetManager Instance { get; private set; }

    /// <summary>
    /// The entity manager, used to determine whether the local client is the scene host.
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
    /// Cached target state per enemy root.
    /// </summary>
    private readonly Dictionary<GameObject, EnemyTargetState> _targetStates = [];

    /// <summary>
    /// Cache mapping a game object that owns an FSM action to the enemy root it belongs to.
    /// </summary>
    private readonly Dictionary<GameObject, GameObject> _rootCache = [];

    /// <summary>
    /// Whether the hooks for this manager are currently registered.
    /// </summary>
    private bool _hooksRegistered;

    public EnemyTargetManager(
        EntityManager entityManager,
        Dictionary<ushort, ClientPlayerData> playerData,
        ModSettings modSettings
    ) {
        _entityManager = entityManager;
        _playerData = playerData;
        _modSettings = modSettings;

        Instance = this;
    }

    /// <summary>
    /// Register the hooks that override FSM target acquisition.
    /// </summary>
    public void RegisterHooks() {
        if (_hooksRegistered) {
            return;
        }

        _hooksRegistered = true;

        On.GetHero.OnEnter += OnGetHeroEnter;

        On.HutongGames.PlayMaker.Actions.ChaseObject.OnEnter += OnChaseObjectEnter;
        On.HutongGames.PlayMaker.Actions.ChaseObject.OnFixedUpdate += OnChaseObjectFixedUpdate;
        On.HutongGames.PlayMaker.Actions.ChaseObjectV2.OnEnter += OnChaseObjectV2Enter;
        On.HutongGames.PlayMaker.Actions.ChaseObjectV2.OnFixedUpdate += OnChaseObjectV2FixedUpdate;
        On.HutongGames.PlayMaker.Actions.ChaseObjectGround.OnEnter += OnChaseObjectGroundEnter;
        On.HutongGames.PlayMaker.Actions.ChaseObjectGround.OnFixedUpdate += OnChaseObjectGroundFixedUpdate;
        On.HutongGames.PlayMaker.Actions.ChaseObjectVertical.OnEnter += OnChaseObjectVerticalEnter;
        On.HutongGames.PlayMaker.Actions.ChaseObjectVertical.OnFixedUpdate += OnChaseObjectVerticalFixedUpdate;

        On.HutongGames.PlayMaker.Actions.FaceObject.OnEnter += OnFaceObjectEnter;
        On.HutongGames.PlayMaker.Actions.FaceObject.OnUpdate += OnFaceObjectUpdate;

        On.LineOfSightDetector.Update += OnLineOfSightUpdate;
    }

    /// <summary>
    /// Deregister the hooks of this manager.
    /// </summary>
    public void DeregisterHooks() {
        if (!_hooksRegistered) {
            return;
        }

        _hooksRegistered = false;

        On.GetHero.OnEnter -= OnGetHeroEnter;

        On.HutongGames.PlayMaker.Actions.ChaseObject.OnEnter -= OnChaseObjectEnter;
        On.HutongGames.PlayMaker.Actions.ChaseObject.OnFixedUpdate -= OnChaseObjectFixedUpdate;
        On.HutongGames.PlayMaker.Actions.ChaseObjectV2.OnEnter -= OnChaseObjectV2Enter;
        On.HutongGames.PlayMaker.Actions.ChaseObjectV2.OnFixedUpdate -= OnChaseObjectV2FixedUpdate;
        On.HutongGames.PlayMaker.Actions.ChaseObjectGround.OnEnter -= OnChaseObjectGroundEnter;
        On.HutongGames.PlayMaker.Actions.ChaseObjectGround.OnFixedUpdate -= OnChaseObjectGroundFixedUpdate;
        On.HutongGames.PlayMaker.Actions.ChaseObjectVertical.OnEnter -= OnChaseObjectVerticalEnter;
        On.HutongGames.PlayMaker.Actions.ChaseObjectVertical.OnFixedUpdate -= OnChaseObjectVerticalFixedUpdate;

        On.HutongGames.PlayMaker.Actions.FaceObject.OnEnter -= OnFaceObjectEnter;
        On.HutongGames.PlayMaker.Actions.FaceObject.OnUpdate -= OnFaceObjectUpdate;

        On.LineOfSightDetector.Update -= OnLineOfSightUpdate;

        Clear();
    }

    /// <summary>
    /// Clear cached target state. Called when the scene changes or the client disconnects.
    /// </summary>
    public void OnSceneChanged() {
        Clear();
    }

    /// <summary>
    /// Clear cached target state on disconnect.
    /// </summary>
    public void OnDisconnected() {
        Clear();
    }

    /// <summary>
    /// Record that an enemy was hit by a local player, so it focuses that player for a short while.
    /// </summary>
    /// <param name="healthManager">The health manager of the enemy that was hit.</param>
    /// <param name="source">The game object that caused the hit.</param>
    public void NotifyLocalDamage(HealthManager healthManager, GameObject source) {
        if (!IsActive() || healthManager == null) {
            return;
        }

        var root = ResolveEnemyRoot(healthManager.gameObject);
        if (root == null) {
            root = healthManager.gameObject;
        }

        var candidates = GatherCandidates(root.transform.position);
        if (candidates.Count == 0) {
            return;
        }

        PlayerCandidate chosen = null;
        if (source != null) {
            foreach (var candidate in candidates) {
                if (IsOwnedBy(source, candidate)) {
                    chosen = candidate;
                    break;
                }
            }

            if (chosen == null) {
                var sourcePosition = source.transform.position;
                chosen = FindClosest(candidates, sourcePosition);
            }
        }

        chosen ??= candidates[0];

        SetAggro(root, chosen.Target);
    }

    /// <summary>
    /// Record that an enemy was damaged by a remote client. Since the forwarded damage does not contain the
    /// attacker, the closest remote player to the enemy is used as the aggro target.
    /// </summary>
    /// <param name="healthManager">The health manager of the enemy that was damaged.</param>
    public void NotifyForwardedDamage(HealthManager healthManager) {
        if (!IsActive() || healthManager == null) {
            return;
        }

        var root = ResolveEnemyRoot(healthManager.gameObject);
        if (root == null) {
            root = healthManager.gameObject;
        }

        var candidates = GatherCandidates(root.transform.position);
        if (candidates.Count == 0) {
            return;
        }

        PlayerCandidate chosen = null;
        foreach (var candidate in candidates) {
            if (!candidate.IsLocal) {
                chosen = candidate;
                break;
            }
        }

        chosen ??= candidates[0];

        SetAggro(root, chosen.Target);
    }

    /// <summary>
    /// Check whether the target override is currently active.
    /// </summary>
    /// <returns>Whether enemies should be retargeted.</returns>
    private bool IsActive() {
        return _modSettings != null
               && _modSettings.EnemyTargeting
               && _entityManager != null
               && _entityManager.IsSceneHostDetermined
               && _entityManager.IsSceneHost
               && HeroController.instance != null;
    }

    /// <summary>
    /// Set the aggro target for the given enemy root.
    /// </summary>
    /// <param name="root">The enemy root.</param>
    /// <param name="target">The player object to focus.</param>
    private void SetAggro(GameObject root, GameObject target) {
        if (root == null || target == null) {
            return;
        }

        var state = GetOrCreateState(root);
        state.Target = target;
        state.AggroTarget = target;
        state.AggroUntil = Time.time + AggroDuration;
        state.NextEvaluation = 0f;
    }

    /// <summary>
    /// Get the target that the given enemy should use.
    /// </summary>
    /// <param name="root">The root game object of the enemy.</param>
    /// <returns>The player object to target, or null if none is available.</returns>
    private GameObject GetTarget(GameObject root) {
        if (root == null) {
            return null;
        }

        var now = Time.time;
        var state = GetOrCreateState(root);

        if (now < state.NextEvaluation) {
            return state.Target;
        }

        state.NextEvaluation = now + EvaluationInterval;

        var candidates = GatherCandidates(root.transform.position);
        if (candidates.Count == 0) {
            state.Target = null;
            state.AggroTarget = null;
            return null;
        }

        // Aggro from a hit takes priority for a short while
        if (state.AggroTarget != null && now < state.AggroUntil) {
            if (candidates.Exists(candidate => candidate.Target == state.AggroTarget)) {
                state.Target = state.AggroTarget;
                return state.Target;
            }
        }

        // Drop a target that left the scene, died, or is downed
        if (state.Target != null && !candidates.Exists(candidate => candidate.Target == state.Target)) {
            state.Target = null;
        }

        var nearest = candidates[0];
        var isBoss = IsBoss(root.GetComponent<HealthManager>());

        if (isBoss) {
            if (state.Target == null || now >= state.NextBossRetarget) {
                state.NextBossRetarget = now + BossRetargetInterval;
                state.Target = ChooseBossTarget(candidates, state.Target);
            }

            return state.Target;
        }

        if (state.Target == null) {
            state.Target = nearest.Target;
            return state.Target;
        }

        var current = candidates.Find(candidate => candidate.Target == state.Target);
        if (current != null
            && nearest.DistanceSquaredTo(root.transform.position) < current.DistanceSquaredTo(root.transform.position) * SwitchDistanceRatio * SwitchDistanceRatio) {
            state.Target = nearest.Target;
        }

        return state.Target;
    }

    /// <summary>
    /// Choose the target for a boss. Bosses periodically switch to another player so they do not focus a
    /// single player for the whole fight.
    /// </summary>
    /// <param name="candidates">The sorted list of player candidates.</param>
    /// <param name="currentTarget">The current target, if any.</param>
    /// <returns>The newly chosen target.</returns>
    private static GameObject ChooseBossTarget(List<PlayerCandidate> candidates, GameObject currentTarget) {
        if (candidates.Count == 0) {
            return null;
        }

        if (currentTarget != null && candidates.Count > 1) {
            foreach (var candidate in candidates) {
                if (candidate.Target != currentTarget) {
                    return candidate.Target;
                }
            }
        }

        return candidates[0].Target;
    }

    /// <summary>
    /// Gather all living players that are present in the local scene, sorted by distance to the given
    /// position.
    /// </summary>
    /// <param name="position">The position to sort by.</param>
    /// <returns>The sorted list of player candidates.</returns>
    private List<PlayerCandidate> GatherCandidates(Vector3 position) {
        var candidates = new List<PlayerCandidate>();

        var hero = HeroController.instance;
        if (hero != null && !DownedPlayerRegistry.IsLocalDowned) {
            var heroObject = hero.gameObject;
            if (heroObject != null && heroObject.activeInHierarchy) {
                candidates.Add(new PlayerCandidate(
                    heroObject,
                    heroObject,
                    hero.transform.position,
                    true
                ));
            }
        }

        foreach (var playerData in _playerData.Values) {
            if (!playerData.IsInLocalScene || DownedPlayerRegistry.IsDowned(playerData.Id)) {
                continue;
            }

            var playerObject = playerData.PlayerObject;
            if (playerObject == null || !playerObject.activeInHierarchy) {
                continue;
            }

            var container = playerData.PlayerContainer;
            candidates.Add(new PlayerCandidate(
                playerObject,
                container != null ? container : playerObject,
                playerObject.transform.position,
                false
            ));
        }

        candidates.Sort((a, b) =>
            a.DistanceSquaredTo(position).CompareTo(b.DistanceSquaredTo(position)));

        return candidates;
    }

    /// <summary>
    /// Find the candidate closest to the given position.
    /// </summary>
    /// <param name="candidates">The list of candidates.</param>
    /// <param name="position">The position to compare with.</param>
    /// <returns>The closest candidate.</returns>
    private static PlayerCandidate FindClosest(List<PlayerCandidate> candidates, Vector3 position) {
        PlayerCandidate closest = null;
        var closestDistance = float.MaxValue;

        foreach (var candidate in candidates) {
            var distance = candidate.DistanceSquaredTo(position);
            if (distance < closestDistance) {
                closestDistance = distance;
                closest = candidate;
            }
        }

        return closest;
    }

    /// <summary>
    /// Check whether the given game object belongs to the given player.
    /// </summary>
    /// <param name="gameObject">The game object to check.</param>
    /// <param name="candidate">The player candidate.</param>
    /// <returns>Whether the game object belongs to the player.</returns>
    private static bool IsOwnedBy(GameObject gameObject, PlayerCandidate candidate) {
        if (gameObject == null) {
            return false;
        }

        if (gameObject == candidate.Target || gameObject == candidate.Container) {
            return true;
        }

        var transform = gameObject.transform;
        return transform.IsChildOf(candidate.Target.transform)
               || transform.IsChildOf(candidate.Container.transform);
    }

    /// <summary>
    /// Check whether the given game object is a player object or a child of one.
    /// </summary>
    /// <param name="gameObject">The game object to check.</param>
    /// <returns>Whether the object belongs to a player.</returns>
    private bool IsPlayerObject(GameObject gameObject) {
        if (gameObject == null) {
            return false;
        }

        var hero = HeroController.instance;
        if (hero != null) {
            var heroObject = hero.gameObject;
            if (gameObject == heroObject || gameObject.transform.IsChildOf(heroObject.transform)) {
                return true;
            }
        }

        foreach (var playerData in _playerData.Values) {
            var container = playerData.PlayerContainer;
            if (container == null) {
                continue;
            }

            if (gameObject == container || gameObject.transform.IsChildOf(container.transform)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Check whether the given health manager belongs to a boss.
    /// </summary>
    /// <param name="healthManager">The health manager.</param>
    /// <returns>Whether the health manager is a boss.</returns>
    private static bool IsBoss(HealthManager healthManager) {
        if (healthManager == null) {
            return false;
        }

        var bossSceneController = BossSceneController.Instance;
        if (bossSceneController == null || bossSceneController.bosses == null) {
            return false;
        }

        foreach (var boss in bossSceneController.bosses) {
            if (boss == healthManager) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolve the enemy root that the given game object belongs to.
    /// </summary>
    /// <param name="owner">The game object owning an FSM action.</param>
    /// <returns>The enemy root, or null if the object does not belong to an enemy.</returns>
    private GameObject ResolveEnemyRoot(GameObject owner) {
        if (owner == null) {
            return null;
        }

        if (_rootCache.TryGetValue(owner, out var cached)) {
            return cached;
        }

        var healthManager = owner.GetComponent<HealthManager>();
        if (healthManager == null) {
            healthManager = owner.GetComponentInParent<HealthManager>();
        }

        if (healthManager == null) {
            healthManager = owner.GetComponentInChildren<HealthManager>();
        }

        var root = healthManager != null ? healthManager.gameObject : null;
        _rootCache[owner] = root;

        return root;
    }

    /// <summary>
    /// Try to get the target for the enemy that owns the given FSM action.
    /// </summary>
    /// <param name="owner">The game object owning the FSM action.</param>
    /// <param name="target">The target to use, if any.</param>
    /// <returns>Whether a target was found.</returns>
    private bool TryGetTargetForAction(GameObject owner, out GameObject target) {
        target = null;

        if (!IsActive()) {
            return false;
        }

        var root = ResolveEnemyRoot(owner);
        if (root == null) {
            return false;
        }

        target = GetTarget(root);

        return target != null;
    }

    /// <summary>
    /// Get or create the target state for the given enemy root.
    /// </summary>
    /// <param name="root">The enemy root.</param>
    /// <returns>The target state.</returns>
    private EnemyTargetState GetOrCreateState(GameObject root) {
        if (!_targetStates.TryGetValue(root, out var state)) {
            state = new EnemyTargetState();
            _targetStates[root] = state;
        }

        return state;
    }

    /// <summary>
    /// Clear all cached target state.
    /// </summary>
    private void Clear() {
        _targetStates.Clear();
        _rootCache.Clear();
    }

    #region Hooks

    private void OnGetHeroEnter(On.GetHero.orig_OnEnter orig, GetHero self) {
        orig(self);

        if (TryGetTargetForAction(self.Owner, out var target)) {
            self.storeResult.Value = target;
        }
    }

    private void OnChaseObjectEnter(
        On.HutongGames.PlayMaker.Actions.ChaseObject.orig_OnEnter orig,
        HutongGames.PlayMaker.Actions.ChaseObject self
    ) {
        OverrideChaseTarget(self.Owner, self.target);
        orig(self);
    }

    private void OnChaseObjectFixedUpdate(
        On.HutongGames.PlayMaker.Actions.ChaseObject.orig_OnFixedUpdate orig,
        HutongGames.PlayMaker.Actions.ChaseObject self
    ) {
        OverrideChaseTarget(self.Owner, self.target);
        orig(self);
    }

    private void OnChaseObjectV2Enter(
        On.HutongGames.PlayMaker.Actions.ChaseObjectV2.orig_OnEnter orig,
        HutongGames.PlayMaker.Actions.ChaseObjectV2 self
    ) {
        OverrideChaseTarget(self.Owner, self.target);
        orig(self);
    }

    private void OnChaseObjectV2FixedUpdate(
        On.HutongGames.PlayMaker.Actions.ChaseObjectV2.orig_OnFixedUpdate orig,
        HutongGames.PlayMaker.Actions.ChaseObjectV2 self
    ) {
        OverrideChaseTarget(self.Owner, self.target);
        orig(self);
    }

    private void OnChaseObjectGroundEnter(
        On.HutongGames.PlayMaker.Actions.ChaseObjectGround.orig_OnEnter orig,
        HutongGames.PlayMaker.Actions.ChaseObjectGround self
    ) {
        OverrideChaseTarget(self.Owner, self.target);
        orig(self);
    }

    private void OnChaseObjectGroundFixedUpdate(
        On.HutongGames.PlayMaker.Actions.ChaseObjectGround.orig_OnFixedUpdate orig,
        HutongGames.PlayMaker.Actions.ChaseObjectGround self
    ) {
        OverrideChaseTarget(self.Owner, self.target);
        orig(self);
    }

    private void OnChaseObjectVerticalEnter(
        On.HutongGames.PlayMaker.Actions.ChaseObjectVertical.orig_OnEnter orig,
        HutongGames.PlayMaker.Actions.ChaseObjectVertical self
    ) {
        OverrideChaseTarget(self.Owner, self.target);
        orig(self);
    }

    private void OnChaseObjectVerticalFixedUpdate(
        On.HutongGames.PlayMaker.Actions.ChaseObjectVertical.orig_OnFixedUpdate orig,
        HutongGames.PlayMaker.Actions.ChaseObjectVertical self
    ) {
        OverrideChaseTarget(self.Owner, self.target);
        orig(self);
    }

    private void OnFaceObjectEnter(
        On.HutongGames.PlayMaker.Actions.FaceObject.orig_OnEnter orig,
        HutongGames.PlayMaker.Actions.FaceObject self
    ) {
        OverrideFaceTarget(self.Owner, self.objectB);
        orig(self);
    }

    private void OnFaceObjectUpdate(
        On.HutongGames.PlayMaker.Actions.FaceObject.orig_OnUpdate orig,
        HutongGames.PlayMaker.Actions.FaceObject self
    ) {
        OverrideFaceTarget(self.Owner, self.objectB);
        orig(self);
    }

    private void OnLineOfSightUpdate(On.LineOfSightDetector.orig_Update orig, LineOfSightDetector self) {
        if (!TryGetTargetForAction(self.gameObject, out var target)) {
            orig(self);
            return;
        }

        var hero = HeroController.instance;
        if (hero != null && target == hero.gameObject) {
            orig(self);
            return;
        }

        UpdateLineOfSight(self, target);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Override the target of a chase action if it currently chases a player.
    /// </summary>
    /// <param name="owner">The owner of the action.</param>
    /// <param name="targetVariable">The target variable of the action.</param>
    private void OverrideChaseTarget(
        GameObject owner,
        HutongGames.PlayMaker.FsmGameObject targetVariable
    ) {
        if (targetVariable == null || !IsPlayerObject(targetVariable.Value)) {
            return;
        }

        if (TryGetTargetForAction(owner, out var target)) {
            targetVariable.Value = target;
        }
    }

    /// <summary>
    /// Override the target of a face action if it currently faces a player.
    /// </summary>
    /// <param name="owner">The owner of the action.</param>
    /// <param name="targetVariable">The target variable of the action.</param>
    private void OverrideFaceTarget(
        GameObject owner,
        HutongGames.PlayMaker.FsmGameObject targetVariable
    ) {
        if (targetVariable == null || targetVariable.IsNone || !IsPlayerObject(targetVariable.Value)) {
            return;
        }

        if (TryGetTargetForAction(owner, out var target)) {
            targetVariable.Value = target;
        }
    }

    /// <summary>
    /// Re-implementation of the vanilla line of sight update that uses the chosen player instead of the
    /// local hero.
    /// </summary>
    /// <param name="detector">The line of sight detector.</param>
    /// <param name="target">The player to check line of sight for.</param>
    private static void UpdateLineOfSight(LineOfSightDetector detector, GameObject target) {
        var alertRanges = ReflectionHelper.GetField<LineOfSightDetector, AlertRange[]>(detector, "alertRanges");

        if (alertRanges != null && alertRanges.Length > 0) {
            var heroInRange = false;
            foreach (var alertRange in alertRanges) {
                if (alertRange != null && alertRange.IsHeroInRange) {
                    heroInRange = true;
                    break;
                }
            }

            if (!heroInRange) {
                ReflectionHelper.SetField(detector, "canSeeHero", false);
                return;
            }
        }

        if (target == null) {
            ReflectionHelper.SetField(detector, "canSeeHero", false);
            return;
        }

        var detectorPosition = (Vector2) detector.transform.position;
        var targetPosition = (Vector2) target.transform.position;
        var difference = targetPosition - detectorPosition;

        var hit = Physics2D.Raycast(
            detectorPosition,
            difference.normalized,
            difference.magnitude,
            LineOfSightMask
        );

        ReflectionHelper.SetField(detector, "canSeeHero", hit.collider == null);
    }

    #endregion

    /// <summary>
    /// Cached targeting state for an enemy.
    /// </summary>
    private class EnemyTargetState {
        /// <summary>
        /// The current chosen target.
        /// </summary>
        public GameObject Target { get; set; }

        /// <summary>
        /// The player that hit the enemy most recently.
        /// </summary>
        public GameObject AggroTarget { get; set; }

        /// <summary>
        /// The time until which the aggro target is used.
        /// </summary>
        public float AggroUntil { get; set; }

        /// <summary>
        /// The next time the target is re-evaluated.
        /// </summary>
        public float NextEvaluation { get; set; }

        /// <summary>
        /// The next time a boss switches its target.
        /// </summary>
        public float NextBossRetarget { get; set; }
    }

    /// <summary>
    /// A player that enemies can target.
    /// </summary>
    private class PlayerCandidate {
        /// <summary>
        /// The game object that FSM actions should target.
        /// </summary>
        public GameObject Target { get; }

        /// <summary>
        /// The root container of the player, used to check ownership of hit sources.
        /// </summary>
        public GameObject Container { get; }

        /// <summary>
        /// The position of the player.
        /// </summary>
        private Vector3 Position { get; }

        /// <summary>
        /// Whether this is the local player.
        /// </summary>
        public bool IsLocal { get; }

        public PlayerCandidate(GameObject target, GameObject container, Vector3 position, bool isLocal) {
            Target = target;
            Container = container;
            Position = position;
            IsLocal = isLocal;
        }

        /// <summary>
        /// Squared distance from this player to the given position.
        /// </summary>
        /// <param name="position">The position to compare with.</param>
        /// <returns>The squared distance.</returns>
        public float DistanceSquaredTo(Vector3 position) {
            var difference = Position - position;
            return difference.sqrMagnitude;
        }
    }
}
