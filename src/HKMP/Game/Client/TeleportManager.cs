using System.Collections.Generic;
using Hkmp.Game.Settings;
using Hkmp.Networking.Client;
using Hkmp.Ui;
using Hkmp.Util;
using UnityEngine;
using Logger = Hkmp.Logging.Logger;

namespace Hkmp.Game.Client;

/// <summary>
/// Manager for teleporting the local player to a teammate in the same scene. The teleport costs a small amount
/// of Geo, or one dream essence when the player does not have enough Geo, and has a cooldown so it cannot be used
/// to skip through a level. Teleporting between scenes is deliberately not supported.
/// </summary>
internal class TeleportManager {
    /// <summary>
    /// Amount of Geo a teleport costs.
    /// </summary>
    private const int GeoCost = 50;

    /// <summary>
    /// Amount of dream essence a teleport costs when the player does not have enough Geo.
    /// </summary>
    private const int EssenceCost = 1;

    /// <summary>
    /// Time in seconds before another teleport can be used.
    /// </summary>
    private const float Cooldown = 20f;

    /// <summary>
    /// The net client instance.
    /// </summary>
    private readonly NetClient _netClient;

    /// <summary>
    /// The mapping of remote player IDs to player data.
    /// </summary>
    private readonly Dictionary<ushort, ClientPlayerData> _playerData;

    /// <summary>
    /// The mod settings of the local client.
    /// </summary>
    private readonly ModSettings _modSettings;

    /// <summary>
    /// Realtime timestamp before which no new teleport can be used.
    /// </summary>
    private float _nextTeleportTime;

    public TeleportManager(
        NetClient netClient,
        Dictionary<ushort, ClientPlayerData> playerData,
        ModSettings modSettings
    ) {
        _netClient = netClient;
        _playerData = playerData;
        _modSettings = modSettings;
    }

    /// <summary>
    /// Initialize the teleport manager by subscribing to the update event.
    /// </summary>
    public void Initialize() {
        if (MonoBehaviourUtil.Instance != null) {
            MonoBehaviourUtil.Instance.OnUpdateEvent += OnUpdate;
        }
    }

    /// <summary>
    /// Check whether the teleport keybind was pressed and try to teleport to a teammate.
    /// </summary>
    private void OnUpdate() {
        if (!_netClient.IsConnected || _modSettings == null || !_modSettings.TeleportToTeammate) {
            return;
        }

        var keybind = _modSettings.Keybinds != null ? _modSettings.Keybinds.TeleportToTeammate : null;
        if (keybind == null || !keybind.WasPressed) {
            return;
        }

        TryTeleportToClosestTeammate();
    }

    /// <summary>
    /// Try to teleport to the closest teammate in the current scene.
    /// </summary>
    private void TryTeleportToClosestTeammate() {
        var now = Time.realtimeSinceStartup;
        if (now < _nextTeleportTime) {
            UiManager.InternalChatBox.AddMessage(
                Localization.Format(
                    "You can teleport again in {0} s",
                    Mathf.CeilToInt(_nextTeleportTime - now)
                )
            );
            return;
        }

        if (DownedPlayerRegistry.IsLocalDowned) {
            return;
        }

        var hero = HeroController.instance;
        var playerData = PlayerData.instance;
        if (hero == null || playerData == null) {
            return;
        }

        if (SceneUtil.IsNonGameplayScene(SceneUtil.GetCurrentSceneName())) {
            return;
        }

        var target = FindClosestTeammate(hero.transform.position);
        if (target == null) {
            UiManager.InternalChatBox.AddMessage(Localization.Get("No teammate in this location"));
            _nextTeleportTime = now + 0.5f;
            return;
        }

        var payWithGeo = playerData.geo >= GeoCost;
        if (!payWithGeo && playerData.dreamOrbs < EssenceCost) {
            UiManager.InternalChatBox.AddMessage(
                Localization.Get("Teleporting costs 50 geo or 1 dream essence")
            );
            _nextTeleportTime = now + 0.5f;
            return;
        }

        if (payWithGeo) {
            hero.TakeGeo(GeoCost);
        } else {
            playerData.dreamOrbs -= EssenceCost;
        }

        var destination = FindSafePosition(target.PlayerContainer.transform.position);
        MoveHero(hero, destination);

        _nextTeleportTime = now + Cooldown;

        UiManager.InternalChatBox.AddMessage(
            payWithGeo
                ? Localization.Format("Teleported to {0} for 50 geo", target.Username)
                : Localization.Format("Teleported to {0} for 1 dream essence", target.Username)
        );

        Logger.Info($"Teleported to player ({target.Id}, {target.Username})");
    }

    /// <summary>
    /// Find the closest teammate that is in the current scene and can be teleported to.
    /// </summary>
    /// <param name="localPosition">The position of the local player.</param>
    /// <returns>The player data of the closest teammate, or null if there is none.</returns>
    private ClientPlayerData FindClosestTeammate(Vector3 localPosition) {
        ClientPlayerData closest = null;
        var bestDistance = float.MaxValue;

        foreach (var idPlayerDataPair in _playerData) {
            var candidate = idPlayerDataPair.Value;
            if (!candidate.IsInLocalScene || candidate.PlayerContainer == null) {
                continue;
            }

            if (DownedPlayerRegistry.IsDowned(idPlayerDataPair.Key)) {
                continue;
            }

            var distance = Vector3.Distance(localPosition, candidate.PlayerContainer.transform.position);
            if (distance >= bestDistance) {
                continue;
            }

            bestDistance = distance;
            closest = candidate;
        }

        return closest;
    }

    /// <summary>
    /// Find a safe position near the given target position by looking for the ground below it. If no ground is
    /// found, the target position itself is used.
    /// </summary>
    /// <param name="target">The position of the teammate.</param>
    /// <returns>The position the local player should be placed at.</returns>
    private static Vector3 FindSafePosition(Vector3 target) {
        var origin = new Vector2(target.x, target.y + 2f);
        var hits = Physics2D.RaycastAll(origin, Vector2.down, 8f);

        foreach (var hit in hits) {
            var collider = hit.collider;
            if (collider == null || collider.isTrigger) {
                continue;
            }

            // Do not land on top of the teammate or on an enemy
            if (collider.GetComponentInParent<HeroController>() != null
                || collider.GetComponentInParent<HealthManager>() != null) {
                continue;
            }

            return new Vector3(target.x, hit.point.y + 0.6f, target.z);
        }

        return target;
    }

    /// <summary>
    /// Move the local player to the given position and make sure the camera follows immediately.
    /// </summary>
    /// <param name="hero">The hero controller of the local player.</param>
    /// <param name="position">The position to move to.</param>
    private static void MoveHero(HeroController hero, Vector3 position) {
        hero.transform.position = position;

        var rigidbody = hero.GetComponent<Rigidbody2D>();
        if (rigidbody != null) {
            rigidbody.position = new Vector2(position.x, position.y);
            rigidbody.velocity = Vector2.zero;
        }

        var gameCameras = GameCameras.instance;
        if (gameCameras != null && gameCameras.cameraController != null) {
            gameCameras.cameraController.SnapTo(position.x, position.y);
        }
    }
}
