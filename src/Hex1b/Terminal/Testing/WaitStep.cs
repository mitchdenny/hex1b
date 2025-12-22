namespace Hex1b.Terminal.Testing;

/// <summary>
/// A step that pauses for a specified duration.
/// </summary>
public sealed record WaitStep(TimeSpan Duration) : InputStep
{
    internal override async Task ExecuteAsync(Hex1bTerminal terminal, CancellationToken ct)
    {
        await Task.Delay(Duration, ct);
    }
}
