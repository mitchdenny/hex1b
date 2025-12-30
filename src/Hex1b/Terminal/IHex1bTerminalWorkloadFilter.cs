using Hex1b.Tokens;

namespace Hex1b.Terminal;

/// <summary>
/// A filter that observes data flowing between the terminal and workload.
/// </summary>
/// <remarks>
/// <para>
/// Workload filters see:
/// <list type="bullet">
///   <item>Output FROM the workload (ANSI sequences heading to display)</item>
///   <item>Input TO the workload (keystrokes, mouse events)</item>
///   <item>Resize events</item>
///   <item>Frame completion signals (when the workload has finished a batch of output)</item>
/// </list>
/// </para>
/// <para>
/// Use cases include:
/// <list type="bullet">
///   <item>Recording terminal sessions (Asciinema)</item>
///   <item>Logging/debugging</item>
///   <item>Performance analysis</item>
///   <item>Testing instrumentation</item>
/// </list>
/// </para>
/// </remarks>
public interface IHex1bTerminalWorkloadFilter
{
    /// <summary>
    /// Called when the terminal session starts.
    /// </summary>
    /// <param name="width">Initial terminal width.</param>
    /// <param name="height">Initial terminal height.</param>
    /// <param name="timestamp">When the session started.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default);

    /// <summary>
    /// Called when output data is read from the workload.
    /// </summary>
    /// <remarks>
    /// This is called for each chunk of data read from the workload's output channel.
    /// Multiple chunks may arrive in quick succession before <see cref="OnFrameCompleteAsync"/> is called.
    /// Use <see cref="AnsiTokenSerializer.Serialize(IEnumerable{AnsiToken})"/> to convert tokens back to bytes if needed.
    /// </remarks>
    /// <param name="tokens">The parsed ANSI tokens from the workload output.</param>
    /// <param name="elapsed">Time elapsed since session start.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask OnOutputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default);

    /// <summary>
    /// Called when the workload output channel is drained (no more data immediately available).
    /// </summary>
    /// <remarks>
    /// This signals a logical "frame boundary" - the workload has finished its current
    /// batch of output. For TUI applications, this typically corresponds to a complete
    /// render cycle.
    /// </remarks>
    /// <param name="elapsed">Time elapsed since session start.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask OnFrameCompleteAsync(TimeSpan elapsed, CancellationToken ct = default);

    /// <summary>
    /// Called when input is being sent to the workload.
    /// </summary>
    /// <param name="data">The raw input bytes.</param>
    /// <param name="elapsed">Time elapsed since session start.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask OnInputAsync(ReadOnlyMemory<byte> data, TimeSpan elapsed, CancellationToken ct = default);

    /// <summary>
    /// Called when the terminal is resized.
    /// </summary>
    /// <param name="width">New width in columns.</param>
    /// <param name="height">New height in rows.</param>
    /// <param name="elapsed">Time elapsed since session start.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default);

    /// <summary>
    /// Called when the terminal session ends.
    /// </summary>
    /// <param name="elapsed">Total duration of the session.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default);
}
