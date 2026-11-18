using System;
using Hkmp.Api.Command.Client;
using Hkmp.Game.Client;
using Hkmp.Networking.Packet.Data;
using Hkmp.Ui;

namespace Hkmp.Game.Command.Client;

/// <summary>
/// Command for sending Geo or dream essence to a teammate. The target is the closest teammate in the current
/// scene unless a name is given.
/// </summary>
internal class GiveCommand : IClientCommand {
    /// <inheritdoc />
    public string Trigger => "/give";

    /// <inheritdoc />
    public string[] Aliases => ["/pay", "/givegeo", "/giveessence", "/essence"];

    /// <summary>
    /// The transfer manager that performs the transfer.
    /// </summary>
    private readonly TransferManager _transferManager;

    public GiveCommand(TransferManager transferManager) {
        _transferManager = transferManager;
    }

    /// <inheritdoc />
    public void Execute(string[] arguments) {
        if (arguments.Length < 2) {
            UiManager.InternalChatBox.AddMessage(Localization.Get("Usage: /give <amount> [name]"));
            UiManager.InternalChatBox.AddMessage(Localization.Get("Usage: /giveessence <amount> [name]"));
            return;
        }

        if (!int.TryParse(arguments[1], out var amount)) {
            UiManager.InternalChatBox.AddMessage(Localization.Get("The amount must be a whole number"));
            return;
        }

        var isEssence = arguments[0] == "/giveessence" || arguments[0] == "/essence";
        var targetName = arguments.Length > 2
            ? string.Join(" ", arguments, 2, arguments.Length - 2)
            : null;

        _transferManager.TryTransfer(
            isEssence ? PlayerTransferType.Essence : PlayerTransferType.Geo,
            amount,
            targetName
        );
    }
}
