namespace Hex1b.Reflow;

/// <summary>
/// A scrollback row passed to the reflow provider, containing cell data and the
/// terminal width at the time the row was captured.
/// </summary>
/// <param name="Cells">The cell data for the row.</param>
/// <param name="OriginalWidth">The terminal width when this row was scrolled off screen.</param>
public readonly record struct ReflowScrollbackRow(
    TerminalCell[] Cells,
    int OriginalWidth);
