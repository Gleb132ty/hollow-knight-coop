using Hkmp.Game.Client;
using Hkmp.Networking.Client;
using Hkmp.Networking.Packet.Data;
using Hkmp.Util;
using UnityEngine;
using Logger = Hkmp.Logging.Logger;

namespace Hkmp.Game.Client.Entity.Component;

// TODO: make sure that the data sent on death is saved as state on the server, so new clients entering
// scenes can start with the entity disabled/already dead
/// <inheritdoc />
/// This component manages the <see cref="HealthManager"/> component of the entity.
internal class HealthManagerComponent : EntityComponent {
    /// <summary>
    /// Host-client pair of health manager components of the entity.
    /// </summary>
    private readonly HostClientPair<HealthManager> _healthManager;

    /// <summary>
    /// Boolean indicating whether the health manager of the client entity is allowed to die.
    /// </summary>
    private bool _allowDeath;

    /// <summary>
    /// The last value for the "invincible" variable of the health manager.
    /// </summary>
    private bool _lastInvincible;

    /// <summary>
    /// The last value for the "invincibleFromDirection" variable of the health manager.
    /// </summary>
    private int _lastInvincibleFromDirection;

    /// <summary>
    /// Whether the host initialization has already been performed. Since this component is registered for
    /// multiple component types, the entity calls <see cref="InitializeHost"/> more than once.
    /// </summary>
    private bool _initializedHost;

    /// <summary>
    /// The last health value that was sent to the server, used to avoid sending duplicate updates.
    /// </summary>
    private int _lastSentHp = int.MinValue;

    public HealthManagerComponent(
        NetClient netClient,
        ushort entityId,
        HostClientPair<GameObject> gameObject,
        HostClientPair<HealthManager> healthManager
    ) : base(netClient, entityId, gameObject) {
        _healthManager = healthManager;

        _lastInvincible = healthManager.Host.IsInvincible;
        _lastInvincibleFromDirection = healthManager.Host.InvincibleFromDirection;

        On.HealthManager.Die += HealthManagerOnDie;
        On.HealthManager.TakeDamage += HealthManagerOnTakeDamage;
        MonoBehaviourUtil.Instance.OnUpdateEvent += OnUpdate;
    }

    /// <summary>
    /// Callback method for when the health manager dies.
    /// </summary>
    /// <param name="orig">The original method.</param>
    /// <param name="self">The health manager instance.</param>
    /// <param name="attackDirection">The direction of the attack that caused the death.</param>
    /// <param name="attackType">The type of attack that caused the death.</param>
    /// <param name="ignoreEvasion">Whether to ignore evasion.</param>
    private void HealthManagerOnDie(
        On.HealthManager.orig_Die orig,
        HealthManager self,
        float? attackDirection,
        AttackTypes attackType,
        bool ignoreEvasion
    ) {
        if (self != _healthManager.Host && self != _healthManager.Client) {
            orig(self, attackDirection, attackType, ignoreEvasion);
            return;
        }

        if (self == _healthManager.Client) {
            if (!_allowDeath) {
                Logger.Info("HealthManager Die was called on client entity");
            } else {
                Logger.Info("HealthManager Die was called on client entity, but it is allowed death");

                orig(self, attackDirection, attackType, ignoreEvasion);

                _allowDeath = false;
            }

            return;
        }

        Logger.Info("HealthManager Die was called on host entity");

        orig(self, attackDirection, attackType, ignoreEvasion);

        var data = new EntityNetworkData {
            Type = EntityComponentType.Death
        };

        if (attackDirection.HasValue) {
            data.Packet.Write(true);
            data.Packet.Write(attackDirection.Value);
        } else {
            data.Packet.Write(false);
        }

        data.Packet.Write((byte) attackType);

        data.Packet.Write(ignoreEvasion);

        SendData(data);

        // The health value has changed as part of the death, so make sure other clients receive the final value
        SendHealthData(force: true);
    }

    /// <summary>
    /// Callback method for when damage is dealt to the health manager.
    /// </summary>
    /// <param name="orig">The original method.</param>
    /// <param name="self">The health manager instance.</param>
    /// <param name="hitInstance">The hit instance describing the damage.</param>
    private void HealthManagerOnTakeDamage(
        On.HealthManager.orig_TakeDamage orig,
        HealthManager self,
        HitInstance hitInstance
    ) {
        orig(self, hitInstance);

        if (self == _healthManager.Host && !IsControlled) {
            // We are the scene host and the entity took damage locally, so update the other clients
            SendHealthData(force: false);

            // Make the enemy focus the player that hit it for a short while
            EnemyTargetManager.Instance?.NotifyLocalDamage(self, hitInstance.Source);

            return;
        }

        if (self == _healthManager.Client && IsControlled) {
            // We are a scene client, so the damage was dealt to our local copy of the entity. Forward
            // the amount of damage to the scene host, which is authoritative for the health value.
            var damage = CalculateDamage(self, hitInstance);
            if (damage <= 0) {
                return;
            }

            var data = new EntityNetworkData {
                Type = EntityComponentType.HealthDamage
            };
            data.Packet.Write(damage);
            SendData(data);
        }
    }

    /// <summary>
    /// Calculate the damage that a hit instance would deal to the given health manager.
    /// </summary>
    /// <param name="healthManager">The health manager that was hit.</param>
    /// <param name="hitInstance">The hit instance.</param>
    /// <returns>The amount of damage.</returns>
    private static int CalculateDamage(HealthManager healthManager, HitInstance hitInstance) {
        if (hitInstance.DamageDealt <= 0) {
            return 0;
        }

        if (healthManager.damageOverride) {
            return 1;
        }

        return Mathf.Max(1, Mathf.RoundToInt(hitInstance.DamageDealt * hitInstance.Multiplier));
    }

    /// <summary>
    /// Send the current health of the host entity to the server.
    /// </summary>
    /// <param name="force">Whether to send the data even if the health value did not change.</param>
    private void SendHealthData(bool force) {
        var host = _healthManager.Host;
        if (host == null) {
            return;
        }

        var hp = Mathf.Max(0, host.hp);
        if (!force && hp == _lastSentHp) {
            return;
        }

        _lastSentHp = hp;

        var maxHp = EnemyHealthManager.GetMaxHp(host);
        if (maxHp < hp) {
            maxHp = hp;
            EnemyHealthManager.SetMaxHp(host, maxHp);
        }

        var data = new EntityNetworkData {
            Type = EntityComponentType.Health
        };
        data.Packet.Write(hp);
        data.Packet.Write(maxHp);
        SendData(data);
    }

    /// <summary>
    /// Callback method for updates to check whether invincibility changes.
    /// </summary>
    private void OnUpdate() {
        if (!IsControlled) {
            // We are the scene host, so make sure any change in health (for example due to scaling on
            // spawn, regeneration or scripted damage) is sent to the other clients
            SendHealthData(force: false);
        }

        var data = new EntityNetworkData {
            Type = EntityComponentType.Invincibility
        };

        var shouldSend = false;

        var newInvincible = _healthManager.Host.IsInvincible;
        if (newInvincible != _lastInvincible) {
            _lastInvincible = newInvincible;
            shouldSend = true;
        }
        data.Packet.Write(newInvincible);

        var newInvincibleFromDir = _healthManager.Host.InvincibleFromDirection;
        if (newInvincibleFromDir != _lastInvincibleFromDirection) {
            _lastInvincibleFromDirection = newInvincibleFromDir;
            shouldSend = true;
        }
        data.Packet.Write((byte) newInvincibleFromDir);

        if (shouldSend) {
            SendData(data);
        }
    }

    /// <inheritdoc />
    public override void InitializeHost() {
        if (_initializedHost) {
            return;
        }

        _initializedHost = true;

        // The host is authoritative for the health value, so send it to the server for other clients
        _lastSentHp = int.MinValue;
        SendHealthData(force: true);
    }

    /// <inheritdoc />
    public override void Update(EntityNetworkData data, bool alreadyInSceneUpdate) {
        if (data.Type == EntityComponentType.HealthDamage) {
            if (IsControlled) {
                // This client is not the scene host for this entity, so the damage is not meant for it
                return;
            }

            var damage = data.Packet.ReadInt();
            ApplyForwardedDamage(damage);
            return;
        }

        if (!IsControlled) {
            Logger.Info("  Entity was not controlled");
            return;
        }

        if (data.Type == EntityComponentType.Death) {
            var attackDirection = new float?();
            if (data.Packet.ReadBool()) {
                attackDirection = data.Packet.ReadFloat();
            }

            var attackType = (AttackTypes) data.Packet.ReadByte();
            var ignoreEvasion = data.Packet.ReadBool();

            // Set a boolean to indicate that the client health manager is allowed to execute the Die method
            _allowDeath = true;
            _healthManager.Client.Die(attackDirection, attackType, ignoreEvasion);
        } else if (data.Type == EntityComponentType.Invincibility) {
            var newInvincible = data.Packet.ReadBool();
            var newInvincibleFromDir = data.Packet.ReadByte();

            if (_healthManager.Host != null) {
                _healthManager.Host.IsInvincible = newInvincible;
                _healthManager.Host.InvincibleFromDirection = newInvincibleFromDir;
            }

            if (_healthManager.Client != null) {
                _healthManager.Client.IsInvincible = newInvincible;
                _healthManager.Client.InvincibleFromDirection = newInvincibleFromDir;
            }
        } else if (data.Type == EntityComponentType.Health) {
            var hp = data.Packet.ReadInt();
            var maxHp = data.Packet.ReadInt();

            if (_healthManager.Host != null) {
                _healthManager.Host.hp = hp;
                EnemyHealthManager.SetMaxHp(_healthManager.Host, maxHp);
            }

            if (_healthManager.Client != null) {
                _healthManager.Client.hp = hp;
                EnemyHealthManager.SetMaxHp(_healthManager.Client, maxHp);
            }
        }
    }

    /// <summary>
    /// Apply damage that was forwarded by a scene client to the host entity.
    /// </summary>
    /// <param name="damage">The amount of damage.</param>
    private void ApplyForwardedDamage(int damage) {
        var host = _healthManager.Host;
        if (host == null || damage <= 0) {
            return;
        }

        if (host.isDead || host.CheckInvincible()) {
            return;
        }

        host.ApplyExtraDamage(damage);

        // Make the enemy focus a remote player, since forwarded damage does not carry the attacker
        EnemyTargetManager.Instance?.NotifyForwardedDamage(host);

        // Keep the client copy in sync as well, in case this client becomes the scene host later
        if (_healthManager.Client != null) {
            _healthManager.Client.hp = host.hp;
        }

        SendHealthData(force: true);
    }

    /// <inheritdoc />
    public override void Destroy() {
        On.HealthManager.Die -= HealthManagerOnDie;
        On.HealthManager.TakeDamage -= HealthManagerOnTakeDamage;
        MonoBehaviourUtil.Instance.OnUpdateEvent -= OnUpdate;
    }
}
