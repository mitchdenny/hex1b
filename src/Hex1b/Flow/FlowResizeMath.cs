namespace Hex1b.Flow;

/// <summary>
/// Pure helpers for the flow runner's resize handler. Extracted so the
/// behavioural choice between the legacy "clear the whole visible area" path
/// and the soft-wrap "clear only the active-step region" path can be unit
/// tested without spinning up a real terminal pipeline.
/// </summary>
internal static class FlowResizeMath
{
    /// <summary>
    /// Clamps the desired step height to the terminal's available rows.
    /// </summary>
    /// <param name="maxHeight">Caller-supplied max height (from
    /// <see cref="Hex1bFlowStepOptions.MaxHeight"/>); when <c>null</c> the
    /// step is allowed to fill the terminal.</param>
    /// <param name="terminalHeight">New terminal height in rows.</param>
    /// <returns>Step height in rows, never less than 1.</returns>
    public static int ComputeStepHeight(int? maxHeight, int terminalHeight)
    {
        var stepHeight = Math.Min(maxHeight ?? terminalHeight, terminalHeight);
        return stepHeight < 1 ? 1 : stepHeight;
    }

    /// <summary>
    /// Selects the rows that the resize handler should clear before the
    /// active step's app re-renders into the new region.
    /// </summary>
    /// <param name="useSoftWrapTombstones">The
    /// <see cref="Hex1bFlowOptions.UseSoftWrapTombstones"/> flag.</param>
    /// <param name="terminalHeight">New terminal height in rows.</param>
    /// <param name="stepHeight">New step height in rows (see
    /// <see cref="ComputeStepHeight(int?, int)"/>).</param>
    /// <returns>
    /// A <c>(rowOrigin, height)</c> tuple suitable for
    /// <c>ClearRegion(rowOrigin, height)</c>. Under the legacy path this is
    /// always <c>(0, terminalHeight)</c> — every row in the visible area gets
    /// cleared because cell-positioned tombstones cannot survive a resize
    /// anyway. The soft-wrap path no longer calls into this helper at all:
    /// the runner owns the resize repaint there (clears the viewport, redraws
    /// every tracked tombstone at the new width via
    /// <c>SoftWrapEmitter</c>, then re-anchors the active step). The
    /// soft-wrap branch in this method is retained only for tests and
    /// possible future reuse — production code on the soft-wrap path
    /// bypasses it.
    /// </returns>
    public static (int RowOrigin, int Height) ComputeClearRegion(
        bool useSoftWrapTombstones,
        int terminalHeight,
        int stepHeight)
    {
        if (useSoftWrapTombstones)
        {
            var rowOrigin = Math.Max(0, terminalHeight - stepHeight);
            return (rowOrigin, stepHeight);
        }
        return (0, terminalHeight);
    }

    /// <summary>
    /// Computes the display row where the active step's top will sit, given
    /// the per-paragraph logical widths of every tombstone emitted above the
    /// active step and the current terminal width.
    /// </summary>
    /// <param name="initialRowOrigin">The row at which the very first
    /// tombstone (or active step, if none) starts. Captured at flow start
    /// and never changes for the life of the flow.</param>
    /// <param name="tombstoneParagraphWidths">For each tombstone, in
    /// emission order, the logical width (in cells) of each CR+LF-terminated
    /// paragraph it contains. Hard-newline-terminated paragraphs never merge
    /// across resize on any surveyed emulator, so we only need their widths
    /// to recompute reflow at any terminal width.</param>
    /// <param name="terminalWidth">Current terminal width in cells. Must be
    /// at least 1.</param>
    /// <returns>The 0-based display row where the active step begins after
    /// the host terminal reflows the tombstones above it at the given
    /// width.</returns>
    /// <remarks>
    /// This is the core primitive the resize handler relies on: it lets the
    /// runner answer "where should I move the cursor before erasing the
    /// active-step region?" without round-tripping a CPR query to the
    /// terminal. Its correctness is pinned by the feasibility tests in
    /// <c>FlowResizeRowOriginFeasibilityTests</c>, which drive the same
    /// inputs through a reference reflow simulator and assert agreement.
    /// </remarks>
    public static int ComputeRowOriginAtWidth(
        int initialRowOrigin,
        IReadOnlyList<IReadOnlyList<int>> tombstoneParagraphWidths,
        int terminalWidth)
    {
        if (terminalWidth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(terminalWidth),
                terminalWidth, "terminalWidth must be at least 1");
        }

        var rows = initialRowOrigin;
        foreach (var tombstone in tombstoneParagraphWidths)
        {
            foreach (var paragraphWidth in tombstone)
            {
                if (paragraphWidth < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(tombstoneParagraphWidths), paragraphWidth,
                        "paragraph widths must be non-negative");
                }

                rows += Math.Max(1, (paragraphWidth + terminalWidth - 1) / terminalWidth);
            }
        }
        return rows;
    }
}
