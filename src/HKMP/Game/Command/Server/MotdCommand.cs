using System;
using Hkmp.Api.Command.Server;
using Hkmp.Game.Server;

namespace Hkmp.Game.Command.Server;

/// <summary>
/// Command for setting or clearing the message of the day of the server.
/// </summary>
internal class MotdCommand : IServerCommand {
    /// <summary>
    /// The maximum length of the message of the day.
    /// </summary>
    private const int MaxMotdLength = 120;

    /// <inheritdoc />
    public string Trigger => "/motd";

    /// <inheritdoc />
    public string[] Aliases => Array.Empty<string>();

    /// <inheritdoc />
    public bool AuthorizedOnly => true;

    /// <summary>
    /// The server manager whose settings are changed.
    /// </summary>
    private readonly ServerManager _serverManager;

    public MotdCommand(ServerManager serverManager) {
        _serverManager = serverManager;
    }

    /// <inheritdoc />
    public void Execute(ICommandSender commandSender, string[] args) {
        if (args.Length < 2) {
            commandSender.SendMessage($"Invalid usage: {Trigger} <message|off>");
            return;
        }

        var settings = _serverManager.InternalServerSettings;

        var message = string.Join(" ", args, 1, args.Length - 1);
        if (message.Equals("off", StringComparison.OrdinalIgnoreCase)) {
            settings.Motd = "";
            commandSender.SendMessage("Message of the day removed");
            return;
        }

        if (message.Length > MaxMotdLength) {
            message = message.Substring(0, MaxMotdLength);
        }

        settings.Motd = message;
        commandSender.SendMessage($"Message of the day set to: {message}");
    }
}
