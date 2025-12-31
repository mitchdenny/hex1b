using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for when a rescue widget catches an exception.
/// </summary>
/// <param name="Widget">The rescue widget that caught the exception.</param>
/// <param name="Node">The rescue node.</param>
/// <param name="Exception">The exception that was caught.</param>
/// <param name="Phase">The phase in which the error occurred.</param>
public sealed record RescueEventArgs(
    RescueWidget Widget,
    RescueNode Node,
    Exception Exception,
    RescueErrorPhase Phase);

/// <summary>
/// Event arguments for when a rescue widget is reset.
/// </summary>
/// <param name="Widget">The rescue widget being reset.</param>
/// <param name="Node">The rescue node.</param>
/// <param name="Exception">The exception that was previously caught (before reset).</param>
/// <param name="Phase">The phase in which the error previously occurred.</param>
public sealed record RescueResetEventArgs(
    RescueWidget Widget,
    RescueNode Node,
    Exception Exception,
    RescueErrorPhase Phase);
