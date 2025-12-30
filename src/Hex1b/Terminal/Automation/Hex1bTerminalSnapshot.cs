using Hex1b.Layout;

namespace Hex1b.Terminal.Automation;

/// <summary>
/// An immutable snapshot of terminal state at a point in time.
/// Used for assertions and wait conditions in test sequences.
/// </summary>
public sealed class Hex1bTerminalSnapshot : IHex1bTerminalRegion, IDisposable
{
    private readonly TerminalCell[,] _cells;
    private bool _disposed;

    internal Hex1bTerminalSnapshot(Hex1bTerminal terminal)
    {
        Terminal = terminal;
        Width = terminal.Width;
        Height = terminal.Height;
        CursorX = terminal.CursorX;
        CursorY = terminal.CursorY;
        InAlternateScreen = terminal.InAlternateScreen;
        Timestamp = DateTimeOffset.UtcNow;
        CellPixelWidth = terminal.Capabilities.CellPixelWidth;
        CellPixelHeight = terminal.Capabilities.CellPixelHeight;

        // Get a deep copy of the cell buffer, adding refs for tracked objects
        _cells = terminal.GetScreenBuffer(addTrackedObjectRefs: true);
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

        // Release all Sixel data references
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                _cells[y, x].TrackedSixel?.Release();
            }
        }
    }
}
