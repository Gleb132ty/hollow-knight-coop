using System.Collections.Generic;
using Hkmp.Menu;
using Modding.Converters;
using Newtonsoft.Json;

namespace Hkmp.Game.Settings;

/// <summary>
/// Settings class that stores user preferences.
/// </summary>
internal class ModSettings {
    /// <summary>
    /// The authentication key for the user.
    /// </summary>
    public string AuthKey { get; set; }

    /// <summary>
    /// The keybinds for HKMP.
    /// </summary>
    [JsonConverter(typeof(PlayerActionSetConverter))]
    public Keybinds Keybinds { get; set; } = new();

    /// <summary>
    /// The last used address to join a server.
    /// </summary>
    public string ConnectAddress { get; set; }

    /// <summary>
    /// The last used port to join a server.
    /// </summary>
    public int ConnectPort { get; set; } = -1;

    /// <summary>
    /// The last used username to join a server.
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// Whether to display a UI element for the ping.
    /// </summary>
    public bool DisplayPing { get; set; }

    /// <summary>
    /// Set of addon names for addons that are disabled by the user.
    /// </summary>
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Global
    public HashSet<string> DisabledAddons { get; set; } = [];

    /// <summary>
    /// Whether full synchronisation of bosses, enemies, worlds, and saves is enabled.
    /// </summary>
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Global
    public bool FullSynchronisation { get; set; } = true;

    /// <summary>
    /// Whether to display health bars above damaged enemies.
    /// </summary>
    public bool EnemyHealthBars { get; set; } = true;

    /// <summary>
    /// The multiplier applied to the maximum health of enemies and bosses while connected to a server.
    /// </summary>
    public float EnemyHealthMultiplier { get; set; } = 1.5f;

    /// <summary>
    /// Whether downed players can be revived by defeating the soul that rises from their body.
    /// </summary>
    public bool ReviveEnabled { get; set; } = true;

    /// <summary>
    /// The number of hit points of the soul that rises from a downed player. Defeating it revives the player.
    /// </summary>
    public int SoulHitPoints { get; set; } = 30;

    /// <summary>
    /// Whether enemies and bosses should target the closest living player instead of always targeting the
    /// local player.
    /// </summary>
    public bool EnemyTargeting { get; set; } = true;

    /// <summary>
    /// Whether battle gates should stay open for a player that has not entered the arena yet.
    /// </summary>
    public bool GateHoldOpen { get; set; } = true;

    /// <summary>
    /// Whether the icons of other players on the map should only be shown while the local player has the
    /// Wayward Compass charm equipped.
    /// </summary>
    public bool MapIconsRequireCompass { get; set; } = true;

    /// <summary>
    /// The last used server settings in a hosted server.
    /// </summary>
    public ServerSettings ServerSettings { get; set; }
}
