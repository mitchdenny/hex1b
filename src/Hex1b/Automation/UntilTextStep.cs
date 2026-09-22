using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Moves until a text string is found (inclusive).
/// Supports graphemes by selecting adjacent cells for multi-cell characters.
/// </summary>
internal sealed class UntilTextStep : IPatternStep
{
    private readonly Direction _direction;
    private readonly string _text;

    public UntilTextStep(Direction direction, string text)
    {
        _direction = direction;
        _text = text;
    }

    public StepResult Execute(PatternExecutionState state)
    {
        int textIndex = 0;
        
        while (true)
        {
            if (!state.Move(_direction))
                return StepResult.Failed; // Hit boundary without finding match

            var cell = state.Region.GetCell(state.X, state.Y);
            var cellChar = cell.Character;
            
            // Compare character by character within the text
            bool matched = false;
            int remaining = _text.Length - textIndex;
            
            if (remaining > 0 && cellChar.Length > 0)
            {
                // Handle grapheme comparison - cell may contain multiple chars for graphemes
                var textPart = _text.Substring(textIndex, Math.Min(cellChar.Length, remaining));
                if (cellChar == textPart)
                {
                    textIndex += cellChar.Length;
                    matched = true;
                }
            }
            
            if (!matched)
            {
                // Reset and try from this position
                textIndex = 0;
                
                // Check if this cell starts the text
                if (_text.Length > 0 && cellChar.Length > 0)
                {
                    var textPart = _text.Substring(0, Math.Min(cellChar.Length, _text.Length));
                    if (cellChar == textPart)
                    {
                        textIndex = cellChar.Length;
                    }
                }
            }
            
            state.AddTraversedCell();
            
            // For wide characters (East Asian width), the next cell may be a continuation
            // Check if character is likely a wide character by measuring grapheme display width
            int graphemeWidth = GraphemeHelper.GetClusterDisplayWidth(cellChar);
            if (graphemeWidth > 1 && _direction == Direction.Right)
            {
                // Wide character spans two cells, add continuation cells
                for (int i = 1; i < graphemeWidth; i++)
                {
                    if (state.Move(_direction))
                    {
                        state.AddTraversedCell();
                    }
                }
            }
            
            if (textIndex >= _text.Length)
                return StepResult.Succeeded; // Found the complete text
        }
    }
}
