using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Repeats a pattern.
/// </summary>
internal sealed class RepeatStep : IPatternStep
{
    private readonly ImmutableList<IPatternStep> _steps;
    private readonly int? _count;

    public RepeatStep(ImmutableList<IPatternStep> steps, int? count)
    {
        _steps = steps;
        _count = count;
    }

    public StepResult Execute(PatternExecutionState state)
    {
        int iterations = 0;

        while (true)
        {
            if (_count.HasValue && iterations >= _count.Value)
                break;

            var savedState = state.Clone();
            bool succeeded = true;

            foreach (var step in _steps)
            {
                var result = step.Execute(state);
                if (!result.Success)
                {
                    succeeded = false;
                    break;
                }
            }

            if (!succeeded)
            {
                // Restore state
                state.X = savedState.X;
                state.Y = savedState.Y;
                state.TraversedCells.Clear();
                state.TraversedCells.AddRange(savedState.TraversedCells);
                state.PreviousCell = savedState.PreviousCell;
                state.PreviousPosition = savedState.PreviousPosition;
                break;
            }

            iterations++;

            // If no count specified and no progress, stop to prevent infinite loop
            if (!_count.HasValue && 
                state.X == savedState.X && 
                state.Y == savedState.Y &&
                state.TraversedCells.Count == savedState.TraversedCells.Count)
            {
                break;
            }
        }

        // If count specified, we must match exactly that many times
        if (_count.HasValue && iterations != _count.Value)
            return StepResult.Failed;

        return StepResult.Succeeded;
    }
}
