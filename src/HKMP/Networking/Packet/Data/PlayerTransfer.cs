namespace Hkmp.Networking.Packet.Data;

/// <summary>
/// The kind of resource that can be transferred to a teammate.
/// </summary>
internal enum PlayerTransferType : byte {
    /// <summary>
    /// Geo, the regular currency of the game.
    /// </summary>
    Geo = 0,

    /// <summary>
    /// Dream essence, the currency of the Dream Nail.
    /// </summary>
    Essence = 1
}

/// <summary>
/// Packet data for a transfer of Geo or dream essence between two players. When sent from a client to the server,
/// the ID is the player that should receive the resources. When sent from the server to a client, the ID is the
/// player that sent the resources.
/// </summary>
internal class PlayerTransfer : ReliableGenericClientData {
    /// <summary>
    /// The kind of resource that is transferred.
    /// </summary>
    public PlayerTransferType TransferType { get; set; }

    /// <summary>
    /// The amount of resources that is transferred.
    /// </summary>
    public int Amount { get; set; }

    /// <inheritdoc />
    public override void WriteData(IPacket packet) {
        base.WriteData(packet);

        packet.Write((byte) TransferType);
        packet.Write(Amount);
    }

    /// <inheritdoc />
    public override void ReadData(IPacket packet) {
        base.ReadData(packet);

        TransferType = (PlayerTransferType) packet.ReadByte();
        Amount = packet.ReadInt();
    }
}

/// <summary>
/// Packet data for the result of a transfer request, sent back to the player that requested the transfer so that
/// the resources can be refunded if the server rejected the request.
/// </summary>
internal class PlayerTransferResult : PlayerTransfer {
    /// <summary>
    /// Whether the transfer was accepted by the server.
    /// </summary>
    public bool Success { get; set; }

    /// <inheritdoc />
    public override void WriteData(IPacket packet) {
        base.WriteData(packet);

        packet.Write(Success);
    }

    /// <inheritdoc />
    public override void ReadData(IPacket packet) {
        base.ReadData(packet);

        Success = packet.ReadBool();
    }
}
