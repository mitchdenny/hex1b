using System.Runtime.ExceptionServices;

namespace Hex1b.Automation;

internal static class TapeExecution
{
    internal static async Task<TapeExecutionResult> ExecuteAsync(TapePreparedRun prepared,
        Hex1bTerminal terminal, Func<Task>? startWorkload, Func<Task<int>?>? observeExit, CancellationToken ct)
    {
        var clock = terminal.AutomationTimeProvider;
        var started = clock.GetTimestamp();
        var capture = new TapeCaptureSession(terminal, prepared);
        TapeCommand? active = null;
        var completed = 0;
        Exception? failure = null;
        OperationCanceledException? cancellationFailure = null;
        Hex1bTerminalSnapshot? finalSnapshot = null;

        try
        {
            ct.ThrowIfCancellationRequested();
            if (prepared.TerminalSize is { } dimensions)
                await terminal.ResizeForAutomationAsync(dimensions.Width, dimensions.Height, ct);
            await capture.StartAsync(ct);
            if (startWorkload is not null)
                await startWorkload();
            foreach (var command in prepared.Commands)
            {
                ct.ThrowIfCancellationRequested();
                active = command.Command;
                if (command.Sequence is { } sequence)
                {
                    if (observeExit?.Invoke() is { IsCompleted: true } exit)
                    {
                        var code = await exit;
                        if (sequence.Steps.Any(s => s is TextInputStep or KeyInputStep or MouseInputStep or TapeRepeatedKeyStep))
                            throw new IOException($"The owned workload exited with code {code} before this input command.");
                    }
                    using var stepSnapshot = await ApplySequenceAsync(sequence, terminal, observeExit?.Invoke(), ct);
                }
                if (command.CaptureControl == TapeCaptureControl.Hide)
                    await capture.HideAsync(ct);
                else if (command.CaptureControl == TapeCaptureControl.Show)
                    await capture.ShowAsync(ct);
                completed++;
                await capture.CheckpointAsync(command.GoldenPath, ct);
            }
            if (observeExit?.Invoke() is { IsCompleted: true } finishedWorkload)
                await finishedWorkload;
        }
        catch (Exception ex)
        {
            // This is the execution boundary: retain the original failure while finalizing captures.
            failure = ex;
            cancellationFailure = ex as OperationCanceledException;
        }

        try
        {
            finalSnapshot = await capture.FinishAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            failure = failure is null ? ex : new AggregateException(failure, ex);
            try
            {
                finalSnapshot = terminal.CreateSnapshot();
            }
            catch (Exception snapshotError)
            {
                failure = new AggregateException(failure, snapshotError);
            }
        }

        if (failure is not null)
        {
            var text = finalSnapshot?.GetScreenText() ?? "<terminal snapshot unavailable>";
            finalSnapshot?.Dispose();
            ReleaseTimeoutSnapshots(failure);
            if (failure is OperationCanceledException)
                ExceptionDispatchInfo.Capture(failure).Throw();
            if (cancellationFailure is not null)
                throw new OperationCanceledException("Tape playback was cancelled and capture finalization also failed.",
                    failure, cancellationFailure.CancellationToken);
            throw new TapePlaybackException(active, completed, text, capture.CreatedArtifacts, failure);
        }

        return new TapeExecutionResult(finalSnapshot ?? throw new InvalidOperationException("Capture returned no final snapshot."),
            completed, clock.GetElapsedTime(started), capture.CreatedArtifacts, prepared.Diagnostics);
    }

    private static async Task<Hex1bTerminalSnapshot> ApplySequenceAsync(Hex1bTerminalInputSequence sequence,
        Hex1bTerminal terminal, Task<int>? workload, CancellationToken ct)
    {
        if (workload is null)
            return await sequence.ApplyAsync(terminal, ct);

        using var stepCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var step = sequence.ApplyAsync(terminal, stepCancellation.Token);
        if (await Task.WhenAny(step, workload) == workload && !workload.IsCompletedSuccessfully)
        {
            // A startup/pump failure must interrupt even an arbitrarily long Wait or Sleep.
            await stepCancellation.CancelAsync();
            Exception? stepFailure = null;
            try
            {
                using var snapshot = await step;
            }
            catch (OperationCanceledException) when (stepCancellation.IsCancellationRequested) { }
            catch (Exception ex)
            {
                stepFailure = ex;
            }
            try
            {
                await workload;
            }
            catch (Exception ex) when (stepFailure is not null)
            {
                throw new AggregateException(ex, stepFailure);
            }
        }
        return await step;
    }

    private static void ReleaseTimeoutSnapshots(Exception exception)
    {
        if (exception is WaitUntilTimeoutException timeout)
            timeout.TerminalSnapshot?.Dispose();
        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions)
                ReleaseTimeoutSnapshots(inner);
        }
        else if (exception.InnerException is { } inner)
            ReleaseTimeoutSnapshots(inner);
    }
}
