using InControl;

namespace Hkmp.Menu;

/// <summary>
/// Class that stores keybinds for HKMP, to allow them to be (de)serialized to the settings file.
/// </summary>
internal class Keybinds : PlayerActionSet {
    /// <summary>
    /// Keybind to open the chat.
    /// </summary>
    public PlayerAction OpenChat { get; }

    /// <summary>
    /// Keybind to give up while downed and respawn at the last bench.
    /// </summary>
    public PlayerAction RespawnAtBench { get; }

    /// <summary>
    /// Keybind to show the statistics of the current session in chat.
    /// </summary>
    public PlayerAction ShowSessionStats { get; }

    /// <summary>
    /// Keybind to heal the closest injured teammate.
    /// </summary>
    public PlayerAction HealTeammate { get; }

    /// <summary>
    /// Keybind to teleport to the closest teammate in the current scene.
    /// </summary>
    public PlayerAction TeleportToTeammate { get; }

    public Keybinds() {
        OpenChat = CreatePlayerAction("OpenChat");
        OpenChat.AddDefaultBinding(Key.T);

        RespawnAtBench = CreatePlayerAction("RespawnAtBench");
        RespawnAtBench.AddDefaultBinding(Key.R);

        ShowSessionStats = CreatePlayerAction("ShowSessionStats");
        ShowSessionStats.AddDefaultBinding(Key.F8);

        HealTeammate = CreatePlayerAction("HealTeammate");
        HealTeammate.AddDefaultBinding(Key.H);

        TeleportToTeammate = CreatePlayerAction("TeleportToTeammate");
        TeleportToTeammate.AddDefaultBinding(Key.G);
    }
}
