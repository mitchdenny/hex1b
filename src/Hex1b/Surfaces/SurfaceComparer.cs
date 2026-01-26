using Hex1b.Theming;
using Hex1b.Tokens;

namespace Hex1b.Surfaces;

/// <summary>
/// Provides methods for comparing surfaces and generating output.
/// </summary>
public static class SurfaceComparer
{
    /// <summary>
    /// Compares two surfaces and returns the differences.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This performs a cell-by-cell comparison of the two surfaces.
    /// Cells are considered equal if all their visual properties match
    /// (character, foreground, background, attributes).
    /// </para>
    /// <para>
    /// The surfaces must have the same dimensions.
    /// </para>
    /// </remarks>
    /// <param name="previous">The previous surface state.</param>
    /// <param name="current">The current surface state.</param>
    /// <returns>A <see cref="SurfaceDiff"/> containing all changed cells.</returns>
    /// <exception cref="ArgumentException">Thrown if the surfaces have different dimensions.</exception>
    public static SurfaceDiff Compare(Surface previous, Surface current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        if (previous.Width != current.Width || previous.Height != current.Height)
        {
            throw new ArgumentException(
                $"Surfaces must have the same dimensions. Previous: {previous.Width}x{previous.Height}, Current: {current.Width}x{current.Height}");
        }

        var changedCells = new List<ChangedCell>();

        for (var y = 0; y < current.Height; y++)
        {
            for (var x = 0; x < current.Width; x++)
            {
                var prevCell = previous[x, y];
                var currCell = current[x, y];

                if (!CellsEqual(prevCell, currCell))
                {
                    changedCells.Add(new ChangedCell(x, y, currCell));
                }
            }
        }

        return new SurfaceDiff(changedCells);
    }

    /// <summary>
    /// Compares a surface against an empty (default) state.
    /// </summary>
    /// <remarks>
    /// This is useful for initial rendering where there is no previous state.
    /// All non-empty cells in the surface will be included in the diff.
    /// </remarks>
    /// <param name="surface">The surface to compare against empty.</param>
    /// <returns>A <see cref="SurfaceDiff"/> containing all non-empty cells.</returns>
    public static SurfaceDiff CompareToEmpty(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var changedCells = new List<ChangedCell>();

        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                var cell = surface[x, y];

                if (!CellsEqual(cell, SurfaceCells.Empty))
                {
                    changedCells.Add(new ChangedCell(x, y, cell));
                }
            }
        }

        return new SurfaceDiff(changedCells);
    }

    /// <summary>
    /// Generates an optimized list of ANSI tokens to render the diff.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The generated tokens are optimized for minimal output:
    /// <list type="bullet">
    ///   <item>Cells are processed in row-major order for optimal cursor movement</item>
    ///   <item>SGR state is tracked to avoid redundant attribute sequences</item>
    ///   <item>Cursor positioning is omitted when the cursor naturally advances</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="diff">The surface diff to render.</param>
    /// <returns>A list of ANSI tokens that will render the changes.</returns>
    public static IReadOnlyList<AnsiToken> ToTokens(SurfaceDiff diff)
    {
        ArgumentNullException.ThrowIfNull(diff);

        if (diff.IsEmpty)
            return Array.Empty<AnsiToken>();

        var tokens = new List<AnsiToken>();

        // Track current state to minimize redundant tokens
        int cursorX = -1;
        int cursorY = -1;
        Hex1bColor? currentFg = null;
        Hex1bColor? currentBg = null;
        CellAttributes currentAttrs = CellAttributes.None;
        bool stateUnknown = true;

        foreach (var change in diff.ChangedCells)
        {
            // Skip continuation cells - they're handled by the wide character before them
            if (change.Cell.IsContinuation)
                continue;

            // Position cursor if needed
            if (change.Y != cursorY || change.X != cursorX)
            {
                // ANSI cursor position is 1-based
                tokens.Add(new CursorPositionToken(change.Y + 1, change.X + 1));
                cursorY = change.Y;
                cursorX = change.X;
            }

            // Generate SGR if attributes changed or state is unknown
            var needsSgr = stateUnknown ||
                           !ColorsEqual(change.Cell.Foreground, currentFg) ||
                           !ColorsEqual(change.Cell.Background, currentBg) ||
                           change.Cell.Attributes != currentAttrs;

            if (needsSgr)
            {
                var sgrParams = BuildSgrParameters(
                    change.Cell,
                    stateUnknown,
                    ref currentFg,
                    ref currentBg,
                    ref currentAttrs);
                
                if (!string.IsNullOrEmpty(sgrParams))
                {
                    tokens.Add(new SgrToken(sgrParams));
                }
                stateUnknown = false;
            }

            // Output the character
            tokens.Add(new TextToken(change.Cell.Character));
            
            // Cursor advances by display width
            cursorX += Math.Max(1, change.Cell.DisplayWidth);
        }

        return tokens;
    }

    /// <summary>
    /// Generates an ANSI string to render the diff.
    /// </summary>
    /// <remarks>
    /// This is a convenience method that generates tokens and serializes them
    /// to a single ANSI string. For integration with existing token pipelines,
    /// use <see cref="ToTokens"/> instead.
    /// </remarks>
    /// <param name="diff">The surface diff to render.</param>
    /// <returns>An ANSI escape sequence string that will render the changes.</returns>
    public static string ToAnsiString(SurfaceDiff diff)
    {
        var tokens = ToTokens(diff);
        
        if (tokens.Count == 0)
            return string.Empty;

        return AnsiTokenSerializer.Serialize(tokens);
    }

    /// <summary>
    /// Creates a full-surface diff that treats the entire surface as changed.
    /// </summary>
    /// <remarks>
    /// This is useful for forced full refreshes (e.g., after resize).
    /// </remarks>
    /// <param name="surface">The surface to create a full diff for.</param>
    /// <returns>A <see cref="SurfaceDiff"/> containing all cells.</returns>
    public static SurfaceDiff CreateFullDiff(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var changedCells = new List<ChangedCell>(surface.CellCount);

        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                changedCells.Add(new ChangedCell(x, y, surface[x, y]));
            }
        }

        return new SurfaceDiff(changedCells);
    }

    #region Private Helpers

    private static bool CellsEqual(SurfaceCell a, SurfaceCell b)
    {
        // Compare visual properties only
        // Note: We don't compare Sixel/Hyperlink tracked objects by reference
        // because same visual content should be considered equal
        return a.Character == b.Character &&
               ColorsEqual(a.Foreground, b.Foreground) &&
               ColorsEqual(a.Background, b.Background) &&
               a.Attributes == b.Attributes &&
               a.DisplayWidth == b.DisplayWidth;
    }

    private static bool ColorsEqual(Hex1bColor? a, Hex1bColor? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return a.Value.R == b.Value.R && 
               a.Value.G == b.Value.G && 
               a.Value.B == b.Value.B;
    }

    private static string BuildSgrParameters(
        SurfaceCell targetCell,
        bool stateUnknown,
        ref Hex1bColor? currentFg,
        ref Hex1bColor? currentBg,
        ref CellAttributes currentAttrs)
    {
        var parts = new List<string>();

        // Check if we need a reset first
        // Reset if: state is unknown, OR turning OFF any attributes, OR clearing colors
        var turnedOff = currentAttrs & ~targetCell.Attributes;
        bool needsReset = stateUnknown ||
                         turnedOff != CellAttributes.None ||
                         (currentFg is not null && targetCell.Foreground is null) ||
                         (currentBg is not null && targetCell.Background is null);

        if (needsReset)
        {
            parts.Add("0"); // Reset to defaults
            currentAttrs = CellAttributes.None;
            currentFg = null;
            currentBg = null;
        }

        // Add attributes that need to be turned on
        var toTurnOn = targetCell.Attributes & ~currentAttrs;

        if ((toTurnOn & CellAttributes.Bold) != 0)
            parts.Add("1");
        if ((toTurnOn & CellAttributes.Dim) != 0)
            parts.Add("2");
        if ((toTurnOn & CellAttributes.Italic) != 0)
            parts.Add("3");
        if ((toTurnOn & CellAttributes.Underline) != 0)
            parts.Add("4");
        if ((toTurnOn & CellAttributes.Blink) != 0)
            parts.Add("5");
        if ((toTurnOn & CellAttributes.Reverse) != 0)
            parts.Add("7");
        if ((toTurnOn & CellAttributes.Hidden) != 0)
            parts.Add("8");
        if ((toTurnOn & CellAttributes.Strikethrough) != 0)
            parts.Add("9");
        if ((toTurnOn & CellAttributes.Overline) != 0)
            parts.Add("53");

        // Add foreground color if different
        if (!ColorsEqual(targetCell.Foreground, currentFg) && targetCell.Foreground is not null)
        {
            parts.Add(BuildColorSgr(targetCell.Foreground.Value, isForeground: true));
        }

        // Add background color if different
        if (!ColorsEqual(targetCell.Background, currentBg) && targetCell.Background is not null)
        {
            parts.Add(BuildColorSgr(targetCell.Background.Value, isForeground: false));
        }

        // Update tracked state
        currentAttrs = targetCell.Attributes;
        currentFg = targetCell.Foreground;
        currentBg = targetCell.Background;

        return string.Join(";", parts);
    }

    private static string BuildColorSgr(Hex1bColor color, bool isForeground)
    {
        // Use 24-bit color (SGR 38;2;r;g;b for foreground, 48;2;r;g;b for background)
        var prefix = isForeground ? "38;2" : "48;2";
        return $"{prefix};{color.R};{color.G};{color.B}";
    }

    #endregion
}
