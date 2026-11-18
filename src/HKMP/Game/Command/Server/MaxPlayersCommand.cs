using System;
using Hkmp.Api.Command.Server;
using Hkmp.Game.Server;

namespace Hkmp.Game.Command.Server;

/// <summary>
/// Command for setting the maximum number of players of the server.
/// </summary>
internal class MaxPlayersCommand : IServerCommand {
    /// <summary>
    /// The highest player limit that can be set.
    /// </summary>
    private const int MaxPlayerLimit = 16;

    /// <inheritdoc />
    public string Trigger => "/maxplayers";

    /// <inheritdoc />
    public string[] Aliases => Array.Empty<string>();

    /// <inheritdoc />
    public bool AuthorizedOnly => true;

    /// <summary>
    /// The server manager whose settings are changed.
    /// </summary>
    private readonly ServerManager _serverManager;

    public MaxPlayersCommand(ServerManager serverManager) {
        _serverManager = serverManager;
    }

    /// <inheritdoc />
    public void Execute(ICommandSender commandSender, string[] args) {
        if (args.Length < 2) {
            commandSender.SendMessage($"Invalid usage: {Trigger} <0-{MaxPlayerLimit}> (0 means no limit)");
            return;
        }

        if (!int.TryParse(args[1], out var maxPlayers) || maxPlayers < 0 || maxPlayers > MaxPlayerLimit) {
            commandSender.SendMessage($"Invalid number, use a value between 0 and {MaxPlayerLimit}");
            return;
        }

        _serverManager.InternalServerSettings.MaxPlayers = (byte) maxPlayers;

        if (maxPlayers == 0) {
            commandSender.SendMessage("Player limit removed");
        } else {
            commandSender.SendMessage($"Player limit set to {maxPlayers}, it applies to new connections");
        }
    }
}
