using System.Threading.Channels;

namespace Hex1b.Terminal;

/// <summary>
/// A presentation adapter that broadcasts output to multiple child adapters and aggregates input.
/// </summary>
/// <remarks>
/// <para>
/// This adapter allows terminal output to be sent to multiple presentation adapters simultaneously,
/// such as a console adapter for passthrough and a Blazor adapter for web viewing.
/// </para>
/// <para>
/// Input is collected from all adapters and merged into a single stream. The first adapter
/// in the list is considered the "primary" adapter and determines dimensions and capabilities.
/// </para>
/// </remarks>
public sealed class MultiheadedPresentationAdapter : IHex1bTerminalPresentationAdapter
{
    private readonly IHex1bTerminalPresentationAdapter[] _adapters;
    private readonly IHex1bTerminalPresentationAdapter _primary;
    private readonly Channel<ReadOnlyMemory<byte>> _inputChannel;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly List<Task> _inputPumpTasks = [];
    private bool _disposed;

    /// <summary>
    /// Creates a new multiheaded presentation adapter.
    /// </summary>
    /// <param name="adapters">The child adapters. The first adapter is considered primary.</param>
    /// <exception cref="ArgumentException">Thrown if no adapters are provided.</exception>
    public MultiheadedPresentationAdapter(params IHex1bTerminalPresentationAdapter[] adapters)
    {
        if (adapters.Length == 0)
            throw new ArgumentException("At least one adapter is required", nameof(adapters));

        _adapters = adapters;
        _primary = adapters[0];
        _inputChannel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        // Wire up resize events from all adapters
        foreach (var adapter in _adapters)
        {
            adapter.Resized += OnAdapterResized;
            adapter.Disconnected += OnAdapterDisconnected;
        }

        // Start input pump tasks for all adapters
        foreach (var adapter in _adapters)
        {
            _inputPumpTasks.Add(PumpInputAsync(adapter, _disposeCts.Token));
        }
    }

    private void OnAdapterResized(int width, int height)
    {
        // Forward resize events from any adapter
        Resized?.Invoke(width, height);
    }

    private void OnAdapterDisconnected()
    {
        // Only fire disconnected when primary adapter disconnects
        // Other adapters can reconnect without affecting the terminal
    }

    /// <inheritdoc />
    public int Width => _primary.Width;

    /// <inheritdoc />
    public int Height => _primary.Height;

    /// <inheritdoc />
    public TerminalCapabilities Capabilities => _primary.Capabilities;

    /// <inheritdoc />
    public event Action<int, int>? Resized;

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <inheritdoc />
    public async ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_disposed) return;

        // Broadcast to all adapters in parallel
        var tasks = new List<Task>(_adapters.Length);
        foreach (var adapter in _adapters)
        {
            tasks.Add(adapter.WriteOutputAsync(data, ct).AsTask());
        }
        
        await Task.WhenAll(tasks);
    }

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
    {
        if (_disposed) return ReadOnlyMemory<byte>.Empty;

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token);
            return await _inputChannel.Reader.ReadAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
        catch (ChannelClosedException)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
    }

    private async Task PumpInputAsync(IHex1bTerminalPresentationAdapter adapter, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var data = await adapter.ReadInputAsync(ct);
                if (data.IsEmpty)
                {
                    // Adapter disconnected - only exit if it's the primary
                    if (adapter == _primary)
                    {
                        _inputChannel.Writer.Complete();
                        Disconnected?.Invoke();
                        break;
                    }
                    // For non-primary adapters, just exit the pump
                    break;
                }

                // Make a copy of the data since ReadOnlyMemory may be reused
                await _inputChannel.Writer.WriteAsync(data.ToArray(), ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    /// <inheritdoc />
    public async ValueTask FlushAsync(CancellationToken ct = default)
    {
        foreach (var adapter in _adapters)
        {
            await adapter.FlushAsync(ct);
        }
    }

    /// <inheritdoc />
    public async ValueTask EnterRawModeAsync(CancellationToken ct = default)
    {
        foreach (var adapter in _adapters)
        {
            await adapter.EnterRawModeAsync(ct);
        }
    }

    /// <inheritdoc />
    public async ValueTask ExitRawModeAsync(CancellationToken ct = default)
    {
        foreach (var adapter in _adapters)
        {
            await adapter.ExitRawModeAsync(ct);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _disposeCts.Cancel();
        _inputChannel.Writer.Complete();

        // Wait for input pumps to finish
        try
        {
            await Task.WhenAll(_inputPumpTasks);
        }
        catch
        {
            // Ignore errors during shutdown
        }

        // Dispose all adapters
        foreach (var adapter in _adapters)
        {
            adapter.Resized -= OnAdapterResized;
            adapter.Disconnected -= OnAdapterDisconnected;
            await adapter.DisposeAsync();
        }

        _disposeCts.Dispose();
    }
}
