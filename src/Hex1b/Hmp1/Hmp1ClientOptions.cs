namespace Hex1b;

/// <summary>
/// Configures an HMP v1 client workload built via the
/// <see cref="Hmp1BuilderExtensions"/> family of builder extensions.
/// </summary>
/// <remarks>
/// <para>
/// Carries the transport (<see cref="StreamFactory"/>, an optional
/// <see cref="StreamTransform"/>), the handshake hints sent in the
/// <see cref="Hmp1FrameType.ClientHello"/> frame, and single-delegate
/// event hooks invoked over the lifetime of the connection. Each hook
/// is optional; null hooks are skipped.
/// </para>
/// <para>
/// <see cref="StreamFactory"/> is <c>required init</c> so the canonical
/// <see cref="Hmp1BuilderExtensions.WithHmp1Client(Hex1bTerminalBuilder, Hmp1ClientOptions)"/>
/// path can construct the options bag with the transport pre-populated;
/// the convenience wrappers (<c>WithHmp1Stream</c>, <c>WithHmp1UdsClient</c>)
/// pre-populate it on your behalf and invoke an additional configure
/// callback, which cannot accidentally replace the transport.
/// </para>
/// <para>
/// The <see cref="OnConnected"/> callback receives an
/// <see cref="Hmp1ConnectedEventArgs"/> whose
/// <see cref="Hmp1ConnectedEventArgs.Connection"/> property is the
/// <see cref="IHmp1ConnectionHandle"/> for runtime calls
/// (e.g. <see cref="IHmp1ConnectionHandle.RequestPrimaryAsync"/>).
/// Stash the handle for later use; this is the only place it's
/// surfaced when the easy-path builder extensions are used.
/// </para>
/// </remarks>
public sealed class Hmp1ClientOptions
{
    /// <summary>
    /// Transport factory invoked when the workload starts. Returns a
    /// bidirectional stream connected to the producer. <c>required
    /// init</c> so callers cannot accidentally replace the transport
    /// from a configure callback.
    /// </summary>
    public required Func<CancellationToken, Task<Stream>> StreamFactory { get; init; }

    /// <summary>
    /// Optional async stream-wrap applied between the
    /// <see cref="StreamFactory"/> connect and the HMP v1 handshake.
    /// Use this to layer TLS, compression, or other framing on top of
    /// the raw transport without having to wire it into a custom
    /// <see cref="StreamFactory"/>.
    /// </summary>
    public Func<Stream, Task<Stream>>? StreamTransform { get; set; }

    /// <summary>
    /// Optional human-readable label sent in the
    /// <see cref="Hmp1FrameType.ClientHello"/> frame so other peers can
    /// identify this client in roster output. When null, an
    /// auto-generated 13-character base-36 identifier is sent.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Optional default-role hint sent in the
    /// <see cref="Hmp1FrameType.ClientHello"/> frame. The producer may
    /// honour or ignore the hint.
    /// </summary>
    public Hmp1Role? DefaultRole { get; set; }

    /// <summary>
    /// Invoked once after the HMP v1 handshake (ClientHello → Hello →
    /// StateSync) completes successfully. The argument carries the
    /// <see cref="IHmp1ConnectionHandle"/> for this client plus initial
    /// state (peer ID, current primary, peer roster, producer dims).
    /// </summary>
    /// <remarks>
    /// Awaited inline by <see cref="Hmp1WorkloadAdapter.ConnectAsync"/>
    /// before <c>ConnectAsync</c> returns. Multicast assignment via
    /// <c>+=</c> is supported — each handler is awaited independently
    /// with its own exception isolation. Handlers should not call
    /// <see cref="IAsyncDisposable.DisposeAsync"/> on the connection
    /// from inside the callback (the read pump is not yet running on
    /// this path; for the per-event callbacks below, an
    /// <see cref="AsyncLocal{T}"/> guard makes self-disposal safe).
    /// </remarks>
    public Func<Hmp1ConnectedEventArgs, CancellationToken, ValueTask>? OnConnected { get; set; }

    /// <summary>
    /// Invoked when this client's role transitions between primary and
    /// secondary.
    /// </summary>
    /// <remarks>
    /// Awaited inline by the read pump. A slow handler back-pressures
    /// frame processing — offload long work yourself if you need
    /// non-blocking observation. Multicast (<c>+=</c>) is supported.
    /// </remarks>
    public Func<RoleChangedEventArgs, CancellationToken, ValueTask>? OnRoleChanged { get; set; }

    /// <summary>
    /// Invoked when another peer joins the same producer.
    /// </summary>
    /// <remarks>
    /// Awaited inline by the read pump. See <see cref="OnRoleChanged"/>
    /// for back-pressure and multicast notes.
    /// </remarks>
    public Func<PeerJoinEventArgs, CancellationToken, ValueTask>? OnPeerJoined { get; set; }

    /// <summary>
    /// Invoked when another peer leaves the same producer.
    /// </summary>
    /// <remarks>
    /// Awaited inline by the read pump. See <see cref="OnRoleChanged"/>
    /// for back-pressure and multicast notes.
    /// </remarks>
    public Func<PeerLeaveEventArgs, CancellationToken, ValueTask>? OnPeerLeft { get; set; }

    /// <summary>
    /// Invoked when the producer's PTY dimensions change at runtime —
    /// either because we (as primary) requested a new size, or because
    /// another peer became primary and broadcast different dims.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Does NOT fire for the initial dims learned in the handshake;
    /// those are surfaced via <see cref="OnConnected"/> instead.
    /// </para>
    /// <para>
    /// Awaited inline by the read pump. See <see cref="OnRoleChanged"/>
    /// for back-pressure and multicast notes.
    /// </para>
    /// </remarks>
    public Func<RemoteResizedEventArgs, CancellationToken, ValueTask>? OnRemoteResized { get; set; }

    /// <summary>
    /// Invoked once when the underlying transport stream closes,
    /// regardless of cause (server shutdown, network error, local cancel).
    /// </summary>
    /// <remarks>
    /// Receives <see cref="CancellationToken.None"/> — by the time this
    /// callback runs the adapter's lifetime token has already been
    /// cancelled, and passing it would cause naive handlers to
    /// short-circuit the very cleanup work the disconnect callback
    /// exists to perform. Awaited inline; multicast supported.
    /// </remarks>
    public Func<CancellationToken, ValueTask>? OnDisconnected { get; set; }
}
