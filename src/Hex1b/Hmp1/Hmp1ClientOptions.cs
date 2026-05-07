namespace Hex1b;

/// <summary>
/// Configures an HMP v1 client workload built via the
/// <see cref="Hmp1BuilderExtensions.WithHmp1Client(Hex1bTerminalBuilder, System.Func{System.Threading.CancellationToken, System.Threading.Tasks.Task{System.IO.Stream}}, System.Action{Hmp1ClientOptions}?)"/>
/// family of builder extensions.
/// </summary>
/// <remarks>
/// <para>
/// Holds the handshake hints sent in the <see cref="Hmp1FrameType.ClientHello"/>
/// frame plus single-delegate event hooks invoked over the lifetime of the
/// connection. Each hook is optional; null hooks are skipped.
/// </para>
/// <para>
/// Hooks are wired up onto the underlying <see cref="Hmp1WorkloadAdapter"/>
/// before the workload is started, so callers see the very first events emitted
/// by <see cref="Hmp1WorkloadAdapter.ConnectAsync"/> (notably <see cref="OnConnected"/>).
/// </para>
/// <para>
/// For consumers that need a long-lived adapter reference for runtime queries
/// (e.g. calling <see cref="Hmp1WorkloadAdapter.RequestPrimaryAsync"/> from a
/// render loop), construct the adapter directly via
/// <see cref="Hmp1WorkloadAdapter(System.Func{System.Threading.CancellationToken, System.Threading.Tasks.Task{System.IO.Stream}}, string?, string?)"/>
/// and attach it through <see cref="Hex1bTerminalBuilder.WithWorkload"/>.
/// </para>
/// </remarks>
public sealed class Hmp1ClientOptions
{
    /// <summary>
    /// Optional human-readable label sent in the
    /// <see cref="Hmp1FrameType.ClientHello"/> frame so other peers can
    /// identify this client in roster output.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Optional default-role hint sent in the
    /// <see cref="Hmp1FrameType.ClientHello"/> frame (typically
    /// <c>"viewer"</c> or <c>"interactive"</c>). The server may honour
    /// or ignore the hint.
    /// </summary>
    public string? DefaultRole { get; set; }

    /// <summary>
    /// Invoked once after the HMP v1 handshake (ClientHello → Hello →
    /// StateSync) completes successfully. The argument carries the
    /// server-assigned peer ID, current primary, peer roster, and
    /// producer dims.
    /// </summary>
    public System.Action<Hmp1ConnectedEventArgs>? OnConnected { get; set; }

    /// <summary>
    /// Invoked when this client's role transitions between primary and
    /// secondary. Use this to drive UX state, seed dim trackers after
    /// a successful <see cref="Hmp1WorkloadAdapter.RequestPrimaryAsync"/>,
    /// or to hand off rendering responsibility.
    /// </summary>
    public System.Action<RoleChangedEventArgs>? OnRoleChanged { get; set; }

    /// <summary>
    /// Invoked when another peer joins the same producer.
    /// </summary>
    public System.Action<PeerJoinEventArgs>? OnPeerJoined { get; set; }

    /// <summary>
    /// Invoked when another peer leaves the same producer.
    /// </summary>
    public System.Action<PeerLeaveEventArgs>? OnPeerLeft { get; set; }

    /// <summary>
    /// Invoked when the producer's PTY dimensions change at runtime —
    /// either because we (as primary) requested a new size, or because
    /// another peer became primary and broadcast different dims.
    /// </summary>
    /// <remarks>
    /// Does NOT fire for the initial dims learned in the handshake; those
    /// are surfaced via <see cref="OnConnected"/> instead.
    /// </remarks>
    public System.Action<RemoteResizedEventArgs>? OnRemoteResized { get; set; }

    /// <summary>
    /// Invoked once when the underlying transport stream closes,
    /// regardless of cause (server shutdown, network error, local cancel).
    /// </summary>
    public System.Action? OnDisconnected { get; set; }
}
