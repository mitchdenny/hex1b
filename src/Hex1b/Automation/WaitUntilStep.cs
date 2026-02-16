namespace Hex1b.Automation;

/// <summary>
/// A step that waits until a condition is met on the terminal.
/// </summary>
public sealed record WaitUntilStep(
    Func<Hex1bTerminalSnapshot, bool> Predicate,
    TimeSpan Timeout,
    string? Description = null,
    string? PredicateExpression = null,
    string? CallerFilePath = null,
    int? CallerLineNumber = null) : TestStep
{
    internal override async Task ExecuteAsync(
        Hex1bTerminal terminal,
        Hex1bTerminalInputSequenceOptions options,
        CancellationToken ct)
    {
        var timeProvider = options.TimeProvider ?? TimeProvider.System;
        var effectiveTimeout = Timeout * options.TimeoutMultiplier;
        var deadline = timeProvider.GetUtcNow() + effectiveTimeout;

        while (timeProvider.GetUtcNow() < deadline)
        {
            ct.ThrowIfCancellationRequested();

            // CreateSnapshot auto-flushes pending output
            using var snapshot = terminal.CreateSnapshot();

            if (Predicate(snapshot))
                return;

            await DelayAsync(timeProvider, options.PollInterval, ct);
        }

        // Timeout - capture final state for diagnostics
        var finalSnapshot = terminal.CreateSnapshot();
        var description = Description ?? PredicateExpression ?? "condition";
        throw new WaitUntilTimeoutException(
            effectiveTimeout,
            description,
            finalSnapshot,
            CallerFilePath,
            CallerLineNumber);
    }
}
