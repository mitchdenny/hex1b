using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Embeds sub-pattern steps.
/// </summary>
internal sealed class CompositeStep : IPatternStep
{
    private readonly ImmutableList<IPatternStep> _steps;

    public CompositeStep(ImmutableList<IPatternStep> steps)
    {
        _steps = steps;
    }

    public StepResult Execute(PatternExecutionState state)
    {
        foreach (var step in _steps)
        {
            var result = step.Execute(state);
            if (!result.Success)
                return StepResult.Failed;
        }
        return StepResult.Succeeded;
    }
}
