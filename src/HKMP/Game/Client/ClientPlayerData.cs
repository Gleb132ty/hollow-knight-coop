using Hkmp.Api.Client;
using UnityEngine;

namespace Hkmp.Game.Client;

/// <inheritdoc />
internal class ClientPlayerData : IClientPlayer {
    /// <inheritdoc />
    public ushort Id { get; }

    /// <inheritdoc />
    public string Username { get; }

    /// <inheritdoc />
    public bool IsInLocalScene { get; set; }

    /// <inheritdoc />
    public GameObject PlayerContainer { get; set; }

    /// <inheritdoc />
    public GameObject PlayerObject { get; set; }

    /// <inheritdoc />
    public Team Team { get; set; }

    /// <inheritdoc />
    public byte SkinId { get; set; }

    /// <summary>
    /// The amount of health (masks) the player last reported to us.
    /// </summary>
    public byte Health { get; set; }

    /// <summary>
    /// The maximum amount of health (masks) the player last reported to us.
    /// </summary>
    public byte MaxHealth { get; set; }

    /// <summary>
    /// Whether we have received any health update for this player yet.
    /// </summary>
    public bool HealthKnown { get; set; }

    public ClientPlayerData(
        ushort id,
        string username
    ) {
        Id = id;
        Username = username;
    }
}
