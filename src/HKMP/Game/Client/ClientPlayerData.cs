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

    /// <summary>
    /// The round trip time of this player's connection to the server, in milliseconds.
    /// </summary>
    public ushort Ping { get; set; }

    /// <summary>
    /// Whether we have received any ping update for this player yet.
    /// </summary>
    public bool PingKnown { get; set; }

    /// <summary>
    /// The color the player picked for their name and their row in the teammate HUD, as an index into the
    /// teammate palette. Zero means that the color is derived from the username.
    /// </summary>
    public byte PlayerColorIndex { get; set; }

    /// <summary>
    /// Whether we have received the chosen color of this player yet.
    /// </summary>
    public bool ColorKnown { get; set; }

    /// <summary>
    /// Whether the player is currently in a boss fight.
    /// </summary>
    public bool BossFightActive { get; set; }

    /// <summary>
    /// Whether we have received any boss fight update for this player yet.
    /// </summary>
    public bool BossFightKnown { get; set; }

    public ClientPlayerData(
        ushort id,
        string username
    ) {
        Id = id;
        Username = username;
    }
}
