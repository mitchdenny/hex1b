using Hex1b;

/// <summary>
/// Replays a fixed list of byte chunks and then disconnects.
/// </summary>
/// <remarks>
/// Used by the headless inspection helpers, which feed one fixture into a
/// short-lived terminal to read back the resulting model. Interactive runs use
/// <see cref="PagedScreenWorkloadAdapter"/> instead, because they must wait for
/// input between screens.
/// </remarks>
internal sealed class DemoWorkloadAdapter(IReadOnlyList<byte[]> chunks) : IHex1bTerminalWorkloadAdapter
{
    private readonly object _eventLock = new();
    private Action? _disconnected;
    private bool _completed;
    private int _nextChunk;

    public event Action? Disconnected
    {
        add
        {
            var invokeNow = false;
            lock (_eventLock)
            {
                _disconnected += value;
                invokeNow = _completed;
            }
            if (invokeNow)
                value?.Invoke();
        }
        remove
        {
            lock (_eventLock)
                _disconnected -= value;
        }
    }

    public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
    {
        if (_nextChunk < chunks.Count)
            return ValueTask.FromResult<ReadOnlyMemory<byte>>(chunks[_nextChunk++]);

        Action? disconnected;
        lock (_eventLock)
        {
            _completed = true;
            disconnected = _disconnected;
        }
        disconnected?.Invoke();
        return ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
    }

    public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        => ValueTask.CompletedTask;

    public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
        => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
