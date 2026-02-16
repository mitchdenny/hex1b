namespace Hex1b.Reflow;

/// <summary>
/// Opt-in interface that presentation adapters can implement to provide
/// terminal-emulator-specific reflow behavior during resize operations.
/// </summary>
/// <remarks>
/// <para>
/// When <see cref="Hex1bTerminal"/> resizes, it checks if its presentation adapter
/// implements this interface. If so, it calls <see cref="Reflow"/> with the current
/// buffer state and applies the returned result. If the adapter does not implement
/// this interface, the terminal falls back to simple crop-and-extend behavior.
/// </para>
/// <para>
/// Different terminal emulators (xterm, kitty, alacritty) handle reflow differently.
/// The adapter controls the reflow algorithm — <see cref="Hex1bTerminal"/> is fully
/// decoupled from how reflow works.
/// </para>
/// <para>
/// Pre-built strategies are available for adapters to delegate to.
/// </para>
/// </remarks>
public interface ITerminalReflowProvider
{
    /// <summary>
    /// Gets whether reflow is enabled. When <c>false</c>, the terminal uses
    /// standard crop-and-extend resize behavior even though the adapter
    /// implements this interface. Defaults to <c>true</c>.
    /// </summary>
    bool ReflowEnabled => true;

    /// <summary>
    /// Performs reflow of terminal content during a resize operation.
    /// </summary>
    /// <param name="context">The current terminal state including screen buffer, scrollback, and cursor position.</param>
    /// <returns>The reflowed terminal state with new buffer, scrollback, and cursor position.</returns>
    ReflowResult Reflow(ReflowContext context);

    /// <summary>
    /// Gets whether absolute cursor positioning (CUP, HVP) should clear the
    /// <see cref="CellAttributes.SoftWrap"/> flag on the current row's last cell.
    /// </summary>
    /// <remarks>
    /// Most terminal emulators break the reflow chain when absolute positioning is used,
    /// because it indicates the application is managing screen layout directly.
    /// When <c>true</c>, the terminal clears <see cref="CellAttributes.SoftWrap"/> from
    /// the last cell of the row before moving the cursor.
    /// </remarks>
    bool ShouldClearSoftWrapOnAbsolutePosition { get; }
}

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
    int? SavedCursorY = null);

/// <summary>
/// A scrollback row passed to the reflow provider, containing cell data and the
/// terminal width at the time the row was captured.
/// </summary>
/// <param name="Cells">The cell data for the row.</param>
/// <param name="OriginalWidth">The terminal width when this row was scrolled off screen.</param>
public readonly record struct ReflowScrollbackRow(
    TerminalCell[] Cells,
    int OriginalWidth);

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
    int? NewSavedCursorY = null);
