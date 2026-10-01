using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Optionally matches a sub-pattern.
/// </summary>
internal sealed class OptionalStep : IPatternStep
{
    private readonly ImmutableList<IPatternStep> _steps;

    public OptionalStep(ImmutableList<IPatternStep> steps)
    {
        _steps = steps;
    }

    public StepResult Execute(PatternExecutionState state)
    {
        // Save state
        var savedState = state.Clone();

        foreach (var step in _steps)
        {
            var result = step.Execute(state);
            if (!result.Success)
            {
                // Restore state - optional pattern failed
                state.X = savedState.X;
                state.Y = savedState.Y;
                state.TraversedCells.Clear();
                state.TraversedCells.AddRange(savedState.TraversedCells);
                state.PreviousCell = savedState.PreviousCell;
                state.PreviousPosition = savedState.PreviousPosition;
                break;
            }
        }

        return StepResult.Succeeded; // Always succeeds
    }
}
