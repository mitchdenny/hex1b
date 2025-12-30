using System.Text;
using Hex1b.Theming;

namespace Hex1b.Terminal.Automation;

/// <summary>
/// Extension methods for rendering terminal regions to ANSI escape code format.
/// </summary>
public static class TerminalRegionAnsiExtensions
{
    /// <summary>
    /// Renders the terminal region to an ANSI escape code string.
    /// The output represents the region as a series of ANSI sequences that,
    /// when printed to a terminal, would display the captured content.
    /// </summary>
    /// <param name="region">The terminal region to render.</param>
    /// <param name="options">Optional rendering options.</param>
    /// <returns>An ANSI escape code string representation of the terminal region.</returns>
    public static string ToAnsi(this IHex1bTerminalRegion region, TerminalAnsiOptions? options = null)
    {
        options ??= TerminalAnsiOptions.Default;
        return RenderToAnsi(region, options, cursorX: null, cursorY: null);
    }

    /// <summary>
    /// Renders the terminal snapshot to an ANSI escape code string, including cursor position.
    /// </summary>
    /// <param name="snapshot">The terminal snapshot to render.</param>
    /// <param name="options">Optional rendering options.</param>
    /// <returns>An ANSI escape code string representation of the terminal snapshot.</returns>
    public static string ToAnsi(this Hex1bTerminalSnapshot snapshot, TerminalAnsiOptions? options = null)
    {
        options ??= TerminalAnsiOptions.Default;
        return RenderToAnsi(snapshot, options, snapshot.CursorX, snapshot.CursorY);
    }

    private static string RenderToAnsi(IHex1bTerminalRegion region, TerminalAnsiOptions options, int? cursorX, int? cursorY)
    {
        var sb = new StringBuilder();

        // Optional: clear screen and reset cursor to home (absolute positioning mode)
        if (options.IncludeClearScreen)
        {
            sb.Append("\x1b[2J");  // Clear entire screen
            sb.Append("\x1b[H");   // Move cursor to home (1,1)
        }
        else
        {
            // Relative positioning mode: scroll down to make room, then render in place
            // First, emit newlines to scroll the terminal and make room for content
            for (int i = 0; i < region.Height; i++)
            {
                sb.AppendLine();
            }
            // Move cursor back up to the start of our rendering area
            sb.Append($"\x1b[{region.Height}A");
        }

        // Group cells by row for efficient row-based rendering
        var cellsByRow = new Dictionary<int, List<(int X, TerminalCell Cell)>>();
        for (int y = 0; y < region.Height; y++)
        {
            cellsByRow[y] = new List<(int, TerminalCell)>();
            for (int x = 0; x < region.Width; x++)
            {
                cellsByRow[y].Add((x, region.GetCell(x, y)));
            }
            // Sort cells in this row by sequence number
            cellsByRow[y].Sort((a, b) => a.Cell.Sequence.CompareTo(b.Cell.Sequence));
        }

        // Track current state to minimize escape sequences
        Hex1bColor? currentFg = null;
        Hex1bColor? currentBg = null;
        CellAttributes currentAttrs = CellAttributes.None;

        // Render row by row
        for (int row = 0; row < region.Height; row++)
        {
            if (row > 0)
            {
                // Move to the next row: go to start of line and down one
                sb.Append("\r\n");
            }
            else
            {
                // First row: just go to start of line
                sb.Append("\r");
            }

            // Render all cells in this row (sorted by sequence)
            foreach (var (x, cell) in cellsByRow[row])
            {
                var ch = cell.Character;

                // Skip empty continuation cells (used for wide characters)
                if (string.IsNullOrEmpty(ch))
                    continue;

                // Skip null characters unless we want to render them as spaces
                if (ch == "\0")
                {
                    if (!options.RenderNullAsSpace)
                        continue;
                    ch = " ";
                }

                // Position cursor within the row using absolute column (relative to line start)
                // CSI n G = Cursor Horizontal Absolute (move to column n)
                sb.Append($"\x1b[{x + 1}G");

                // Build SGR (Select Graphic Rendition) sequence
                var sgrParams = new List<int>();

                // Check if we need to reset attributes
                var needsReset = false;
                var targetAttrs = cell.Attributes;
                var isReverse = (targetAttrs & CellAttributes.Reverse) != 0;

                // Determine target colors (accounting for reverse video)
                var targetFg = isReverse ? cell.Background : cell.Foreground;
                var targetBg = isReverse ? cell.Foreground : cell.Background;

                // Hidden: don't emit character but do set up styling for the cell space
                var isHidden = (targetAttrs & CellAttributes.Hidden) != 0;

                // Check if attributes differ (excluding reverse which we handle via color swap)
                var effectiveCurrentAttrs = currentAttrs & ~CellAttributes.Reverse;
                var effectiveTargetAttrs = targetAttrs & ~CellAttributes.Reverse;

                // If any attribute was removed, we need to reset first
                if ((effectiveCurrentAttrs & ~effectiveTargetAttrs) != 0)
                {
                    needsReset = true;
                }

                if (needsReset || (currentFg.HasValue && !targetFg.HasValue) || (currentBg.HasValue && !targetBg.HasValue))
                {
                    sgrParams.Add(0);  // Reset all attributes
                    currentAttrs = CellAttributes.None;
                    currentFg = null;
                    currentBg = null;
                }

                // Add text attributes
                if ((targetAttrs & CellAttributes.Bold) != 0 && (currentAttrs & CellAttributes.Bold) == 0)
                    sgrParams.Add(1);
                if ((targetAttrs & CellAttributes.Dim) != 0 && (currentAttrs & CellAttributes.Dim) == 0)
                    sgrParams.Add(2);
                if ((targetAttrs & CellAttributes.Italic) != 0 && (currentAttrs & CellAttributes.Italic) == 0)
                    sgrParams.Add(3);
                if ((targetAttrs & CellAttributes.Underline) != 0 && (currentAttrs & CellAttributes.Underline) == 0)
                    sgrParams.Add(4);
                if ((targetAttrs & CellAttributes.Blink) != 0 && (currentAttrs & CellAttributes.Blink) == 0)
                    sgrParams.Add(5);
                if ((targetAttrs & CellAttributes.Hidden) != 0 && (currentAttrs & CellAttributes.Hidden) == 0)
                    sgrParams.Add(8);
                if ((targetAttrs & CellAttributes.Strikethrough) != 0 && (currentAttrs & CellAttributes.Strikethrough) == 0)
                    sgrParams.Add(9);
                if ((targetAttrs & CellAttributes.Overline) != 0 && (currentAttrs & CellAttributes.Overline) == 0)
                    sgrParams.Add(53);

                // Add foreground color (24-bit true color)
                if (targetFg.HasValue && !ColorsEqual(targetFg, currentFg))
                {
                    sgrParams.Add(38);
                    sgrParams.Add(2);
                    sgrParams.Add(targetFg.Value.R);
                    sgrParams.Add(targetFg.Value.G);
                    sgrParams.Add(targetFg.Value.B);
                }
                else if (!targetFg.HasValue && currentFg.HasValue)
                {
                    sgrParams.Add(39);  // Default foreground
                }

                // Add background color (24-bit true color)
                if (targetBg.HasValue && !ColorsEqual(targetBg, currentBg))
                {
                    sgrParams.Add(48);
                    sgrParams.Add(2);
                    sgrParams.Add(targetBg.Value.R);
                    sgrParams.Add(targetBg.Value.G);
                    sgrParams.Add(targetBg.Value.B);
                }
                else if (!targetBg.HasValue && currentBg.HasValue)
                {
                    sgrParams.Add(49);  // Default background
                }

                // Emit SGR sequence if needed
                if (sgrParams.Count > 0)
                {
                    sb.Append($"\x1b[{string.Join(";", sgrParams)}m");
                }

                // Update current state
                currentAttrs = targetAttrs;
                currentFg = targetFg;
                currentBg = targetBg;

                // Emit the character (unless hidden, in which case emit space for background)
                if (isHidden)
                {
                    // For hidden text, emit spaces to show the background
                    var charWidth = DisplayWidth.GetGraphemeWidth(ch);
                    sb.Append(new string(' ', charWidth));
                }
                else
                {
                    sb.Append(ch);
                }
            }
        }

        // Reset attributes at the end
        if (options.ResetAtEnd)
        {
            sb.Append("\x1b[0m");
        }

        // Position cursor at the end of the rendered content
        if (options.IncludeCursorPosition && cursorX.HasValue && cursorY.HasValue)
        {
            // Move to the cursor position relative to the start of our content
            // First, go to the correct row (relative from current position at last row)
            var rowsUp = region.Height - 1 - cursorY.Value;
            if (rowsUp > 0)
            {
                sb.Append($"\x1b[{rowsUp}A");  // Move up
            }
            else if (rowsUp < 0)
            {
                sb.Append($"\x1b[{-rowsUp}B");  // Move down
            }
            // Then go to the column
            sb.Append($"\x1b[{cursorX.Value + 1}G");
        }

        // Add final newline for clean file output
        if (options.IncludeTrailingNewline)
        {
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static bool ColorsEqual(Hex1bColor? a, Hex1bColor? b)
    {
        if (!a.HasValue && !b.HasValue)
            return true;
        if (!a.HasValue || !b.HasValue)
            return false;
        return a.Value.R == b.Value.R && a.Value.G == b.Value.G && a.Value.B == b.Value.B;
    }
}

/// <summary>
/// Options for ANSI rendering of terminal regions.
/// </summary>
public class TerminalAnsiOptions
{
    /// <summary>
    /// Default options for ANSI rendering.
    /// </summary>
    public static readonly TerminalAnsiOptions Default = new();

    /// <summary>
    /// Whether to include escape sequences to clear the screen and reset cursor.
    /// Default is false to just render the content.
    /// </summary>
    public bool IncludeClearScreen { get; set; } = false;

    /// <summary>
    /// Whether to reset all attributes at the end of the output.
    /// Default is true to ensure terminal returns to normal state.
    /// </summary>
    public bool ResetAtEnd { get; set; } = true;

    /// <summary>
    /// Whether to render null characters as spaces.
    /// Default is false to skip them entirely.
    /// </summary>
    public bool RenderNullAsSpace { get; set; } = false;

    /// <summary>
    /// Whether to include cursor positioning at the end (for snapshots).
    /// Default is true.
    /// </summary>
    public bool IncludeCursorPosition { get; set; } = true;

    /// <summary>
    /// Whether to include a trailing newline at the end of output.
    /// Useful when the output will be written to a file.
    /// Default is true.
    /// </summary>
    public bool IncludeTrailingNewline { get; set; } = true;
}
