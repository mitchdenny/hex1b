namespace Hex1b;

/// <summary>
/// Terminal-side interface: What Hex1bTerminal needs from any workload.
/// Raw byte streams for maximum flexibility.
/// </summary>
/// <remarks>
/// <para>
/// This interface represents the "workload side" of the terminal - the process
/// or application connected to the terminal. It deals with raw bytes only.
/// </para>
/// <para>
/// Data flow:
/// <list type="bullet">
///   <item><see cref="ReadOutputAsync"/> - Terminal reads output FROM the workload (ANSI to display)</item>
///   <item><see cref="WriteInputAsync"/> - Terminal writes input TO the workload (keystrokes, mouse)</item>
/// </list>
/// </para>
/// <para>
/// Implementations:
/// <list type="bullet">
///   <item><see cref="Hex1bAppWorkloadAdapter"/> - For Hex1bApp TUI applications</item>
///   <item><see cref="StreamWorkloadAdapter"/> - For testing with raw streams</item>
///   <item>ProcessWorkloadAdapter (future) - For PTY-connected processes</item>
/// </list>
/// </para>
/// </remarks>
public interface IHex1bTerminalWorkloadAdapter : IAsyncDisposable
{
    /// <summary>
    /// Read output FROM the workload (ANSI sequences to display).
    /// The terminal calls this to get data to parse and send to presentation.
    /// Returns empty when workload has no more output (should be called in a loop).
    /// </summary>
    ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Write input TO the workload (raw bytes from keyboard/mouse).
    /// The terminal calls this when it receives input from the presentation layer.
    /// </summary>
    ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
    
    /// <summary>
    /// Notify workload of terminal resize.
    /// </summary>
    ValueTask ResizeAsync(int width, int height, CancellationToken ct = default);
    
    /// <summary>
    /// Raised when workload has disconnected/exited.
    /// </summary>
    event Action? Disconnected;
}
