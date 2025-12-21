namespace Hex1b.Terminal;

/// <summary>
/// Presentation-side interface: Where rendered output goes TO and user input comes FROM.
/// This is the "user side" of the virtual terminal - the actual display device.
/// </summary>
/// <remarks>
/// <para>
/// This interface mirrors <see cref="IHex1bTerminalWorkloadAdapter"/> but for the opposite side
/// of the terminal. While the workload adapter connects to the application generating output,
/// the presentation adapter connects to the device displaying that output.
/// </para>
/// <para>
/// Implementations include:
/// <list type="bullet">
///   <item><description><c>ConsolePresentationAdapter</c> - Real console I/O</description></item>
///   <item><description><c>WebSocketPresentationAdapter</c> - Browser-based terminal</description></item>
///   <item><description><c>LegacyConsolePresentationAdapter</c> - Wraps existing ConsoleHex1bTerminal</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IHex1bTerminalPresentationAdapter : IAsyncDisposable
{
    /// <summary>
    /// Write rendered output TO the presentation layer (display).
    /// </summary>
    /// <remarks>
    /// The format depends on capabilities - could be raw ANSI sequences,
    /// delta protocol updates, or other optimized formats.
    /// </remarks>
    /// <param name="data">The output data to send to the display.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
    
    /// <summary>
    /// Receive input (keystrokes, mouse events as ANSI sequences) FROM the user.
    /// </summary>
    /// <remarks>
    /// Returns raw bytes that need to be parsed into terminal events.
    /// Returns empty memory when the connection ends.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Raw input bytes from the user, or empty when disconnected.</returns>
    ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Current terminal width in columns.
    /// </summary>
    int Width { get; }
    
    /// <summary>
    /// Current terminal height in rows.
    /// </summary>
    int Height { get; }
    
    /// <summary>
    /// Capability hints that inform optimization strategies.
    /// </summary>
    /// <remarks>
    /// The terminal core uses these to decide how to format output
    /// (e.g., whether to use delta protocol, sixel graphics, etc.).
    /// </remarks>
    TerminalCapabilities Capabilities { get; }
    
    /// <summary>
    /// Raised when the presentation layer is resized by the user.
    /// </summary>
    /// <remarks>
    /// Parameters are (newWidth, newHeight).
    /// </remarks>
    event Action<int, int>? Resized;
    
    /// <summary>
    /// Raised when the presentation layer disconnects (e.g., terminal closed, WebSocket dropped).
    /// </summary>
    event Action? Disconnected;
    
    /// <summary>
    /// Flush any buffered output immediately.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    ValueTask FlushAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Enter TUI mode (alternate screen, raw input, hide cursor, etc.).
    /// </summary>
    /// <remarks>
    /// This sets up the terminal for full-screen TUI operation.
    /// Call <see cref="ExitTuiModeAsync"/> to restore normal operation.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    ValueTask EnterTuiModeAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Exit TUI mode and restore normal terminal operation.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    ValueTask ExitTuiModeAsync(CancellationToken ct = default);
}
