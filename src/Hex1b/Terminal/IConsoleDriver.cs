namespace Hex1b.Terminal;

/// <summary>
/// Platform-specific console driver interface for raw terminal I/O.
/// </summary>
/// <remarks>
/// Implementations handle the platform-specific details of:
/// - Entering/exiting raw mode (disabling line buffering, echo, etc.)
/// - Reading raw bytes from stdin
/// - Writing raw bytes to stdout
/// - Detecting terminal size and resize events
/// </remarks>
internal interface IConsoleDriver : IDisposable
{
    /// <summary>
    /// Enter raw mode - disable line buffering, echo, signal handling.
    /// </summary>
    void EnterRawMode();
    
    /// <summary>
    /// Exit raw mode - restore original terminal settings.
    /// </summary>
    void ExitRawMode();
    
    /// <summary>
    /// Check if data is available to read without blocking.
    /// </summary>
    bool DataAvailable { get; }
    
    /// <summary>
    /// Read raw bytes from stdin.
    /// </summary>
    /// <param name="buffer">Buffer to read into.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of bytes read, or 0 if EOF/cancelled.</returns>
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default);
    
    /// <summary>
    /// Write raw bytes to stdout.
    /// </summary>
    /// <param name="data">Data to write.</param>
    void Write(ReadOnlySpan<byte> data);
    
    /// <summary>
    /// Flush stdout.
    /// </summary>
    void Flush();
    
    /// <summary>
    /// Drain any pending input from the buffer without processing it.
    /// Used during shutdown to prevent leftover escape sequences from appearing.
    /// </summary>
    void DrainInput();
    
    /// <summary>
    /// Current terminal width in columns.
    /// </summary>
    int Width { get; }
    
    /// <summary>
    /// Current terminal height in rows.
    /// </summary>
    int Height { get; }
    
    /// <summary>
    /// Raised when terminal is resized.
    /// </summary>
    event Action<int, int>? Resized;
}
