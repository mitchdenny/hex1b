namespace Hex1b;

public sealed partial class Hex1bTerminal
{
    private static readonly TimeSpan SynchronizedOutputTimeout = TimeSpan.FromSeconds(1);
    private TaskCompletionSource? _synchronizedOutputCompletion;
    private ITimer? _synchronizedOutputTimer;
    private long _synchronizedOutputStarted;

    internal TerminalWidgetRenderFrame? CaptureTerminalWidgetFrame(int scrollbackOffset)
    {
        lock (_bufferLock)
        {
            if (_disposed || _synchronizedOutputCompletion is not null)
                return null;

            using var snapshot = CreateSnapshot(scrollbackOffset);
            return new TerminalWidgetRenderFrame(snapshot, _scrollbackBuffer?.Count ?? 0);
        }
    }

    // Called under _bufferLock. Rendering waits; parsing and input must keep running.
    private void SetSynchronizedOutputMode(bool enabled)
    {
        if (!enabled)
        {
            _synchronizedOutputTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _synchronizedOutputCompletion?.TrySetResult();
            _synchronizedOutputCompletion = null;
            return;
        }

        // Repeated begin markers are idempotent and cannot extend a stuck update forever.
        if (_synchronizedOutputCompletion is not null)
            return;

        _synchronizedOutputStarted = _timeProvider.GetTimestamp();
        _synchronizedOutputCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _synchronizedOutputTimer ??= _timeProvider.CreateTimer(
            _ => OnSynchronizedOutputTimeout(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _synchronizedOutputTimer.Change(SynchronizedOutputTimeout, Timeout.InfiniteTimeSpan);
    }

    private void OnSynchronizedOutputTimeout()
    {
        lock (_bufferLock)
        {
            if (_disposed || _synchronizedOutputCompletion is null)
                return;

            // A callback queued for an older update must not release a newer one early.
            var remaining = SynchronizedOutputTimeout -
                _timeProvider.GetElapsedTime(_synchronizedOutputStarted);
            if (remaining > TimeSpan.Zero)
            {
                _synchronizedOutputTimer!.Change(remaining, Timeout.InfiniteTimeSpan);
                return;
            }

            SetSynchronizedOutputMode(false);
        }
        NotifyPresentationInvalidated();
    }
}
