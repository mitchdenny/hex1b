using System.Threading.Channels;

namespace Hex1b;

// One app-loop consumer; requests may arrive from any thread.
internal sealed class FrameScheduler(TimeSpan interval, TimeProvider timeProvider) : IDisposable
{
    private readonly object _lock = new();
    private readonly Channel<bool> _requests = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
    private readonly CancellationTokenSource _lifetime = new();
    private bool _hasStarted;
    private long _lastStarted;
    private bool _disposed;

    internal void RequestFrame()
    {
        lock (_lock)
        {
            if (!_disposed)
                _requests.Writer.TryWrite(true);
        }
    }

    internal async Task<bool> WaitForFrameAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource waitLifetime;
        lock (_lock)
        {
            if (_disposed)
                return false;
            waitLifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        }
        using var linked = waitLifetime;
        try
        {
            while (await _requests.Reader.WaitToReadAsync(linked.Token).ConfigureAwait(false))
            {
                TimeSpan remaining;
                lock (_lock)
                    remaining = GetRemaining();
                if (remaining <= TimeSpan.Zero)
                    return true;

                await Task.Delay(remaining, timeProvider, linked.Token).ConfigureAwait(false);
            }
            return false;
        }
        catch (OperationCanceledException) when (_disposed && !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    internal bool TryBeginFrame(bool bypassPacing = false)
    {
        lock (_lock)
        {
            if (_disposed || (!bypassPacing && GetRemaining() > TimeSpan.Zero) ||
                !_requests.Reader.TryRead(out _))
                return false;

            _lastStarted = timeProvider.GetTimestamp();
            _hasStarted = true;
            return true;
        }
    }

    private TimeSpan GetRemaining() => _hasStarted
        ? interval - timeProvider.GetElapsedTime(_lastStarted)
        : TimeSpan.Zero;

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            _disposed = true;
            _requests.Writer.TryComplete();
        }
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
