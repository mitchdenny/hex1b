using Hex1b.Tokens;

namespace Hex1b.Terminal;

/// <summary>
/// A filter that observes data flowing between the terminal and presentation layer.
/// </summary>
/// <remarks>
/// <para>
/// Presentation filters see:
/// <list type="bullet">
///   <item>Output TO the presentation layer (after terminal processing)</item>
///   <item>Input FROM the presentation layer (raw user input)</item>
/// </list>
/// </para>
/// <para>
/// Use cases include:
/// <list type="bullet">
///   <item>Render optimization (jitter elimination, batching)</item>
///   <item>Output transformation (e.g., delta encoding)</item>
///   <item>Input preprocessing</item>
///   <item>Network protocol adaptation</item>
/// </list>
/// </para>
/// </remarks>
public interface IHex1bTerminalPresentationFilter
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
    /// Called when output is being sent to the presentation layer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The filter receives applied tokens with their cell impacts, allowing filters to
    /// understand exactly which cells were affected by each token. This enables advanced
    /// optimizations like delta encoding where only changed cells are transmitted.
    /// </para>
    /// <para>
    /// Use <see cref="AnsiTokenSerializer.Serialize(IEnumerable{AnsiToken})"/> to convert tokens to bytes if needed.
    /// </para>
    /// </remarks>
    /// <param name="appliedTokens">The applied tokens with their cell impacts.</param>
    /// <param name="elapsed">Time elapsed since session start.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The tokens to send to the presentation layer. Return the original tokens to pass through, or a modified list.</returns>
    ValueTask<IReadOnlyList<AnsiToken>> OnOutputAsync(IReadOnlyList<AppliedToken> appliedTokens, TimeSpan elapsed, CancellationToken ct = default);

    /// <summary>
    /// Called when input is received from the presentation layer.
    /// </summary>
    /// <param name="data">The raw input bytes from the user.</param>
    /// <param name="elapsed">Time elapsed since session start.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask OnInputAsync(ReadOnlyMemory<byte> data, TimeSpan elapsed, CancellationToken ct = default);

    /// <summary>
    /// Called when the terminal is resized by the presentation layer.
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
