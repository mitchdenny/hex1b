namespace Hex1b.Terminal.Automation;

/// <summary>
/// A step that waits until a condition is met on the terminal.
/// </summary>
public sealed record WaitUntilStep(
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

            if (Predicate(snapshot))
                return;

            await DelayAsync(timeProvider, options.PollInterval, ct);
        }

        // Timeout - capture final state for diagnostics
        var finalSnapshot = terminal.CreateSnapshot();
        var description = Description ?? "condition";
        throw new TimeoutException(
            $"WaitUntil timed out after {Timeout} waiting for {description}.\n" +
            $"Terminal state:\n{finalSnapshot.GetDisplayText()}");
    }
}
