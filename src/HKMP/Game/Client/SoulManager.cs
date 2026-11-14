using System;
using System.Collections;
using System.Collections.Generic;
using Hkmp.Game.Settings;
using Hkmp.Networking.Client;
using Hkmp.Util;
using UnityEngine;
using Logger = Hkmp.Logging.Logger;

namespace Hkmp.Game.Client;

/// <summary>
/// Manager for the soul of a downed player. When a teammate is downed, a hostile soul rises from their body on
/// every client that has that player in the scene. The soul chases and damages the living players; defeating it
/// sends a revive request for the downed player.
/// </summary>
internal class SoulManager {
    /// <summary>
    /// Name of the game object that represents the soul, used by the automated tests.
    /// </summary>
    public const string SoulObjectName = "HKMP Soul";

    /// <summary>
    /// Layer index of the enemy layer. Souls need to be on this layer so that nail hits and spells can hit them.
    /// </summary>
    private const int EnemyLayer = 11;

    /// <summary>
    /// Damage dealt to a living player when the soul touches them.
    /// </summary>
    private const int SoulDamage = 1;

    /// <summary>
    /// Height above the downed body at which the soul floats.
    /// </summary>
    private const float SpawnHeight = 1.1f;

    /// <summary>
    /// Distance from the downed body at which the soul stops chasing and floats back towards the body.
    /// </summary>
    private const float LeashRadius = 14f;

    /// <summary>
    /// Vertical distance at which the soul still follows the player. Above this it glides back down.
    /// </summary>
    private const float VerticalRange = 3.5f;

    /// <summary>
    /// Time in seconds for which the hint about the soul is shown after it appears.
    /// </summary>
    private const float HintDuration = 8f;

    /// <summary>
    /// The net client instance.
    /// </summary>
    private readonly NetClient _netClient;

    /// <summary>
    /// The player manager instance used to obtain remote player objects.
    /// </summary>
    private readonly PlayerManager _playerManager;

    /// <summary>
    /// Mapping of remote player IDs to player data.
    /// </summary>
    private readonly Dictionary<ushort, ClientPlayerData> _playerData;

    /// <summary>
    /// The mod settings instance.
    /// </summary>
    private readonly ModSettings _modSettings;

    /// <summary>
    /// The souls that are currently alive, mapped by the ID of the downed player they belong to.
    /// </summary>
    private readonly Dictionary<ushort, SoulController> _souls = [];

    /// <summary>
    /// Remaining time for which the hint about a defeated soul should be shown.
    /// </summary>
    private float _hintTimer;

    /// <summary>
    /// Name of the player whose soul is shown in the hint.
    /// </summary>
    private string _hintPlayerName;

    /// <summary>
    /// Texture used to draw the overlay.
    /// </summary>
    private Texture2D _whiteTexture;

    /// <summary>
    /// Cached label style used by the overlay.
    /// </summary>
    private GUIStyle _labelStyle;

    /// <summary>
    /// Constructs the soul manager.
    /// </summary>
    /// <param name="netClient">The net client instance.</param>
    /// <param name="playerManager">The player manager instance.</param>
    /// <param name="playerData">Mapping of remote player IDs to player data.</param>
    /// <param name="modSettings">The mod settings instance.</param>
    public SoulManager(
        NetClient netClient,
        PlayerManager playerManager,
        Dictionary<ushort, ClientPlayerData> playerData,
        ModSettings modSettings
    ) {
        _netClient = netClient;
        _playerManager = playerManager;
        _playerData = playerData;
        _modSettings = modSettings;
    }

    /// <summary>
    /// Initialize the soul manager by registering its update callbacks.
    /// </summary>
    public void Initialize() {
        if (MonoBehaviourUtil.Instance != null) {
            MonoBehaviourUtil.Instance.OnUpdateEvent += OnUpdate;
            MonoBehaviourUtil.Instance.OnGUIEvent += OnGui;
        }
    }

    /// <summary>
    /// Callback method for when a remote player has been downed. Spawns the soul that the other players have to
    /// defeat to revive them.
    /// </summary>
    /// <param name="id">The ID of the downed player.</param>
    public void OnRemotePlayerDowned(ushort id) {
        if (!_modSettings.ReviveEnabled || !_netClient.IsConnected || MonoBehaviourUtil.Instance == null) {
            return;
        }

        if (_souls.ContainsKey(id)) {
            return;
        }

        if (_playerData.TryGetValue(id, out var data) && !data.IsInLocalScene) {
            // The soul is spawned when this player enters our scene, not in the scene they went down in
            return;
        }

        MonoBehaviourUtil.Instance.StartCoroutine(SpawnSoul(id));
    }

    /// <summary>
    /// Callback method for when a remote player has been revived or gave up.
    /// </summary>
    /// <param name="id">The ID of the player.</param>
    public void OnRemotePlayerRevived(ushort id) {
        RemoveSoul(id);
    }

    /// <summary>
    /// Callback method for when a remote player leaves our scene or disconnects.
    /// </summary>
    /// <param name="id">The ID of the player.</param>
    public void OnPlayerLeft(ushort id) {
        RemoveSoul(id);
    }

    /// <summary>
    /// Callback method for when the local player changes scenes. The souls of the previous scene are gone.
    /// </summary>
    public void OnLocalSceneChange() {
        _souls.Clear();
        _hintTimer = 0f;
    }

    /// <summary>
    /// Callback method for when the local client disconnects.
    /// </summary>
    public void OnDisconnected() {
        foreach (var soul in _souls.Values) {
            if (soul != null) {
                UnityEngine.Object.Destroy(soul.gameObject);
            }
        }

        _souls.Clear();
        _hintTimer = 0f;
    }

    /// <summary>
    /// Coroutine that waits for the avatar of the downed player and spawns the soul on top of it.
    /// </summary>
    /// <param name="id">The ID of the downed player.</param>
    /// <returns>An enumerator for the coroutine.</returns>
    private IEnumerator SpawnSoul(ushort id) {
        GameObject avatar = null;

        var deadline = Time.realtimeSinceStartup + 4f;
        while (Time.realtimeSinceStartup < deadline) {
            if (!DownedPlayerRegistry.IsDowned(id)) {
                // The player was revived or gave up before the avatar appeared
                yield break;
            }

            if (_playerData.TryGetValue(id, out var data) && data.IsInLocalScene) {
                avatar = _playerManager.GetPlayerObject(id);
                if (avatar != null && avatar.activeInHierarchy) {
                    break;
                }
            }

            yield return new WaitForSeconds(0.1f);
        }

        if (avatar == null || !DownedPlayerRegistry.IsDowned(id) || !_netClient.IsConnected) {
            yield break;
        }

        SpawnSoulObject(id, avatar.transform.position);
    }

    /// <summary>
    /// Spawn the soul object for the given player at the given position.
    /// </summary>
    /// <param name="ownerId">The ID of the downed player.</param>
    /// <param name="anchor">The position of the downed body.</param>
    private void SpawnSoulObject(ushort ownerId, Vector3 anchor) {
        var spawnPosition = new Vector3(anchor.x, anchor.y + SpawnHeight, anchor.z);

        var soulObject = CreateSoulObject(spawnPosition);
        if (soulObject == null) {
            Logger.Warn("Could not create the soul of the downed player");
            return;
        }

        soulObject.name = SoulObjectName;

        var controller = soulObject.AddComponent<SoulController>();
        controller.Initialize(
            ownerId,
            anchor,
            Mathf.Max(10, _modSettings.SoulHitPoints),
            OnSoulDefeated
        );

        _souls[ownerId] = controller;

        var name = _playerData.TryGetValue(ownerId, out var data) ? data.Username : ownerId.ToString();
        _hintPlayerName = name;
        _hintTimer = HintDuration;

        Logger.Info($"Spawned the soul of player {ownerId} at {spawnPosition}");
    }

    /// <summary>
    /// Create the game object that represents the soul, using the shade prefab of the current scene when it is
    /// available and a simple ghost of the knight otherwise.
    /// </summary>
    /// <param name="position">The position to spawn the soul at.</param>
    /// <returns>The created game object, or null if it could not be created.</returns>
    private static GameObject CreateSoulObject(Vector3 position) {
        GameObject soulObject = null;

        var sceneManager = UnityEngine.Object.FindObjectOfType<global::SceneManager>();
        var prefab = sceneManager == null ? null : sceneManager.hollowShadeObject;

        if (prefab != null) {
            soulObject = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);

            // The vanilla shade is driven by its own PlayMaker FSMs which would fight our movement code and
            // interact with the vanilla shade save data, so we drive the soul entirely ourselves
            foreach (var fsm in soulObject.GetComponentsInChildren<PlayMakerFSM>(true)) {
                UnityEngine.Object.Destroy(fsm);
            }
        } else {
            soulObject = CreateGhostObject(position);
        }

        if (soulObject == null) {
            return null;
        }

        SetLayerRecursively(soulObject, EnemyLayer);

        var healthManager = soulObject.GetComponent<HealthManager>();
        if (healthManager == null) {
            healthManager = soulObject.AddComponent<HealthManager>();
        }

        var damageHero = soulObject.GetComponent<DamageHero>();
        if (damageHero == null) {
            damageHero = soulObject.AddComponent<DamageHero>();
        }

        damageHero.damageDealt = SoulDamage;
        damageHero.hazardType = 1;
        damageHero.shadowDashHazard = false;
        damageHero.resetOnEnable = false;

        healthManager.hasSpecialDeath = false;
        healthManager.deathReset = false;

        // Defeating the soul of a teammate should never drop geo. The vanilla fields are private, so they are
        // cleared through reflection instead of relying on the prefab defaults.
        ClearGeoDrops(healthManager);

        var collider = soulObject.GetComponent<Collider2D>();
        if (collider == null) {
            var boxCollider = soulObject.AddComponent<BoxCollider2D>();
            boxCollider.size = new Vector2(1.1f, 1.6f);
            boxCollider.offset = new Vector2(0f, 0.5f);
            collider = boxCollider;
        }

        // A soul has to have a non-trigger collider, otherwise the hero's trigger based hit box never sees it
        collider.isTrigger = false;

        var rigidbody = soulObject.GetComponent<Rigidbody2D>();
        if (rigidbody != null) {
            UnityEngine.Object.Destroy(rigidbody);
        }

        return soulObject;
    }

    /// <summary>
    /// Clear the geo drops of the given health manager, so defeating the soul never drops geo.
    /// </summary>
    /// <param name="healthManager">The health manager to clear the geo drops for.</param>
    private static void ClearGeoDrops(HealthManager healthManager) {
        var type = typeof(HealthManager);
        foreach (var fieldName in new[] { "smallGeoDrops", "mediumGeoDrops", "largeGeoDrops" }) {
            var field = type.GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
            );

            if (field != null && field.FieldType == typeof(int)) {
                field.SetValue(healthManager, 0);
            }
        }
    }

    /// <summary>
    /// Create a simple ghost of the knight, used when the current scene has no shade prefab.
    /// </summary>
    /// <param name="position">The position to spawn the ghost at.</param>
    /// <returns>The created game object, or null if the knight sprites are not available.</returns>
    private static GameObject CreateGhostObject(Vector3 position) {
        var hero = HeroController.instance;
        if (hero == null) {
            return null;
        }

        var heroSprite = hero.GetComponent<tk2dSprite>();
        var heroAnimator = hero.GetComponent<tk2dSpriteAnimator>();
        if (heroSprite == null || heroSprite.Collection == null) {
            return null;
        }

        var soulObject = new GameObject(SoulObjectName);
        soulObject.transform.position = position;

        var sprite = soulObject.AddComponent<tk2dSprite>();
        sprite.Collection = heroSprite.Collection;
        sprite.SetSprite(heroSprite.spriteId);
        sprite.color = new Color(0.55f, 0.65f, 1f, 0.85f);

        if (heroAnimator != null && heroAnimator.Library != null) {
            var animator = soulObject.AddComponent<tk2dSpriteAnimator>();
            animator.Library = heroAnimator.Library;
            if (animator.GetClipByName("Idle") != null) {
                animator.Play("Idle");
            }
        }

        return soulObject;
    }

    /// <summary>
    /// Set the given layer on the object and all of its children.
    /// </summary>
    /// <param name="gameObject">The object to change.</param>
    /// <param name="layer">The layer to set.</param>
    private static void SetLayerRecursively(GameObject gameObject, int layer) {
        gameObject.layer = layer;

        foreach (var child in gameObject.GetComponentsInChildren<Transform>(true)) {
            child.gameObject.layer = layer;
        }
    }

    /// <summary>
    /// Callback method for when a soul has been defeated by the local player.
    /// </summary>
    /// <param name="ownerId">The ID of the downed player the soul belongs to.</param>
    private void OnSoulDefeated(ushort ownerId) {
        if (!_souls.ContainsKey(ownerId)) {
            return;
        }

        if (!_netClient.IsConnected) {
            return;
        }

        Logger.Info($"Soul of player {ownerId} defeated, requesting a revive");

        _netClient.UpdateManager.SetPlayerReviveRequest(ownerId);
    }

    /// <summary>
    /// Destroy the soul of the given player, if it exists.
    /// </summary>
    /// <param name="id">The ID of the player.</param>
    private void RemoveSoul(ushort id) {
        if (!_souls.TryGetValue(id, out var soul)) {
            return;
        }

        _souls.Remove(id);

        if (soul != null) {
            UnityEngine.Object.Destroy(soul.gameObject);
        }
    }

    /// <summary>
    /// Callback method that runs each update tick.
    /// </summary>
    private void OnUpdate() {
        if (_hintTimer > 0f) {
            _hintTimer -= Time.deltaTime;
        }

        if (_souls.Count == 0) {
            return;
        }

        List<ushort> toRemove = null;

        foreach (var idSoulPair in _souls) {
            if (idSoulPair.Value == null) {
                toRemove ??= [];
                toRemove.Add(idSoulPair.Key);
                continue;
            }

            if (!DownedPlayerRegistry.IsDowned(idSoulPair.Key)) {
                // The player was revived by their own timer or gave up, the soul is no longer needed
                if (idSoulPair.Value != null) {
                    UnityEngine.Object.Destroy(idSoulPair.Value.gameObject);
                }

                toRemove ??= [];
                toRemove.Add(idSoulPair.Key);
            }
        }

        if (toRemove == null) {
            return;
        }

        foreach (var id in toRemove) {
            _souls.Remove(id);
        }
    }

    /// <summary>
    /// Callback method for drawing the soul overlay.
    /// </summary>
    private void OnGui() {
        if (!_modSettings.ReviveEnabled) {
            return;
        }

        if (_hintTimer > 0f && !string.IsNullOrEmpty(_hintPlayerName)) {
            EnsureTexture();
            EnsureLabelStyle();

            var scale = Screen.height / 1080f;
            var width = 560f * scale;
            var height = 46f * scale;
            var rect = new Rect(
                (Screen.width - width) / 2f,
                Screen.height * 0.09f,
                width,
                height
            );

            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(rect, _whiteTexture);

            GUI.color = Color.white;
            GUI.Label(
                rect,
                Localization.Format("Defeat {0}'s soul to revive them", _hintPlayerName),
                _labelStyle
            );
        }

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
    /// Marker component that tells the enemy health scaling that the object it is attached to is a soul and
    /// should keep the health it was given.
    /// </summary>
    public class SoulMarker : MonoBehaviour { }

    /// <summary>
    /// Component that drives a single soul the same way the vanilla shade behaves: it hovers around the closest
    /// living player, repositions above them, telegraphs an attack and then lunges with a slash. It damages them on
    /// contact and reports back to the manager when it is defeated.
    /// </summary>
    private class SoulController : MonoBehaviour {
        /// <summary>
        /// States of the shade-like behaviour of the soul.
        /// </summary>
        private enum SoulState {
            Hover,
            Reposition,
            Antic,
            Lunge,
            Recover
        }

        /// <summary>
        /// The ID of the downed player this soul belongs to.
        /// </summary>
        public ushort OwnerId;

        /// <summary>
        /// The position of the downed body, used to keep the fight near the body.
        /// </summary>
        private Vector3 _anchor;

        /// <summary>
        /// The number of hit points the soul has.
        /// </summary>
        private int _hitPoints;

        /// <summary>
        /// Callback invoked when the soul is defeated.
        /// </summary>
        private Action<ushort> _onDefeated;

        /// <summary>
        /// The health manager of the soul.
        /// </summary>
        private HealthManager _healthManager;

        /// <summary>
        /// The damage component of the soul.
        /// </summary>
        private DamageHero _damageHero;

        /// <summary>
        /// The sprite used to fade the soul out when it is defeated.
        /// </summary>
        private tk2dSprite _sprite;

        /// <summary>
        /// Whether the health of the soul has already been applied.
        /// </summary>
        private bool _healthApplied;

        /// <summary>
        /// Whether the soul has already been defeated.
        /// </summary>
        private bool _defeated;

        /// <summary>
        /// Height of the soul above the downed body.
        /// </summary>
        private float _baseY;

        /// <summary>
        /// Height of the soul above the player it is attacking, used as the aim point of its attacks.
        /// </summary>
        private const float AimHeight = 1.1f;

        /// <summary>
        /// Distance at which the soul hovers from the player it is attacking.
        /// </summary>
        private const float HoverDistance = 2.3f;

        /// <summary>
        /// The sprite animator of the soul, if the shade prefab was used.
        /// </summary>
        private tk2dSpriteAnimator _animator;

        /// <summary>
        /// The idle/hover clip of the shade animation.
        /// </summary>
        private tk2dSpriteAnimationClip _flyClip;

        /// <summary>
        /// The clip played while the soul telegraphs its attack.
        /// </summary>
        private tk2dSpriteAnimationClip _anticClip;

        /// <summary>
        /// The clip played while the soul lunges with its slash.
        /// </summary>
        private tk2dSpriteAnimationClip _slashClip;

        /// <summary>
        /// The child object of the shade prefab that shows the slash.
        /// </summary>
        private GameObject _slashObject;

        /// <summary>
        /// The animator of the slash child object.
        /// </summary>
        private tk2dSpriteAnimator _slashAnimator;

        /// <summary>
        /// The child objects used for the death effect of the shade.
        /// </summary>
        private GameObject _deathSprite;

        /// <summary>
        /// The child objects used for the death effect of the shade.
        /// </summary>
        private GameObject _deathGlow;

        /// <summary>
        /// The current state of the shade-like behaviour.
        /// </summary>
        private SoulState _state;

        /// <summary>
        /// Remaining time in the current state.
        /// </summary>
        private float _stateTimer;

        /// <summary>
        /// The direction of the current lunge, fixed when the lunge starts so that it can be dodged.
        /// </summary>
        private Vector3 _lungeDirection;

        /// <summary>
        /// The target position of the current repositioning flight.
        /// </summary>
        private Vector3 _repositionTarget;

        /// <summary>
        /// Facing direction of the soul, 1 for right and -1 for left.
        /// </summary>
        private float _facing = 1f;

        /// <summary>
        /// Timer used to decide when the soul stops hovering and attacks again.
        /// </summary>
        private float _attackCooldown;

        /// <summary>
        /// The Z coordinate the soul keeps, so it stays in the foreground layer of the scene.
        /// </summary>
        private float _z;

        /// <summary>
        /// Initialise the controller with the data of the downed player it belongs to.
        /// </summary>
        /// <param name="ownerId">The ID of the downed player.</param>
        /// <param name="anchor">The position of the downed body.</param>
        /// <param name="hitPoints">The number of hit points the soul has.</param>
        /// <param name="onDefeated">Callback invoked when the soul is defeated.</param>
        public void Initialize(
            ushort ownerId,
            Vector3 anchor,
            int hitPoints,
            Action<ushort> onDefeated
        ) {
            OwnerId = ownerId;
            _anchor = anchor;
            _hitPoints = hitPoints;
            _onDefeated = onDefeated;
            _baseY = anchor.y + SpawnHeight;
            _z = anchor.z;
        }

        /// <summary>
        /// Unity method called when the component is created.
        /// </summary>
        private void Awake() {
            gameObject.AddComponent<SoulMarker>();
        }

        /// <summary>
        /// Unity method called on the first frame.
        /// </summary>
        private void Start() {
            _healthManager = GetComponent<HealthManager>();
            _damageHero = GetComponent<DamageHero>();
            _sprite = GetComponentInChildren<tk2dSprite>();

            if (_healthManager != null) {
                _healthManager.OnDeath += OnHealthManagerDeath;
            }

            _animator = GetComponent<tk2dSpriteAnimator>();
            if (_animator == null) {
                _animator = GetComponentInChildren<tk2dSpriteAnimator>();
            }

            _flyClip = ResolveClip(
                _animator,
                ["Fly", "Hover", "Float", "Idle"],
                ["Fly", "Float", "Hover", "Idle"]
            );
            _anticClip = ResolveClip(
                _animator,
                ["Slash Antic", "Attack Antic", "Antic"],
                ["Antic", "Charge", "Startle"]
            );
            _slashClip = ResolveClip(
                _animator,
                ["Slash", "Attack"],
                ["Slash", "Attack"]
            );

            _slashObject = FindChildObject(transform, "Slash");
            if (_slashObject != null) {
                // The slash of the shade prefab is a hazard of its own, but the soul already damages players
                // with the hazard on its body, so the slash is used purely as a visual
                foreach (var slashDamageHero in _slashObject.GetComponentsInChildren<DamageHero>(true)) {
                    slashDamageHero.damageDealt = 0;
                    slashDamageHero.enabled = false;
                }

                foreach (var slashCollider in _slashObject.GetComponentsInChildren<Collider2D>(true)) {
                    slashCollider.enabled = false;
                }

                _slashAnimator = _slashObject.GetComponent<tk2dSpriteAnimator>();
                _slashObject.SetActive(false);
            }

            _deathSprite = FindChildObject(transform, "Death Sprite");
            _deathGlow = FindChildObject(transform, "Death Glow");

            if (_animator != null && _animator.Library != null && _animator.Library.clips != null) {
                var clipNames = new List<string>();
                foreach (var clip in _animator.Library.clips) {
                    if (clip != null) {
                        clipNames.Add(clip.name);
                    }
                }

                Logger.Info($"Soul animation clips: {string.Join(", ", clipNames)}");
            }

            EnterHover();
            _attackCooldown = UnityEngine.Random.Range(0.6f, 1.2f);
        }

        /// <summary>
        /// Unity method called when the component is destroyed.
        /// </summary>
        private void OnDestroy() {
            if (_healthManager != null) {
                _healthManager.OnDeath -= OnHealthManagerDeath;
            }
        }

        /// <summary>
        /// Unity method called every frame.
        /// </summary>
        private void Update() {
            if (_defeated) {
                return;
            }

            if (_healthManager == null) {
                Defeat();
                return;
            }

            if (!_healthApplied) {
                // HealthManager.Start scales the health of every enemy, so the fixed value of the soul is set
                // after that has happened. The SoulMarker makes sure the scaling skips this object.
                _healthApplied = true;

                _healthManager.hp = Mathf.Max(1, _hitPoints);
                EnemyHealthManager.SetMaxHp(_healthManager, Mathf.Max(1, _hitPoints));
            }

            if (_healthManager.hp <= 0 || _healthManager.isDead) {
                Defeat();
                return;
            }

            var hero = HeroController.instance;
            if (hero == null) {
                return;
            }

            var heroPosition = hero.transform.position;
            var heroCenter = new Vector3(heroPosition.x, heroPosition.y + AimHeight, _z);
            var leashed = (heroPosition - _anchor).sqrMagnitude > LeashRadius * LeashRadius
                          || Mathf.Abs(heroPosition.y - _anchor.y) > VerticalRange;

            if (_attackCooldown > 0f) {
                _attackCooldown -= Time.deltaTime;
            }

            _stateTimer -= Time.deltaTime;

            if (leashed) {
                // The player ran away from the body, so the soul returns to the body instead of following them
                // across the whole scene
                if (_state != SoulState.Hover) {
                    EnterHover();
                }
            }

            var aim = leashed ? new Vector3(_anchor.x, _baseY, _z) : heroCenter;

            switch (_state) {
                case SoulState.Hover:
                    UpdateHover(aim, leashed);
                    if (_stateTimer <= 0f) {
                        if (leashed || _attackCooldown > 0f
                            || (aim - transform.position).sqrMagnitude > 18f * 18f) {
                            _stateTimer = 0.6f;
                        } else if (UnityEngine.Random.value < 0.45f) {
                            EnterReposition(aim);
                        } else {
                            EnterAntic();
                        }
                    }

                    break;
                case SoulState.Reposition:
                    UpdateReposition(aim);
                    if (_stateTimer <= 0f
                        || (transform.position - _repositionTarget).sqrMagnitude < 0.09f) {
                        EnterAntic();
                    }

                    break;
                case SoulState.Antic:
                    UpdateAntic(aim);
                    if (_stateTimer <= 0f) {
                        EnterLunge(aim);
                    }

                    break;
                case SoulState.Lunge:
                    UpdateLunge();
                    if (_stateTimer <= 0f) {
                        EnterRecover();
                    }

                    break;
                case SoulState.Recover:
                    UpdateRecover(aim);
                    if (_stateTimer <= 0f) {
                        EnterHover();
                    }

                    break;
            }
        }

        /// <summary>
        /// Enter the hovering state, in which the soul circles the player and waits before its next attack.
        /// </summary>
        private void EnterHover() {
            _state = SoulState.Hover;
            _stateTimer = UnityEngine.Random.Range(1.1f, 2.2f);
            HideSlash();
            PlayClip(_flyClip);
        }

        /// <summary>
        /// Update the hovering state: keep a fixed distance from the player and bob up and down.
        /// </summary>
        /// <param name="aim">The point the soul is circling.</param>
        /// <param name="leashed">Whether the player is too far from the body.</param>
        private void UpdateHover(Vector3 aim, bool leashed) {
            Vector3 desired;
            if (leashed) {
                desired = new Vector3(_anchor.x, _baseY + Mathf.Sin(Time.time * 2.1f) * 0.2f, _z);
            } else {
                var away = HorizontalDirectionFrom(aim, transform.position);
                desired = aim - away * HoverDistance
                          + new Vector3(0f, 1f, 0f) * (Mathf.Sin(Time.time * 2.3f) * 0.18f);
            }

            MoveTowards(desired, 3.6f);
            FaceTowards(aim);
            PlayClip(_flyClip);
        }

        /// <summary>
        /// Enter the repositioning state, in which the soul quickly flies to a point above the player.
        /// </summary>
        /// <param name="aim">The point the soul is circling.</param>
        private void EnterReposition(Vector3 aim) {
            _state = SoulState.Reposition;
            _stateTimer = 0.9f;

            var side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            var height = UnityEngine.Random.Range(1.6f, 2.4f);

            _repositionTarget = new Vector3(aim.x + side * 1.7f, aim.y + height, _z);
        }

        /// <summary>
        /// Update the repositioning state by flying towards the chosen point.
        /// </summary>
        /// <param name="aim">The point the soul is circling.</param>
        private void UpdateReposition(Vector3 aim) {
            MoveTowards(_repositionTarget, 11f);
            FaceTowards(aim);
            PlayClip(_flyClip);
        }

        /// <summary>
        /// Enter the telegraph state, in which the soul stops and pulls back before lunging.
        /// </summary>
        private void EnterAntic() {
            _state = SoulState.Antic;
            _stateTimer = 0.45f;
            HideSlash();
            PlayClip(_anticClip ?? _flyClip, true);
        }

        /// <summary>
        /// Update the telegraph state by slowly drifting away from the player.
        /// </summary>
        /// <param name="aim">The point the soul is aiming at.</param>
        private void UpdateAntic(Vector3 aim) {
            var away = HorizontalDirectionFrom(aim, transform.position);
            MoveTowards(transform.position + away * 0.4f, 1.4f);
            FaceTowards(aim);
        }

        /// <summary>
        /// Enter the lunge state and lock in the direction of the attack.
        /// </summary>
        /// <param name="aim">The point the soul is aiming at.</param>
        private void EnterLunge(Vector3 aim) {
            _state = SoulState.Lunge;
            _stateTimer = 0.22f;

            var delta = aim - transform.position;
            _lungeDirection = delta.sqrMagnitude > 0.0001f
                ? delta.normalized
                : new Vector3(_facing, 0f, 0f);
            FaceTowards(aim);

            ShowSlash();
            PlayClip(_slashClip ?? _anticClip ?? _flyClip, true);

            if (_slashAnimator != null) {
                if (_slashClip != null) {
                    _slashAnimator.Play(_slashClip);
                } else if (_slashAnimator.Library != null
                           && _slashAnimator.Library.clips != null
                           && _slashAnimator.Library.clips.Length > 0) {
                    _slashAnimator.Play(_slashAnimator.Library.clips[0]);
                }
            }
        }

        /// <summary>
        /// Update the lunge state by moving along the locked in direction.
        /// </summary>
        private void UpdateLunge() {
            transform.position += _lungeDirection * (16f * Time.deltaTime);
        }

        /// <summary>
        /// Enter the recovery state after a lunge.
        /// </summary>
        private void EnterRecover() {
            _state = SoulState.Recover;
            _stateTimer = 0.55f;
            _attackCooldown = UnityEngine.Random.Range(1.2f, 2.2f);
            HideSlash();
            PlayClip(_flyClip, true);
        }

        /// <summary>
        /// Update the recovery state by drifting away from the player.
        /// </summary>
        /// <param name="aim">The point the soul is aiming at.</param>
        private void UpdateRecover(Vector3 aim) {
            var away = HorizontalDirectionFrom(aim, transform.position);
            MoveTowards(transform.position + away * 1.5f, 3f);
            FaceTowards(aim);
        }

        /// <summary>
        /// Move the soul towards the given position at the given speed.
        /// </summary>
        /// <param name="target">The position to move to.</param>
        /// <param name="speed">The speed in units per second.</param>
        private void MoveTowards(Vector3 target, float speed) {
            var position = transform.position;
            var delta = target - position;
            var distance = delta.magnitude;
            if (distance <= 0.0001f) {
                return;
            }

            var step = speed * Time.deltaTime;
            position += step >= distance ? delta : delta / distance * step;
            transform.position = new Vector3(position.x, position.y, _z);
        }

        /// <summary>
        /// Turn the soul towards the given position.
        /// </summary>
        /// <param name="target">The position to look at.</param>
        private void FaceTowards(Vector3 target) {
            var direction = target.x - transform.position.x;
            if (Mathf.Abs(direction) < 0.05f) {
                return;
            }

            _facing = direction >= 0f ? 1f : -1f;

            var scale = transform.localScale;
            if (Mathf.Sign(scale.x) != _facing) {
                scale.x = Mathf.Abs(scale.x) * _facing;
                transform.localScale = scale;
            }
        }

        /// <summary>
        /// Get the normalised direction from the given aim point to the given position.
        /// </summary>
        private static Vector3 HorizontalDirectionFrom(Vector3 aim, Vector3 position) {
            var delta = position - aim;
            if (delta.sqrMagnitude < 0.0001f) {
                return Vector3.right;
            }

            return delta.normalized;
        }

        /// <summary>
        /// Show the slash visual of the shade prefab in front of the soul.
        /// </summary>
        private void ShowSlash() {
            if (_slashObject == null) {
                return;
            }

            _slashObject.SetActive(true);
            _slashObject.transform.localPosition = new Vector3(0.75f * _facing, -0.1f, 0f);

            var scale = _slashObject.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * _facing;
            _slashObject.transform.localScale = scale;
        }

        /// <summary>
        /// Hide the slash visual of the shade prefab.
        /// </summary>
        private void HideSlash() {
            if (_slashObject != null && _slashObject.activeSelf) {
                _slashObject.SetActive(false);
            }
        }

        /// <summary>
        /// Play the given animation clip on the soul, unless it is already playing it.
        /// </summary>
        /// <param name="clip">The clip to play.</param>
        /// <param name="restart">Whether to restart the clip if it is already playing.</param>
        private void PlayClip(tk2dSpriteAnimationClip clip, bool restart = false) {
            if (_animator == null || clip == null) {
                return;
            }

            if (!restart && _animator.CurrentClip == clip) {
                return;
            }

            _animator.Play(clip);
        }

        /// <summary>
        /// Resolve an animation clip of the shade prefab by its name, falling back to a partial match.
        /// </summary>
        /// <param name="animator">The animator to take the clips from.</param>
        /// <param name="exactNames">The clip names to try first.</param>
        /// <param name="keywords">The keywords to look for in clip names as a fallback.</param>
        /// <returns>The resolved clip, or null if no clip matched.</returns>
        private static tk2dSpriteAnimationClip ResolveClip(
            tk2dSpriteAnimator animator,
            string[] exactNames,
            string[] keywords
        ) {
            if (animator == null || animator.Library == null || animator.Library.clips == null) {
                return null;
            }

            foreach (var exactName in exactNames) {
                foreach (var clip in animator.Library.clips) {
                    if (clip != null && string.Equals(clip.name, exactName, StringComparison.OrdinalIgnoreCase)) {
                        return clip;
                    }
                }
            }

            foreach (var keyword in keywords) {
                foreach (var clip in animator.Library.clips) {
                    if (clip != null
                        && clip.name != null
                        && clip.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) {
                        return clip;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Find a child object by its name.
        /// </summary>
        /// <param name="root">The root transform to search in.</param>
        /// <param name="name">The name of the child.</param>
        /// <returns>The child object, or null if it was not found.</returns>
        private static GameObject FindChildObject(Transform root, string name) {
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) {
                if (child.name == name) {
                    return child.gameObject;
                }
            }

            return null;
        }

        /// <summary>
        /// Show the death effect of the shade prefab when the soul is defeated.
        /// </summary>
        private void ShowDeathEffect() {
            HideSlash();

            foreach (var effect in new[] { _deathGlow, _deathSprite }) {
                if (effect == null) {
                    continue;
                }

                effect.SetActive(true);

                var effectAnimator = effect.GetComponent<tk2dSpriteAnimator>();
                if (effectAnimator != null
                    && effectAnimator.Library != null
                    && effectAnimator.Library.clips != null
                    && effectAnimator.Library.clips.Length > 0) {
                    effectAnimator.Play(effectAnimator.Library.clips[0]);
                }
            }
        }

        /// <summary>
        /// Callback method for when the health manager of the soul reports its death.
        /// </summary>
        private void OnHealthManagerDeath() {
            Defeat();
        }

        /// <summary>
        /// Defeat the soul: disable its damage and colliders, fade it out and inform the manager.
        /// </summary>
        private void Defeat() {
            if (_defeated) {
                return;
            }

            _defeated = true;

            if (_damageHero != null) {
                _damageHero.damageDealt = 0;
            }

            foreach (var collider in GetComponentsInChildren<Collider2D>()) {
                if (collider != null) {
                    collider.enabled = false;
                }
            }

            ShowDeathEffect();

            Logger.Info($"The soul of player {OwnerId} was defeated");

            _onDefeated?.Invoke(OwnerId);

            if (MonoBehaviourUtil.Instance != null) {
                MonoBehaviourUtil.Instance.StartCoroutine(FadeOut());
            } else {
                UnityEngine.Object.Destroy(gameObject);
            }
        }

        /// <summary>
        /// Fade the soul out and destroy it.
        /// </summary>
        /// <returns>An enumerator for the coroutine.</returns>
        private IEnumerator FadeOut() {
            var duration = 0.45f;
            var elapsed = 0f;

            while (elapsed < duration && this != null) {
                elapsed += Time.deltaTime;

                if (_sprite != null) {
                    var remaining = 1f - Mathf.Clamp01(elapsed / duration);
                    var color = _sprite.color;
                    color.a = remaining;
                    _sprite.color = color;
                }

                transform.localScale = Vector3.one * (1f + elapsed * 0.6f);

                yield return null;
            }

            if (this != null) {
                UnityEngine.Object.Destroy(gameObject);
            }
        }
    }
}
