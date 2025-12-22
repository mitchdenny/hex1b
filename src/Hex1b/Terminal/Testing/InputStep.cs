namespace Hex1b.Terminal.Testing;

/// <summary>
/// Base class for input sequence steps.
/// Each step knows how to execute itself against a terminal.
/// </summary>
public abstract record InputStep
{
    /// <summary>
    /// Executes this step against the terminal.
    /// </summary>
    internal abstract Task ExecuteAsync(Hex1bTerminal terminal, CancellationToken ct);
}
