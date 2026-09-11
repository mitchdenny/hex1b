namespace Hex1b.Automation;

/// <summary>
/// Base class for test sequence steps.
/// Each step knows how to execute itself against a terminal.
/// </summary>
public abstract record TestStep
{
    /// <summary>
    /// Executes this step against the terminal.
    /// </summary>
    internal abstract Task ExecuteAsync(
        Hex1bTerminal terminal,
        Hex1bTerminalInputSequenceOptions options,
        CancellationToken ct);

    /// <summary>
    /// Creates a delay using the specified TimeProvider.
    /// When using FakeTimeProvider, the test must advance time externally.
    /// </summary>
    protected static Task DelayAsync(TimeProvider timeProvider, TimeSpan delay, CancellationToken ct)
    {
        return Task.Delay(delay <= TimeSpan.Zero ? TimeSpan.Zero : delay, timeProvider, ct);
    }
}
