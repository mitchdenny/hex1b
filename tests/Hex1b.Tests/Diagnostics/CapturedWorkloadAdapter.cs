namespace Hex1b.Tests.Diagnostics;

internal sealed class CapturedWorkloadAdapter : IHex1bTerminalWorkloadAdapter
{
    private readonly byte[][] _outputChunks;
    private readonly object _gate = new();
    private readonly List<byte> _written = [];
    private readonly TaskCompletionSource _disposedSignal =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _nextChunk;
    private bool _disposed;
    private (int Width, int Height)? _lastResize;

    public CapturedWorkloadAdapter(IReadOnlyList<byte[]> outputChunks)
    {
        ArgumentNullException.ThrowIfNull(outputChunks);
        _outputChunks = outputChunks.Select(chunk => chunk.ToArray()).ToArray();
    }

    public byte[] WrittenBytes { get { lock (_gate) return _written.ToArray(); } }
    public byte[] WrittenInput => WrittenBytes;
    public (int Width, int Height)? LastResize { get { lock (_gate) return _lastResize; } }
    public event Action? Disconnected;

    public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_nextChunk < _outputChunks.Length)
                return _outputChunks[_nextChunk++].ToArray();
        }

        await _disposedSignal.Task.WaitAsync(ct).ConfigureAwait(false);
        return ReadOnlyMemory<byte>.Empty;
    }

    public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _written.AddRange(data.ToArray());
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _lastResize = (width, height);
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed)
                return ValueTask.CompletedTask;
            _disposed = true;
        }
        _disposedSignal.TrySetResult();
        Disconnected?.Invoke();
        return ValueTask.CompletedTask;
    }
}
