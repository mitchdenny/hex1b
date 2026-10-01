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
