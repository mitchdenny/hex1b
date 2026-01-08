namespace Hex1b.Terminal.Automation;

/// <summary>
/// Context provided to cell predicates during pattern matching.
/// Contains information about the current position, cell, and match state.
/// </summary>
public sealed class CellMatchContext
{
    private readonly IHex1bTerminalRegion _region;
    private readonly List<TraversedCell> _traversedCells;
    private readonly TerminalCell _matchStartCell;
    private readonly (int X, int Y) _matchStartPosition;

    internal CellMatchContext(
        IHex1bTerminalRegion region,
        int x,
        int y,
        TerminalCell cell,
        TerminalCell matchStartCell,
        (int X, int Y) matchStartPosition,
        TerminalCell previousCell,
        (int X, int Y) previousPosition,
        List<TraversedCell> traversedCells)
    {
        _region = region;
        X = x;
        Y = y;
        Cell = cell;
        _matchStartCell = matchStartCell;
        _matchStartPosition = matchStartPosition;
        PreviousCell = previousCell;
        PreviousPosition = previousPosition;
        _traversedCells = traversedCells;
    }

    /// <summary>
    /// The region being searched.
    /// </summary>
    public IHex1bTerminalRegion Region => _region;

    /// <summary>
    /// Current X position in the region.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Current Y position in the region.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// The cell at the current position.
    /// </summary>
    public TerminalCell Cell { get; }

    /// <summary>
    /// The first cell of this match (where Find() matched).
    /// </summary>
    public TerminalCell MatchStartCell => _matchStartCell;

    /// <summary>
    /// The position where this match started.
    /// </summary>
    public (int X, int Y) MatchStartPosition => _matchStartPosition;

    /// <summary>
    /// The cell from the previous step, or MatchStartCell if this is the first step after Find.
    /// </summary>
    public TerminalCell PreviousCell { get; }

    /// <summary>
    /// The position of the previous step.
    /// </summary>
    public (int X, int Y) PreviousPosition { get; }

    /// <summary>
    /// All cells traversed so far in this match attempt.
    /// </summary>
    public IReadOnlyList<TraversedCell> TraversedCells => _traversedCells;

    /// <summary>
    /// Gets the cell at a relative offset from current position.
    /// Returns TerminalCell.Empty if out of bounds.
    /// </summary>
    public TerminalCell GetRelative(int deltaX, int deltaY)
    {
        return _region.GetCell(X + deltaX, Y + deltaY);
    }

    /// <summary>
    /// Gets the cell at an absolute position.
    /// Returns TerminalCell.Empty if out of bounds.
    /// </summary>
    public TerminalCell GetAbsolute(int x, int y)
    {
        return _region.GetCell(x, y);
    }

    /// <summary>
    /// Creates a new context with updated position and cell information.
    /// </summary>
    internal CellMatchContext MoveTo(int newX, int newY)
    {
        var newCell = _region.GetCell(newX, newY);
        return new CellMatchContext(
            _region,
            newX,
            newY,
            newCell,
            _matchStartCell,
            _matchStartPosition,
            Cell,
            (X, Y),
            _traversedCells);
    }

    /// <summary>
    /// Creates a context for a new match starting position.
    /// </summary>
    internal static CellMatchContext CreateInitial(
        IHex1bTerminalRegion region,
        int x,
        int y,
        List<TraversedCell> traversedCells)
    {
        var cell = region.GetCell(x, y);
        return new CellMatchContext(
            region,
            x,
            y,
            cell,
            cell,
            (x, y),
            cell,
            (x, y),
            traversedCells);
    }
}
