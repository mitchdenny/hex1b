using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Matches at the current cursor position (continuation step).
/// Unlike FindPredicateStep, this is for sub-patterns that continue from where we left off.
/// </summary>
internal sealed class MatchPredicateStep : IPatternStep
{
    private readonly Func<CellMatchContext, bool> _predicate;

    public MatchPredicateStep(Func<CellMatchContext, bool> predicate)
    {
        _predicate = predicate;
    }

    public StepResult Execute(PatternExecutionState state)
    {
        var context = state.CreateContext();
        if (_predicate(context))
        {
            state.AddTraversedCell();
            return StepResult.Succeeded;
        }
        return StepResult.Failed;
    }
}
