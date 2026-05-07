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
    public Action<Hmp1ConnectedEventArgs>? OnConnected { get; set; }

    /// <summary>
    /// Invoked when this client's role transitions between primary and
    /// secondary.
    /// </summary>
    public Action<RoleChangedEventArgs>? OnRoleChanged { get; set; }

    /// <summary>
    /// Invoked when another peer joins the same producer.
    /// </summary>
    public Action<PeerJoinEventArgs>? OnPeerJoined { get; set; }

    /// <summary>
    /// Invoked when another peer leaves the same producer.
    /// </summary>
    public Action<PeerLeaveEventArgs>? OnPeerLeft { get; set; }

    /// <summary>
    /// Invoked when the producer's PTY dimensions change at runtime —
    /// either because we (as primary) requested a new size, or because
    /// another peer became primary and broadcast different dims.
    /// </summary>
    /// <remarks>
    /// Does NOT fire for the initial dims learned in the handshake;
    /// those are surfaced via <see cref="OnConnected"/> instead.
    /// </remarks>
    public Action<RemoteResizedEventArgs>? OnRemoteResized { get; set; }

    /// <summary>
    /// Invoked once when the underlying transport stream closes,
    /// regardless of cause (server shutdown, network error, local cancel).
    /// </summary>
    public Action? OnDisconnected { get; set; }
}
