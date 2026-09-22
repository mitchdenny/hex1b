using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;

namespace Hex1b;

/// <summary>
/// Arguments for the <see cref="IHmp1ConnectionHandle.OnPeerLeft"/>
/// callback.
/// </summary>
public sealed class PeerLeaveEventArgs : EventArgs
{
    internal PeerLeaveEventArgs(string peerId)
    {
        PeerId = peerId;
    }

    /// <summary>Peer ID of the leaving peer.</summary>
    public string PeerId { get; }
}
