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
    /// Whether to draw the teammate HUD: the list of connected players, the mark above teammates that are
    /// visible and the arrow pointing to teammates that are off screen.
    /// </summary>
    public bool TeammateHud { get; set; } = true;

    /// <summary>
    /// Whether the camera should follow a living teammate while the local player is downed and waiting for a
    /// revive.
    /// </summary>
    public bool SpectatorCamera { get; set; } = true;

    /// <summary>
    /// The amount of health (masks) a revived player reappears with.
    /// </summary>
    public int ReviveHealth { get; set; } = 3;

    /// <summary>
    /// Whether the amount of health (masks) the player that defeated the soul of a downed teammate is healed for.
    /// </summary>
    public int ReviverHealAmount { get; set; } = 2;

    /// <summary>
    /// Whether the progress of arenas (Colosseum trials and Godhome boss sequences) should be shared and refreshed
    /// in the current scene when a teammate completes it.
    /// </summary>
    public bool ArenaSync { get; set; } = true;

    /// <summary>
    /// Whether every teammate gets their own color in the teammate HUD and above their character.
    /// </summary>
    public bool TeammateColors { get; set; } = true;

    /// <summary>
    /// The color this player picked for their name and their row in the teammate HUD, as an index into the
    /// teammate palette. Zero means that the color is derived from the username.
    /// </summary>
    public int PlayerColorIndex { get; set; }

    /// <summary>
    /// Scale of the teammate HUD, where 1.0 is the default size.
    /// </summary>
    public float HudScale { get; set; } = 1f;

    /// <summary>
    /// Whether the teammate HUD uses a compact layout with a single row per teammate.
    /// </summary>
    public bool HudCompact { get; set; }

    /// <summary>
    /// Whether the ping of every teammate is shown in the teammate HUD.
    /// </summary>
    public bool HudShowPing { get; set; } = true;

    /// <summary>
    /// Whether the HUD shows the health bar of the boss that is fought in the current scene and whether a
    /// teammate is currently in a boss fight.
    /// </summary>
    public bool BossHud { get; set; } = true;

    /// <summary>
    /// Whether the local player can teleport to a teammate in the same scene for a small amount of Geo.
    /// </summary>
    public bool TeleportToTeammate { get; set; } = true;

    /// <summary>
    /// Whether the positions of teammates and entities are smoothed between network updates.
    /// </summary>
    public bool AvatarSmoothing { get; set; } = true;

    /// <summary>
    /// Whether the local player can heal a nearby teammate by spending soul.
    /// </summary>
    public bool HealTeammate { get; set; } = true;

    /// <summary>
    /// Whether the maximum health of bosses is scaled with the number of players in the scene. Off by default, so
    /// the enemy health stays as it was in previous versions unless the player enables it or applies a profile.
    /// </summary>
    public bool BossHealthScaling { get; set; }

    /// <summary>
    /// Extra maximum health for a boss for every additional player in the scene, as a fraction of its base
    /// health. For example, 0.5 means a boss has 1.5x health with two players and 2x health with three.
    /// </summary>
    public float BossHealthPerPlayer { get; set; } = 0.5f;

    /// <summary>
    /// Whether map progress (visited and mapped scenes) is shared with the team.
    /// </summary>
    public bool ShareMap { get; set; } = true;

    /// <summary>
    /// Whether Hunter's Journal progress is shared with the team.
    /// </summary>
    public bool ShareJournal { get; set; } = true;

    /// <summary>
    /// Whether dream essence (dream orbs) is shared with the team.
    /// </summary>
    public bool ShareEssence { get; set; } = true;

    /// <summary>
    /// Whether rewards such as grubs, Grubfather rewards and Sly's stock are shared with the team.
    /// </summary>
    public bool ShareRewards { get; set; } = true;

    /// <summary>
    /// Whether the progress of conversations with NPCs, such as Quirrel, Zote, Bretta and Cloth, is shared with
    /// the team, so every player sees the same dialogue state.
    /// </summary>
    public bool ShareNpcDialogue { get; set; } = true;

    /// <summary>
    /// The name of the settings profile that was applied last, or "custom" if the settings were changed by hand.
    /// </summary>
    public string SettingsProfile { get; set; } = "custom";

    /// <summary>
    /// Whether the client should try to reconnect to the last server automatically when the connection is lost.
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// Whether to show the statistics of the current session (time, downs, revives) when the session stats
    /// keybind is pressed.
    /// </summary>
    public bool SessionStats { get; set; } = true;

    /// <summary>
    /// The last used server settings in a hosted server.
    /// </summary>
    public ServerSettings ServerSettings { get; set; }
}
