using System.Collections.Immutable;

namespace Hex1b.Terminal.Automation;

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
            var match = TryMatchInternal(startPos.X, startPos.Y, startPos.InitialCellsToAdd, startPos.FindStepIndex);
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
            var match = TryMatchInternal(startPos.X, startPos.Y, startPos.InitialCellsToAdd, startPos.FindStepIndex);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private List<(int X, int Y, int InitialCellsToAdd, int FindStepIndex)> FindStartingPositions()
    {
        var positions = new List<(int X, int Y, int InitialCellsToAdd, int FindStepIndex)>();

        if (_steps.Count == 0)
            return positions;

        // Find the first Find step (there may be BeginCapture steps before it)
        int findStepIndex = -1;
        for (int i = 0; i < _steps.Count; i++)
        {
            if (_steps[i] is FindPredicateStep or FindRegexStep)
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
                    positions.Add((x, y, 1, findStepIndex)); // Add the starting cell
                }
            }
            else if (findStep is FindRegexStep regexStep)
            {
                var found = regexStep.FindStartingPositions(_region);
                foreach (var (x, y, length) in found)
                {
                    positions.Add((x, y, length, findStepIndex)); // Add all matched cells
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
                    positions.Add((x, y, 0, -1));
                }
            }
        }

        return positions;
    }

    private CellPatternMatch? TryMatchInternal(int startX, int startY, int initialCellsToAdd, int findStepIndex)
    {
        var state = new PatternExecutionState(_region)
        {
            X = startX,
            Y = startY,
            MatchStartCell = _region.GetCell(startX, startY),
            MatchStartPosition = (startX, startY),
            PreviousCell = _region.GetCell(startX, startY),
            PreviousPosition = (startX, startY)
        };

        // Track capture stack for proper EndCapture handling
        var captureStack = new Stack<string>();

        // Process steps before Find (typically BeginCapture steps)
        for (int i = 0; i < findStepIndex && findStepIndex >= 0; i++)
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

        // Add initial cells (for Find steps) - with active captures applied
        if (initialCellsToAdd > 0)
        {
            for (int i = 0; i < initialCellsToAdd; i++)
            {
                state.AddTraversedCell();
                if (i < initialCellsToAdd - 1)
                {
                    state.X++;
                }
            }
            // Position is now at the last character of the initial match
        }

        // Execute remaining steps (skip steps up to and including Find)
        int startStepIndex = findStepIndex >= 0 ? findStepIndex + 1 : 0;
        
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

    private static string GetCaptureName(BeginCaptureStep step)
    {
        return step.Name;
    }
}
