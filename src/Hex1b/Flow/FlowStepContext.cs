using Hex1b.Widgets;

namespace Hex1b.Flow;

/// <summary>
/// Context passed to a flow step's widget builder. Extends <see cref="RootContext"/>
/// with a <see cref="Step"/> property that provides access to the <see cref="FlowStep"/>
/// handle, enabling event handlers to call <see cref="FlowStep.Complete()"/>,
/// <see cref="FlowStep.Invalidate"/>, etc. without needing a separate variable.
/// </summary>
public class FlowStepContext : RootContext
{
    internal FlowStepContext(FlowStep step)
    {
        Step = step;
    }

    /// <summary>
    /// The <see cref="FlowStep"/> handle for the currently running step.
    /// Use this in event handlers to invalidate, complete, or request focus changes.
    /// </summary>
    public FlowStep Step { get; }
}
