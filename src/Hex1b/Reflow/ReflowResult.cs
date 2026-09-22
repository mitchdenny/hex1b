namespace Hex1b.Reflow;

/// <summary>
/// Result returned from <see cref="ITerminalReflowProvider.Reflow"/> containing
/// the reflowed terminal state.
/// </summary>
/// <param name="ScreenRows">
/// The new screen buffer rows after reflow. Must contain exactly <c>NewHeight</c> rows,
/// each with exactly <c>NewWidth</c> cells.
/// </param>
/// <param name="ScrollbackRows">
/// The new scrollback rows after reflow, ordered oldest to newest.
/// May be larger or smaller than the input scrollback (promotion/demotion).
/// </param>
/// <param name="CursorX">The cursor column position (0-based) after reflow.</param>
/// <param name="CursorY">The cursor row position (0-based) after reflow.</param>
/// <param name="NewSavedCursorX">The reflowed DECSC saved cursor column, or <c>null</c> if not reflowed.</param>
/// <param name="NewSavedCursorY">The reflowed DECSC saved cursor row, or <c>null</c> if not reflowed.</param>
public readonly record struct ReflowResult(
    TerminalCell[][] ScreenRows,
    ReflowScrollbackRow[] ScrollbackRows,
    int CursorX,
    int CursorY,
    int? NewSavedCursorX = null,
    int? NewSavedCursorY = null)
{
    internal bool PendingWrap { get; init; }
    internal bool SavedPendingWrap { get; init; }
}
