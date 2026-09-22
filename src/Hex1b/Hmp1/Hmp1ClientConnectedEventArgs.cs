namespace Hex1b;

/// <summary>
/// Arguments for the <see cref="Hmp1ServerOptions.OnClientConnected"/>
/// callback. Fires after a new HMP v1 client completes its
/// ClientHello → Hello → StateSync handshake.
/// </summary>
public sealed class Hmp1ClientConnectedEventArgs : EventArgs
{
    internal Hmp1ClientConnectedEventArgs(string peerId, string? displayName, Hmp1Role? defaultRole)
    {
        PeerId = peerId;
        DisplayName = displayName;
        DefaultRole = defaultRole;
    }

    /// <summary>The peer ID the producer assigned to this client.</summary>
    public string PeerId { get; }

    /// <summary>The display name the client supplied in its ClientHello.</summary>
    public string? DisplayName { get; }

    /// <summary>The role hint the client supplied, if any.</summary>
    public Hmp1Role? DefaultRole { get; }
}
