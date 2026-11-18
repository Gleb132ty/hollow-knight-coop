using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GlobalEnums;
using Hkmp.Animation;
using Hkmp.Game.Settings;
using Hkmp.Networking.Client;
using Hkmp.Util;
using HutongGames.PlayMaker;
using UnityEngine;
using Logger = Hkmp.Logging.Logger;

namespace Hkmp.Game.Client;

/// <summary>
/// Manager for the revive feature. Suppresses the vanilla death of the local player when another player in the
/// same scene can revive them by defeating the soul that rises from their body, and restores players when they
/// are revived.
/// </summary>
internal class ReviveManager {
    /// <summary>
    /// Time in seconds after which a downed player automatically gives up and respawns at the last bench.
    /// </summary>
    private const float AutoGiveUpDelay = 45f;

    /// <summary>
    /// Distance in world units between the visual centers of two players sitting on the same bench. Players are
    /// spread symmetrically around the bench in username order, so nobody touches or overlaps anyone else.
    /// </summary>
    private const float BenchSeatSpacing = 1.6f;

    /// <summary>
    /// Maximum horizontal distance in world units between the local player and a bench for the seat offset to
    /// be applied.
    /// </summary>
    private const float BenchSeatMaxDistance = 3f;

    /// <summary>
    /// Interval in seconds between searches for the bench closest to the local player.
    /// </summary>
    private const float BenchSearchInterval = 0.5f;

    /// <summary>
    /// Interval in seconds between searches for the teammate the spectator camera should follow.
    /// </summary>
    private const float SpectatorSearchInterval = 0.5f;

    /// <summary>
    /// Bench FSM states during which the local player is sitting on the bench and the seat offset is enforced.
    /// </summary>
    private static readonly HashSet<string> BenchRestingStates = new HashSet<string> {
        "Resting",
        "Fall Asleep",
        "Startle",
        "Update Map Silently",
        "Inv Map Open",
        "Map Idle",
        "Close Map",
        "Close Anim"
    };

    /// <summary>
    /// The private vanilla method that starts the real death routine, including geo loss, the shade and the
    /// respawn at the last bench.
    /// </summary>
    private static readonly MethodInfo DieMethod = typeof(HeroController).GetMethod(
        "Die",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    /// <summary>
    /// The field of the camera target that holds the transform the camera follows.
    /// </summary>
    private static readonly FieldInfo CameraTargetHeroField = typeof(CameraTarget).GetField(
        "heroTransform",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
    );

    /// <summary>
    /// Sentinel ID used by the server to signal the receiving client that it was revived.
    /// </summary>
    public const ushort SelfReviveId = ushort.MaxValue;

    /// <summary>
    /// The net client instance.
    /// </summary>
    private readonly NetClient _netClient;

    /// <summary>
    /// The player manager instance used to obtain remote player objects.
    /// </summary>
    private readonly PlayerManager _playerManager;

    /// <summary>
    /// The animation manager instance.
    /// </summary>
    private readonly AnimationManager _animationManager;

    /// <summary>
    /// Mapping of remote player IDs to player data.
    /// </summary>
    private readonly Dictionary<ushort, ClientPlayerData> _playerData;

    /// <summary>
    /// The mod settings instance.
    /// </summary>
    private readonly ModSettings _modSettings;

    /// <summary>
    /// The current server settings, used to check whether PvP is enabled.
    /// </summary>
    private readonly ServerSettings _serverSettings;

    /// <summary>
    /// Callback that returns the username of the local player, used to give every player a stable bench seat.
    /// </summary>
    private readonly Func<string> _localUsernameProvider;

    /// <summary>
    /// Whether the local player is currently downed.
    /// </summary>
    private bool _localDowned;

    /// <summary>
    /// Remaining time until the local player is automatically revived.
    /// </summary>
    private float _autoReviveTimer;

    /// <summary>
    /// Whether an automatic give up request has already been performed.
    /// </summary>
    private bool _giveUpRequested;

    /// <summary>
    /// Whether the next invocation of the vanilla death routine should not be suppressed, because the local
    /// player gave up while downed.
    /// </summary>
    private bool _forceRealDeath;

    /// <summary>
    /// Realtime timestamp at which the real death was forced, used to reset the flag if the vanilla death routine
    /// never runs.
    /// </summary>
    private float _forceRealDeathSince;

    /// <summary>
    /// Whether the local player is currently respawning at a bench and should be seated on it.
    /// </summary>
    private bool _waitingForBenchSeat;

    /// <summary>
    /// The bench the local player is currently sitting on, or null when they are not sitting.
    /// </summary>
    private RestBench _seatingBench;

    /// <summary>
    /// Horizontal distance between the local player's transform and the center of their sprite while sitting.
    /// Used so that players are spread by their visible sprites rather than by their transform origins, which
    /// shift depending on the direction the knight sits in.
    /// </summary>
    private float _seatingSpriteOffset;

    /// <summary>
    /// Whether the sprite offset for the current bench has been measured. The sprite renderer is disabled during
    /// the respawn fade, so the measurement is retried until it succeeds.
    /// </summary>
    private bool _seatingSpriteOffsetMeasured;

    /// <summary>
    /// Bench closest to the local player, cached to avoid searching the scene every frame.
    /// </summary>
    private RestBench _nearestBench;

    /// <summary>
    /// Realtime timestamp at which the closest bench is searched for again.
    /// </summary>
    private float _nextBenchSearchTime;

    /// <summary>
    /// The camera target whose hero transform was replaced while spectating, or null when not spectating.
    /// </summary>
    private CameraTarget _spectatorCameraTarget;

    /// <summary>
    /// The hero transform that was set on the camera target before spectating started.
    /// </summary>
    private Transform _spectatorOriginalHero;

    /// <summary>
    /// The remote player currently followed by the spectator camera, or null when nobody is followed.
    /// </summary>
    private ClientPlayerData _spectatorPlayer;

    /// <summary>
    /// Realtime timestamp at which the spectator camera looks for another teammate to follow.
    /// </summary>
    private float _nextSpectatorSearchTime;

    /// <summary>
    /// Texture used to draw the overlay.
    /// </summary>
    private Texture2D _whiteTexture;

    /// <summary>
    /// Cached label style used by the overlay.
    /// </summary>
    private GUIStyle _labelStyle;

    /// <summary>
    /// Constructs the revive manager.
    /// </summary>
    /// <param name="netClient">The net client instance.</param>
    /// <param name="playerManager">The player manager instance.</param>
    /// <param name="animationManager">The animation manager instance.</param>
    /// <param name="playerData">Mapping of remote player IDs to player data.</param>
    /// <param name="modSettings">The mod settings instance.</param>
    /// <param name="serverSettings">The current server settings, used to check whether PvP is enabled.</param>
    /// <param name="localUsernameProvider">Callback that returns the username of the local player.</param>
    public ReviveManager(
        NetClient netClient,
        PlayerManager playerManager,
        AnimationManager animationManager,
        Dictionary<ushort, ClientPlayerData> playerData,
        ModSettings modSettings,
        ServerSettings serverSettings,
        Func<string> localUsernameProvider
    ) {
        _netClient = netClient;
        _playerManager = playerManager;
        _animationManager = animationManager;
        _playerData = playerData;
        _modSettings = modSettings;
        _serverSettings = serverSettings;
        _localUsernameProvider = localUsernameProvider;
    }

    /// <summary>
    /// Initialize the revive manager by registering game hooks.
    /// </summary>
    public void Initialize() {
        On.HeroController.Die += OnHeroControllerDie;
        On.HeroController.DieFromHazard += OnHeroControllerDieFromHazard;
        On.HeroController.Respawn += OnHeroControllerRespawn;
        On.HeroController.TakeDamage += OnHeroControllerTakeDamage;

        if (MonoBehaviourUtil.Instance != null) {
            MonoBehaviourUtil.Instance.OnUpdateEvent += OnUpdate;
            MonoBehaviourUtil.Instance.OnGUIEvent += OnGui;
        }
    }

    /// <summary>
    /// Callback method for when a remote player has been downed.
    /// </summary>
    /// <param name="id">The ID of the downed player.</param>
    public void OnRemotePlayerDowned(ushort id) {
        DownedPlayerRegistry.Add(id);

        SessionStats.RegisterTeamDown();

        _animationManager.OnPlayerDowned(id);

        Logger.Info($"Player {id} is downed");
    }

    /// <summary>
    /// Callback method for when a remote player has been revived.
    /// </summary>
    /// <param name="id">The ID of the revived player.</param>
    public void OnRemotePlayerRevived(ushort id) {
        DownedPlayerRegistry.Remove(id);

        SessionStats.RegisterTeamRevive();

        _animationManager.OnPlayerRevived(id);

        Logger.Info($"Player {id} was revived");
    }

    /// <summary>
    /// Callback method for when the server signals that the local player has been revived.
    /// </summary>
    public void OnSelfRevive() {
        if (!_localDowned) {
            return;
        }

        SessionStats.RegisterLocalRevive();

        RestoreLocalPlayer();
    }

    /// <summary>
    /// Callback method for when a remote player leaves our scene or disconnects.
    /// </summary>
    /// <param name="id">The ID of the player.</param>
    public void OnPlayerLeft(ushort id) {
        DownedPlayerRegistry.Remove(id);
    }

    /// <summary>
    /// Callback method for when the local player changes scenes. Remote avatars are recycled, so all remote
    /// downed state is discarded. The server will resend the state for the new scene.
    /// </summary>
    public void OnLocalSceneChange() {
        DownedPlayerRegistry.ClearRemote();

        StopSpectatorCamera();

        _nearestBench = null;
        _nextBenchSearchTime = 0f;
        ResetBenchSeat();
    }

    /// <summary>
    /// Callback method for when the local client disconnects. Restores the local player if they were downed and
    /// clears all state.
    /// </summary>
    public void OnDisconnected() {
        if (_localDowned) {
            RestoreLocalPlayer();
        }

        DownedPlayerRegistry.Clear();

        StopSpectatorCamera();

        _nearestBench = null;
        _nextBenchSearchTime = 0f;
        ResetBenchSeat();
    }

    /// <summary>
    /// Callback method for when the local player dies. Suppresses the vanilla death when a revive is possible.
    /// </summary>
    /// <param name="orig">The original method.</param>
    /// <param name="self">The hero controller instance.</param>
    /// <returns>An empty enumerator when the death was suppressed, otherwise the original enumerator.</returns>
    private IEnumerator OnHeroControllerDie(On.HeroController.orig_Die orig, HeroController self) {
        if (_forceRealDeath) {
            // The local player gave up while downed, so the vanilla death has to run for real
            _forceRealDeath = false;
            return orig(self);
        }

        if (TryBecomeDowned(self, false)) {
            return EmptyRoutine();
        }

        return orig(self);
    }

    /// <summary>
    /// Callback method for when the local player dies from a hazard. Suppresses the vanilla death when a revive
    /// is possible.
    /// </summary>
    /// <param name="orig">The original method.</param>
    /// <param name="self">The hero controller instance.</param>
    /// <param name="hazardType">The type of hazard that killed the player.</param>
    /// <param name="angle">The angle of the hazard.</param>
    /// <returns>An empty enumerator when the death was suppressed, otherwise the original enumerator.</returns>
    private IEnumerator OnHeroControllerDieFromHazard(
        On.HeroController.orig_DieFromHazard orig,
        HeroController self,
        HazardType hazardType,
        float angle
    ) {
        if (TryBecomeDowned(self, true)) {
            return EmptyRoutine();
        }

        return orig(self, hazardType, angle);
    }

    /// <summary>
    /// Callback method for when the local player respawns after a real death. The bench seat itself is applied
    /// by <see cref="UpdateBenchSeat"/> while the player is sitting.
    /// </summary>
    /// <param name="orig">The original method.</param>
    /// <param name="self">The hero controller instance.</param>
    /// <returns>An enumerator that wraps the vanilla respawn routine.</returns>
    private IEnumerator OnHeroControllerRespawn(On.HeroController.orig_Respawn orig, HeroController self) {
        yield return orig(self);

        if (!_waitingForBenchSeat) {
            yield break;
        }

        _waitingForBenchSeat = false;
        _forceRealDeath = false;

        Logger.Info("Respawned after giving up, the bench seat will be applied while sitting");
    }

    /// <summary>
    /// Callback method for when the local player takes damage. Damage is ignored while the player is downed.
    /// </summary>
    /// <param name="orig">The original method.</param>
    /// <param name="self">The hero controller instance.</param>
    /// <param name="go">The game object that dealt the damage.</param>
    /// <param name="damageSide">The side the damage came from.</param>
    /// <param name="damageAmount">The amount of damage.</param>
    /// <param name="hazardType">The type of hazard.</param>
    private void OnHeroControllerTakeDamage(
        On.HeroController.orig_TakeDamage orig,
        HeroController self,
        GameObject go,
        CollisionSide damageSide,
        int damageAmount,
        int hazardType
    ) {
        if (DownedPlayerRegistry.IsLocalDowned) {
            // Downed players are invulnerable until they are revived. Skipping the original method also prevents
            // the death coroutines from firing again, which would otherwise lead to the vanilla respawn.
            return;
        }

        if (!_serverSettings.IsPvpEnabled && IsRemotePlayerSource(go)) {
            // Friendly fire is disabled, so effects of other players (slashes, spells, auras) do not hurt us
            return;
        }

        orig(self, go, damageSide, damageAmount, hazardType);
    }

    /// <summary>
    /// Attempt to turn the local player into the downed state instead of letting them die.
    /// </summary>
    /// <param name="hero">The hero controller instance.</param>
    /// <param name="fromHazard">Whether the player died from a hazard.</param>
    /// <returns>Whether the death was suppressed and the player became downed.</returns>
    private bool TryBecomeDowned(HeroController hero, bool fromHazard) {
        if (!_netClient.IsConnected || !_modSettings.ReviveEnabled) {
            return false;
        }

        // If we are already downed, make sure the vanilla death does not run either
        if (_localDowned || DownedPlayerRegistry.IsLocalDowned) {
            return true;
        }

        // A revive is only possible when at least one other player in the scene is not downed
        if (!HasLivingTeammateInScene()) {
            return false;
        }

        Logger.Info("Local player died, entering downed state instead of dying");

        _localDowned = true;
        DownedPlayerRegistry.SetLocalDowned(true);

        SessionStats.RegisterLocalDown();

        _autoReviveTimer = AutoGiveUpDelay;
        _giveUpRequested = false;

        hero.RelinquishControl();
        hero.StopAnimationControl();
        hero.AffectedByGravity(false);

        hero.cState.dead = false;
        hero.cState.hazardDeath = false;
        hero.cState.invulnerable = true;

        var rigidbody = hero.GetComponent<Rigidbody2D>();
        if (rigidbody != null) {
            rigidbody.velocity = Vector2.zero;
        }

        if (fromHazard) {
            RepositionAfterHazard(hero);
        }

        var animator = hero.GetComponent<tk2dSpriteAnimator>();
        if (animator != null && animator.GetClipByName("Death") != null) {
            animator.Stop();
            animator.Play("Death");
        }

        if (_netClient.IsConnected) {
            _netClient.UpdateManager.SetPlayerDowned();
        }

        StartSpectatorCamera();

        return true;
    }

    /// <summary>
    /// Check whether there is at least one remote player in the local scene that is not downed.
    /// </summary>
    /// <returns>Whether a living teammate is present.</returns>
    private bool HasLivingTeammateInScene() {
        foreach (var idPlayerDataPair in _playerData) {
            if (!idPlayerDataPair.Value.IsInLocalScene) {
                continue;
            }

            if (DownedPlayerRegistry.IsDowned(idPlayerDataPair.Key)) {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Move the local player to the last hazard respawn location.
    /// </summary>
    /// <param name="hero">The hero controller instance.</param>
    private static void RepositionAfterHazard(HeroController hero) {
        var playerData = hero.playerData;
        if (playerData == null) {
            return;
        }

        var respawnPosition = playerData.GetVector3("hazardRespawnLocation");
        if (respawnPosition == Vector3.zero) {
            return;
        }

        hero.transform.position = new Vector3(
            respawnPosition.x,
            respawnPosition.y,
            hero.transform.position.z
        );

        var rigidbody = hero.GetComponent<Rigidbody2D>();
        if (rigidbody != null) {
            rigidbody.velocity = Vector2.zero;
        }
    }

    /// <summary>
    /// Start the spectator camera, which makes the game camera follow a living teammate while the local player
    /// is downed.
    /// </summary>
    private void StartSpectatorCamera() {
        if (!_modSettings.SpectatorCamera) {
            return;
        }

        var gameCameras = GameCameras.instance;
        if (gameCameras == null || gameCameras.cameraTarget == null) {
            return;
        }

        _spectatorCameraTarget = gameCameras.cameraTarget;
        _spectatorOriginalHero = GetCameraHero(_spectatorCameraTarget);
        _nextSpectatorSearchTime = 0f;

        UpdateSpectatorCamera();
    }

    /// <summary>
    /// Stop the spectator camera and restore the camera to follow the local player.
    /// </summary>
    private void StopSpectatorCamera() {
        if (_spectatorCameraTarget != null && _spectatorOriginalHero != null) {
            SetCameraHero(_spectatorCameraTarget, _spectatorOriginalHero);
        }

        _spectatorCameraTarget = null;
        _spectatorOriginalHero = null;
        _spectatorPlayer = null;
        _nextSpectatorSearchTime = 0f;
    }

    /// <summary>
    /// Update the teammate the spectator camera follows. The closest living teammate is chosen and kept, so that
    /// the camera does not jump between players every frame.
    /// </summary>
    private void UpdateSpectatorCamera() {
        if (_spectatorCameraTarget == null) {
            return;
        }

        if (!_localDowned || !_modSettings.SpectatorCamera) {
            StopSpectatorCamera();
            return;
        }

        var now = Time.realtimeSinceStartup;
        if (now >= _nextSpectatorSearchTime) {
            _nextSpectatorSearchTime = now + SpectatorSearchInterval;
            _spectatorPlayer = FindSpectatorTarget();
        }

        var target = _spectatorPlayer?.PlayerContainer;
        SetCameraHero(
            _spectatorCameraTarget,
            target != null ? target.transform : _spectatorOriginalHero
        );
    }

    /// <summary>
    /// Read the transform the camera currently follows.
    /// </summary>
    /// <param name="cameraTarget">The camera target instance.</param>
    /// <returns>The transform the camera follows, or null when it cannot be read.</returns>
    private static Transform GetCameraHero(CameraTarget cameraTarget) {
        if (cameraTarget == null || CameraTargetHeroField == null) {
            return null;
        }

        return CameraTargetHeroField.GetValue(cameraTarget) as Transform;
    }

    /// <summary>
    /// Set the transform the camera should follow.
    /// </summary>
    /// <param name="cameraTarget">The camera target instance.</param>
    /// <param name="hero">The transform to follow.</param>
    private static void SetCameraHero(CameraTarget cameraTarget, Transform hero) {
        if (cameraTarget == null || hero == null || CameraTargetHeroField == null) {
            return;
        }

        CameraTargetHeroField.SetValue(cameraTarget, hero);
    }

    /// <summary>
    /// Find the living teammate in the local scene that is closest to the downed player.
    /// </summary>
    /// <returns>The player data of the teammate, or null when nobody can be followed.</returns>
    private ClientPlayerData FindSpectatorTarget() {
        var hero = HeroController.instance;
        var heroPosition = hero != null ? hero.transform.position : Vector3.zero;

        ClientPlayerData closest = null;
        var closestDistance = float.MaxValue;

        foreach (var idPlayerDataPair in _playerData) {
            var playerData = idPlayerDataPair.Value;
            if (!playerData.IsInLocalScene) {
                continue;
            }

            if (DownedPlayerRegistry.IsDowned(idPlayerDataPair.Key)) {
                continue;
            }

            var container = playerData.PlayerContainer;
            if (container == null) {
                continue;
            }

            var distance = (container.transform.position - heroPosition).sqrMagnitude;
            if (distance < closestDistance) {
                closestDistance = distance;
                closest = playerData;
            }
        }

        return closest;
    }

    /// <summary>
    /// Check whether the given damage source belongs to a remote player (their avatar or one of their effects).
    /// </summary>
    /// <param name="source">The game object that dealt the damage.</param>
    /// <returns>Whether the source belongs to a remote player.</returns>
    private bool IsRemotePlayerSource(GameObject source) {
        if (source == null) {
            return false;
        }

        var sourceTransform = source.transform;

        foreach (var playerData in _playerData.Values) {
            var container = playerData.PlayerContainer;
            if (container == null) {
                continue;
            }

            var containerTransform = container.transform;
            if (sourceTransform == containerTransform || sourceTransform.IsChildOf(containerTransform)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Restore the local player from the downed state.
    /// </summary>
    private void RestoreLocalPlayer() {
        _localDowned = false;
        DownedPlayerRegistry.SetLocalDowned(false);

        _autoReviveTimer = 0f;
        _giveUpRequested = false;

        StopSpectatorCamera();

        var hero = HeroController.instance;
        if (hero == null) {
            return;
        }

        Logger.Info("Restoring local player after revive");

        hero.cState.dead = false;
        hero.cState.hazardDeath = false;
        hero.cState.invulnerable = false;

        hero.MaxHealth();
        hero.AffectedByGravity(true);
        hero.RegainControl();
        hero.StartAnimationControl();

        // A revived player comes back with a limited amount of masks, but with a full soul bar
        var playerData = hero.playerData;
        if (playerData != null) {
            var maxHealth = Mathf.Max(1, playerData.CurrentMaxHealth);
            var reviveHealth = Mathf.Clamp(_modSettings.ReviveHealth, 1, maxHealth);

            playerData.health = reviveHealth;
            hero.proxyFSM.SendEvent("HeroCtrl-Healed");

            hero.SetMPCharge(playerData.maxMP);
        }

        var rigidbody = hero.GetComponent<Rigidbody2D>();
        if (rigidbody != null) {
            rigidbody.velocity = Vector2.zero;
        }

        var animator = hero.GetComponent<tk2dSpriteAnimator>();
        if (animator != null && animator.GetClipByName("Idle") != null) {
            animator.Stop();
            animator.Play("Idle");
        }

        hero.cState.invulnerable = true;
        MonoBehaviourUtil.Instance.StartCoroutine(ClearInvulnerability(hero, 1.5f));
    }

    /// <summary>
    /// Clear the invulnerability of the hero after the given delay. The vanilla <c>Invulnerable</c> method is
    /// private, so we reproduce its relevant effect here.
    /// </summary>
    /// <param name="hero">The hero controller instance.</param>
    /// <param name="delay">The delay in seconds.</param>
    /// <returns>An enumerator for the coroutine.</returns>
    private static IEnumerator ClearInvulnerability(HeroController hero, float delay) {
        yield return new WaitForSeconds(delay);

        if (hero != null) {
            hero.cState.invulnerable = false;
        }
    }

    /// <summary>
    /// Give up while downed: notify the server, clear the downed state and perform a real death so the vanilla
    /// penalties (geo loss and shade) apply and the player respawns at their last bench.
    /// </summary>
    private void GiveUp() {
        if (!_localDowned) {
            return;
        }

        Logger.Info("Local player gave up while downed, performing a real death");

        SessionStats.RegisterLocalGiveUp();

        if (_netClient.IsConnected) {
            _netClient.UpdateManager.SetPlayerGiveUp();
        }

        _localDowned = false;
        DownedPlayerRegistry.SetLocalDowned(false);

        _autoReviveTimer = 0f;
        _giveUpRequested = false;

        StopSpectatorCamera();

        var hero = HeroController.instance;
        if (hero == null) {
            return;
        }

        // Undo the changes that were made when the player became downed, otherwise the vanilla death routine
        // would early return or continue in a broken state
        hero.cState.dead = false;
        hero.cState.hazardDeath = false;
        hero.cState.invulnerable = false;

        hero.AffectedByGravity(true);
        hero.RegainControl();
        hero.StartAnimationControl();

        var animator = hero.GetComponent<tk2dSpriteAnimator>();
        if (animator != null && animator.GetClipByName("Idle") != null) {
            animator.Stop();
            animator.Play("Idle");
        }

        _waitingForBenchSeat = true;

        // Run the vanilla death routine. Since HeroController.Die is private, invoke it via reflection; the
        // detour installed by our hook still runs, so the flag above allows the real death through exactly once.
        _forceRealDeath = true;
        _forceRealDeathSince = Time.realtimeSinceStartup;

        if (MonoBehaviourUtil.Instance != null && DieMethod != null) {
            var deathRoutine = DieMethod.Invoke(hero, null) as IEnumerator;
            if (deathRoutine != null) {
                MonoBehaviourUtil.Instance.StartCoroutine(deathRoutine);
            }
        }
    }

    /// <summary>
    /// Keep the local player on their own seat while they are sitting on a bench. Every player gets a stable
    /// seat based on their username, so players sitting on the same bench are spread out instead of touching.
    /// </summary>
    private void UpdateBenchSeat() {
        var hero = HeroController.instance;
        if (hero == null) {
            ResetBenchSeat();
            return;
        }

        if (_nearestBench == null || Time.realtimeSinceStartup >= _nextBenchSearchTime) {
            _nearestBench = FindClosestBench(hero);
            _nextBenchSearchTime = Time.realtimeSinceStartup + BenchSearchInterval;
        }

        var bench = _nearestBench;
        if (bench == null || !bench.gameObject.activeInHierarchy) {
            ResetBenchSeat();
            return;
        }

        var heroPosition = hero.transform.position;
        if (Mathf.Abs(heroPosition.x - bench.transform.position.x) > BenchSeatMaxDistance) {
            ResetBenchSeat();
            return;
        }

        var benchFsm = LocateBenchFsm(bench);
        var state = benchFsm?.Fsm?.ActiveStateName;
        if (state == null || !BenchRestingStates.Contains(state)) {
            ResetBenchSeat();
            return;
        }

        if (_seatingBench != bench) {
            _seatingBench = bench;
            _seatingSpriteOffsetMeasured = TryGetSpriteCenterOffset(hero, out _seatingSpriteOffset);

            Logger.Info(
                $"Player is sitting on bench {bench.gameObject.name} at x={bench.transform.position.x:F2}; " +
                $"seat offset={GetBenchSeatOffset():F2}"
            );
        } else if (!_seatingSpriteOffsetMeasured) {
            _seatingSpriteOffsetMeasured = TryGetSpriteCenterOffset(hero, out _seatingSpriteOffset);
        }

        // Shift the player so that the center of their sprite, not their transform origin, lands on the seat.
        // The vanilla sitting position shifts the sprite depending on the direction the knight faces, which
        // otherwise makes two players sit almost on top of each other.
        var targetX = bench.transform.position.x + GetBenchSeatOffset() - _seatingSpriteOffset;
        if (Mathf.Abs(heroPosition.x - targetX) > 0.001f) {
            hero.transform.position = new Vector3(targetX, heroPosition.y, heroPosition.z);
        }
    }

    /// <summary>
    /// Stop applying the bench seat offset, for example when the player gets up or leaves the scene.
    /// </summary>
    private void ResetBenchSeat() {
        _seatingBench = null;
        _seatingSpriteOffset = 0f;
        _seatingSpriteOffsetMeasured = false;
    }

    /// <summary>
    /// Horizontal offset of the local player's seat relative to the center of the bench. Players are ordered by
    /// their username, which every client sees identically, so each player gets the same stable seat.
    /// </summary>
    /// <returns>The horizontal seat offset in world units.</returns>
    private float GetBenchSeatOffset() {
        var localUsername = _localUsernameProvider?.Invoke();
        if (string.IsNullOrEmpty(localUsername)) {
            return 0f;
        }

        var usernames = new List<string>();
        foreach (var playerData in _playerData.Values) {
            if (playerData != null && !string.IsNullOrEmpty(playerData.Username)) {
                usernames.Add(playerData.Username);
            }
        }

        usernames.Add(localUsername);
        usernames.Sort(StringComparer.OrdinalIgnoreCase);

        var index = usernames.FindIndex(username =>
            string.Equals(username, localUsername, StringComparison.OrdinalIgnoreCase)
        );
        if (index < 0) {
            return 0f;
        }

        return ((usernames.Count - 1) / 2f - index) * BenchSeatSpacing;
    }

    /// <summary>
    /// Horizontal distance between the center of the local player's sprite and their transform origin.
    /// </summary>
    /// <param name="hero">The hero controller instance.</param>
    /// <param name="offset">The measured offset, or zero when it could not be measured.</param>
    /// <returns>Whether the offset could be measured.</returns>
    private static bool TryGetSpriteCenterOffset(HeroController hero, out float offset) {
        offset = 0f;

        var renderer = hero.GetComponent<MeshRenderer>();
        if (renderer == null || !renderer.enabled) {
            return false;
        }

        var bounds = renderer.bounds;
        if (bounds.size.x <= 0.001f) {
            return false;
        }

        offset = bounds.center.x - hero.transform.position.x;
        return true;
    }

    /// <summary>
    /// Locate the bench control FSM on the given bench.
    /// </summary>
    /// <param name="bench">The bench instance.</param>
    /// <returns>The FSM, or null if the bench has no control FSM.</returns>
    private static PlayMakerFSM LocateBenchFsm(RestBench bench) {
        var benchFsm = FSMUtility.LocateFSM(bench.gameObject, "Bench Control");
        if (benchFsm == null) {
            benchFsm = bench.GetComponent<PlayMakerFSM>();
        }

        return benchFsm;
    }

    /// <summary>
    /// Find the bench in the current scene that is closest to the given hero.
    /// </summary>
    /// <param name="hero">The hero controller instance.</param>
    /// <returns>The closest bench, or null if there is no bench in the scene.</returns>
    private static RestBench FindClosestBench(HeroController hero) {
        RestBench closest = null;
        var closestDistance = float.MaxValue;

        foreach (var bench in UnityEngine.Object.FindObjectsOfType<RestBench>()) {
            if (bench == null || !bench.gameObject.activeInHierarchy) {
                continue;
            }

            var distance = (bench.transform.position - hero.transform.position).sqrMagnitude;
            if (distance < closestDistance) {
                closestDistance = distance;
                closest = bench;
            }
        }

        return closest;
    }

    /// <summary>
    /// Callback method that runs each update tick.
    /// </summary>
    private void OnUpdate() {
        if (_forceRealDeath && Time.realtimeSinceStartup - _forceRealDeathSince > 5f) {
            // The vanilla death routine did not consume the flag, reset it so future deaths are suppressed
            // normally again
            _forceRealDeath = false;
        }

        UpdateBenchSeat();

        if (_localDowned && !_netClient.IsConnected) {
            // The connection was lost while downed, so restore the player locally to prevent a soft lock
            RestoreLocalPlayer();
            return;
        }

        if (_localDowned) {
            UpdateSpectatorCamera();

            _autoReviveTimer -= Time.deltaTime;

            var giveUpPressed = _modSettings.Keybinds != null
                                && _modSettings.Keybinds.RespawnAtBench != null
                                && _modSettings.Keybinds.RespawnAtBench.WasPressed;

            if (giveUpPressed) {
                Logger.Info("Local player pressed the give up key while downed");
                GiveUp();
                return;
            }

            if (_autoReviveTimer <= 0f && !_giveUpRequested) {
                _giveUpRequested = true;

                Logger.Info("Auto revive timer expired, giving up and respawning at the last bench");

                GiveUp();
            }
        }
    }

    /// <summary>
    /// Callback method for drawing the revive overlay.
    /// </summary>
    private void OnGui() {
        if (!_modSettings.ReviveEnabled) {
            return;
        }

        if (!_localDowned) {
            return;
        }

        EnsureTexture();
        EnsureLabelStyle();

        var scale = Screen.height / 1080f;

        var width = 480f * scale;
        var height = 46f * scale;
        var rect = new Rect(
            (Screen.width - width) / 2f,
            Screen.height * 0.14f,
            width,
            height
        );

        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(rect, _whiteTexture);

        GUI.color = Color.white;

        var seconds = Mathf.Max(0, Mathf.CeilToInt(_autoReviveTimer));
        GUI.Label(rect, Localization.Format("DOWNED - your soul must be defeated ({0}s)", seconds), _labelStyle);

        var hintRect = new Rect(
            (Screen.width - width) / 2f,
            Screen.height * 0.14f + height + 4f * scale,
            width,
            height
        );

        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(hintRect, _whiteTexture);

        GUI.color = Color.white;
        GUI.Label(
            hintRect,
            Localization.Format(
                "Press {0} to give up and respawn at the last bench",
                GetGiveUpKeyName()
            ),
            _labelStyle
        );

        GUI.color = Color.white;
    }

    /// <summary>
    /// Ensure that the texture used for drawing the overlay exists.
    /// </summary>
    private void EnsureTexture() {
        if (_whiteTexture != null) {
            return;
        }

        _whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false) {
            hideFlags = HideFlags.HideAndDontSave
        };
        _whiteTexture.SetPixel(0, 0, Color.white);
        _whiteTexture.Apply();
    }

    /// <summary>
    /// Ensure that the label style used by the overlay exists and has the correct font size.
    /// </summary>
    private void EnsureLabelStyle() {
        var fontSize = Mathf.Max(10, Mathf.RoundToInt(18f * Screen.height / 1080f));

        if (_labelStyle == null) {
            _labelStyle = new GUIStyle(GUI.skin.label) {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
        }

        if (_labelStyle.fontSize != fontSize) {
            _labelStyle.fontSize = fontSize;
        }
    }

    /// <summary>
    /// Get the name of the key that is currently bound to giving up while downed.
    /// </summary>
    /// <returns>The name of the key shown in the overlay.</returns>
    private string GetGiveUpKeyName() {
        try {
            var action = _modSettings.Keybinds?.RespawnAtBench;
            if (action != null && action.Bindings.Count > 0) {
                var name = action.Bindings[0].Name;
                if (!string.IsNullOrEmpty(name)) {
                    return name;
                }
            }
        } catch {
            // Fall back to the default key below
        }

        return "R";
    }

    /// <summary>
    /// Empty coroutine used to replace the vanilla death coroutine.
    /// </summary>
    /// <returns>An enumerator that immediately stops.</returns>
    private static IEnumerator EmptyRoutine() {
        yield break;
    }
}
