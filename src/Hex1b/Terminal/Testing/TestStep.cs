namespace Hex1b.Terminal.Testing;

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
        Hex1bTestSequenceOptions options,
        CancellationToken ct);
}
