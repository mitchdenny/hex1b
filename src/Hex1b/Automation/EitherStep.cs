using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Tries first pattern, falls back to second.
/// </summary>
internal sealed class EitherStep : IPatternStep
{
    private readonly ImmutableList<IPatternStep> _first;
    private readonly ImmutableList<IPatternStep> _second;

    public EitherStep(ImmutableList<IPatternStep> first, ImmutableList<IPatternStep> second)
    {
        _first = first;
        _second = second;
    }

    public StepResult Execute(PatternExecutionState state)
    {
        // Try first
        var savedState = state.Clone();
        
        bool firstSucceeded = true;
        foreach (var step in _first)
        {
            var result = step.Execute(state);
            if (!result.Success)
            {
                firstSucceeded = false;
                break;
            }
        }

        if (firstSucceeded)
            return StepResult.Succeeded;

        // Restore and try second
        state.X = savedState.X;
        state.Y = savedState.Y;
        state.TraversedCells.Clear();
        state.TraversedCells.AddRange(savedState.TraversedCells);
        state.PreviousCell = savedState.PreviousCell;
        state.PreviousPosition = savedState.PreviousPosition;

        foreach (var step in _second)
        {
            var result = step.Execute(state);
            if (!result.Success)
                return StepResult.Failed;
        }

        return StepResult.Succeeded;
    }
}
