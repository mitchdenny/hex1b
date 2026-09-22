using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Moves while predicate returns true.
/// Moves first, then checks. Cursor ends at the last matching position (or original if none matched).
/// </summary>
internal sealed class WhileStep : IPatternStep
{
    private readonly Direction _direction;
    private readonly Func<CellMatchContext, bool> _predicate;

    public WhileStep(Direction direction, Func<CellMatchContext, bool> predicate)
    {
        _direction = direction;
        _predicate = predicate;
    }

    public StepResult Execute(PatternExecutionState state)
    {
        while (true)
        {
            // Save position before moving
            var (prevX, prevY) = (state.X, state.Y);

            if (!state.Move(_direction))
                break; // Hit boundary, stop

            var context = state.CreateContext();
            if (!_predicate(context))
            {
                // Restore position - this cell doesn't match
                state.X = prevX;
                state.Y = prevY;
                break;
            }

            state.AddTraversedCell();
        }

        return StepResult.Succeeded; // Zero matches is valid
    }
}
