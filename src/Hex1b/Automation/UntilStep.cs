using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Moves until predicate returns true (inclusive).
/// </summary>
internal sealed class UntilStep : IPatternStep
{
    private readonly Direction _direction;
    private readonly Func<CellMatchContext, bool> _predicate;

    public UntilStep(Direction direction, Func<CellMatchContext, bool> predicate)
    {
        _direction = direction;
        _predicate = predicate;
    }

    public StepResult Execute(PatternExecutionState state)
    {
        while (true)
        {
            if (!state.Move(_direction))
                return StepResult.Failed; // Hit boundary without finding match

            state.AddTraversedCell();

            var context = state.CreateContext();
            if (_predicate(context))
                return StepResult.Succeeded; // Found the terminator
        }
    }
}
