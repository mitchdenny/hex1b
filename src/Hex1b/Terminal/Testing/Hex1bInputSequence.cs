namespace Hex1b.Terminal.Testing;

/// <summary>
/// A sequence of input steps that can be applied to a terminal.
/// Built using <see cref="Hex1bInputSequenceBuilder"/>.
/// </summary>
public sealed class Hex1bInputSequence
{
    private readonly IReadOnlyList<InputStep> _steps;

    internal Hex1bInputSequence(IReadOnlyList<InputStep> steps)
    {
        _steps = steps;
    }

    /// <summary>
    /// Gets the steps in this sequence.
    /// </summary>
    public IReadOnlyList<InputStep> Steps => _steps;

    /// <summary>
    /// Applies this input sequence to the terminal.
    /// Steps are executed in order, with timing handled by the steps themselves.
    /// </summary>
    /// <param name="terminal">The terminal to apply the sequence to.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task ApplyAsync(Hex1bTerminal terminal, CancellationToken ct = default)
    {
        foreach (var step in _steps)
        {
            ct.ThrowIfCancellationRequested();
            await step.ExecuteAsync(terminal, ct);
        }
    }

    /// <summary>
    /// Applies this input sequence synchronously.
    /// Only works correctly for sequences without delays.
    /// </summary>
    /// <param name="terminal">The terminal to apply the sequence to.</param>
    public void Apply(Hex1bTerminal terminal)
    {
        ApplyAsync(terminal, CancellationToken.None).GetAwaiter().GetResult();
    }
}
