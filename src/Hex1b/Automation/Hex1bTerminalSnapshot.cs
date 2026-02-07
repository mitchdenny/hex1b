using Hex1b.Layout;

namespace Hex1b.Automation;

/// <summary>
/// An immutable snapshot of terminal state at a point in time.
/// Used for assertions and wait conditions in test sequences.
/// </summary>
public sealed class Hex1bTerminalSnapshot : IHex1bTerminalRegion, IDisposable
{
    private readonly TerminalCell[,] _cells;
    private bool _disposed;

    /// <summary>
    /// Number of scrollback lines included in this snapshot (prepended above visible content).
    /// </summary>
    public int ScrollbackLineCount { get; }

    internal Hex1bTerminalSnapshot(Hex1bTerminal terminal)
        : this(terminal, scrollbackLines: 0, scrollbackWidth: ScrollbackWidth.CurrentTerminal)
    {
    }

    internal Hex1bTerminalSnapshot(Hex1bTerminal terminal, int scrollbackLines, ScrollbackWidth scrollbackWidth)
    {
        Terminal = terminal;
        var terminalWidth = terminal.Width;
        var terminalHeight = terminal.Height;
        CursorX = terminal.CursorX;
        CursorY = terminal.CursorY;
        InAlternateScreen = terminal.InAlternateScreen;
        Timestamp = DateTimeOffset.UtcNow;
        CellPixelWidth = terminal.Capabilities.CellPixelWidth;
        CellPixelHeight = terminal.Capabilities.CellPixelHeight;

        // Get scrollback rows if requested
        ScrollbackRow[] scrollbackRows = [];
        if (scrollbackLines > 0 && terminal.Scrollback is { } scrollback)
        {
            scrollbackRows = scrollback.GetLines(scrollbackLines);
        }
        ScrollbackLineCount = scrollbackRows.Length;

        // Determine snapshot dimensions
        int snapshotWidth;
        if (scrollbackWidth == ScrollbackWidth.Original && scrollbackRows.Length > 0)
        {
            snapshotWidth = terminalWidth;
            foreach (var row in scrollbackRows)
            {
                if (row.OriginalWidth > snapshotWidth)
                    snapshotWidth = row.OriginalWidth;
            }
        }
        else
        {
            snapshotWidth = terminalWidth;
        }

        int totalHeight = scrollbackRows.Length + terminalHeight;
        Width = snapshotWidth;
        Height = totalHeight;

        _cells = new TerminalCell[totalHeight, snapshotWidth];

        // Fill scrollback rows (top of snapshot)
        for (int rowIdx = 0; rowIdx < scrollbackRows.Length; rowIdx++)
        {
            var row = scrollbackRows[rowIdx];
            int copyWidth = Math.Min(row.Cells.Length, snapshotWidth);
            for (int x = 0; x < copyWidth; x++)
            {
                _cells[rowIdx, x] = row.Cells[x];
                row.Cells[x].TrackedSixel?.AddRef();
                row.Cells[x].TrackedHyperlink?.AddRef();
            }
            // Remaining columns are default (TerminalCell.Empty equivalent)
        }

        // Fill visible area (below scrollback)
        var screenBuffer = terminal.GetScreenBuffer(addTrackedObjectRefs: true);
        for (int y = 0; y < terminalHeight; y++)
        {
            int copyWidth = Math.Min(terminalWidth, snapshotWidth);
            for (int x = 0; x < copyWidth; x++)
            {
                _cells[scrollbackRows.Length + y, x] = screenBuffer[y, x];
            }
            // If screen is narrower than snapshotWidth, remaining columns stay empty
        }

        // Adjust cursor position to account for prepended scrollback rows
        CursorY += scrollbackRows.Length;
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
    /// Width of a terminal character cell in pixels.
    /// </summary>
    public int CellPixelWidth { get; }
    
    /// <summary>
    /// Height of a terminal character cell in pixels.
    /// </summary>
    public int CellPixelHeight { get; }

    /// <inheritdoc />
    public TerminalCell GetCell(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return TerminalCell.Empty;
        return _cells[y, x];
    }

    /// <inheritdoc />
    public Hex1bTerminalSnapshotRegion GetRegion(Rect bounds)
    {
        return new Hex1bTerminalSnapshotRegion(this, bounds);
    }

    /// <summary>
    /// Gets the full screen text with all lines separated by newlines.
    /// </summary>
    /// <remarks>Legacy method for backward compatibility.</remarks>
    public string GetScreenText() => this.GetText();

    /// <summary>
    /// Releases tracked object references held by this snapshot.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        // Release all tracked object references
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                _cells[y, x].TrackedSixel?.Release();
                _cells[y, x].TrackedHyperlink?.Release();
            }
        }
    }
}
