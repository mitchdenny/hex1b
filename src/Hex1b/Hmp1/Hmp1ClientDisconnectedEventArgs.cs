namespace Hex1b;

/// <summary>
/// Arguments for the <see cref="Hmp1ServerOptions.OnClientDisconnected"/>
/// callback. Fires when a per-client session ends.
/// </summary>
public sealed class Hmp1ClientDisconnectedEventArgs : EventArgs
{
    internal Hmp1ClientDisconnectedEventArgs(string peerId, string? displayName)
    {
        PeerId = peerId;
        DisplayName = displayName;
    }

    /// <summary>The peer ID of the disconnecting client.</summary>
    public string PeerId { get; }

    /// <summary>The display name the client supplied in its ClientHello.</summary>
    public string? DisplayName { get; }
}
