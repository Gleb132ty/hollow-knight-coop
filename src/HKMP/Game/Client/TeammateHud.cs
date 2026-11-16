using System.Collections.Generic;
using Hkmp.Game.Settings;
using Hkmp.Networking.Client;
using Hkmp.Util;
using UnityEngine;

namespace Hkmp.Game.Client;

/// <summary>
/// Manager that keeps the local client informed about the state of the other players (their health and whether
/// they are downed) and draws the teammate overlay: a status list in the corner of the screen, a mark above the
/// teammates that are visible and an arrow pointing to the teammates that are off screen.
/// </summary>
internal class TeammateHud {
    /// <summary>
    /// Interval in seconds between periodic health updates. Health is also sent immediately whenever it changes.
    /// </summary>
    private const float HealthSendInterval = 1f;

    /// <summary>
    /// Height in pixels (at 1080p) of a single row in the teammate list.
    /// </summary>
    private const float RowHeight = 28f;

    /// <summary>
    /// Width in pixels (at 1080p) of the teammate list panel.
    /// </summary>
    private const float PanelWidth = 300f;

    /// <summary>
    /// Distance in pixels (at 1080p) between the edge of the screen and the off-screen arrows.
    /// </summary>
    private const float ArrowInset = 70f;

    /// <summary>
    /// Color used for a teammate that is alive and healthy.
    /// </summary>
    private static readonly Color AliveColor = new Color(0.45f, 0.95f, 0.55f, 1f);

    /// <summary>
    /// Color used for a teammate that is downed and waiting for a revive.
    /// </summary>
    private static readonly Color DownedColor = new Color(1f, 0.35f, 0.35f, 1f);

    /// <summary>
    /// Color used for a teammate that is not in the current scene.
    /// </summary>
    private static readonly Color AwayColor = new Color(0.65f, 0.65f, 0.7f, 1f);

    /// <summary>
    /// Color used for the background of a row.
    /// </summary>
    private static readonly Color RowBackgroundColor = new Color(0f, 0f, 0f, 0.55f);

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
    /// The health values last sent to the server.
    /// </summary>
    private byte _lastSentHealth = byte.MaxValue;

    /// <summary>
    /// The maximum health values last sent to the server.
    /// </summary>
    private byte _lastSentMaxHealth = byte.MaxValue;

    /// <summary>
    /// Realtime timestamp at which the health should be sent again even if it did not change.
    /// </summary>
    private float _nextHealthSendTime;

    /// <summary>
    /// Texture used for drawing solid rectangles.
    /// </summary>
    private Texture2D _whiteTexture;

    /// <summary>
    /// Texture of an arrow pointing up, used for the off-screen indicators.
    /// </summary>
    private Texture2D _arrowTexture;

    /// <summary>
    /// Texture of a diamond mark, used for teammates that are on screen.
    /// </summary>
    private Texture2D _markTexture;

    /// <summary>
    /// Cached label style for the teammate list.
    /// </summary>
    private GUIStyle _labelStyle;

    /// <summary>
    /// Cached right-aligned label style for the teammate list.
    /// </summary>
    private GUIStyle _rightLabelStyle;

    /// <summary>
    /// Cached label style for small texts next to the off-screen arrows.
    /// </summary>
    private GUIStyle _smallLabelStyle;

    /// <summary>
    /// Reusable list of the players that are drawn, so we do not allocate one every GUI tick.
    /// </summary>
    private readonly List<KeyValuePair<ushort, ClientPlayerData>> _sortedPlayers =
        new List<KeyValuePair<ushort, ClientPlayerData>>();

    public TeammateHud(
        NetClient netClient,
        Dictionary<ushort, ClientPlayerData> playerData,
        ModSettings modSettings
    ) {
        _netClient = netClient;
        _playerData = playerData;
        _modSettings = modSettings;
    }

    /// <summary>
    /// Initialize the teammate HUD by subscribing to the update and GUI events.
    /// </summary>
    public void Initialize() {
        if (MonoBehaviourUtil.Instance != null) {
            MonoBehaviourUtil.Instance.OnUpdateEvent += OnUpdate;
            MonoBehaviourUtil.Instance.OnGUIEvent += OnGui;
        }
    }

    /// <summary>
    /// Send the current health of the local player to the server whenever it changes.
    /// </summary>
    private void OnUpdate() {
        if (!_netClient.IsConnected) {
            _lastSentHealth = byte.MaxValue;
            _lastSentMaxHealth = byte.MaxValue;
            return;
        }

        var playerData = PlayerData.instance;
        if (playerData == null || HeroController.instance == null) {
            return;
        }

        // Do not report health while the game is in a menu or a cutscene
        if (SceneUtil.IsNonGameplayScene(SceneUtil.GetCurrentSceneName())) {
            return;
        }

        var maxHealth = (byte) Mathf.Clamp(playerData.CurrentMaxHealth, 0, 255);
        var health = (byte) Mathf.Clamp(playerData.health, 0, 255);

        var now = Time.realtimeSinceStartup;
        if (health == _lastSentHealth && maxHealth == _lastSentMaxHealth && now < _nextHealthSendTime) {
            return;
        }

        _lastSentHealth = health;
        _lastSentMaxHealth = maxHealth;
        _nextHealthSendTime = now + HealthSendInterval;

        _netClient.UpdateManager.UpdatePlayerHealth(health, maxHealth);
    }

    /// <summary>
    /// Called when a health update for a player is received.
    /// </summary>
    /// <param name="id">The ID of the player.</param>
    /// <param name="health">The current amount of health of the player.</param>
    /// <param name="maxHealth">The maximum amount of health of the player.</param>
    public void OnPlayerHealthUpdate(ushort id, byte health, byte maxHealth) {
        if (!_playerData.TryGetValue(id, out var playerData)) {
            return;
        }

        playerData.Health = health;
        playerData.MaxHealth = maxHealth;
        playerData.HealthKnown = true;
    }

    /// <summary>
    /// Draw the teammate list, the marks above visible teammates and the arrows to teammates that are off
    /// screen.
    /// </summary>
    private void OnGui() {
        if (Event.current == null || Event.current.type != EventType.Repaint) {
            return;
        }

        if (!_netClient.IsConnected || !_modSettings.TeammateHud) {
            return;
        }

        var gameManager = global::GameManager.instance;
        if (gameManager == null || gameManager.isPaused) {
            return;
        }

        if (SceneUtil.IsNonGameplayScene(SceneUtil.GetCurrentSceneName())) {
            return;
        }

        EnsureTextures();
        EnsureStyles();

        DrawTeammateList();
        DrawTeammateMarks();
    }

    /// <summary>
    /// Draw the list of the connected players with their health and state.
    /// </summary>
    private void DrawTeammateList() {
        _sortedPlayers.Clear();
        foreach (var idPlayerDataPair in _playerData) {
            _sortedPlayers.Add(idPlayerDataPair);
        }

        if (_sortedPlayers.Count == 0) {
            return;
        }

        _sortedPlayers.Sort((first, second) =>
            string.CompareOrdinal(first.Value.Username, second.Value.Username)
        );

        var scale = Screen.height / 1080f;
        var rowHeight = RowHeight * scale;
        var panelWidth = PanelWidth * scale;
        var panelHeight = rowHeight * _sortedPlayers.Count;

        var panelRect = new Rect(
            18f * scale,
            Screen.height * 0.30f,
            panelWidth,
            panelHeight
        );

        GUI.color = new Color(0f, 0f, 0f, 0.35f);
        GUI.DrawTexture(panelRect, _whiteTexture);
        GUI.color = Color.white;

        var rowY = panelRect.y;
        foreach (var idPlayerDataPair in _sortedPlayers) {
            var playerData = idPlayerDataPair.Value;

            var rowRect = new Rect(panelRect.x, rowY, panelWidth, rowHeight);
            GUI.color = RowBackgroundColor;
            GUI.DrawTexture(new Rect(rowRect.x, rowRect.y, rowRect.width, rowRect.height - 2f * scale), _whiteTexture);

            Color stateColor;
            string stateText;
            if (DownedPlayerRegistry.IsDowned(idPlayerDataPair.Key)) {
                stateColor = DownedColor;
                stateText = Localization.Get("Downed");
            } else if (!playerData.IsInLocalScene) {
                stateColor = AwayColor;
                stateText = Localization.Get("Other location");
            } else {
                stateColor = AliveColor;
                stateText = string.Empty;
            }

            // Color bar on the left side of the row
            GUI.color = stateColor;
            GUI.DrawTexture(
                new Rect(rowRect.x, rowRect.y, 5f * scale, rowRect.height - 2f * scale),
                _whiteTexture
            );

            GUI.color = Color.white;
            GUI.Label(
                new Rect(rowRect.x + 12f * scale, rowRect.y, rowRect.width * 0.55f, rowRect.height),
                playerData.Username,
                _labelStyle
            );

            var healthText = stateText;
            if (healthText.Length == 0) {
                healthText = playerData.HealthKnown
                    ? $"{playerData.Health}/{playerData.MaxHealth}"
                    : Localization.Get("No data");
            }

            GUI.color = stateColor;
            GUI.Label(
                new Rect(rowRect.x + rowRect.width * 0.45f, rowRect.y, rowRect.width * 0.53f, rowRect.height),
                healthText,
                _rightLabelStyle
            );

            GUI.color = Color.white;

            rowY += rowHeight;
        }
    }

    /// <summary>
    /// Draw a mark above every teammate in the current scene that is visible and an arrow at the edge of the
    /// screen for every teammate that is off screen.
    /// </summary>
    private void DrawTeammateMarks() {
        var gameCameras = GameCameras.instance;
        var camera = gameCameras != null ? gameCameras.mainCamera : Camera.main;
        if (camera == null) {
            return;
        }

        var hero = HeroController.instance;
        var heroPosition = hero != null ? hero.transform.position : Vector3.zero;

        var scale = Screen.height / 1080f;
        var markSize = 22f * scale;
        var arrowSize = 34f * scale;
        var inset = ArrowInset * scale;

        var screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

        foreach (var idPlayerDataPair in _playerData) {
            var playerData = idPlayerDataPair.Value;
            if (!playerData.IsInLocalScene) {
                continue;
            }

            var container = playerData.PlayerContainer;
            if (container == null) {
                continue;
            }

            var worldPosition = container.transform.position + new Vector3(0f, 1.1f, 0f);
            var screenPosition = camera.WorldToScreenPoint(worldPosition);

            // Unity's screen coordinates have their origin at the bottom left, while GUI coordinates have
            // their origin at the top left
            var guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);

            var isDowned = DownedPlayerRegistry.IsDowned(idPlayerDataPair.Key);
            var color = isDowned ? DownedColor : AliveColor;

            var isVisible = screenPosition.z > 0f
                            && guiPosition.x >= inset
                            && guiPosition.x <= Screen.width - inset
                            && guiPosition.y >= inset
                            && guiPosition.y <= Screen.height - inset;

            if (isVisible) {
                GUI.color = color;
                GUI.DrawTexture(
                    new Rect(
                        guiPosition.x - markSize / 2f,
                        guiPosition.y - markSize,
                        markSize,
                        markSize
                    ),
                    _markTexture
                );

                if (isDowned) {
                    GUI.Label(
                        new Rect(
                            guiPosition.x - 90f * scale,
                            guiPosition.y - markSize - 24f * scale,
                            180f * scale,
                            24f * scale
                        ),
                        Localization.Get("Downed"),
                        _smallLabelStyle
                    );
                }

                GUI.color = Color.white;
                continue;
            }

            // The teammate is off screen: place an arrow at the edge of the screen pointing towards them
            var delta = guiPosition - screenCenter;
            if (delta.sqrMagnitude < 0.0001f) {
                continue;
            }

            var halfWidth = Screen.width / 2f - inset;
            var halfHeight = Screen.height / 2f - inset;

            var scaleX = Mathf.Abs(delta.x) > 0.0001f ? halfWidth / Mathf.Abs(delta.x) : float.MaxValue;
            var scaleY = Mathf.Abs(delta.y) > 0.0001f ? halfHeight / Mathf.Abs(delta.y) : float.MaxValue;
            var factor = Mathf.Min(scaleX, scaleY);

            var arrowPosition = screenCenter + delta * factor;

            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle + 90f, arrowPosition);

            GUI.color = color;
            GUI.DrawTexture(
                new Rect(
                    arrowPosition.x - arrowSize / 2f,
                    arrowPosition.y - arrowSize / 2f,
                    arrowSize,
                    arrowSize
                ),
                _arrowTexture
            );

            GUI.matrix = matrix;

            GUI.color = color * new Color(1f, 1f, 1f, 0.9f);

            var distance = Vector3.Distance(heroPosition, container.transform.position);
            GUI.Label(
                new Rect(
                    arrowPosition.x - 40f * scale,
                    arrowPosition.y + arrowSize / 2f,
                    80f * scale,
                    22f * scale
                ),
                Mathf.RoundToInt(distance).ToString(),
                _smallLabelStyle
            );

            GUI.color = Color.white;
        }
    }

    /// <summary>
    /// Ensure that the textures used by the overlay exist.
    /// </summary>
    private void EnsureTextures() {
        if (_whiteTexture == null) {
            _whiteTexture = CreateSolidTexture();
        }

        if (_arrowTexture == null) {
            _arrowTexture = CreateArrowTexture();
        }

        if (_markTexture == null) {
            _markTexture = CreateMarkTexture();
        }
    }

    /// <summary>
    /// Create a 1x1 white texture used for drawing solid rectangles.
    /// </summary>
    /// <returns>The created texture.</returns>
    private static Texture2D CreateSolidTexture() {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) {
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        return texture;
    }

    /// <summary>
    /// Create the texture of an arrow pointing up.
    /// </summary>
    /// <returns>The created texture.</returns>
    private static Texture2D CreateArrowTexture() {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear
        };

        for (var y = 0; y < size; y++) {
            for (var x = 0; x < size; x++) {
                // A triangle pointing up with a wide base at the bottom
                var normalizedY = y / (float) (size - 1);
                var halfWidth = normalizedY * 0.48f;
                var normalizedX = Mathf.Abs(x - (size - 1) / 2f) / (size - 1);

                texture.SetPixel(x, y, normalizedX <= halfWidth ? Color.white : Color.clear);
            }
        }

        texture.Apply();

        return texture;
    }

    /// <summary>
    /// Create the texture of a diamond mark.
    /// </summary>
    /// <returns>The created texture.</returns>
    private static Texture2D CreateMarkTexture() {
        const int size = 24;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear
        };

        var center = (size - 1) / 2f;

        for (var y = 0; y < size; y++) {
            for (var x = 0; x < size; x++) {
                var distance = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                texture.SetPixel(x, y, distance <= center ? Color.white : Color.clear);
            }
        }

        texture.Apply();

        return texture;
    }

    /// <summary>
    /// Ensure that the label styles used by the overlay exist and have the correct font size.
    /// </summary>
    private void EnsureStyles() {
        var fontSize = Mathf.Max(10, Mathf.RoundToInt(17f * Screen.height / 1080f));
        var smallFontSize = Mathf.Max(9, Mathf.RoundToInt(14f * Screen.height / 1080f));

        if (_labelStyle == null) {
            _labelStyle = new GUIStyle(GUI.skin.label) {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold
            };
        }

        if (_labelStyle.fontSize != fontSize) {
            _labelStyle.fontSize = fontSize;
        }

        if (_rightLabelStyle == null) {
            _rightLabelStyle = new GUIStyle(GUI.skin.label) {
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Bold
            };
        }

        if (_rightLabelStyle.fontSize != fontSize) {
            _rightLabelStyle.fontSize = fontSize;
        }

        if (_smallLabelStyle == null) {
            _smallLabelStyle = new GUIStyle(GUI.skin.label) {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
        }

        if (_smallLabelStyle.fontSize != smallFontSize) {
            _smallLabelStyle.fontSize = smallFontSize;
        }
    }
}
