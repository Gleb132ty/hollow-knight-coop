namespace Hkmp.Networking.Packet.Data;

/// <summary>
/// Packet data for generic data at least containing a client ID, that should be sent reliably and is
/// never dropped in favour of newer data.
/// </summary>
internal class ReliableGenericClientData : GenericClientData {
    public ReliableGenericClientData() {
        IsReliable = true;

        // Downed and revive events are state changes that must always be delivered. Dropping an older
        // event in favour of a newer one would make a client miss the transition entirely.
        DropReliableDataIfNewerExists = false;
    }
}
