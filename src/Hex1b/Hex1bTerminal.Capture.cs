using Hex1b.Automation;
using Hex1b.Tokens;

namespace Hex1b;

public sealed partial class Hex1bTerminal
{
    // All registration, publication, and boundary operations share the screen lock.
    // Publication queues owned data only; user code runs on the scope's consumer.
    private List<TerminalCaptureScope>? _captures;
    private long _captureSequence;
    private int _captureApplicationDepth;

    /// <summary>
    /// Attaches to an existing terminal without changing its construction-time filters.
    /// Complete tokens are observed at application, so pending UTF-8/escape sequences
    /// stay with the live parser and are published once when completed. States that
    /// cannot be represented faithfully by an ANSI seed fail explicitly.
    /// </summary>
    internal async Task<TerminalCaptureScope> BeginCaptureAsync(
        Func<TerminalCaptureEvent, CancellationToken, ValueTask> observer,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ct.ThrowIfCancellationRequested();
        TerminalCaptureScope scope;
        Task barrier;
        lock (_bufferLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureCaptureBoundaryUnsafe();
            scope = new(this, observer);
            scope.InitialState = CreateCaptureBoundaryUnsafe(scope, seed: true, initial: true);
            (_captures ??= []).Add(scope);
            EnqueueCaptureStateUnsafe(scope, scope.InitialState);
            barrier = scope.EnqueueBarrier();
        }
        scope.Start();
        try
        {
            await barrier.WaitAsync(ct).ConfigureAwait(false);
            scope.ThrowIfFailed();
            return scope;
        }
        catch
        {
            await scope.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    internal async Task<TerminalCaptureBoundary> CaptureBoundaryAsync(
        TerminalCaptureScope scope, bool? visible, bool detach, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        TerminalCaptureBoundary boundary;
        Task barrier;
        lock (_bufferLock)
        {
            scope.ThrowIfFailed();
            ObjectDisposedException.ThrowIf(_disposed || !scope.Attached, scope);
            EnsureCaptureBoundaryUnsafe();
            var seed = visible == true && !scope.Visible;
            boundary = CreateCaptureBoundaryUnsafe(scope, seed);
            if (visible is { } nextVisibility)
                scope.Visible = nextVisibility;
            if (seed)
                EnqueueCaptureStateUnsafe(scope, boundary);
            barrier = scope.EnqueueBarrier();
            if (detach)
                RemoveCapture(scope);
        }
        try
        {
            await barrier.WaitAsync(ct).ConfigureAwait(false);
            scope.ThrowIfFailed();
            return boundary;
        }
        catch
        {
            boundary.Dispose();
            throw;
        }
    }

    internal void RemoveCapture(TerminalCaptureScope scope)
    {
        lock (_bufferLock)
        {
            scope.Attached = false;
            _captures?.Remove(scope);
            if (_captures?.Count == 0)
                _captures = null;
            scope.Complete();
        }
    }

    private TerminalCaptureBoundary CreateCaptureBoundaryUnsafe(
        TerminalCaptureScope scope, bool seed, bool initial = false)
    {
        var snapshot = CreateSnapshot();
        Hex1bTerminalSnapshot? bufferStart = null;
        try
        {
            bufferStart = new(this,
                CaptureSnapshotState(0, ScrollbackWidth.CurrentTerminal, textViewportTop: 0),
                ScrollbackWidth.CurrentTerminal, TerminalCell.Empty);
            var ansi = seed ? CreateCaptureReplayUnsafe() : "";
            return new(snapshot, bufferStart, ansi, ++_captureSequence, initial ? TimeSpan.Zero : scope.Elapsed);
        }
        catch
        {
            snapshot.Dispose();
            bufferStart?.Dispose();
            throw;
        }
    }

    private void EnqueueCaptureStateUnsafe(TerminalCaptureScope scope, TerminalCaptureBoundary state) =>
        scope.Enqueue(new(TerminalCaptureEventKind.State, state.Ansi,
            state.Snapshot.Width, state.Snapshot.Height, state.Sequence, state.Elapsed));

    private void PublishCaptureOutputUnsafe(IReadOnlyList<AnsiToken> tokens,
        IReadOnlyDictionary<DcsToken, DcsFrame>? framedDcs = null)
    {
        if (_captures is null || tokens.Count == 0 || _disposed || !_captures.Any(scope => scope.Visible))
            return;

        // Serialization happens before a workload's pooled token list is returned.
        string output;
        try
        {
            if (framedDcs?.Values.Any(frame => frame.RetentionLimitExceeded) == true ||
                tokens.OfType<DcsToken>().Any(token => token.Payload.Any(character => character > 127)))
                throw new NotSupportedException("Live capture cannot faithfully serialize this DCS payload.");
            output = AnsiTokenSerializer.Serialize(tokens);
        }
        catch (Exception error)
        {
            for (var index = _captures.Count - 1; index >= 0; index--)
            {
                var scope = _captures[index];
                if (!scope.Visible)
                    continue;
                scope.Complete(error);
                RemoveCapture(scope);
                if (_captures is null)
                    break;
            }
            return;
        }
        var sequence = ++_captureSequence;
        foreach (var scope in _captures)
        {
            if (scope.Visible)
                scope.Enqueue(new(TerminalCaptureEventKind.Output, output, _width, _height,
                    sequence, scope.Elapsed));
        }
    }

    private void PublishCaptureResizeUnsafe()
    {
        if (_captures is null || _disposed)
            return;

        var sequence = ++_captureSequence;
        foreach (var scope in _captures)
        {
            if (scope.Visible)
                scope.Enqueue(new(TerminalCaptureEventKind.Resize, "", _width, _height,
                    sequence, scope.Elapsed));
        }
    }

    private void EndCapturesUnsafe()
        => FailCapturesUnsafe(new ObjectDisposedException(nameof(Hex1bTerminal)));

    private void FailCapturesUnsafe(Exception error)
    {
        if (_captures is null)
            return;

        foreach (var scope in _captures)
        {
            scope.Attached = false;
            scope.Complete(error);
        }
        _captures = null;
    }

    private void EnsureCaptureBoundaryUnsafe()
    {
        if (_captureApplicationDepth != 0)
            throw new InvalidOperationException("Capture boundaries cannot run reentrantly during output application.");
    }

    private readonly struct CaptureApplication : IDisposable
    {
        private readonly Hex1bTerminal _terminal;

        internal CaptureApplication(Hex1bTerminal terminal)
        {
            _terminal = terminal;
            if (terminal._captureApplicationDepth != 0 && terminal._captures is not null)
                terminal.FailCapturesUnsafe(new InvalidOperationException("Output application reentered during capture."));
            terminal._captureApplicationDepth++;
        }

        public void Dispose() => _terminal._captureApplicationDepth--;
    }
}
