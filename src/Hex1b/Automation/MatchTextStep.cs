using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Matches exact text at the current cursor position (continuation step).
/// Unlike FindTextStep, this does not search - it matches exactly where the cursor is.
/// After matching, cursor is left on the last matched character (consistent with Find behavior).
/// </summary>
internal sealed class MatchTextStep : IPatternStep
{
    private readonly string _text;

    public MatchTextStep(string text)
    {
        _text = text;
    }

    public StepResult Execute(PatternExecutionState state)
    {
        var region = state.Region;
        int x = state.X;
        int y = state.Y;
        
        int textIndex = 0;
        int lastMatchedX = x;
        int lastMatchedY = y;
        
        while (textIndex < _text.Length)
        {
            if (x >= region.Width)
                return StepResult.Failed;
            
            var cell = region.GetCell(x, y);
            var cellChar = cell.Character;
            
            if (cellChar.Length == 0)
                return StepResult.Failed;
            
            // Handle multi-character graphemes
            if (_text.Length - textIndex >= cellChar.Length &&
                _text.Substring(textIndex, cellChar.Length) == cellChar)
            {
                state.X = x;
                state.Y = y;
                state.AddTraversedCell();
                
                lastMatchedX = x;
                lastMatchedY = y;
                
                textIndex += cellChar.Length;
                x++;
            }
            else
            {
                return StepResult.Failed;
            }
        }
        
        // Leave cursor on the last matched character
        // This is consistent with Find behavior and allows RightWhile to move away
        state.X = lastMatchedX;
        state.Y = lastMatchedY;
        
        return StepResult.Succeeded;
    }
}
