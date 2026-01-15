namespace Hex1b.Automation;

/// <summary>
/// A step that waits while a condition is true on the terminal.
/// Completes when the condition becomes false.
/// </summary>
public sealed record WaitWhileStep(
    Func<Hex1bTerminalSnapshot, bool> Predicate,
    TimeSpan Timeout,
    string? Description = null) : TestStep
{
    internal override async Task ExecuteAsync(
        Hex1bTerminal terminal,
        Hex1bTerminalInputSequenceOptions options,
        CancellationToken ct)
    {
        var timeProvider = options.TimeProvider ?? TimeProvider.System;
        var deadline = timeProvider.GetUtcNow() + Timeout;

        while (timeProvider.GetUtcNow() < deadline)
        {
            ct.ThrowIfCancellationRequested();

            // CreateSnapshot auto-flushes pending output
            var snapshot = terminal.CreateSnapshot();

            if (!Predicate(snapshot))
                return;

            await DelayAsync(timeProvider, options.PollInterval, ct);
        }

        // Timeout - capture final state for diagnostics
        var finalSnapshot = terminal.CreateSnapshot();
        var description = Description ?? "condition to become false";
        throw new TimeoutException(
            $"WaitWhile timed out after {Timeout} waiting for {description}.\n" +
            $"Terminal state:\n{finalSnapshot.GetDisplayText()}");
    }
}
