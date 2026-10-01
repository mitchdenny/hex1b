using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Begins a named capture.
/// </summary>
internal sealed class BeginCaptureStep : IPatternStep
{
    public string Name { get; }

    public BeginCaptureStep(string name)
    {
        Name = name;
    }

    public StepResult Execute(PatternExecutionState state)
    {
        state.ActiveCaptures.Add(Name);
        return StepResult.Succeeded;
    }
}
