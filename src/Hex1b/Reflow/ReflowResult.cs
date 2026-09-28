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
    /// <summary>Gets the rendition of each output screen row, in the same order as <see cref="ScreenRows"/>.</summary>
    /// <remarks>
    /// Supply one value per output row when the input contains enlarged screen or history rows.
    /// An empty array is accepted for legacy providers only when the input contains no enlarged rows.
    /// To intentionally normalize enlarged text, return explicit single-width values.
    /// </remarks>
    public LineRendition[] LineRenditions { get; init; } = [];

    internal void ValidateLineRenditions(ReflowContext context)
    {
        if (LineRenditions.Length != context.NewHeight &&
            (LineRenditions.Length != 0 ||
             context.LineRenditions.Any(mode => mode != LineRendition.SingleWidth) ||
             context.ScrollbackRows.Any(row => row.Rendition != LineRendition.SingleWidth)))
            throw new InvalidOperationException("Reflow must supply one line rendition per output row for enlarged text.");
        if (LineRenditions.Any(mode => !Enum.IsDefined(mode)) ||
            ScrollbackRows.Any(row => !Enum.IsDefined(row.Rendition)))
            throw new InvalidOperationException("Reflow returned an invalid line rendition.");
    }
}
