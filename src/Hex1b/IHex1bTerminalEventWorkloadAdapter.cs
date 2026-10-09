using Hex1b.Input;

namespace Hex1b;

/// <summary>
/// Optional workload capability for receiving parsed input events instead of raw presentation bytes.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface to use <see cref="Hex1bTerminal"/>'s input decoding without
/// depending on <see cref="Hex1bApp"/>. The terminal delivers keyboard, mouse, and streaming
/// paste events through <see cref="WriteInputEventAsync"/>. Escape-sequence timeouts and
/// UTF-8 buffering apply to this event stream.
/// </para>
/// <para>
/// Workloads that implement only <see cref="IHex1bTerminalWorkloadAdapter"/> receive
/// original presentation bytes through <see cref="IHex1bTerminalWorkloadAdapter.WriteInputAsync"/>
/// and perform their own input decoding. Implementing this capability selects event delivery,
/// not both event and raw-byte delivery.
/// </para>
/// <para>
/// Explicit raw input sent through <see cref="Hex1bTerminal.SendInputAsync"/> still uses
/// <see cref="IHex1bTerminalWorkloadAdapter.WriteInputAsync"/>.
/// </para>
/// </remarks>
public interface IHex1bTerminalEventWorkloadAdapter : IHex1bTerminalWorkloadAdapter
{
    /// <summary>
    /// Writes a parsed input event to the workload, waiting for capacity if necessary.
    /// </summary>
    /// <param name="evt">The input event to deliver.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the event has been accepted.</returns>
    ValueTask WriteInputEventAsync(Hex1bEvent evt, CancellationToken ct = default);

    /// <summary>
    /// Attempts to write an input event without waiting, for synchronous input injection.
    /// </summary>
    /// <param name="evt">The input event to deliver.</param>
    /// <returns>True if the event was accepted; otherwise, false.</returns>
    bool TryWriteInputEvent(Hex1bEvent evt);
}
