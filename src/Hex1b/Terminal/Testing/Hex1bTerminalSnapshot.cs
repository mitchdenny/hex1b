namespace Hex1b.Terminal.Testing;

/// <summary>
/// An immutable snapshot of terminal state at a point in time.
/// Used for assertions and wait conditions in test sequences.
/// </summary>
public sealed class Hex1bTerminalSnapshot
{
    private readonly TerminalCell[,] _cells;
    private readonly string[] _lineCache;

    internal Hex1bTerminalSnapshot(Hex1bTerminal terminal)
    {
        Terminal = terminal;
        Width = terminal.Width;
        Height = terminal.Height;
        CursorX = terminal.CursorX;
        CursorY = terminal.CursorY;
        InAlternateScreen = terminal.InAlternateScreen;
        Timestamp = DateTimeOffset.UtcNow;
        RawOutput = terminal.RawOutput;

        // Get a deep copy of the cell buffer
        _cells = terminal.GetScreenBuffer();

        // Pre-compute line cache for efficient text operations
        _lineCache = new string[Height];
        for (int y = 0; y < Height; y++)
        {
            _lineCache[y] = BuildLine(y);
        }
    }

    /// <summary>
    /// Reference to the live terminal (for advanced scenarios).
    /// </summary>
    public Hex1bTerminal Terminal { get; }

    /// <summary>
    /// Terminal width at snapshot time.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Terminal height at snapshot time.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Cursor X position at snapshot time.
    /// </summary>
    public int CursorX { get; }

    /// <summary>
    /// Cursor Y position at snapshot time.
    /// </summary>
    public int CursorY { get; }

    /// <summary>
    /// When the snapshot was taken.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Whether the terminal was in alternate screen mode at snapshot time.
    /// </summary>
    public bool InAlternateScreen { get; }

    /// <summary>
    /// Raw ANSI output at snapshot time.
    /// </summary>
    public string RawOutput { get; }

    /// <summary>
    /// Gets the cell at the specified position.
    /// </summary>
    public TerminalCell GetCell(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return TerminalCell.Empty;
        return _cells[y, x];
    }

    /// <summary>
    /// Gets the text content of a line.
    /// </summary>
    public string GetLine(int y)
    {
        if (y < 0 || y >= Height)
            return "";
        return _lineCache[y];
    }

    /// <summary>
    /// Gets the text content of a line with trailing whitespace removed.
    /// </summary>
    public string GetLineTrimmed(int y) => GetLine(y).TrimEnd();

    /// <summary>
    /// Checks if the terminal contains the specified text anywhere.
    /// </summary>
    public bool ContainsText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return true;

        for (int y = 0; y < Height; y++)
        {
            if (_lineCache[y].Contains(text, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Gets all non-empty lines from the terminal.
    /// </summary>
    public IEnumerable<string> GetNonEmptyLines()
    {
        for (int y = 0; y < Height; y++)
        {
            var line = GetLineTrimmed(y);
            if (!string.IsNullOrEmpty(line))
                yield return line;
        }
    }

    /// <summary>
    /// Gets the full screen text with all lines separated by newlines.
    /// </summary>
    public string GetScreenText()
    {
        return string.Join("\n", _lineCache);
    }

    /// <summary>
    /// Gets all lines as a single string for display/debugging (non-empty lines only).
    /// </summary>
    public string GetDisplayText()
    {
        return string.Join("\n", GetNonEmptyLines());
    }

    /// <summary>
    /// Finds all occurrences of the specified text on the screen.
    /// Returns a list of (line, column) positions.
    /// </summary>
    public List<(int Line, int Column)> FindText(string text)
    {
        var results = new List<(int, int)>();
        if (string.IsNullOrEmpty(text))
            return results;

        for (int y = 0; y < Height; y++)
        {
            var line = _lineCache[y];
            var index = 0;
            while ((index = line.IndexOf(text, index, StringComparison.Ordinal)) >= 0)
            {
                results.Add((y, index));
                index++;
            }
        }
        return results;
    }

    private string BuildLine(int y)
    {
        var chars = new char[Width];
        for (int x = 0; x < Width; x++)
        {
            var cell = _cells[y, x];
            chars[x] = cell.Character == '\0' ? ' ' : cell.Character;
        }
        return new string(chars);
    }
}
