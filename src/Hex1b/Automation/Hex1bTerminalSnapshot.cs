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
        : this(terminal, scrollbackLines: 0, scrollbackWidth: ScrollbackWidth.CurrentTerminal, voidCell: TerminalCell.Empty)
    {
    }

    internal Hex1bTerminalSnapshot(Hex1bTerminal terminal, int scrollbackLines, ScrollbackWidth scrollbackWidth, TerminalCell voidCell)
    {
        Terminal = terminal;
        var state = terminal.CaptureSnapshotState(scrollbackLines, scrollbackWidth);
        var terminalWidth = state.TerminalWidth;
        var terminalHeight = state.TerminalHeight;
        CursorX = state.CursorX;
        CursorY = state.CursorY;
        InAlternateScreen = state.InAlternateScreen;
        CursorVisible = state.CursorVisible;
        BracketedPasteEnabled = state.BracketedPasteEnabled;
        ApplicationCursorKeysEnabled = state.ApplicationCursorKeysEnabled;
        ApplicationKeypadEnabled = state.ApplicationKeypadEnabled;
        FocusEventsEnabled = state.FocusEventsEnabled;
        MouseProtocolX10Enabled = state.MouseProtocolX10Enabled;
        MouseProtocolNormalEnabled = state.MouseProtocolNormalEnabled;
        MouseProtocolHighlightEnabled = state.MouseProtocolHighlightEnabled;
        MouseProtocolButtonEnabled = state.MouseProtocolButtonEnabled;
        MouseProtocolAnyEnabled = state.MouseProtocolAnyEnabled;
        MouseEncodingUtf8Enabled = state.MouseEncodingUtf8Enabled;
        MouseEncodingSgrEnabled = state.MouseEncodingSgrEnabled;
        MouseEncodingUrxvtEnabled = state.MouseEncodingUrxvtEnabled;
        CursorShape = state.CursorShape;
        Timestamp = state.Timestamp;
        CellPixelWidth = state.CellPixelWidth;
        CellPixelHeight = state.CellPixelHeight;
        KgpPlacements = state.KgpPlacements;
        KgpImages = state.KgpImages;

        var scrollbackRows = state.ScrollbackRows;
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

        // Pre-fill with void cell if snapshot is wider than any source row
        if (snapshotWidth > terminalWidth)
        {
            for (int y = 0; y < totalHeight; y++)
            {
                for (int x = 0; x < snapshotWidth; x++)
                {
                    _cells[y, x] = voidCell;
                }
            }
        }

        // Fill scrollback rows (top of snapshot)
        for (int rowIdx = 0; rowIdx < scrollbackRows.Length; rowIdx++)
        {
            var row = scrollbackRows[rowIdx];
            int copyWidth = Math.Min(row.Cells.Length, snapshotWidth);
            for (int x = 0; x < copyWidth; x++)
                _cells[rowIdx, x] = row.Cells[x];
        }

        // Fill visible area (below scrollback)
        var screenBuffer = state.ScreenBuffer;
        for (int y = 0; y < terminalHeight; y++)
        {
            int copyWidth = Math.Min(terminalWidth, snapshotWidth);
            for (int x = 0; x < copyWidth; x++)
            {
                _cells[scrollbackRows.Length + y, x] = screenBuffer[y, x];
            }
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
    /// Whether the cursor was visible (DECTCEM, DEC mode 25) at snapshot time.
    /// </summary>
    public bool CursorVisible { get; }

    /// <summary>
    /// Whether bracketed-paste mode (DEC mode 2004) was enabled at snapshot time.
    /// </summary>
    public bool BracketedPasteEnabled { get; }

    /// <summary>
    /// Whether application-cursor-keys mode (DECCKM, DEC mode 1) was enabled at snapshot time.
    /// </summary>
    public bool ApplicationCursorKeysEnabled { get; }

    /// <summary>
    /// Whether application-keypad mode (DECKPAM, ESC =) was enabled at snapshot time.
    /// </summary>
    public bool ApplicationKeypadEnabled { get; }

    /// <summary>
    /// Whether focus-in/focus-out reporting (DEC mode 1004) was enabled at snapshot time.
    /// </summary>
    public bool FocusEventsEnabled { get; }

    /// <summary>
    /// Whether X10-compatibility mouse tracking (DEC mode 9) was enabled at snapshot time.
    /// </summary>
    public bool MouseProtocolX10Enabled { get; }

    /// <summary>
    /// Whether X11 normal mouse tracking (DEC mode 1000) was enabled at snapshot time.
    /// </summary>
    public bool MouseProtocolNormalEnabled { get; }

    /// <summary>
    /// Whether highlight mouse tracking (DEC mode 1001) was enabled at snapshot time.
    /// </summary>
    public bool MouseProtocolHighlightEnabled { get; }

    /// <summary>
    /// Whether button-event mouse tracking (DEC mode 1002) was enabled at snapshot time.
    /// </summary>
    public bool MouseProtocolButtonEnabled { get; }

    /// <summary>
    /// Whether any-event mouse tracking (DEC mode 1003) was enabled at snapshot time.
    /// </summary>
    public bool MouseProtocolAnyEnabled { get; }

    /// <summary>
    /// Whether UTF-8 mouse encoding (DEC mode 1005) was enabled at snapshot time.
    /// </summary>
    public bool MouseEncodingUtf8Enabled { get; }

    /// <summary>
    /// Whether SGR mouse encoding (DEC mode 1006) was enabled at snapshot time.
    /// </summary>
    public bool MouseEncodingSgrEnabled { get; }

    /// <summary>
    /// Whether urxvt mouse encoding (DEC mode 1015) was enabled at snapshot time.
    /// </summary>
    public bool MouseEncodingUrxvtEnabled { get; }

    /// <summary>
    /// DECSCUSR cursor shape value at snapshot time. <c>0</c> means "default";
    /// values 1–6 follow the standard <c>CSI Ps SP q</c> mapping.
    /// </summary>
    public int CursorShape { get; }
    
    /// <summary>
    /// Width of a terminal character cell in pixels.
    /// </summary>
    public int CellPixelWidth { get; }
    
    /// <summary>
    /// Height of a terminal character cell in pixels.
    /// </summary>
    public int CellPixelHeight { get; }

    /// <summary>
    /// KGP image placements active at snapshot time.
    /// </summary>
    public IReadOnlyList<KgpPlacement> KgpPlacements { get; }

    /// <summary>
    /// KGP image data referenced by placements, keyed by image ID.
    /// </summary>
    public IReadOnlyDictionary<uint, KgpImageData> KgpImages { get; }

    /// <inheritdoc />
    public TerminalCell GetCell(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return TerminalCell.Empty;
        return _cells[y, x];
    }

    /// <summary>
    /// Checks if any cell in the snapshot contains Sixel data.
    /// </summary>
    public bool ContainsSixelData()
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (_cells[y, x].TrackedSixel is not null)
                    return true;
            }
        }
        return false;
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
