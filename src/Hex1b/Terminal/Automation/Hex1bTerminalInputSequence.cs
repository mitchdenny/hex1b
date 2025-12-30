namespace Hex1b.Terminal.Automation;

/// <summary>
/// A sequence of test steps that can be applied to a terminal.
/// Built using <see cref="Hex1bTerminalInputSequenceBuilder"/>.
/// </summary>
public sealed class Hex1bTerminalInputSequence
{
    private readonly IReadOnlyList<TestStep> _steps;
    private readonly Hex1bTerminalInputSequenceOptions _options;

    internal Hex1bTerminalInputSequence(IReadOnlyList<TestStep> steps, Hex1bTerminalInputSequenceOptions options)
    {
        _steps = steps;
        _options = options;
    }

    /// <summary>
    /// Gets the steps in this sequence.
    /// </summary>
    public IReadOnlyList<TestStep> Steps => _steps;

    /// <summary>
    /// Gets the options for this sequence.
    /// </summary>
    public Hex1bTerminalInputSequenceOptions Options => _options;

    /// <summary>
    /// Applies this test sequence to the terminal.
    /// Steps are executed in order, with timing handled by the steps themselves.
    /// Returns a snapshot of the terminal state after the sequence completes.
    /// </summary>
    /// <param name="terminal">The terminal to apply the sequence to.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A snapshot of the terminal state after all steps have been executed.</returns>
    public async Task<Hex1bTerminalSnapshot> ApplyAsync(Hex1bTerminal terminal, CancellationToken ct = default)
    {
        foreach (var step in _steps)
        {
            ct.ThrowIfCancellationRequested();
            await step.ExecuteAsync(terminal, _options, ct);
        }
        return terminal.CreateSnapshot();
    }

    /// <summary>
    /// Applies this test sequence synchronously.
    /// Only works correctly for sequences without delays or wait conditions.
    /// Returns a snapshot of the terminal state after the sequence completes.
    /// </summary>
    /// <param name="terminal">The terminal to apply the sequence to.</param>
    /// <returns>A snapshot of the terminal state after all steps have been executed.</returns>
    public Hex1bTerminalSnapshot Apply(Hex1bTerminal terminal)
    {
        return ApplyAsync(terminal, CancellationToken.None).GetAwaiter().GetResult();
    }
}
