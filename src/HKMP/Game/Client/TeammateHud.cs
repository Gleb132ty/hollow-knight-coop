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
    private const float RowHeight = 46f;

    /// <summary>
    /// Width in pixels (at 1080p) of the teammate list panel.
    /// </summary>
    private const float PanelWidth = 300f;

    /// <summary>
    /// Distance in pixels (at 1080p) between the edge of the screen and the off-screen markers.
    /// </summary>
    private const float MarkerInset = 70f;

    /// <summary>
    /// Size in pixels (at 1080p) of a single mask in the teammate list.
    /// </summary>
    private const float MaskSize = 13f;

    /// <summary>
    /// Horizontal distance in pixels (at 1080p) between two masks in the teammate list.
    /// </summary>
    private const float MaskSpacing = 4f;

    /// <summary>
    /// Speed at which a marker fades in or out.
    /// </summary>
    private const float MarkerFadeSpeed = 2.5f;

    /// <summary>
    /// Speed of the blinking (semi-transparent to transparent) of a marker.
    /// </summary>
    private const float MarkerPulseSpeed = 2.6f;

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
    /// Texture of a mask, used in the teammate list and as the marker above teammates.
    /// </summary>
    private Texture2D _maskTexture;

    /// <summary>
    /// Current opacity of the marker of every remote player, so that a marker blinks while the teammate is off
    /// screen and dissolves once the teammate is visible in the camera view.
    /// </summary>
    private readonly Dictionary<ushort, float> _markerAlpha = new Dictionary<ushort, float>();

    /// <summary>
    /// Cached label style for the teammate list.
    /// </summary>
    private GUIStyle _labelStyle;

    /// <summary>
    /// Cached small right-aligned label style for the state text in the teammate list.
    /// </summary>
    private GUIStyle _smallRightLabelStyle;

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
                new Rect(rowRect.x + 12f * scale, rowRect.y + 1f * scale, rowRect.width * 0.62f, 24f * scale),
                playerData.Username,
                _labelStyle
            );

            if (stateText.Length > 0) {
                GUI.color = stateColor;
                GUI.Label(
                    new Rect(
                        rowRect.x + rowRect.width * 0.5f,
                        rowRect.y + 1f * scale,
                        rowRect.width * 0.48f - 6f * scale,
                        24f * scale
                    ),
                    stateText,
                    _smallRightLabelStyle
                );
                GUI.color = Color.white;
            }

            DrawMasks(
                new Rect(
                    rowRect.x + 12f * scale,
                    rowRect.y + 24f * scale,
                    rowRect.width - 24f * scale,
                    18f * scale
                ),
                playerData,
                stateColor,
                scale
            );

            rowY += rowHeight;
        }
    }

    /// <summary>
    /// Draw the masks of a teammate below their name: a filled mask for every health point and a dimmed mask for
    /// every missing one. If the health of a teammate is not known yet, only dimmed masks are drawn.
    /// </summary>
    /// <param name="masksRect">The rectangle in which the masks are drawn.</param>
    /// <param name="playerData">The player data containing the health values.</param>
    /// <param name="stateColor">The color of the filled masks.</param>
    /// <param name="scale">The scale of the overlay.</param>
    private void DrawMasks(Rect masksRect, ClientPlayerData playerData, Color stateColor, float scale) {
        var health = playerData.HealthKnown ? playerData.Health : (byte) 0;
        var maxHealth = playerData.HealthKnown ? playerData.MaxHealth : (byte) 0;

        // Fall back to the numeric value if a player somehow has more masks than fit into the row
        var maxMasksPerRow = Mathf.Max(1, Mathf.FloorToInt(masksRect.width / ((MaskSize + MaskSpacing) * scale)));
        if (maxHealth > maxMasksPerRow) {
            GUI.color = stateColor;
            GUI.Label(
                new Rect(masksRect.x, masksRect.y, masksRect.width, masksRect.height),
                playerData.HealthKnown ? $"{health}/{maxHealth}" : Localization.Get("No data"),
                _labelStyle
            );
            GUI.color = Color.white;
            return;
        }

        var maskSize = MaskSize * scale;
        var step = (MaskSize + MaskSpacing) * scale;

        for (var i = 0; i < maxHealth; i++) {
            var filled = i < health;

            var color = filled ? stateColor : new Color(1f, 1f, 1f, 0.16f);
            if (filled) {
                color.a = 0.95f;
            }

            GUI.color = color;
            GUI.DrawTexture(
                new Rect(masksRect.x + i * step, masksRect.y, maskSize, masksRect.height),
                _maskTexture
            );
        }

        GUI.color = Color.white;
    }

    /// <summary>
    /// Draw the marker of every teammate in the current scene: a mask that blinks while the teammate is off
    /// screen and dissolves into the teammate once they are visible in the camera view.
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
        var markerSize = 34f * scale;
        var inset = MarkerInset * scale;
        var deltaTime = Time.deltaTime;

        var screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

        // Forget the markers of players that are no longer known
        if (_markerAlpha.Count > _playerData.Count) {
            var stale = new List<ushort>();
            foreach (var id in _markerAlpha.Keys) {
                if (!_playerData.ContainsKey(id)) {
                    stale.Add(id);
                }
            }

            foreach (var id in stale) {
                _markerAlpha.Remove(id);
            }
        }

        foreach (var idPlayerDataPair in _playerData) {
            var id = idPlayerDataPair.Key;
            var playerData = idPlayerDataPair.Value;
            if (!playerData.IsInLocalScene) {
                _markerAlpha.Remove(id);
                continue;
            }

            var container = playerData.PlayerContainer;
            if (container == null) {
                _markerAlpha.Remove(id);
                continue;
            }

            var worldPosition = container.transform.position + new Vector3(0f, 1.1f, 0f);
            var screenPosition = camera.WorldToScreenPoint(worldPosition);

            // Unity's screen coordinates have their origin at the bottom left, while GUI coordinates have their
            // origin at the top left
            var guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);

            var isDowned = DownedPlayerRegistry.IsDowned(id);
            var color = isDowned ? DownedColor : AliveColor;

            var isVisible = screenPosition.z > 0f
                            && guiPosition.x >= 0f
                            && guiPosition.x <= Screen.width
                            && guiPosition.y >= 0f
                            && guiPosition.y <= Screen.height;

            // An off screen marker blinks between semi-transparent and transparent, a visible one dissolves onto
            // the teammate instead of hanging above their head
            var targetAlpha = isVisible
                ? 0f
                : 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.time * MarkerPulseSpeed));

            _markerAlpha.TryGetValue(id, out var currentAlpha);
            var alpha = Mathf.MoveTowards(currentAlpha, targetAlpha, MarkerFadeSpeed * deltaTime);
            _markerAlpha[id] = alpha;

            if (alpha <= 0.01f) {
                continue;
            }

            Vector2 markerPosition;
            if (isVisible) {
                markerPosition = new Vector2(guiPosition.x, guiPosition.y - markerSize * 0.5f);
            } else {
                var delta = guiPosition - screenCenter;
                if (delta.sqrMagnitude < 0.0001f) {
                    continue;
                }

                var halfWidth = Screen.width / 2f - inset;
                var halfHeight = Screen.height / 2f - inset;

                var scaleX = Mathf.Abs(delta.x) > 0.0001f ? halfWidth / Mathf.Abs(delta.x) : float.MaxValue;
                var scaleY = Mathf.Abs(delta.y) > 0.0001f ? halfHeight / Mathf.Abs(delta.y) : float.MaxValue;
                var factor = Mathf.Min(scaleX, scaleY);

                markerPosition = screenCenter + delta * factor;
            }

            GUI.color = color * new Color(1f, 1f, 1f, alpha);

            GUI.DrawTexture(
                new Rect(
                    markerPosition.x - markerSize / 2f,
                    markerPosition.y - markerSize / 2f,
                    markerSize,
                    markerSize
                ),
                _maskTexture
            );

            if (isDowned) {
                GUI.Label(
                    new Rect(
                        markerPosition.x - 90f * scale,
                        markerPosition.y - markerSize / 2f - 24f * scale,
                        180f * scale,
                        24f * scale
                    ),
                    Localization.Get("Downed"),
                    _smallLabelStyle
                );
            } else if (!isVisible) {
                var distance = Vector3.Distance(heroPosition, container.transform.position);
                GUI.Label(
                    new Rect(
                        markerPosition.x - 40f * scale,
                        markerPosition.y + markerSize / 2f,
                        80f * scale,
                        22f * scale
                    ),
                    Mathf.RoundToInt(distance).ToString(),
                    _smallLabelStyle
                );
            }

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

        if (_maskTexture == null) {
            _maskTexture = CreateMaskTexture();
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
    /// Create the texture of the mask of the Knight: a rounded white face with two cut out eye sockets. The same
    /// texture is used for the masks in the teammate list and for the marker above a teammate.
    /// </summary>
    /// <returns>The created texture.</returns>
    private static Texture2D CreateMaskTexture() {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear
        };

        var center = (size - 1) / 2f;

        for (var y = 0; y < size; y++) {
            for (var x = 0; x < size; x++) {
                // Normalized coordinates in the range [-1, 1] with the origin in the center
                var nx = (x - center) / center;
                var ny = (y - center) / center;

                // The face is a rounded oval that narrows towards the chin
                var width = ny < 0f ? 0.74f * (1f - 0.22f * -ny) : 0.74f;
                var face = nx * nx / (width * width) + ny * ny / (0.9f * 0.9f);

                // Two vertical eye sockets cut out of the face, so the background shows through them
                var eyeX = Mathf.Abs(nx) - 0.33f;
                var eyeY = ny - 0.12f;
                var eye = eyeX * eyeX / (0.13f * 0.13f) + eyeY * eyeY / (0.24f * 0.24f);

                var insideFace = face <= 1f;
                var insideEye = eye <= 1f;

                texture.SetPixel(x, y, insideFace && !insideEye ? Color.white : Color.clear);
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

        if (_smallRightLabelStyle == null) {
            _smallRightLabelStyle = new GUIStyle(GUI.skin.label) {
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Bold
            };
        }

        if (_smallRightLabelStyle.fontSize != smallFontSize) {
            _smallRightLabelStyle.fontSize = smallFontSize;
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
