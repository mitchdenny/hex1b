using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Matches a sequence of characters.
/// </summary>
internal sealed class TextSequenceStep : IPatternStep
{
    private readonly Direction _direction;
    private readonly string _text;

    public TextSequenceStep(Direction direction, string text)
    {
        _direction = direction;
        _text = text;
    }

    public StepResult Execute(PatternExecutionState state)
    {
        foreach (var c in _text)
        {
            if (!state.Move(_direction))
                return StepResult.Failed;

            var cell = state.Region.GetCell(state.X, state.Y);
            if (cell.Character != c.ToString())
                return StepResult.Failed;

            state.AddTraversedCell();
        }

        return StepResult.Succeeded;
    }
}
