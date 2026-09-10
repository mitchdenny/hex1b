using System.Runtime.ExceptionServices;
using System.Threading.Channels;

namespace Hex1b;

/// <summary>
/// Serializes observations independently of the output pump. Callbacks must not await
/// this scope's barriers or disposal. Dispose removes the observer without propagating its failure;
/// Completion and the explicit boundary operations report failures.
/// </summary>
internal sealed class TerminalCaptureScope : IAsyncDisposable
{
    private readonly Hex1bTerminal _terminal;
    private readonly Func<TerminalCaptureEvent, CancellationToken, ValueTask> _observer;
    private readonly Channel<Observation> _queue = Channel.CreateUnbounded<Observation>(
        new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = false });
    private readonly CancellationTokenSource _callbackCancellation = new();
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Exception? _failure;
    private int _disposed;

    internal TerminalCaptureScope(
        Hex1bTerminal terminal,
        Func<TerminalCaptureEvent, CancellationToken, ValueTask> observer)
    {
        _terminal = terminal;
        _observer = observer;
        StartedAt = terminal.AutomationTimeProvider.GetTimestamp();
    }

    internal long StartedAt { get; }
    internal bool Visible { get; set; } = true;
    internal bool Attached { get; set; } = true;
    internal TerminalCaptureBoundary InitialState { get; set; } = null!;
    internal Task Completion => _completion.Task;
    internal TimeSpan Elapsed => _terminal.AutomationTimeProvider.GetElapsedTime(StartedAt);

    internal void Start() => _ = Task.Run(ConsumeAsync);

    internal void Enqueue(TerminalCaptureEvent observation) =>
        _queue.Writer.TryWrite(new(observation, null));

    internal Task EnqueueBarrier()
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_queue.Writer.TryWrite(new(null, signal)))
            signal.TrySetException(_failure ?? new ObjectDisposedException(nameof(TerminalCaptureScope)));
        return signal.Task;
    }

    internal void Complete(Exception? error = null)
    {
        if (error is not null)
            Interlocked.CompareExchange(ref _failure, error, null);
        _queue.Writer.TryComplete();
    }

    internal void ThrowIfFailed()
    {
        if (Volatile.Read(ref _failure) is { } failure)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    /// <summary>Captures state and waits for all observations preceding this boundary.</summary>
    internal Task<TerminalCaptureBoundary> BarrierAsync(CancellationToken ct = default) =>
        _terminal.CaptureBoundaryAsync(this, visible: null, detach: false, ct);

    /// <summary>Omits hidden observations; becoming visible first emits authoritative state.</summary>
    internal Task<TerminalCaptureBoundary> SetVisibilityAsync(bool visible, CancellationToken ct = default) =>
        _terminal.CaptureBoundaryAsync(this, visible, detach: false, ct);

    /// <summary>Removes this observer atomically with a final caller-owned snapshot, then drains it.</summary>
    internal Task<TerminalCaptureBoundary> DetachAsync(CancellationToken ct = default) =>
        _terminal.CaptureBoundaryAsync(this, visible: null, detach: true, ct);

    private async Task ConsumeAsync()
    {
        TaskCompletionSource? currentSignal = null;
        try
        {
            await foreach (var item in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                currentSignal = item.Signal;
                ThrowIfFailed();
                if (item.Event is { } observation)
                    await _observer(observation, _callbackCancellation.Token).ConfigureAwait(false);
                else
                    item.Signal!.TrySetResult();
                currentSignal = null;
            }
            ThrowIfFailed();
            _completion.TrySetResult();
        }
        catch (Exception error)
        {
            Interlocked.CompareExchange(ref _failure, error, null);
            currentSignal?.TrySetException(_failure);
            _terminal.RemoveCapture(this);
            while (_queue.Reader.TryRead(out var item))
                item.Signal?.TrySetException(_failure);
            _completion.TrySetException(_failure);
            // Completion remains available to callers, but an abandoned scope must not
            // generate an unobserved-task exception during terminal shutdown.
            _ = _completion.Task.Exception;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _terminal.RemoveCapture(this);
        await _callbackCancellation.CancelAsync().ConfigureAwait(false);
        try
        {
            await Completion.ConfigureAwait(false);
        }
        catch
        {
            // Cleanup must not replace a workload or playback exception.
        }
        InitialState.Dispose();
        _callbackCancellation.Dispose();
    }

    private sealed record Observation(TerminalCaptureEvent? Event, TaskCompletionSource? Signal);
}
