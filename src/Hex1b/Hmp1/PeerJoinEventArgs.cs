using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;

namespace Hex1b;

/// <summary>
/// Arguments for the <see cref="IHmp1ConnectionHandle.OnPeerJoined"/>
/// callback.
/// </summary>
public sealed class PeerJoinEventArgs : EventArgs
{
    internal PeerJoinEventArgs(string peerId, string? displayName)
    {
        PeerId = peerId;
        DisplayName = displayName;
    }

    /// <summary>Peer ID of the joining peer.</summary>
    public string PeerId { get; }

    /// <summary>Optional human-readable label of the joining peer.</summary>
    public string? DisplayName { get; }
}
