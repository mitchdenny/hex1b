namespace Hex1b;

/// <summary>
/// Optional capability marker for <see cref="IHex1bTerminalWorkloadAdapter"/> implementations
/// whose output cannot start until an asynchronous "connected" milestone is reached
/// (e.g. an HMP1 handshake completes, a WebSocket opens, a remote stream is negotiated).
/// </summary>
/// <remarks>
/// <para>
/// Used by <see cref="PlaceholderWorkloadAdapter"/> and the
/// <c>WithPlaceholderWorkload</c> family of builder extensions to determine
/// when to swap from a placeholder workload to the real primary.
/// </para>
/// <para>
/// A workload that is ready to produce output as soon as it is constructed
/// (a child process, a Hex1bApp, an Asciinema file) does <em>not</em> need
/// to implement this interface — the placeholder adapter will treat such
/// workloads as connected on the first non-empty <see cref="IHex1bTerminalWorkloadAdapter.ReadOutputAsync"/>
/// return.
/// </para>
/// </remarks>
public interface IConnectableWorkloadAdapter
{
    /// <summary>
    /// Completes when the workload's connect / handshake step has finished and it
    /// is ready to emit output. Never faults — connect failures should still leave
    /// this task pending so callers can rely on
    /// <see cref="DisconnectedTask"/> for the disconnect path.
    /// </summary>
    Task ConnectedTask { get; }

    /// <summary>
    /// Completes when the workload has disconnected (transport closed,
    /// remote hung up, or local disposal). Never faults.
    /// </summary>
    Task DisconnectedTask { get; }

    /// <summary>
    /// True after <see cref="ConnectedTask"/> has completed and the
    /// transport has not yet disconnected.
    /// </summary>
    bool IsConnected { get; }
}
