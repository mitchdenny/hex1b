using Hex1b.Reflow;

namespace Hex1b;

/// <summary>
/// A presentation adapter that discards output and provides no input.
/// Use this for headless/testing scenarios where no actual terminal I/O is needed.
/// </summary>
/// <remarks>
/// <para>
/// The headless adapter satisfies the presentation adapter interface but:
/// </para>
/// <list type="bullet">
///   <item>WriteOutputAsync discards all data</item>
///   <item>ReadInputAsync blocks until cancelled</item>
///   <item>Raw mode operations are no-ops</item>
/// </list>
/// <para>
/// This is useful for:
/// </para>
/// <list type="bullet">
///   <item>Unit tests that verify workload behavior without terminal I/O</item>
///   <item>Integration tests with presentation filters (e.g., asciinema recording)</item>
///   <item>CI/CD environments where no TTY is available</item>
/// </list>
/// <para>
/// Optionally implements <see cref="ITerminalReflowProvider"/> when a reflow strategy
/// is configured via <see cref="WithReflowStrategy"/>. This enables testing different
/// terminal emulator reflow behaviors in headless mode.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// await Hex1bTerminal.CreateBuilder()
///     .WithHex1bApp(ctx => ctx.Text("Hello"))
///     .WithHeadless()
///     .WithDimensions(80, 24)
///     .WithAsciinemaRecording("output.cast")
///     .RunAsync();
/// </code>
/// </example>
public sealed class HeadlessPresentationAdapter : IHex1bTerminalPresentationAdapter, ITerminalReflowProvider, IAsyncDisposable, IDisposable
{
    private readonly int _width;
    private readonly int _height;
    private readonly TerminalCapabilities _capabilities;
    private readonly TaskCompletionSource _disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ITerminalReflowProvider _reflowStrategy = NoReflowStrategy.Instance;
    private bool _disposed;

    /// <summary>
    /// Creates a new headless presentation adapter with the specified dimensions.
    /// </summary>
    /// <param name="width">Terminal width in columns.</param>
    /// <param name="height">Terminal height in rows.</param>
    /// <param name="capabilities">Optional terminal capabilities. Defaults to <see cref="TerminalCapabilities.Minimal"/>.</param>
    public HeadlessPresentationAdapter(int width = 80, int height = 24, TerminalCapabilities? capabilities = null)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
        _width = width;
        _height = height;
        _capabilities = capabilities ?? TerminalCapabilities.Minimal;
    }

    /// <inheritdoc />
    public int Width => _width;

    /// <inheritdoc />
    public int Height => _height;

    /// <inheritdoc />
    public TerminalCapabilities Capabilities => _capabilities;

    /// <inheritdoc />
    public event Action<int, int>? Resized;

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <inheritdoc />
    public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        // Discard all output
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
    {
        if (_disposed)
            return ReadOnlyMemory<byte>.Empty;

        // Wait indefinitely until cancelled or disposed
        try
        {
            await _disconnected.Task.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }

        return ReadOnlyMemory<byte>.Empty;
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken ct = default)
        => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask EnterRawModeAsync(CancellationToken ct = default)
        => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask ExitRawModeAsync(CancellationToken ct = default)
        => ValueTask.CompletedTask;

    /// <summary>
    /// Configures the reflow strategy for this adapter. Defaults to <see cref="NoReflowStrategy"/>.
    /// </summary>
    /// <param name="strategy">The reflow strategy to use during resize operations.</param>
    /// <returns>This adapter for fluent chaining.</returns>
    public HeadlessPresentationAdapter WithReflowStrategy(ITerminalReflowProvider strategy)
    {
        _reflowStrategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        return this;
    }

    /// <inheritdoc/>
    public bool ShouldClearSoftWrapOnAbsolutePosition => _reflowStrategy.ShouldClearSoftWrapOnAbsolutePosition;

    /// <inheritdoc/>
    public ReflowResult Reflow(ReflowContext context) => _reflowStrategy.Reflow(context);

    /// <summary>
    /// Triggers a resize event for testing purposes.
    /// </summary>
    /// <param name="width">New width.</param>
    /// <param name="height">New height.</param>
    public void TriggerResize(int width, int height)
    {
        Resized?.Invoke(width, height);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        
        Disconnected?.Invoke();
        _disconnected.TrySetResult();
        
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        Disconnected?.Invoke();
        _disconnected.TrySetResult();
    }
}
