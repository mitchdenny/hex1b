using Hex1b;

/// <summary>
/// Feeds a fixed byte payload to a terminal once and then disconnects.
/// </summary>
/// <remarks>
/// The headless transcript uses this to replay a single rendered frame into an
/// in-memory terminal so it can count the placements that frame leaves behind.
/// </remarks>
internal sealed class ReplayWorkloadAdapter : IHex1bTerminalWorkloadAdapter
{
    private readonly byte[] _payload;
    private readonly Lock _gate = new();

    private bool _sent;
    private bool _completed;
    private Action? _disconnected;

    public ReplayWorkloadAdapter(byte[] payload) => _payload = payload;

    public event Action? Disconnected
    {
        add
        {
            var replay = false;
            lock (_gate)
            {
                if (_completed)
                {
                    replay = true;
                }
                else
                {
                    _disconnected += value;
                }
            }

            if (replay)
            {
                value?.Invoke();
            }
        }
        remove
        {
            lock (_gate)
            {
                _disconnected -= value;
            }
        }
    }

    public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_sent)
            {
                SignalDisconnectedLocked();
                return ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
            }

            _sent = true;
        }

        return ValueTask.FromResult<ReadOnlyMemory<byte>>(_payload);
    }

    public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask ResizeAsync(int width, int height, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        Action? handler;
        lock (_gate)
        {
            handler = _disconnected;
            _disconnected = null;
            _completed = true;
        }

        handler?.Invoke();
        return ValueTask.CompletedTask;
    }

    private void SignalDisconnectedLocked()
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        var handler = _disconnected;
        _disconnected = null;
        handler?.Invoke();
    }
}
