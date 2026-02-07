namespace Hex1b;

/// <summary>
/// Provides data for the scrollback row callback, invoked when a row is scrolled
/// off the top of the terminal screen into the scrollback buffer.
/// </summary>
public sealed class ScrollbackRowEventArgs
{
    internal ScrollbackRowEventArgs(
        Hex1bTerminal terminal,
        ReadOnlyMemory<TerminalCell> cells,
        int originalWidth,
        DateTimeOffset timestamp)
    {
        Terminal = terminal;
        Cells = cells;
        OriginalWidth = originalWidth;
        Timestamp = timestamp;
    }

    /// <summary>
    /// The terminal instance that generated this scrollback row.
    /// </summary>
    public Hex1bTerminal Terminal { get; }

    /// <summary>
    /// The cell data for the row that was scrolled off screen.
    /// </summary>
    public ReadOnlyMemory<TerminalCell> Cells { get; }

    /// <summary>
    /// The terminal width (in columns) when this row was captured.
    /// </summary>
    public int OriginalWidth { get; }

    /// <summary>
    /// When the row was scrolled off screen.
    /// </summary>
    public DateTimeOffset Timestamp { get; }
}
