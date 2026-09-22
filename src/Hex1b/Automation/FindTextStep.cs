using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Finds starting positions using an exact text match.
/// When used as a continuation step (inside ThenEither, etc.), matches text at current position.
/// </summary>
internal sealed class FindTextStep : IPatternStep
{
    private readonly string _text;
    private readonly FindOptions _options;

    public FindTextStep(string text, FindOptions options)
    {
        _text = text;
        _options = options;
    }

    public FindOptions Options => _options;

    public StepResult Execute(PatternExecutionState state)
    {
        // When used as a continuation step, match text at current position
        var region = state.Region;
        int x = state.X;
        int y = state.Y;
        
        // Check if we can match the text starting from current position
        int textIndex = 0;
        int startX = x;
        int startY = y;
        
        while (textIndex < _text.Length)
        {
            if (x >= region.Width)
                return StepResult.Failed;
            
            var cell = region.GetCell(x, y);
            var cellChar = cell.Character;
            
            // Check if this cell's character matches the expected part of the text
            if (cellChar.Length == 0)
                return StepResult.Failed;
            
            // Handle multi-character graphemes
            if (_text.Length - textIndex >= cellChar.Length &&
                _text.Substring(textIndex, cellChar.Length) == cellChar)
            {
                // Add to traversed cells if option is set
                if (_options.IncludeMatchInCells)
                {
                    state.X = x;
                    state.Y = y;
                    state.AddTraversedCell();
                }
                
                textIndex += cellChar.Length;
                x++;
            }
            else
            {
                return StepResult.Failed;
            }
        }
        
        // Position cursor based on options
        if (_options.CursorPosition == FindCursorPosition.End)
        {
            state.X = x; // After the match
            state.Y = y;
        }
        else
        {
            state.X = startX; // At start of match
            state.Y = startY;
        }
        
        return StepResult.Succeeded;
    }

    public List<(int X, int Y, int Length)> FindStartingPositions(IHex1bTerminalRegion region)
    {
        var positions = new List<(int X, int Y, int Length)>();
        
        for (int y = 0; y < region.Height; y++)
        {
            var line = region.GetLine(y);
            int index = 0;
            
            while ((index = line.IndexOf(_text, index, StringComparison.Ordinal)) >= 0)
            {
                positions.Add((index, y, _text.Length));
                index++;
            }
        }
        
        return positions;
    }
}
