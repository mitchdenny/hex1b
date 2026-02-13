namespace Hex1b.Reflow;

/// <summary>
/// Reflow strategy that disables reflow entirely. Content is cropped/extended
/// on resize without re-wrapping. This matches the default terminal behavior
/// when no reflow is supported.
/// </summary>
public sealed class NoReflowStrategy : ITerminalReflowProvider
{
    /// <summary>
    /// Shared singleton instance.
    /// </summary>
    public static readonly NoReflowStrategy Instance = new();

    /// <inheritdoc/>
    public bool ShouldClearSoftWrapOnAbsolutePosition => false;

    /// <inheritdoc/>
    /// <remarks>
    /// Returns the input unchanged — no reflow is performed.
    /// The caller (<see cref="Hex1bTerminal"/>) applies standard crop/extend behavior.
    /// </remarks>
    public ReflowResult Reflow(ReflowContext context)
    {
        // Return a no-op result: crop/extend to new dimensions
        int newWidth = context.NewWidth;
        int newHeight = context.NewHeight;
        var screenRows = new TerminalCell[newHeight][];

        for (int y = 0; y < newHeight; y++)
        {
            var row = new TerminalCell[newWidth];
            if (y < context.ScreenRows.Length)
            {
                int copyWidth = Math.Min(context.ScreenRows[y].Length, newWidth);
                Array.Copy(context.ScreenRows[y], row, copyWidth);
                for (int x = copyWidth; x < newWidth; x++)
                    row[x] = TerminalCell.Empty;
            }
            else
            {
                Array.Fill(row, TerminalCell.Empty);
            }
            screenRows[y] = row;
        }

        return new ReflowResult(
            screenRows,
            context.ScrollbackRows,
            Math.Min(context.CursorX, newWidth - 1),
            Math.Min(context.CursorY, newHeight - 1));
    }
}
