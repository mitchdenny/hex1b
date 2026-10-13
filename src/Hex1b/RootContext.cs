using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Root context for starting widget tree construction.
/// </summary>
public class RootContext : WidgetContext<RootWidget>
{
    internal TerminalProgramStatusReporter? ProgramStatusReporter { get; set; }

    /// <summary>Gets the application's program-status reporting service.</summary>
    /// <exception cref="InvalidOperationException">This context is not attached to an application.</exception>
    public TerminalProgramStatusReporter ProgramStatus => ProgramStatusReporter
        ?? throw new InvalidOperationException("This context is not attached to a Hex1bApp.");

    /// <summary>
    /// The cancellation token for the application lifecycle.
    /// Use this to observe when the application is shutting down.
    /// </summary>
    public CancellationToken CancellationToken { get; internal set; }
}
