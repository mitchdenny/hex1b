using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Terminal.Automation;

/// <summary>
/// Base interface for pattern steps.
/// </summary>
internal interface IPatternStep
{
    /// <summary>
    /// Executes the step and returns whether it succeeded.
    /// </summary>
    StepResult Execute(PatternExecutionState state);
}

/// <summary>
/// Result of executing a pattern step.
/// </summary>
internal readonly struct StepResult
{
    public bool Success { get; }
    public List<(int X, int Y)>? StartingPositions { get; }

    private StepResult(bool success, List<(int X, int Y)>? startingPositions = null)
    {
        Success = success;
        StartingPositions = startingPositions;
    }

    public static StepResult Succeeded => new(true);
    public static StepResult Failed => new(false);
    public static StepResult WithStartingPositions(List<(int X, int Y)> positions) => new(true, positions);
}

/// <summary>
/// Mutable state during pattern execution.
/// </summary>
internal sealed class PatternExecutionState
{
    public IHex1bTerminalRegion Region { get; }
    public int X { get; set; }
    public int Y { get; set; }
    public List<TraversedCell> TraversedCells { get; }
    public HashSet<string> ActiveCaptures { get; }
    public TerminalCell MatchStartCell { get; set; }
    public (int X, int Y) MatchStartPosition { get; set; }
    public TerminalCell PreviousCell { get; set; }
    public (int X, int Y) PreviousPosition { get; set; }

    public PatternExecutionState(IHex1bTerminalRegion region)
    {
        Region = region;
        TraversedCells = new List<TraversedCell>();
        ActiveCaptures = new HashSet<string>();
    }

    public CellMatchContext CreateContext()
    {
        return new CellMatchContext(
            Region,
            X,
            Y,
            Region.GetCell(X, Y),
            MatchStartCell,
            MatchStartPosition,
            PreviousCell,
            PreviousPosition,
            TraversedCells);
    }

    public void AddTraversedCell()
    {
        var cell = Region.GetCell(X, Y);
        IReadOnlySet<string>? captures = ActiveCaptures.Count > 0 
            ? new HashSet<string>(ActiveCaptures) 
            : null;
        
        TraversedCells.Add(new TraversedCell(X, Y, cell, captures));
        PreviousCell = cell;
        PreviousPosition = (X, Y);
    }

    public bool Move(Direction direction, int count = 1)
    {
        for (int i = 0; i < count; i++)
        {
            var (dx, dy) = GetDelta(direction);
            var newX = X + dx;
            var newY = Y + dy;

            if (!IsInBounds(newX, newY))
                return false;

            X = newX;
            Y = newY;
        }
        return true;
    }

    public bool IsInBounds(int x, int y) =>
        x >= 0 && x < Region.Width && y >= 0 && y < Region.Height;

    public static (int dx, int dy) GetDelta(Direction direction) => direction switch
    {
        Direction.Right => (1, 0),
        Direction.Left => (-1, 0),
        Direction.Up => (0, -1),
        Direction.Down => (0, 1),
        _ => (0, 0)
    };

    public PatternExecutionState Clone()
    {
        var clone = new PatternExecutionState(Region)
        {
            X = X,
            Y = Y,
            MatchStartCell = MatchStartCell,
            MatchStartPosition = MatchStartPosition,
            PreviousCell = PreviousCell,
            PreviousPosition = PreviousPosition
        };
        clone.TraversedCells.AddRange(TraversedCells);
        foreach (var cap in ActiveCaptures)
            clone.ActiveCaptures.Add(cap);
        return clone;
    }
}

/// <summary>
/// Finds starting positions using a predicate.
/// </summary>
internal sealed class FindPredicateStep : IPatternStep
{
    private readonly Func<CellMatchContext, bool> _predicate;

    public FindPredicateStep(Func<CellMatchContext, bool> predicate)
    {
        _predicate = predicate;
    }

    public StepResult Execute(PatternExecutionState state)
    {
        // This is used during initial position finding
        var context = state.CreateContext();
        return _predicate(context) ? StepResult.Succeeded : StepResult.Failed;
    }

    public List<(int X, int Y)> FindStartingPositions(IHex1bTerminalRegion region)
    {
        var positions = new List<(int X, int Y)>();
        var tempState = new PatternExecutionState(region);
        
        for (int y = 0; y < region.Height; y++)
        {
            for (int x = 0; x < region.Width; x++)
            {
                tempState.X = x;
                tempState.Y = y;
                tempState.MatchStartCell = region.GetCell(x, y);
                tempState.MatchStartPosition = (x, y);
                tempState.PreviousCell = tempState.MatchStartCell;
                tempState.PreviousPosition = (x, y);
                
                var context = tempState.CreateContext();
                if (_predicate(context))
                {
                    positions.Add((x, y));
                }
            }
        }
        
        return positions;
    }
}

/// <summary>
/// Finds starting positions using a regex pattern.
/// </summary>
internal sealed class FindRegexStep : IPatternStep
{
    private readonly Regex _regex;

    public FindRegexStep(Regex regex)
    {
        _regex = regex;
    }

    public StepResult Execute(PatternExecutionState state)
    {
        // Regex steps add the matched characters to traversed cells
        return StepResult.Succeeded;
    }

    public List<(int X, int Y, int Length)> FindStartingPositions(IHex1bTerminalRegion region)
    {
        var positions = new List<(int X, int Y, int Length)>();
        
        for (int y = 0; y < region.Height; y++)
        {
            var line = region.GetLine(y);
            var matches = _regex.Matches(line);
            
            foreach (Match match in matches)
            {
                positions.Add((match.Index, y, match.Length));
            }
        }
        
        return positions;
    }
}

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

/// <summary>
/// Moves while predicate returns true.
/// </summary>
internal sealed class WhileStep : IPatternStep
{
    private readonly Direction _direction;
    private readonly Func<CellMatchContext, bool> _predicate;

    public WhileStep(Direction direction, Func<CellMatchContext, bool> predicate)
    {
        _direction = direction;
        _predicate = predicate;
    }

    public StepResult Execute(PatternExecutionState state)
    {
        while (true)
        {
            // Save position before moving
            var (prevX, prevY) = (state.X, state.Y);

            if (!state.Move(_direction))
                break; // Hit boundary, stop

            var context = state.CreateContext();
            if (!_predicate(context))
            {
                // Restore position - this cell doesn't match
                state.X = prevX;
                state.Y = prevY;
                break;
            }

            state.AddTraversedCell();
        }

        return StepResult.Succeeded; // Zero matches is valid
    }
}

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

/// <summary>
/// Begins a named capture.
/// </summary>
internal sealed class BeginCaptureStep : IPatternStep
{
    public string Name { get; }

    public BeginCaptureStep(string name)
    {
        Name = name;
    }

    public StepResult Execute(PatternExecutionState state)
    {
        state.ActiveCaptures.Add(Name);
        return StepResult.Succeeded;
    }
}

/// <summary>
/// Ends the most recent capture.
/// </summary>
internal sealed class EndCaptureStep : IPatternStep
{
    public StepResult Execute(PatternExecutionState state)
    {
        // The capture stack is managed by the searcher
        // Here we just need to remove captures - but we don't know which one
        // This needs to be handled differently...
        // For now, we'll track this in the executor
        return StepResult.Succeeded;
    }
}

/// <summary>
/// Embeds sub-pattern steps.
/// </summary>
internal sealed class CompositeStep : IPatternStep
{
    private readonly ImmutableList<IPatternStep> _steps;

    public CompositeStep(ImmutableList<IPatternStep> steps)
    {
        _steps = steps;
    }

    public StepResult Execute(PatternExecutionState state)
    {
        foreach (var step in _steps)
        {
            var result = step.Execute(state);
            if (!result.Success)
                return StepResult.Failed;
        }
        return StepResult.Succeeded;
    }
}

/// <summary>
/// Optionally matches a sub-pattern.
/// </summary>
internal sealed class OptionalStep : IPatternStep
{
    private readonly ImmutableList<IPatternStep> _steps;

    public OptionalStep(ImmutableList<IPatternStep> steps)
    {
        _steps = steps;
    }

    public StepResult Execute(PatternExecutionState state)
    {
        // Save state
        var savedState = state.Clone();

        foreach (var step in _steps)
        {
            var result = step.Execute(state);
            if (!result.Success)
            {
                // Restore state - optional pattern failed
                state.X = savedState.X;
                state.Y = savedState.Y;
                state.TraversedCells.Clear();
                state.TraversedCells.AddRange(savedState.TraversedCells);
                state.PreviousCell = savedState.PreviousCell;
                state.PreviousPosition = savedState.PreviousPosition;
                break;
            }
        }

        return StepResult.Succeeded; // Always succeeds
    }
}

/// <summary>
/// Tries first pattern, falls back to second.
/// </summary>
internal sealed class EitherStep : IPatternStep
{
    private readonly ImmutableList<IPatternStep> _first;
    private readonly ImmutableList<IPatternStep> _second;

    public EitherStep(ImmutableList<IPatternStep> first, ImmutableList<IPatternStep> second)
    {
        _first = first;
        _second = second;
    }

    public StepResult Execute(PatternExecutionState state)
    {
        // Try first
        var savedState = state.Clone();
        
        bool firstSucceeded = true;
        foreach (var step in _first)
        {
            var result = step.Execute(state);
            if (!result.Success)
            {
                firstSucceeded = false;
                break;
            }
        }

        if (firstSucceeded)
            return StepResult.Succeeded;

        // Restore and try second
        state.X = savedState.X;
        state.Y = savedState.Y;
        state.TraversedCells.Clear();
        state.TraversedCells.AddRange(savedState.TraversedCells);
        state.PreviousCell = savedState.PreviousCell;
        state.PreviousPosition = savedState.PreviousPosition;

        foreach (var step in _second)
        {
            var result = step.Execute(state);
            if (!result.Success)
                return StepResult.Failed;
        }

        return StepResult.Succeeded;
    }
}

/// <summary>
/// Repeats a pattern.
/// </summary>
internal sealed class RepeatStep : IPatternStep
{
    private readonly ImmutableList<IPatternStep> _steps;
    private readonly int? _count;

    public RepeatStep(ImmutableList<IPatternStep> steps, int? count)
    {
        _steps = steps;
        _count = count;
    }

    public StepResult Execute(PatternExecutionState state)
    {
        int iterations = 0;

        while (true)
        {
            if (_count.HasValue && iterations >= _count.Value)
                break;

            var savedState = state.Clone();
            bool succeeded = true;

            foreach (var step in _steps)
            {
                var result = step.Execute(state);
                if (!result.Success)
                {
                    succeeded = false;
                    break;
                }
            }

            if (!succeeded)
            {
                // Restore state
                state.X = savedState.X;
                state.Y = savedState.Y;
                state.TraversedCells.Clear();
                state.TraversedCells.AddRange(savedState.TraversedCells);
                state.PreviousCell = savedState.PreviousCell;
                state.PreviousPosition = savedState.PreviousPosition;
                break;
            }

            iterations++;

            // If no count specified and no progress, stop to prevent infinite loop
            if (!_count.HasValue && 
                state.X == savedState.X && 
                state.Y == savedState.Y &&
                state.TraversedCells.Count == savedState.TraversedCells.Count)
            {
                break;
            }
        }

        // If count specified, we must match exactly that many times
        if (_count.HasValue && iterations != _count.Value)
            return StepResult.Failed;

        return StepResult.Succeeded;
    }
}
