using System.Text;
using Hex1b.Layout;

namespace Hex1b.Terminal.Automation;

/// <summary>
/// Represents a single cell that was traversed during pattern matching.
/// </summary>
/// <param name="X">X coordinate of the cell.</param>
/// <param name="Y">Y coordinate of the cell.</param>
/// <param name="Cell">The terminal cell at this position.</param>
/// <param name="CaptureNames">Names of captures this cell belongs to, or null if none.</param>
public readonly record struct TraversedCell(
    int X,
    int Y,
    TerminalCell Cell,
    IReadOnlySet<string>? CaptureNames);

/// <summary>
/// Represents a successful pattern match containing all traversed cells
/// and capture information.
/// </summary>
public sealed class CellPatternMatch
{
    private readonly List<TraversedCell> _cells;
    private readonly Dictionary<string, List<TraversedCell>> _captures;
    private Rect? _bounds;
    private string? _text;

    internal CellPatternMatch(List<TraversedCell> cells)
    {
        _cells = cells;
        _captures = new Dictionary<string, List<TraversedCell>>();
        
        // Build capture index
        foreach (var cell in cells)
        {
            if (cell.CaptureNames != null)
            {
                foreach (var name in cell.CaptureNames)
                {
                    if (!_captures.TryGetValue(name, out var list))
                    {
                        list = new List<TraversedCell>();
                        _captures[name] = list;
                    }
                    list.Add(cell);
                }
            }
        }
    }

    /// <summary>
    /// All cells traversed in order.
    /// </summary>
    public IReadOnlyList<TraversedCell> Cells => _cells;

    /// <summary>
    /// Bounding rectangle containing all matched cells.
    /// </summary>
    public Rect Bounds
    {
        get
        {
            if (_bounds == null)
            {
                _bounds = CalculateBounds(_cells);
            }
            return _bounds.Value;
        }
    }

    /// <summary>
    /// Starting position of the match (first traversed cell).
    /// </summary>
    public (int X, int Y) Start => _cells.Count > 0 ? (_cells[0].X, _cells[0].Y) : (0, 0);

    /// <summary>
    /// Ending position of the match (last traversed cell).
    /// </summary>
    public (int X, int Y) End => _cells.Count > 0 ? (_cells[^1].X, _cells[^1].Y) : (0, 0);

    /// <summary>
    /// Gets the text content of all matched cells.
    /// </summary>
    public string Text
    {
        get
        {
            if (_text == null)
            {
                _text = BuildText(_cells);
            }
            return _text;
        }
    }

    /// <summary>
    /// Names of all captures in this match.
    /// </summary>
    public IReadOnlySet<string> CaptureNames => _captures.Keys.ToHashSet();

    /// <summary>
    /// Checks if a capture with the given name exists.
    /// </summary>
    public bool HasCapture(string name) => _captures.ContainsKey(name);

    /// <summary>
    /// Gets cells belonging to a named capture.
    /// Returns empty list if capture doesn't exist.
    /// </summary>
    public IReadOnlyList<TraversedCell> GetCapture(string name) =>
        _captures.TryGetValue(name, out var cells) ? cells : Array.Empty<TraversedCell>();

    /// <summary>
    /// Gets the text content of a named capture.
    /// Returns empty string if capture doesn't exist.
    /// </summary>
    public string GetCaptureText(string name) =>
        _captures.TryGetValue(name, out var cells) ? BuildText(cells) : "";

    /// <summary>
    /// Gets the bounding rectangle of a named capture.
    /// Returns empty rect if capture doesn't exist.
    /// </summary>
    public Rect GetCaptureBounds(string name) =>
        _captures.TryGetValue(name, out var cells) ? CalculateBounds(cells) : new Rect(0, 0, 0, 0);

    private static string BuildText(IReadOnlyList<TraversedCell> cells)
    {
        if (cells.Count == 0)
            return "";

        var sb = new StringBuilder();
        int? lastY = null;

        foreach (var cell in cells)
        {
            // Add newline when moving to a different row
            if (lastY.HasValue && cell.Y != lastY.Value)
            {
                sb.AppendLine();
            }
            lastY = cell.Y;

            var ch = cell.Cell.Character;
            if (!string.IsNullOrEmpty(ch) && ch != "\0")
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    private static Rect CalculateBounds(IReadOnlyList<TraversedCell> cells)
    {
        if (cells.Count == 0)
            return new Rect(0, 0, 0, 0);

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (var cell in cells)
        {
            minX = Math.Min(minX, cell.X);
            minY = Math.Min(minY, cell.Y);
            maxX = Math.Max(maxX, cell.X);
            maxY = Math.Max(maxY, cell.Y);
        }

        return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
}

/// <summary>
/// Collection of all pattern matches found in a search.
/// </summary>
public sealed class CellPatternSearchResult
{
    private readonly List<CellPatternMatch> _matches;

    internal CellPatternSearchResult(List<CellPatternMatch> matches)
    {
        _matches = matches;
    }

    /// <summary>
    /// All matches found.
    /// </summary>
    public IReadOnlyList<CellPatternMatch> Matches => _matches;

    /// <summary>
    /// Whether any matches were found.
    /// </summary>
    public bool HasMatches => _matches.Count > 0;

    /// <summary>
    /// Number of matches found.
    /// </summary>
    public int Count => _matches.Count;

    /// <summary>
    /// First match, or null if none.
    /// </summary>
    public CellPatternMatch? First => _matches.Count > 0 ? _matches[0] : null;

    /// <summary>
    /// Creates an empty result.
    /// </summary>
    internal static CellPatternSearchResult Empty => new(new List<CellPatternMatch>());
}
