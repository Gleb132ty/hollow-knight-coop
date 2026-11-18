using System.Collections.Generic;
using Hkmp.Game.Settings;
using Hkmp.Networking.Client;
using Hkmp.Ui;
using Hkmp.Util;
using UnityEngine;
using Logger = Hkmp.Logging.Logger;

namespace Hkmp.Game.Client;

/// <summary>
/// Manager for healing a nearby teammate. The local player spends soul to heal one mask of the closest
/// injured teammate in the same scene. The request is sent to the server, which relays it to the teammate so
/// that the health is applied on the client that owns the character.
/// </summary>
internal class HealManager {
    /// <summary>
    /// Amount of soul that healing a teammate costs.
    /// </summary>
    private const int SoulCost = 33;

    /// <summary>
    /// Maximum distance in world units between the local player and the teammate to heal.
    /// </summary>
    private const float HealRange = 9f;

    /// <summary>
    /// Time in seconds before another heal can be requested.
    /// </summary>
    private const float Cooldown = 1.5f;

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
    /// Realtime timestamp before which no new heal can be requested.
    /// </summary>
    private float _nextHealTime;

    public HealManager(
        NetClient netClient,
        Dictionary<ushort, ClientPlayerData> playerData,
        ModSettings modSettings
    ) {
        _netClient = netClient;
        _playerData = playerData;
        _modSettings = modSettings;
    }

    /// <summary>
    /// Initialize the heal manager by subscribing to the update event.
    /// </summary>
    public void Initialize() {
        if (MonoBehaviourUtil.Instance != null) {
            MonoBehaviourUtil.Instance.OnUpdateEvent += OnUpdate;
        }
    }

    /// <summary>
    /// Check whether the heal keybind was pressed and try to heal a teammate.
    /// </summary>
    private void OnUpdate() {
        if (!_netClient.IsConnected || _modSettings == null || !_modSettings.HealTeammate) {
            return;
        }

        var keybind = _modSettings.Keybinds != null ? _modSettings.Keybinds.HealTeammate : null;
        if (keybind == null || !keybind.WasPressed) {
            return;
        }

        TryHealNearestTeammate();
    }

    /// <summary>
    /// Try to heal the closest injured teammate in the current scene.
    /// </summary>
    private void TryHealNearestTeammate() {
        var now = Time.realtimeSinceStartup;
        if (now < _nextHealTime) {
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

        var localPosition = hero.transform.position;
        ClientPlayerData target = null;
        var bestDistance = HealRange;

        foreach (var idPlayerDataPair in _playerData) {
            var remoteData = idPlayerDataPair.Value;
            if (!remoteData.IsInLocalScene || remoteData.PlayerContainer == null) {
                continue;
            }

            if (DownedPlayerRegistry.IsDowned(idPlayerDataPair.Key)) {
                continue;
            }

            if (!remoteData.HealthKnown || remoteData.Health >= remoteData.MaxHealth) {
                continue;
            }

            var distance = Vector3.Distance(localPosition, remoteData.PlayerContainer.transform.position);
            if (distance > bestDistance) {
                continue;
            }

            bestDistance = distance;
            target = remoteData;
        }

        if (target == null) {
            UiManager.InternalChatBox.AddMessage(Localization.Get("No injured teammate nearby"));
            _nextHealTime = now + 0.5f;
            return;
        }

        if (playerData.MPCharge < SoulCost) {
            UiManager.InternalChatBox.AddMessage(Localization.Get("Not enough soul to heal a teammate"));
            _nextHealTime = now + 0.5f;
            return;
        }

        hero.TakeMP(SoulCost);

        _netClient.UpdateManager.SetPlayerHealRequest(target.Id);
        _nextHealTime = now + Cooldown;

        Logger.Info($"Requested heal for player ({target.Id}, {target.Username})");
    }

    /// <summary>
    /// Called when the server tells the local client that a teammate healed them.
    /// </summary>
    /// <param name="healerId">The ID of the teammate that healed the local player.</param>
    public void OnPlayerHealed(ushort healerId) {
        var hero = HeroController.instance;
        if (hero == null) {
            return;
        }

        hero.AddHealth(1);

        var healerName = _playerData.TryGetValue(healerId, out var healerData)
            ? healerData.Username
            : healerId.ToString();

        UiManager.InternalChatBox.AddMessage(Localization.Format("{0} healed you", healerName));
        Logger.Info($"Player ({healerId}, {healerName}) healed the local player");
    }
}
