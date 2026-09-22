namespace Hex1b.Reflow;

/// <summary>
/// Context passed to <see cref="ITerminalReflowProvider.Reflow"/> containing
/// the terminal's current state before resize.
/// </summary>
/// <param name="ScreenRows">
/// The current screen buffer rows, each as a <see cref="TerminalCell"/> array.
/// Ordered top to bottom (index 0 is the top row).
/// </param>
/// <param name="ScrollbackRows">
/// Scrollback buffer rows ordered oldest to newest.
/// Empty if scrollback is not enabled or is empty.
/// </param>
/// <param name="OldWidth">The terminal width before resize.</param>
/// <param name="OldHeight">The terminal height before resize.</param>
/// <param name="NewWidth">The terminal width after resize.</param>
/// <param name="NewHeight">The terminal height after resize.</param>
/// <param name="CursorX">The cursor column position (0-based) before resize.</param>
/// <param name="CursorY">The cursor row position (0-based) before resize.</param>
/// <param name="InAlternateScreen">Whether the terminal is currently in the alternate screen buffer.</param>
/// <param name="SavedCursorX">The DECSC saved cursor column position, or <c>null</c> if no cursor has been saved.</param>
/// <param name="SavedCursorY">The DECSC saved cursor row position, or <c>null</c> if no cursor has been saved.</param>
public readonly record struct ReflowContext(
    TerminalCell[][] ScreenRows,
    ReflowScrollbackRow[] ScrollbackRows,
    int OldWidth,
    int OldHeight,
    int NewWidth,
    int NewHeight,
    int CursorX,
    int CursorY,
    bool InAlternateScreen,
    int? SavedCursorX = null,
    int? SavedCursorY = null)
{
    internal bool PendingWrap { get; init; }
    internal bool SavedPendingWrap { get; init; }
}
