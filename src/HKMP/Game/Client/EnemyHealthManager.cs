using System;
using System.Collections.Generic;
using GlobalEnums;
using Hkmp.Fsm;
using Hkmp.Game.Settings;
using Hkmp.Networking.Client;
using Hkmp.Util;
using UnityEngine;
using Logger = Hkmp.Logging.Logger;

namespace Hkmp.Game.Client;

/// <summary>
/// Manager for enemy health related client-side features, such as health scaling and health bars.
/// </summary>
internal static class EnemyHealthManager {
    /// <summary>
    /// The mod settings instance.
    /// </summary>
    private static ModSettings _modSettings;

    /// <summary>
    /// The net client instance, used to determine whether we are connected to a server.
    /// </summary>
    private static NetClient _netClient;

    /// <summary>
    /// List of health managers that have a known maximum health value.
    /// </summary>
    private static readonly List<HealthManager> HealthManagers = [];

    /// <summary>
    /// Mapping of health manager to its maximum health value.
    /// </summary>
    private static readonly Dictionary<HealthManager, int> MaxHealthValues = [];

    /// <summary>
    /// Texture used to draw the health bars.
    /// </summary>
    private static Texture2D _whiteTexture;

    /// <summary>
    /// Whether this manager has been initialized.
    /// </summary>
    private static bool _initialized;

    /// <summary>
    /// Provider for the number of players in the current scene, including the local player.
    /// </summary>
    private static Func<int> _playerCountProvider;

    /// <summary>
    /// Set the provider that returns how many players are in the current scene, including the local player.
    /// </summary>
    /// <param name="provider">The provider of the player count.</param>
    public static void SetPlayerCountProvider(Func<int> provider) {
        _playerCountProvider = provider;
    }

    /// <summary>
    /// Initialize this manager by registering hooks and the GUI event.
    /// </summary>
    /// <param name="modSettings">The mod settings instance.</param>
    /// <param name="netClient">The net client instance.</param>
    public static void Initialize(ModSettings modSettings, NetClient netClient) {
        if (_initialized) {
            return;
        }

        _initialized = true;
        _modSettings = modSettings;
        _netClient = netClient;

        On.HealthManager.Start += OnHealthManagerStart;

        if (MonoBehaviourUtil.Instance != null) {
            MonoBehaviourUtil.Instance.OnGUIEvent += OnGui;
        }
    }

    /// <summary>
    /// Read-only view of the currently tracked health managers. Used by features that need to find
    /// nearby enemies, such as battle gate logic.
    /// </summary>
    public static IReadOnlyList<HealthManager> TrackedHealthManagers => HealthManagers;

    /// <summary>
    /// Get the maximum health value for the given health manager, if known.
    /// </summary>
    /// <param name="healthManager">The health manager.</param>
    /// <returns>The maximum health value, or the current health value if it is not known.</returns>
    public static int GetMaxHp(HealthManager healthManager) {        if (healthManager == null) {
            return 0;
        }

        if (MaxHealthValues.TryGetValue(healthManager, out var maxHp) && maxHp > 0) {
            return maxHp;
        }

        return Mathf.Max(0, healthManager.hp);
    }

    /// <summary>
    /// Set the maximum health value for the given health manager.
    /// </summary>
    /// <param name="healthManager">The health manager.</param>
    /// <param name="maxHp">The maximum health value.</param>
    public static void SetMaxHp(HealthManager healthManager, int maxHp) {
        if (healthManager == null || maxHp <= 0) {
            return;
        }

        MaxHealthValues[healthManager] = maxHp;

        if (!HealthManagers.Contains(healthManager)) {
            HealthManagers.Add(healthManager);
        }
    }

    /// <summary>
    /// Callback method for when a health manager is started.
    /// </summary>
    /// <param name="orig">The original method.</param>
    /// <param name="self">The health manager instance.</param>
    private static void OnHealthManagerStart(On.HealthManager.orig_Start orig, HealthManager self) {
        orig(self);

        if (self == null) {
            return;
        }

        // HKMP creates client copies of entities to represent the game objects controlled by the scene
        // host. Those should not be scaled again, since the scene host is authoritative for the health
        // value and maximum health value.
        if (self.GetComponent<PositionInterpolation>() != null) {
            return;
        }

        // The soul of a downed player gets its health from the revive feature and must not be scaled as a
        // regular enemy
        if (self.GetComponent<SoulManager.SoulMarker>() != null) {
            SetMaxHp(self, Mathf.Max(0, self.hp));
            return;
        }

        if (!IsEnemy(self)) {
            return;
        }

        var maxHp = Mathf.Max(0, self.hp);

        var multiplier = GetHealthMultiplier();
        if (_netClient != null && _netClient.IsConnected && multiplier > 1.0f && maxHp > 0) {
            var originalHp = maxHp;
            var scaledHp = Mathf.Max(1, Mathf.RoundToInt(maxHp * multiplier));

            self.hp = scaledHp;
            maxHp = scaledHp;

            // Make sure the boss scene controller knows about the adjusted health, so boss health bars
            // and the boss fight logic use the correct maximum value
            if (BossSceneController.Instance != null) {
                BossSceneController.ReportHealth(self, originalHp, scaledHp, false);
            }
        }

        SetMaxHp(self, maxHp);
    }

    /// <summary>
    /// Get the multiplier that is applied to the maximum health of an enemy or boss.
    /// </summary>
    /// <returns>The multiplier.</returns>
    private static float GetHealthMultiplier() {
        if (_modSettings == null) {
            return 1f;
        }

        if (!_modSettings.BossHealthScaling) {
            return _modSettings.EnemyHealthMultiplier;
        }

        var playerCount = 1;
        if (_playerCountProvider != null) {
            try {
                playerCount = _playerCountProvider();
            } catch (Exception exception) {
                Logger.Error($"Failed to determine the number of players in the scene: {exception}");
            }
        }

        playerCount = Mathf.Max(1, playerCount);

        var perPlayer = Mathf.Clamp(_modSettings.BossHealthPerPlayer, 0f, 3f);

        return Mathf.Clamp(1f + perPlayer * (playerCount - 1), 1f, 8f);
    }

    /// <summary>
    /// Check whether the given health manager belongs to an enemy.
    /// </summary>
    /// <param name="healthManager">The health manager.</param>
    /// <returns>Whether the health manager belongs to an enemy.</returns>
    private static bool IsEnemy(HealthManager healthManager) {
        if (healthManager.gameObject.layer == 11) {
            return true;
        }

        return healthManager.GetComponent<EnemyDeathEffects>() != null
               || healthManager.GetComponentInParent<EnemyDeathEffects>() != null;
    }

    /// <summary>
    /// Callback method for drawing the health bars.
    /// </summary>
    private static void OnGui() {
        if (_modSettings == null || !_modSettings.EnemyHealthBars) {
            return;
        }

        if (HealthManagers.Count == 0) {
            return;
        }

        if (global::GameManager.instance == null || global::GameManager.instance.gameState != GameState.PLAYING) {
            return;
        }

        var camera = Camera.main;
        if (camera == null) {
            return;
        }

        EnsureTexture();

        var scale = Screen.height / 1080f;
        var barWidth = 64f * scale;
        var barHeight = 7f * scale;

        for (var i = HealthManagers.Count - 1; i >= 0; i--) {
            var healthManager = HealthManagers[i];
            if (healthManager == null) {
                HealthManagers.RemoveAt(i);
                continue;
            }

            if (!healthManager.isActiveAndEnabled || healthManager.isDead) {
                continue;
            }

            if (!MaxHealthValues.TryGetValue(healthManager, out var maxHp) || maxHp <= 0) {
                continue;
            }

            var hp = Mathf.Clamp(healthManager.hp, 0, maxHp);
            if (hp >= maxHp) {
                continue;
            }

            var renderer = healthManager.GetComponentInChildren<Renderer>();
            if (renderer == null || !renderer.isVisible) {
                continue;
            }

            var bounds = renderer.bounds;
            var worldPosition = new Vector3(
                bounds.center.x,
                bounds.max.y + 0.35f * scale,
                bounds.center.z
            );
            var screenPosition = camera.WorldToScreenPoint(worldPosition);
            if (screenPosition.z <= 0
                || screenPosition.x < 0
                || screenPosition.x > Screen.width
                || screenPosition.y < 0
                || screenPosition.y > Screen.height) {
                continue;
            }

            var x = screenPosition.x - barWidth / 2f;
            var y = Screen.height - screenPosition.y;

            var fraction = (float) hp / maxHp;

            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(new Rect(x - 1f, y - 1f, barWidth + 2f, barHeight + 2f), _whiteTexture);

            GUI.color = new Color(0.85f, 0.15f, 0.15f, 1f);
            GUI.DrawTexture(new Rect(x, y, barWidth, barHeight), _whiteTexture);

            GUI.color = new Color(0.2f, 0.8f, 0.2f, 1f);
            GUI.DrawTexture(new Rect(x, y, barWidth * fraction, barHeight), _whiteTexture);
        }

        GUI.color = Color.white;
    }

    /// <summary>
    /// Ensure that the texture used for drawing health bars exists.
    /// </summary>
    private static void EnsureTexture() {
        if (_whiteTexture != null) {
            return;
        }

        _whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false) {
            hideFlags = HideFlags.HideAndDontSave
        };
        _whiteTexture.SetPixel(0, 0, Color.white);
        _whiteTexture.Apply();
    }
}
