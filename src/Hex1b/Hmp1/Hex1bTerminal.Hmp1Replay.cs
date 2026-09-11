using System.Runtime.ExceptionServices;
using System.Diagnostics;
using Hex1b.Tokens;

namespace Hex1b;

public sealed partial class Hex1bTerminal
{
    // Scoped to one output-state transaction. HMP presentations, including ones
    // inside a composite, must forward replay as control frames rather than live OSC.
    internal Hmp1ActivityState? Hmp1ReplayActivityState { get; private set; }

    private bool _deferHmp1ReplayCallbacks;

    internal async Task WaitForHmp1InitialReplayAsync(CancellationToken cancellationToken)
    {
        if (_workload is not IHmp1TerminalOutputSource source)
            return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        while (source.Hmp1Workload is { ConnectionStarted: true } workload)
        {
            var error = await workload.InitialHandshake.WaitAsync(linked.Token).ConfigureAwait(false)
                ?? await workload.InitialReplay.WaitAsync(linked.Token).ConfigureAwait(false);
            if (!ReferenceEquals(source.Hmp1Workload, workload))
                continue;
            if (error is not null)
                ExceptionDispatchInfo.Capture(error).Throw();
            return;
        }
    }

    private async Task ApplyHmp1ReplayAsync(
        ReadOnlyMemory<byte> bytes, Hmp1TerminalState? state, Hmp1ActivityState? activity, CancellationToken ct)
    {
        if (activity is null)
            throw new InvalidDataException("StateSync requires an activity checkpoint.");
        Hmp1ReplayActivityState = activity;
        var rawPassthrough = _presentationFilters.Count == 0 &&
            _presentation is not ICellImpactAwarePresentationAdapter;
        var fastPath = _workloadFilters.Count == 0 && rawPassthrough;

        if (rawPassthrough && !_disposed && _presentation is not null)
        {
            var started = Stopwatch.GetTimestamp();
            await _presentation.WriteOutputAsync(bytes, ct).ConfigureAwait(false);
            _metrics.TerminalRawPassthroughDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            _metrics.TerminalOutputBytes.Record(bytes.Length);
        }

        // The new baseline supersedes any unfinished prefix from the previous stream.
        _incompleteSequenceBuffer = "";
        _utf8Decoder.Reset();
        _pendingUtf8OutputLength = 0;
        _dcsByteStreamParser.Complete();
        var tokenization = TokenizeRawWorkloadOutput(bytes.Span);
        var tokens = tokenization.Tokens;
        _metrics.TerminalOutputTokens.Record(tokens.Count);
        if (!fastPath && !bytes.IsEmpty)
            await NotifyWorkloadFiltersOutputAsync(tokens).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        IReadOnlyList<AppliedToken> applied;
        bool resized;
        lock (_bufferLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var previous = _activityState;
            var previousTitle = _windowTitle;
            var previousIcon = _iconName;
            _deferHmp1ReplayCallbacks = true;
            BeginActivityStateRestore();
            try
            {
                _titleStack.Clear();
                resized = state is not null && state.Width > 0 && state.Height > 0 &&
                    (_width != state.Width || _height != state.Height);
                if (state is not null)
                {
                    _hmp1State = state;
                    if (resized)
                        Resize(state.Width, state.Height);
                }
                if (fastPath)
                {
                    ApplyTokens(tokens, tokenization.FramedDcs);
                    applied = [];
                }
                else
                {
                    applied = ApplyTokensWithImpacts(tokens, tokenization.FramedDcs);
                }
                ObjectDisposedException.ThrowIf(_disposed, this);
                RestoreActivityState(
                    new TerminalProgress((TerminalProgressState)activity.Progress.State, activity.Progress.Percentage),
                    new TerminalShellIntegration((TerminalShellIntegrationPhase)activity.ShellIntegration.Phase,
                        activity.ShellIntegration.LastExitCode));
            }
            catch
            {
                // Failed application must not install an unrendered checkpoint or emit
                // intermediate activity from a partially applied replay.
                SetActivityState(previous);
                throw;
            }
            finally
            {
                try
                {
                    EndActivityStateRestore();
                }
                finally
                {
                    _deferHmp1ReplayCallbacks = false;
                }
            }

            // The monitor is reentrant: observers must run after painting and the
            // checkpoint commit, not while replay switches screens or restores titles.
            if (previousTitle != _windowTitle)
                WindowTitleChanged?.Invoke(_windowTitle);
            if (previousIcon != _iconName)
                IconNameChanged?.Invoke(_iconName);
            if (resized)
                NotifyPresentationInvalidated();
        }

        // Screen and activity are already committed together. Never hold the buffer
        // monitor across an asynchronous presentation or filter callback.
        if (resized)
        {
            await NotifyPresentationFiltersResizeAsync(state!.Width, state.Height).ConfigureAwait(false);
            await NotifyWorkloadFiltersResizeAsync(state.Width, state.Height).ConfigureAwait(false);
        }
        if (_presentation is ICellImpactAwarePresentationAdapter impactAware)
        {
            if (_presentationFilters.Count > 0)
                await NotifyPresentationFiltersOutputAsync(applied).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);
            await impactAware.WriteOutputWithImpactsAsync(applied, ct).ConfigureAwait(false);
        }
        else if (!rawPassthrough && _presentation is not null)
        {
            var filtered = await NotifyPresentationFiltersOutputAsync(applied).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);
            var filteredBytes = AnsiTokenUtf8Serializer.Serialize(filtered);
            await _presentation.WriteOutputAsync(filteredBytes, ct).ConfigureAwait(false);
            _metrics.TerminalOutputBytes.Record(filteredBytes.Length);
        }
    }
}
