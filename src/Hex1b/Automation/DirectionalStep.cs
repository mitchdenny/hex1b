using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Moves in a direction and optionally matches a predicate.
/// </summary>
internal sealed class DirectionalStep : IPatternStep
{
    private readonly Direction _direction;
    private readonly int _count;
    private readonly Func<CellMatchContext, bool>? _predicate;

    public DirectionalStep(Direction direction, int count, Func<CellMatchContext, bool>? predicate)
    {
        _direction = direction;
        _count = count;
        _predicate = predicate;
    }

    public StepResult Execute(PatternExecutionState state)
    {
        for (int i = 0; i < _count; i++)
        {
            if (!state.Move(_direction))
                return StepResult.Failed;

            if (_predicate != null)
            {
                var context = state.CreateContext();
                if (!_predicate(context))
                    return StepResult.Failed;
            }

            state.AddTraversedCell();
        }

        return StepResult.Succeeded;
    }
}
