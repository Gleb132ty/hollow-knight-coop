using Hkmp.Ui;
using UnityEngine;

namespace Hkmp.Game.Client;

/// <summary>
/// Class that keeps track of statistics of the current multiplayer session: how long the player has been
/// connected, how often they were downed, revived or gave up, and how often the team went down or was revived.
/// The statistics are shown in chat when the session stats keybind is pressed.
/// </summary>
internal static class SessionStats {
    /// <summary>
    /// Time at which the current session started, measured with Time.realtimeSinceStartup.
    /// </summary>
    private static float _startTime;

    /// <summary>
    /// Whether the session has been started. Used to make sure counters are not reported before connecting.
    /// </summary>
    private static bool _started;

    /// <summary>
    /// Number of times the local player was downed.
    /// </summary>
    private static int _localDowns;

    /// <summary>
    /// Number of times the local player was revived.
    /// </summary>
    private static int _localRevives;

    /// <summary>
    /// Number of times the local player gave up and respawned at a bench.
    /// </summary>
    private static int _localGiveUps;

    /// <summary>
    /// Number of times a remote teammate was downed.
    /// </summary>
    private static int _teamDowns;

    /// <summary>
    /// Number of times a remote teammate was revived.
    /// </summary>
    private static int _teamRevives;

    /// <summary>
    /// Start a new session, resetting all counters. Called when the client connects to a server, but not when
    /// reconnecting after a lost connection, so that the statistics survive a short network hiccup.
    /// </summary>
    public static void StartSession() {
        _startTime = Time.realtimeSinceStartup;
        _started = true;

        _localDowns = 0;
        _localRevives = 0;
        _localGiveUps = 0;
        _teamDowns = 0;
        _teamRevives = 0;
    }

    /// <summary>
    /// Register that the local player was downed.
    /// </summary>
    public static void RegisterLocalDown() {
        EnsureStarted();
        _localDowns++;
    }

    /// <summary>
    /// Register that the local player was revived.
    /// </summary>
    public static void RegisterLocalRevive() {
        EnsureStarted();
        _localRevives++;
    }

    /// <summary>
    /// Register that the local player gave up and respawned at a bench.
    /// </summary>
    public static void RegisterLocalGiveUp() {
        EnsureStarted();
        _localGiveUps++;
    }

    /// <summary>
    /// Register that a remote teammate was downed.
    /// </summary>
    public static void RegisterTeamDown() {
        EnsureStarted();
        _teamDowns++;
    }

    /// <summary>
    /// Register that a remote teammate was revived.
    /// </summary>
    public static void RegisterTeamRevive() {
        EnsureStarted();
        _teamRevives++;
    }

    /// <summary>
    /// Show the statistics of the current session in chat.
    /// </summary>
    public static void Announce() {
        var chatBox = UiManager.InternalChatBox;
        if (chatBox == null) {
            return;
        }

        var elapsed = _started ? Mathf.Max(0f, Time.realtimeSinceStartup - _startTime) : 0f;
        var minutes = Mathf.FloorToInt(elapsed / 60f);
        var seconds = Mathf.FloorToInt(elapsed - minutes * 60f);

        chatBox.AddMessage(Localization.Get("Session statistics"));
        chatBox.AddMessage(string.Format(Localization.Get("Session time: {0}:{1:00}"), minutes, seconds));
        chatBox.AddMessage(
            string.Format(
                Localization.Get("Your downs: {0}, revives: {1}, give-ups: {2}"),
                _localDowns,
                _localRevives,
                _localGiveUps
            )
        );
        chatBox.AddMessage(
            string.Format(
                Localization.Get("Team downs: {0}, team revives: {1}"),
                _teamDowns,
                _teamRevives
            )
        );
    }

    /// <summary>
    /// Make sure the session is started, even if a counter was registered before connecting.
    /// </summary>
    private static void EnsureStarted() {
        if (_started) {
            return;
        }

        _startTime = Time.realtimeSinceStartup;
        _started = true;
    }
}
