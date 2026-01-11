using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Specifies where to position the cursor after a Find match.
/// </summary>
public enum FindCursorPosition
{
    /// <summary>
    /// Position cursor at the start of the match (first character).
    /// </summary>
    Start,
    
    /// <summary>
    /// Position cursor at the end of the match (last character).
    /// </summary>
    End
}

/// <summary>
/// Options for Find pattern methods.
/// </summary>
public readonly struct FindOptions
{
    /// <summary>
    /// Default options: cursor at end of match, match cells included.
    /// </summary>
    public static readonly FindOptions Default = new(FindCursorPosition.End, true);
    
    private readonly FindCursorPosition _cursorPosition;
    private readonly bool _includeMatchInCells;
    private readonly bool _isExplicitlySet;
    
    /// <summary>
    /// Where to leave the cursor after matching.
    /// Default is End (after the match) for natural pattern chaining.
    /// </summary>
    public FindCursorPosition CursorPosition => _isExplicitlySet ? _cursorPosition : FindCursorPosition.End;
    
    /// <summary>
    /// Whether to include the matched cells in the result.
    /// When default-initialized, returns true (the default behavior).
    /// </summary>
    public bool IncludeMatchInCells => _isExplicitlySet ? _includeMatchInCells : true;
    
    /// <summary>
    /// Creates FindOptions with the specified settings.
    /// </summary>
    public FindOptions(FindCursorPosition cursorPosition = FindCursorPosition.End, bool includeMatchInCells = true)
    {
        _cursorPosition = cursorPosition;
        _includeMatchInCells = includeMatchInCells;
        _isExplicitlySet = true;
    }
}

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

/// <summary>
/// Finds starting positions using a regex pattern (single-line).
/// Uses the existing FindPattern infrastructure.
/// </summary>
internal sealed class FindRegexStep : IPatternStep
{
    private readonly Regex _regex;
    private readonly FindOptions _options;

    public FindRegexStep(Regex regex, FindOptions options = default)
    {
        _regex = regex;
        _options = options;
    }

    public FindOptions Options => _options;

    public StepResult Execute(PatternExecutionState state)
    {
        return StepResult.Succeeded;
    }

    public List<(int X, int Y, int Length)> FindStartingPositions(IHex1bTerminalRegion region)
    {
        var positions = new List<(int X, int Y, int Length)>();
        
        // Use existing FindPattern infrastructure
        var matches = region.FindPattern(_regex);
        
        foreach (var match in matches)
        {
            positions.Add((match.StartColumn, match.Line, match.Length));
        }
        
        return positions;
    }
}

/// <summary>
/// Finds starting positions using a regex pattern that can span multiple lines.
/// Uses the existing FindMultiLinePattern infrastructure.
/// </summary>
internal sealed class FindMultilineRegexStep : IPatternStep
{
    private readonly Regex _regex;
    private readonly FindOptions _options;
    private readonly bool _trimLines;
    private readonly string? _lineSeparator;

    public FindMultilineRegexStep(Regex regex, FindOptions options = default, bool trimLines = false, string? lineSeparator = "\n")
    {
        _regex = regex;
        _options = options;
        _trimLines = trimLines;
        _lineSeparator = lineSeparator;
    }

    public FindOptions Options => _options;

    public StepResult Execute(PatternExecutionState state)
    {
        return StepResult.Succeeded;
    }

    public List<MultilineMatchPosition> FindStartingPositions(IHex1bTerminalRegion region)
    {
        var positions = new List<MultilineMatchPosition>();
        
        // Use existing FindMultiLinePattern infrastructure
        var matches = region.FindMultiLinePattern(_regex, _trimLines, _lineSeparator);
        
        foreach (var match in matches)
        {
            positions.Add(new MultilineMatchPosition(
                match.StartColumn, match.StartLine,
                match.EndColumn, match.EndLine,
                match.Text));
        }
        
        return positions;
    }
}

/// <summary>
/// Position info for a multiline match.
/// </summary>
internal readonly record struct MultilineMatchPosition(
    int StartX, int StartY,
    int EndX, int EndY,
    string Text);

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
/// Moves first, then checks. Cursor ends at the last matching position (or original if none matched).
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
/// Ends a named capture.
/// </summary>
internal sealed class EndCaptureStep : IPatternStep
{
    public string Name { get; }

    public EndCaptureStep(string name)
    {
        Name = name;
    }

    public StepResult Execute(PatternExecutionState state)
    {
        state.ActiveCaptures.Remove(Name);
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
