using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// Arguments provided to the WhenNotRunning callback.
/// </summary>
/// <param name="Handle">The terminal handle.</param>
/// <param name="State">The current terminal state.</param>
/// <param name="ExitCode">The exit code if the terminal completed, null otherwise.</param>
public sealed record TerminalNotRunningArgs(
    TerminalWidgetHandle Handle,
    TerminalState State,
    int? ExitCode);
