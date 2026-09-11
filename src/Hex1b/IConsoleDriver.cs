using System.Text;

namespace Hex1b;

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
    /// <param name="preserveOPost">If true, preserve output post-processing (LF→CRLF). Useful for WithProcess scenarios.</param>
    void EnterRawMode(bool preserveOPost = false);
    
    /// <summary>
    /// Exit raw mode - restore original terminal settings.
    /// </summary>
    void ExitRawMode();
    
    /// <summary>
    /// Encoding used by bytes returned from <see cref="ReadAsync"/>.
    /// </summary>
    Encoding InputEncoding { get; }

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

    /// <summary>
    /// Attempts to read the terminal window's pixel size directly from the
    /// operating system (for example, via <c>TIOCGWINSZ</c> on Unix), bypassing
    /// any escape-sequence query/response round trip.
    /// </summary>
    /// <param name="pixelWidth">The window width in pixels, when the call succeeds.</param>
    /// <param name="pixelHeight">The window height in pixels, when the call succeeds.</param>
    /// <returns>
    /// <see langword="true"/> when the platform exposes this information and both
    /// values are nonzero; <see langword="false"/> otherwise (including on
    /// platforms, such as Windows, with no equivalent OS-level facility). A
    /// <see langword="false"/> result never distinguishes "not supported on this
    /// platform" from "supported but currently zero" — callers should treat both
    /// the same way: fall through to the next discovery source.
    /// </returns>
    bool TryGetWindowPixelSize(out int pixelWidth, out int pixelHeight);
}
