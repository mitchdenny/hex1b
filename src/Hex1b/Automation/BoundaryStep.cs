using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Moves to a boundary (end of line, start of line, top, or bottom).
/// </summary>
internal sealed class BoundaryStep : IPatternStep
{
    private readonly Direction _direction;

    public BoundaryStep(Direction direction)
    {
        _direction = direction;
    }

    public StepResult Execute(PatternExecutionState state)
    {
        int target = _direction switch
        {
            Direction.Right => state.Region.Width - 1,
            Direction.Left => 0,
            Direction.Down => state.Region.Height - 1,
            Direction.Up => 0,
            _ => 0
        };

        while (true)
        {
            var currentPos = _direction == Direction.Right || _direction == Direction.Left 
                ? state.X 
                : state.Y;
            
            if (currentPos == target)
                break;

            if (!state.Move(_direction))
                break;

            state.AddTraversedCell();
        }

        return StepResult.Succeeded;
    }
}
