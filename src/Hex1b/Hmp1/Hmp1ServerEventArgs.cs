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

/// <summary>
/// Arguments for the <see cref="Hmp1ServerOptions.OnResized"/> callback.
/// Fires when the producer's PTY dimensions change.
/// </summary>
public sealed class Hmp1ServerResizedEventArgs : EventArgs
{
    internal Hmp1ServerResizedEventArgs(int width, int height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>The new PTY width.</summary>
    public int Width { get; }

    /// <summary>The new PTY height.</summary>
    public int Height { get; }
}

/// <summary>
/// Arguments for the <see cref="Hmp1ServerOptions.OnPrimaryChanged"/>
/// callback. Fires when the primary peer changes (including transitions
/// to no-primary).
/// </summary>
public sealed class Hmp1ServerPrimaryChangedEventArgs : EventArgs
{
    internal Hmp1ServerPrimaryChangedEventArgs(string? primaryPeerId)
    {
        PrimaryPeerId = primaryPeerId;
    }

    /// <summary>The new primary peer ID, or null if no peer is primary.</summary>
    public string? PrimaryPeerId { get; }
}
