namespace Hex1b.Terminal.Testing;

/// <summary>
/// A sequence of test steps that can be applied to a terminal.
/// Built using <see cref="Hex1bTestSequenceBuilder"/>.
/// </summary>
public sealed class Hex1bTestSequence
{
    private readonly IReadOnlyList<TestStep> _steps;
    private readonly Hex1bTestSequenceOptions _options;

    internal Hex1bTestSequence(IReadOnlyList<TestStep> steps, Hex1bTestSequenceOptions options)
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
    public Hex1bTestSequenceOptions Options => _options;

    /// <summary>
    /// Applies this test sequence to the terminal.
    /// Steps are executed in order, with timing handled by the steps themselves.
    /// </summary>
    /// <param name="terminal">The terminal to apply the sequence to.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task ApplyAsync(Hex1bTerminal terminal, CancellationToken ct = default)
    {
        foreach (var step in _steps)
        {
            ct.ThrowIfCancellationRequested();
            await step.ExecuteAsync(terminal, _options, ct);
        }
    }

    /// <summary>
    /// Applies this test sequence synchronously.
    /// Only works correctly for sequences without delays or wait conditions.
    /// </summary>
    /// <param name="terminal">The terminal to apply the sequence to.</param>
    public void Apply(Hex1bTerminal terminal)
    {
        ApplyAsync(terminal, CancellationToken.None).GetAwaiter().GetResult();
    }
}
