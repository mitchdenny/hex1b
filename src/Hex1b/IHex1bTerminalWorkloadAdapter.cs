using Hex1b.Input;

namespace Hex1b;

/// <summary>
/// Terminal-side interface: What Hex1bTerminal needs from any workload.
/// Provides raw output with a choice of raw or parsed input.
/// </summary>
/// <remarks>
/// <para>
/// This interface represents the "workload side" of the terminal - the process
/// or application connected to the terminal.
/// </para>
/// <para>
/// Input defaults to original raw bytes. Return <see cref="Hex1bTerminalInputMode.ParsedEvents"/>
/// from <see cref="InputMode"/> and implement event delivery
/// to use the terminal's input decoding without depending on <see cref="Hex1bApp"/>.
/// </para>
/// <para>
/// Data flow:
/// <list type="bullet">
///   <item><see cref="ReadOutputAsync"/> - Terminal reads output FROM the workload (ANSI to display)</item>
///   <item><see cref="WriteInputAsync"/> or <see cref="WriteInputEventAsync"/> - Terminal writes input TO the workload</item>
/// </list>
/// </para>
/// <para>
/// Implementations:
/// <list type="bullet">
///   <item><see cref="Hex1bAppWorkloadAdapter"/> - For Hex1bApp TUI applications</item>
///   <item><see cref="StreamWorkloadAdapter"/> - For testing with raw streams</item>
///   <item><see cref="RemoteTerminalWorkloadAdapter"/> - For connecting to a remote terminal over WebSocket</item>
///   <item>ProcessWorkloadAdapter (future) - For PTY-connected processes</item>
/// </list>
/// </para>
/// </remarks>
public interface IHex1bTerminalWorkloadAdapter : IAsyncDisposable
{
    /// <summary>
    /// Gets the input representation requested by this workload.
    /// Defaults to <see cref="Hex1bTerminalInputMode.RawBytes"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The terminal captures this value at construction. Parsed-event workloads must
    /// implement <see cref="WriteInputEventAsync"/> and <see cref="TryWriteInputEvent"/>.
    /// Selecting parsed input does not also deliver the original presentation bytes.
    /// </para>
    /// <para>
    /// Explicit raw input sent through <see cref="Hex1bTerminal.SendInputAsync"/> and
    /// terminal protocol responses still use <see cref="WriteInputAsync"/>.
    /// </para>
    /// </remarks>
    Hex1bTerminalInputMode InputMode => Hex1bTerminalInputMode.RawBytes;

    /// <summary>
    /// Gets whether the workload's upstream terminal owns protocol query responses.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="false"/> for locally hosted workloads. Remote
    /// terminal adapters return <see langword="true"/> to prevent the local
    /// <see cref="Hex1bTerminal"/> from generating duplicate protocol responses.
    /// This does not suppress keyboard, mouse, or other user input.
    /// </remarks>
    bool HandlesProtocolQueries => false;

    /// <summary>
    /// Read output FROM the workload (ANSI sequences to display).
    /// The terminal calls this to get data to parse and send to presentation.
    /// Returns empty when workload has no more output (should be called in a loop).
    /// </summary>
    ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Write input TO the workload (raw bytes from keyboard/mouse).
    /// The terminal calls this for presentation input when <see cref="InputMode"/> is
    /// <see cref="Hex1bTerminalInputMode.RawBytes"/>. Explicit raw input still uses this method.
    /// </summary>
    ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);

    /// <summary>
    /// Writes a parsed input event to the workload, waiting for capacity if necessary.
    /// </summary>
    /// <param name="evt">The input event to deliver.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the event has been accepted.</returns>
    /// <exception cref="NotSupportedException">The workload has not implemented parsed-event delivery.</exception>
    ValueTask WriteInputEventAsync(Hex1bEvent evt, CancellationToken ct = default)
        => throw new NotSupportedException("Implement WriteInputEventAsync when InputMode is ParsedEvents.");

    /// <summary>
    /// Attempts to write an input event without waiting, for synchronous input injection.
    /// </summary>
    /// <param name="evt">The input event to deliver.</param>
    /// <returns>True if the event was accepted; otherwise, false.</returns>
    /// <exception cref="NotSupportedException">The workload has not implemented parsed-event delivery.</exception>
    bool TryWriteInputEvent(Hex1bEvent evt)
        => throw new NotSupportedException("Implement TryWriteInputEvent when InputMode is ParsedEvents.");
    
    /// <summary>
    /// Notify workload of terminal resize.
    /// </summary>
    ValueTask ResizeAsync(int width, int height, CancellationToken ct = default);
    
    /// <summary>
    /// Raised when workload has disconnected/exited.
    /// </summary>
    event Action? Disconnected;
}
