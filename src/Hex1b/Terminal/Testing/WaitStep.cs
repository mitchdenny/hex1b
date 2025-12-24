namespace Hex1b.Terminal.Testing;

/// <summary>
/// A step that pauses for a specified duration.
/// </summary>
public sealed record WaitStep(TimeSpan Duration) : TestStep
{
    internal override async Task ExecuteAsync(
        Hex1bTerminal terminal,
        Hex1bTestSequenceOptions options,
        CancellationToken ct)
    {
        if (options.TimeProvider is { } timeProvider)
        {
            // Use TimeProvider-based delay - when using FakeTimeProvider, the test
            // should advance time externally to complete this delay
            await DelayAsync(timeProvider, Duration, ct);
            terminal.FlushOutput();
        }
        else
        {
            await Task.Delay(Duration, ct);
        }
    }
}
