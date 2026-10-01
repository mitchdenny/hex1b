using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;

namespace Hex1b;

/// <summary>
/// Arguments for the <see cref="IHmp1ConnectionHandle.OnConnected"/> callback.
/// Carries the state assembled from the Hello and StateSync handshake frames.
/// </summary>
public sealed class Hmp1ConnectedEventArgs : EventArgs
{
    internal Hmp1ConnectedEventArgs(IHmp1ConnectionHandle connection, string peerId, string? primaryPeerId, IReadOnlyList<PeerInfo> peers, int width, int height)
    {
        Connection = connection;
        PeerId = peerId;
        PrimaryPeerId = primaryPeerId;
        Peers = peers;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// The connection handle for this client. Stash this reference for
    /// later runtime calls (e.g. <see cref="IHmp1ConnectionHandle.RequestPrimaryAsync"/>);
    /// no other public surface delivers it when the easy-path
    /// <c>WithHmp1*</c> builder extensions are used.
    /// </summary>
    public IHmp1ConnectionHandle Connection { get; }

    /// <summary>Peer ID assigned by the server.</summary>
    public string PeerId { get; }

    /// <summary>Peer ID of the current primary, or <see langword="null"/> when no peer is primary.</summary>
    public string? PrimaryPeerId { get; }

    /// <summary>Snapshot of the peer roster at handshake time (excluding self).</summary>
    public IReadOnlyList<PeerInfo> Peers { get; }

    /// <summary>Producer PTY width reported in the Hello frame.</summary>
    public int Width { get; }

    /// <summary>Producer PTY height reported in the Hello frame.</summary>
    public int Height { get; }
}
