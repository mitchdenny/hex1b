namespace Hex1b;

/// <summary>
/// User-facing handle to a connected HMP v1 client. Delivered to user
/// code via <see cref="Hmp1ClientOptions.OnConnected"/> when the client
/// is built through the <see cref="Hmp1BuilderExtensions"/> family.
/// </summary>
/// <remarks>
/// <para>
/// This is the recommended surface for runtime queries (peer roster,
/// remote dimensions, primary state) and runtime actions
/// (<see cref="RequestPrimaryAsync"/>) on an HMP1 client. It hides the
/// underlying workload-adapter type so consumers don't need to traffic
/// in <see cref="IHex1bTerminalWorkloadAdapter"/> contracts.
/// </para>
/// <para>
/// <see cref="Hmp1WorkloadAdapter"/> implements this interface; advanced
/// consumers that construct the adapter directly (because they need to
/// drive <see cref="Hmp1WorkloadAdapter.ConnectAsync"/> before assembling
/// the surrounding terminal) get the same surface for free by referring
/// to the adapter through this interface.
/// </para>
/// </remarks>
public interface IHmp1ConnectionHandle
{
    /// <summary>
    /// The display name this client sent in its
    /// <see cref="Hmp1FrameType.ClientHello"/> frame. If the caller did
    /// not supply one, this returns the auto-generated value.
    /// </summary>
    string LocalDisplayName { get; }

    /// <summary>
    /// The role hint this client sent in its ClientHello, or
    /// <see langword="null"/> if no hint was supplied.
    /// </summary>
    Hmp1Role? DefaultRole { get; }

    /// <summary>
    /// The peer ID the producer assigned to this client in its Hello.
    /// </summary>
    string PeerId { get; }

    /// <summary>
    /// Whether this client is the current primary peer.
    /// </summary>
    bool IsPrimary { get; }

    /// <summary>
    /// The producer's PTY width as last observed by this client.
    /// Updated by Resize and RoleChange frames; surfaced via
    /// <see cref="RemoteResized"/>.
    /// </summary>
    int RemoteWidth { get; }

    /// <summary>
    /// The producer's PTY height as last observed by this client.
    /// </summary>
    int RemoteHeight { get; }

    /// <summary>
    /// Snapshot of currently-connected peers (excluding self) as known
    /// to this client. Updated by PeerJoin / PeerLeave frames; subscribe
    /// to <see cref="PeerJoined"/> / <see cref="PeerLeft"/> for
    /// notifications.
    /// </summary>
    IReadOnlyList<PeerInfo> Peers { get; }

    /// <summary>
    /// Raised once after the HMP v1 handshake (ClientHello → Hello →
    /// StateSync) completes successfully.
    /// </summary>
    event EventHandler<Hmp1ConnectedEventArgs>? Connected;

    /// <summary>
    /// Raised when the primary peer changes.
    /// </summary>
    event EventHandler<RoleChangedEventArgs>? RoleChanged;

    /// <summary>
    /// Raised when the producer's PTY dimensions change at runtime.
    /// </summary>
    event EventHandler<RemoteResizedEventArgs>? RemoteResized;

    /// <summary>
    /// Raised when another peer joins the same producer.
    /// </summary>
    event EventHandler<PeerJoinEventArgs>? PeerJoined;

    /// <summary>
    /// Raised when another peer leaves.
    /// </summary>
    event EventHandler<PeerLeaveEventArgs>? PeerLeft;

    /// <summary>
    /// Raised once when the underlying transport stream closes.
    /// </summary>
    event Action? Disconnected;

    /// <summary>
    /// Asks the producer to make this peer the primary at the supplied
    /// dimensions. Observe <see cref="RoleChanged"/> for the
    /// acknowledged transition.
    /// </summary>
    Task RequestPrimaryAsync(int cols, int rows, CancellationToken ct = default);
}
