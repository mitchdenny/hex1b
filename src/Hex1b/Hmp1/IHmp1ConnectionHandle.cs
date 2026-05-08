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
    /// <see cref="OnRemoteResized"/>.
    /// </summary>
    int RemoteWidth { get; }

    /// <summary>
    /// The producer's PTY height as last observed by this client.
    /// </summary>
    int RemoteHeight { get; }

    /// <summary>
    /// Snapshot of currently-connected peers (excluding self) as known
    /// to this client. Updated by PeerJoin / PeerLeave frames; set
    /// <see cref="OnPeerJoined"/> / <see cref="OnPeerLeft"/> for
    /// notifications.
    /// </summary>
    IReadOnlyList<PeerInfo> Peers { get; }

    /// <summary>
    /// Invoked once after the HMP v1 handshake (ClientHello → Hello →
    /// StateSync) completes successfully. Pre-populated from
    /// <see cref="Hmp1ClientOptions.OnConnected"/> at adapter
    /// construction; may be replaced or composed with <c>+=</c> at
    /// runtime (multicast handlers are awaited sequentially with
    /// per-handler exception isolation).
    /// </summary>
    Func<Hmp1ConnectedEventArgs, CancellationToken, Task>? OnConnected { get; set; }

    /// <summary>
    /// Invoked when the primary peer changes.
    /// </summary>
    /// <remarks>
    /// Awaited inline by the read pump. Slow handlers back-pressure
    /// frame processing — offload long work yourself if required.
    /// Multicast (<c>+=</c>) is supported and each handler is awaited
    /// independently. Self-disposal
    /// (<c>await connection.DisposeAsync()</c>) from inside the handler
    /// is guarded against deadlock.
    /// </remarks>
    Func<RoleChangedEventArgs, CancellationToken, Task>? OnRoleChanged { get; set; }

    /// <summary>
    /// Invoked when the producer's PTY dimensions change at runtime.
    /// </summary>
    /// <remarks>
    /// See <see cref="OnRoleChanged"/> for back-pressure, multicast and
    /// self-disposal notes.
    /// </remarks>
    Func<RemoteResizedEventArgs, CancellationToken, Task>? OnRemoteResized { get; set; }

    /// <summary>
    /// Invoked when another peer joins the same producer.
    /// </summary>
    /// <remarks>
    /// See <see cref="OnRoleChanged"/> for back-pressure, multicast and
    /// self-disposal notes.
    /// </remarks>
    Func<PeerJoinEventArgs, CancellationToken, Task>? OnPeerJoined { get; set; }

    /// <summary>
    /// Invoked when another peer leaves.
    /// </summary>
    /// <remarks>
    /// See <see cref="OnRoleChanged"/> for back-pressure, multicast and
    /// self-disposal notes.
    /// </remarks>
    Func<PeerLeaveEventArgs, CancellationToken, Task>? OnPeerLeft { get; set; }

    /// <summary>
    /// Invoked once when the underlying transport stream closes.
    /// </summary>
    /// <remarks>
    /// Receives <see cref="CancellationToken.None"/> — by the time the
    /// callback runs the adapter's lifetime token has been cancelled,
    /// and passing it would short-circuit cleanup work in naive
    /// handlers. Multicast supported.
    /// </remarks>
    Func<CancellationToken, Task>? OnDisconnected { get; set; }

    /// <summary>
    /// Asks the producer to make this peer the primary at the supplied
    /// dimensions. Observe <see cref="OnRoleChanged"/> for the
    /// acknowledged transition.
    /// </summary>
    Task RequestPrimaryAsync(int cols, int rows, CancellationToken ct = default);
}
