using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

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
