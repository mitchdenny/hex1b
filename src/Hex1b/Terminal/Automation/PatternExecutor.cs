using System.Collections.Immutable;

namespace Hex1b.Terminal.Automation;

/// <summary>
/// Represents starting position info for pattern matching.
/// </summary>
internal readonly record struct StartPosition(
    int X, int Y,
    int EndX, int EndY,
    int MatchLength,
    int FindStepIndex,
    FindOptions Options);

/// <summary>
/// Executes a pattern against a terminal region.
/// </summary>
internal sealed class PatternExecutor
{
    private readonly IHex1bTerminalRegion _region;
    private readonly ImmutableList<IPatternStep> _steps;

    public PatternExecutor(IHex1bTerminalRegion region, ImmutableList<IPatternStep> steps)
    {
        _region = region;
        _steps = steps;
    }

    /// <summary>
    /// Executes the pattern and returns all matches.
    /// </summary>
    public CellPatternSearchResult Execute()
    {
        if (_steps.Count == 0)
            return CellPatternSearchResult.Empty;

        var matches = new List<CellPatternMatch>();
        var startingPositions = FindStartingPositions();

        foreach (var startPos in startingPositions)
        {
            var match = TryMatchInternal(startPos);
            if (match != null)
            {
                matches.Add(match);
            }
        }

        // Sort by position (top to bottom, left to right)
        matches.Sort((a, b) =>
        {
            var yCompare = a.Start.Y.CompareTo(b.Start.Y);
            return yCompare != 0 ? yCompare : a.Start.X.CompareTo(b.Start.X);
        });

        return new CellPatternSearchResult(matches);
    }

    /// <summary>
    /// Executes the pattern and returns the first match.
    /// </summary>
    public CellPatternMatch? ExecuteFirst()
    {
        if (_steps.Count == 0)
            return null;

        var startingPositions = FindStartingPositions();

        // Sort by position to ensure we get the "first" one
        startingPositions.Sort((a, b) =>
        {
            var yCompare = a.Y.CompareTo(b.Y);
            return yCompare != 0 ? yCompare : a.X.CompareTo(b.X);
        });

        foreach (var startPos in startingPositions)
        {
            var match = TryMatchInternal(startPos);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private List<StartPosition> FindStartingPositions()
    {
        var positions = new List<StartPosition>();

        if (_steps.Count == 0)
            return positions;

        // Find the first Find step (there may be BeginCapture steps before it)
        int findStepIndex = -1;
        for (int i = 0; i < _steps.Count; i++)
        {
            if (_steps[i] is FindPredicateStep or FindRegexStep or FindTextStep or FindMultilineRegexStep)
            {
                findStepIndex = i;
                break;
            }
        }

        if (findStepIndex >= 0)
        {
            var findStep = _steps[findStepIndex];
            
            if (findStep is FindPredicateStep predicateStep)
            {
                var found = predicateStep.FindStartingPositions(_region);
                foreach (var (x, y) in found)
                {
                    positions.Add(new StartPosition(x, y, x, y, 1, findStepIndex, FindOptions.Default));
                }
            }
            else if (findStep is FindTextStep textStep)
            {
                var found = textStep.FindStartingPositions(_region);
                foreach (var (x, y, length) in found)
                {
                    var endX = x + length - 1;
                    positions.Add(new StartPosition(x, y, endX, y, length, findStepIndex, textStep.Options));
                }
            }
            else if (findStep is FindRegexStep regexStep)
            {
                var found = regexStep.FindStartingPositions(_region);
                foreach (var (x, y, length) in found)
                {
                    var endX = x + length - 1;
                    positions.Add(new StartPosition(x, y, endX, y, length, findStepIndex, regexStep.Options));
                }
            }
            else if (findStep is FindMultilineRegexStep multilineStep)
            {
                var found = multilineStep.FindStartingPositions(_region);
                foreach (var match in found)
                {
                    // Calculate total length across lines (approximate for cell count)
                    var lineCount = match.EndY - match.StartY + 1;
                    var totalLength = match.Text.Length; // Use text length as approximation
                    positions.Add(new StartPosition(
                        match.StartX, match.StartY,
                        match.EndX, match.EndY,
                        totalLength, findStepIndex, multilineStep.Options));
                }
            }
        }
        else
        {
            // No Find step - start from every cell
            for (int y = 0; y < _region.Height; y++)
            {
                for (int x = 0; x < _region.Width; x++)
                {
                    positions.Add(new StartPosition(x, y, x, y, 0, -1, FindOptions.Default));
                }
            }
        }

        return positions;
    }

    private CellPatternMatch? TryMatchInternal(StartPosition startPos)
    {
        // Determine starting position based on options
        int cursorX, cursorY;
        if (startPos.Options.CursorPosition == FindCursorPosition.End)
        {
            cursorX = startPos.EndX;
            cursorY = startPos.EndY;
        }
        else
        {
            cursorX = startPos.X;
            cursorY = startPos.Y;
        }
        
        var state = new PatternExecutionState(_region)
        {
            X = cursorX,
            Y = cursorY,
            MatchStartCell = _region.GetCell(startPos.X, startPos.Y),
            MatchStartPosition = (startPos.X, startPos.Y),
            PreviousCell = _region.GetCell(cursorX, cursorY),
            PreviousPosition = (cursorX, cursorY)
        };

        // Track capture stack for proper EndCapture handling
        var captureStack = new Stack<string>();

        // Process steps before Find (typically BeginCapture steps)
        for (int i = 0; i < startPos.FindStepIndex && startPos.FindStepIndex >= 0; i++)
        {
            var step = _steps[i];
            if (step is BeginCaptureStep beginCapture)
            {
                var name = beginCapture.Name;
                captureStack.Push(name);
                state.ActiveCaptures.Add(name);
            }
            else if (step is EndCaptureStep && captureStack.Count > 0)
            {
                var name = captureStack.Pop();
                state.ActiveCaptures.Remove(name);
            }
        }

        // Add initial cells (for Find steps) if IncludeMatchInCells is true
        if (startPos.MatchLength > 0 && startPos.Options.IncludeMatchInCells)
        {
            AddMatchedCells(state, startPos);
        }

        // Execute remaining steps (skip steps up to and including Find)
        int startStepIndex = startPos.FindStepIndex >= 0 ? startPos.FindStepIndex + 1 : 0;
        
        for (int i = startStepIndex; i < _steps.Count; i++)
        {
            var step = _steps[i];
            
            // Handle capture stack
            if (step is BeginCaptureStep beginCapture)
            {
                var name = beginCapture.Name;
                captureStack.Push(name);
                // Execute the step to add to active captures
            }
            else if (step is EndCaptureStep)
            {
                if (captureStack.Count > 0)
                {
                    var name = captureStack.Pop();
                    state.ActiveCaptures.Remove(name);
                    continue; // Don't execute - just manage state
                }
            }

            var result = step.Execute(state);
            if (!result.Success)
            {
                return null; // Pattern failed
            }
        }

        if (state.TraversedCells.Count == 0)
            return null;

        return new CellPatternMatch(state.TraversedCells);
    }

    private void AddMatchedCells(PatternExecutionState state, StartPosition startPos)
    {
        // Save current position
        int savedX = state.X;
        int savedY = state.Y;
        
        // Move to start position and add cells
        state.X = startPos.X;
        state.Y = startPos.Y;
        
        if (startPos.Y == startPos.EndY)
        {
            // Single line match
            for (int x = startPos.X; x <= startPos.EndX; x++)
            {
                state.X = x;
                state.AddTraversedCell();
            }
        }
        else
        {
            // Multiline match - add cells line by line
            for (int y = startPos.Y; y <= startPos.EndY; y++)
            {
                state.Y = y;
                int startX = (y == startPos.Y) ? startPos.X : 0;
                int endX = (y == startPos.EndY) ? startPos.EndX : _region.Width - 1;
                
                for (int x = startX; x <= endX; x++)
                {
                    state.X = x;
                    state.AddTraversedCell();
                }
            }
        }
        
        // Restore cursor position based on options
        if (startPos.Options.CursorPosition == FindCursorPosition.End)
        {
            state.X = startPos.EndX;
            state.Y = startPos.EndY;
        }
        else
        {
            state.X = savedX;
            state.Y = savedY;
        }
    }
}
