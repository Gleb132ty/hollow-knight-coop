using System.Collections.Generic;
using Hkmp.Game.Settings;
using Hkmp.Networking.Client;
using Hkmp.Networking.Packet.Data;
using Hkmp.Ui;
using Hkmp.Util;
using UnityEngine;
using Logger = Hkmp.Logging.Logger;

namespace Hkmp.Game.Client;

/// <summary>
/// Manager for sending Geo and dream essence to a teammate. The local player pays the resources up front, the
/// server validates the request and relays it to the teammate, and a rejected request is refunded.
/// </summary>
internal class TransferManager {
    /// <summary>
    /// Maximum amount of Geo that can be sent in a single transfer.
    /// </summary>
    private const int MaxGeoPerTransfer = 100000;

    /// <summary>
    /// Maximum amount of dream essence that can be sent in a single transfer.
    /// </summary>
    private const int MaxEssencePerTransfer = 9999;

    /// <summary>
    /// Time in seconds before another transfer can be started.
    /// </summary>
    private const float Cooldown = 1f;

    /// <summary>
    /// The net client instance.
    /// </summary>
    private readonly NetClient _netClient;

    /// <summary>
    /// The mapping of remote player IDs to player data.
    /// </summary>
    private readonly Dictionary<ushort, ClientPlayerData> _playerData;

    /// <summary>
    /// Realtime timestamp before which no new transfer can be started.
    /// </summary>
    private float _nextTransferTime;

    /// <summary>
    /// The transfer that is waiting for the result from the server, if any.
    /// </summary>
    private PendingTransfer _pendingTransfer;

    public TransferManager(NetClient netClient, Dictionary<ushort, ClientPlayerData> playerData) {
        _netClient = netClient;
        _playerData = playerData;
    }

    /// <summary>
    /// Try to send the given amount of Geo or dream essence to a teammate.
    /// </summary>
    /// <param name="transferType">The kind of resource to send.</param>
    /// <param name="amount">The amount of resources to send.</param>
    /// <param name="targetName">The name of the teammate to send to, or null/empty for the closest one.</param>
    public void TryTransfer(PlayerTransferType transferType, int amount, string targetName) {
        if (!_netClient.IsConnected) {
            UiManager.InternalChatBox.AddMessage(Localization.Get("You are not connected to a server"));
            return;
        }

        var maxAmount = transferType == PlayerTransferType.Geo ? MaxGeoPerTransfer : MaxEssencePerTransfer;
        if (amount <= 0 || amount > maxAmount) {
            UiManager.InternalChatBox.AddMessage(
                Localization.Format("Amount must be between 1 and {0}", maxAmount)
            );
            return;
        }

        var now = Time.realtimeSinceStartup;
        if (now < _nextTransferTime) {
            return;
        }

        if (DownedPlayerRegistry.IsLocalDowned) {
            UiManager.InternalChatBox.AddMessage(Localization.Get("You cannot send resources while downed"));
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

        var target = FindTarget(targetName);
        if (target == null) {
            UiManager.InternalChatBox.AddMessage(
                string.IsNullOrEmpty(targetName)
                    ? Localization.Get("No teammate nearby")
                    : Localization.Format("Teammate {0} is not in this location", targetName)
            );
            _nextTransferTime = now + 0.5f;
            return;
        }

        var isGeo = transferType == PlayerTransferType.Geo;
        var balance = isGeo ? playerData.geo : playerData.dreamOrbs;
        if (balance < amount) {
            UiManager.InternalChatBox.AddMessage(
                isGeo
                    ? Localization.Get("Not enough geo")
                    : Localization.Get("Not enough dream essence")
            );
            _nextTransferTime = now + 0.5f;
            return;
        }

        // Pay up front, the server result decides whether the resources are refunded
        if (isGeo) {
            hero.TakeGeo(amount);
        } else {
            playerData.dreamOrbs -= amount;
        }

        _pendingTransfer = new PendingTransfer(transferType, amount);
        _nextTransferTime = now + Cooldown;

        _netClient.UpdateManager.SetPlayerGiveRequest(target.Id, transferType, amount);

        UiManager.InternalChatBox.AddMessage(
            isGeo
                ? Localization.Format("Sending {0} geo to {1}", amount, target.Username)
                : Localization.Format("Sending {0} dream essence to {1}", amount, target.Username)
        );

        Logger.Info($"Requested transfer of {amount} {transferType} to ({target.Id}, {target.Username})");
    }

    /// <summary>
    /// Called when the server reports whether a transfer request was accepted.
    /// </summary>
    /// <param name="result">The result of the transfer request.</param>
    public void OnTransferResult(PlayerTransferResult result) {
        if (_pendingTransfer == null
            || _pendingTransfer.TransferType != result.TransferType
            || _pendingTransfer.Amount != result.Amount) {
            return;
        }

        _pendingTransfer = null;

        var hero = HeroController.instance;
        var playerData = PlayerData.instance;

        if (result.Success) {
            UiManager.InternalChatBox.AddMessage(
                result.TransferType == PlayerTransferType.Geo
                    ? Localization.Format("{0} geo sent", result.Amount)
                    : Localization.Format("{0} dream essence sent", result.Amount)
            );
            return;
        }

        // The server rejected the request, so give the resources back
        if (result.TransferType == PlayerTransferType.Geo) {
            if (hero != null) {
                hero.AddGeo(result.Amount);
            } else if (playerData != null) {
                playerData.geo += result.Amount;
            }
        } else if (playerData != null) {
            playerData.dreamOrbs += result.Amount;
        }

        UiManager.InternalChatBox.AddMessage(Localization.Get("The transfer was rejected, resources returned"));
    }

    /// <summary>
    /// Called when a teammate sent the local player Geo or dream essence.
    /// </summary>
    /// <param name="transfer">The transfer that was received.</param>
    public void OnTransferReceived(PlayerTransfer transfer) {
        if (transfer.Amount <= 0) {
            return;
        }

        var hero = HeroController.instance;
        var playerData = PlayerData.instance;

        if (transfer.TransferType == PlayerTransferType.Geo) {
            if (hero != null) {
                hero.AddGeo(transfer.Amount);
            } else if (playerData != null) {
                playerData.geo += transfer.Amount;
            }
        } else if (playerData != null) {
            playerData.dreamOrbs += transfer.Amount;
        }

        var senderName = _playerData.TryGetValue(transfer.Id, out var senderData)
            ? senderData.Username
            : transfer.Id.ToString();

        UiManager.InternalChatBox.AddMessage(
            transfer.TransferType == PlayerTransferType.Geo
                ? Localization.Format("{0} sent you {1} geo", senderName, transfer.Amount)
                : Localization.Format("{0} sent you {1} dream essence", senderName, transfer.Amount)
        );

        Logger.Info(
            $"Received {transfer.Amount} {transfer.TransferType} from ({transfer.Id}, {senderName})"
        );
    }

    /// <summary>
    /// Find the teammate to send resources to: either the teammate with the given name, or the closest teammate
    /// in the current scene.
    /// </summary>
    /// <param name="targetName">The name of the teammate, or null/empty for the closest one.</param>
    /// <returns>The player data of the target, or null if no suitable teammate was found.</returns>
    private ClientPlayerData FindTarget(string targetName) {
        if (!string.IsNullOrEmpty(targetName)) {
            ClientPlayerData namedTarget = null;
            foreach (var idPlayerDataPair in _playerData) {
                var candidate = idPlayerDataPair.Value;
                if (!candidate.IsInLocalScene || DownedPlayerRegistry.IsDowned(idPlayerDataPair.Key)) {
                    continue;
                }

                if (string.Equals(candidate.Username, targetName, System.StringComparison.OrdinalIgnoreCase)) {
                    return candidate;
                }

                if (namedTarget == null
                    && candidate.Username != null
                    && candidate.Username.StartsWith(targetName, System.StringComparison.OrdinalIgnoreCase)) {
                    namedTarget = candidate;
                }
            }

            return namedTarget;
        }

        var hero = HeroController.instance;
        if (hero == null) {
            return null;
        }

        var localPosition = hero.transform.position;
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
    /// Data class for a transfer that is waiting for the server result.
    /// </summary>
    private class PendingTransfer {
        public PlayerTransferType TransferType { get; }
        public int Amount { get; }

        public PendingTransfer(PlayerTransferType transferType, int amount) {
            TransferType = transferType;
            Amount = amount;
        }
    }
}
