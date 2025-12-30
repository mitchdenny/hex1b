namespace Hex1b.Terminal.Automation;

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
        if (delay <= TimeSpan.Zero)
            return Task.CompletedTask;

        var tcs = new TaskCompletionSource();
        using var registration = ct.Register(() => tcs.TrySetCanceled(ct));
        
        var timer = timeProvider.CreateTimer(
            _ => tcs.TrySetResult(),
            null,
            delay,
            Timeout.InfiniteTimeSpan);
        
        return tcs.Task.ContinueWith(_ => timer.Dispose(), CancellationToken.None);
    }
}
