using System;
using Hkmp.Api.Command.Server;
using Hkmp.Game.Server;
using Hkmp.Networking.Packet.Data;

namespace Hkmp.Game.Command.Server;

/// <summary>
/// Command for setting or clearing the password of the server.
/// </summary>
internal class PasswordCommand : IServerCommand {
    /// <inheritdoc />
    public string Trigger => "/password";

    /// <inheritdoc />
    public string[] Aliases => Array.Empty<string>();

    /// <inheritdoc />
    public bool AuthorizedOnly => true;

    /// <summary>
    /// The server manager whose settings are changed.
    /// </summary>
    private readonly ServerManager _serverManager;

    public PasswordCommand(ServerManager serverManager) {
        _serverManager = serverManager;
    }

    /// <inheritdoc />
    public void Execute(ICommandSender commandSender, string[] args) {
        if (args.Length < 2) {
            commandSender.SendMessage($"Invalid usage: {Trigger} <password|off>");
            return;
        }

        var settings = _serverManager.InternalServerSettings;

        if (args[1].Equals("off", StringComparison.OrdinalIgnoreCase)) {
            settings.Password = "";
            commandSender.SendMessage("Server password removed");
            return;
        }

        if (args[1].Length > ClientInfo.MaxPasswordLength) {
            commandSender.SendMessage(
                $"Password is too long, the maximum is {ClientInfo.MaxPasswordLength} characters"
            );
            return;
        }

        settings.Password = args[1];
        commandSender.SendMessage("Server password set, it applies to new connections");
    }
}
