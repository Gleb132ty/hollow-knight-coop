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

    public Keybinds() {
        OpenChat = CreatePlayerAction("OpenChat");
        OpenChat.AddDefaultBinding(Key.T);

        RespawnAtBench = CreatePlayerAction("RespawnAtBench");
        RespawnAtBench.AddDefaultBinding(Key.R);
    }
}
