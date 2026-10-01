using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Ends a named capture.
/// </summary>
internal sealed class EndCaptureStep : IPatternStep
{
    public string Name { get; }

    public EndCaptureStep(string name)
    {
        Name = name;
    }

    public StepResult Execute(PatternExecutionState state)
    {
        state.ActiveCaptures.Remove(Name);
        return StepResult.Succeeded;
    }
}
