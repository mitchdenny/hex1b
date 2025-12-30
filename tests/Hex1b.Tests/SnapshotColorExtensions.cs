using Hex1b.Terminal.Automation;
using Hex1b.Theming;

namespace Hex1b.Tests;

/// <summary>
/// Extension methods for Hex1bTerminalSnapshot to inspect cell colors.
/// These are testing utilities for verifying rendering correctness.
/// </summary>
public static class SnapshotColorExtensions
{
    /// <summary>
    /// Gets the background color of a cell at the specified position.
    /// Returns null if the cell has no explicit background color (uses terminal default).
    /// </summary>
    public static Hex1bColor? GetBackgroundColor(this Hex1bTerminalSnapshot snapshot, int x, int y)
    {
        var cell = snapshot.GetCell(x, y);
        return cell.Background;
    }

    /// <summary>
    /// Gets the foreground color of a cell at the specified position.
    /// Returns null if the cell has no explicit foreground color (uses terminal default).
    /// </summary>
    public static Hex1bColor? GetForegroundColor(this Hex1bTerminalSnapshot snapshot, int x, int y)
    {
        var cell = snapshot.GetCell(x, y);
        return cell.Foreground;
    }

    /// <summary>
    /// Checks if all cells in a row have the specified background color.
    /// </summary>
    /// <param name="snapshot">The terminal snapshot.</param>
    /// <param name="y">The row to check.</param>
    /// <param name="expectedBackground">The expected background color. Pass null to check for default/no color.</param>
    /// <param name="startX">Optional start column (inclusive).</param>
    /// <param name="endX">Optional end column (exclusive). If not specified, checks to end of row.</param>
    /// <returns>True if all cells in the range have the expected background color.</returns>
    public static bool HasUniformBackgroundColor(this Hex1bTerminalSnapshot snapshot, int y, Hex1bColor? expectedBackground, int startX = 0, int? endX = null)
    {
        if (y < 0 || y >= snapshot.Height)
            return false;

        var actualEndX = endX ?? snapshot.Width;
        actualEndX = Math.Min(actualEndX, snapshot.Width);
        startX = Math.Max(startX, 0);

        for (int x = startX; x < actualEndX; x++)
        {
            var cellBg = snapshot.GetCell(x, y).Background;
            if (!ColorsEqual(cellBg, expectedBackground))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Checks if a rectangular region has uniform background color.
    /// </summary>
    public static bool HasUniformBackgroundColor(this Hex1bTerminalSnapshot snapshot, int startX, int startY, int width, int height, Hex1bColor? expectedBackground)
    {
        for (int y = startY; y < startY + height && y < snapshot.Height; y++)
        {
            if (!snapshot.HasUniformBackgroundColor(y, expectedBackground, startX, startX + width))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Finds cells with a different background color than expected in a region.
    /// Returns a list of (x, y, actualColor) for cells that don't match.
    /// </summary>
    public static List<(int X, int Y, Hex1bColor? ActualBackground)> FindMismatchedBackgrounds(
        this Hex1bTerminalSnapshot snapshot,
        int startX, int startY, int width, int height, Hex1bColor? expectedBackground)
    {
        var mismatches = new List<(int, int, Hex1bColor?)>();
        
        var endX = Math.Min(startX + width, snapshot.Width);
        var endY = Math.Min(startY + height, snapshot.Height);
        startX = Math.Max(startX, 0);
        startY = Math.Max(startY, 0);

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                var cellBg = snapshot.GetCell(x, y).Background;
                if (!ColorsEqual(cellBg, expectedBackground))
                {
                    mismatches.Add((x, y, cellBg));
                }
            }
        }
        return mismatches;
    }

    /// <summary>
    /// Gets a visual representation of the background colors in a region for debugging.
    /// Uses '#' for cells with the expected color and '!' for mismatches.
    /// </summary>
    public static string VisualizeBackgroundColors(
        this Hex1bTerminalSnapshot snapshot,
        int startX, int startY, int width, int height, Hex1bColor? expectedBackground)
    {
        var lines = new List<string>();
        
        var endX = Math.Min(startX + width, snapshot.Width);
        var endY = Math.Min(startY + height, snapshot.Height);
        startX = Math.Max(startX, 0);
        startY = Math.Max(startY, 0);

        for (int y = startY; y < endY; y++)
        {
            var chars = new char[endX - startX];
            for (int x = startX; x < endX; x++)
            {
                var cellBg = snapshot.GetCell(x, y).Background;
                chars[x - startX] = ColorsEqual(cellBg, expectedBackground) ? '#' : '!';
            }
            lines.Add(new string(chars));
        }
        return string.Join("\n", lines);
    }

    private static bool ColorsEqual(Hex1bColor? a, Hex1bColor? b)
    {
        if (a is null && b is null)
            return true;
        if (a is null || b is null)
            return false;
        return a.Value.R == b.Value.R && a.Value.G == b.Value.G && a.Value.B == b.Value.B;
    }
}
