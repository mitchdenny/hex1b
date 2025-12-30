using System.Text;

namespace Hex1b.Terminal.Automation;

/// <summary>
/// Extension methods for <see cref="IHex1bTerminalRegion"/> providing common text operations.
/// </summary>
public static class Hex1bTerminalRegionExtensions
{
    /// <summary>
    /// Gets the text content of a line.
    /// </summary>
    public static string GetLine(this IHex1bTerminalRegion region, int y)
    {
        if (y < 0 || y >= region.Height)
            return "";

        var sb = new StringBuilder();
        for (int x = 0; x < region.Width; x++)
        {
            var cell = region.GetCell(x, y);
            var ch = cell.Character;
            // Skip empty continuation cells (used for wide characters)
            if (string.IsNullOrEmpty(ch))
                continue;
            // Replace null character with space for display
            if (ch == "\0")
                sb.Append(' ');
            else
                sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Gets the text content of a line with trailing whitespace removed.
    /// </summary>
    public static string GetLineTrimmed(this IHex1bTerminalRegion region, int y) 
        => region.GetLine(y).TrimEnd();

    /// <summary>
    /// Checks if the region contains the specified text anywhere.
    /// </summary>
    public static bool ContainsText(this IHex1bTerminalRegion region, string text)
    {
        if (string.IsNullOrEmpty(text))
            return true;

        for (int y = 0; y < region.Height; y++)
        {
            if (region.GetLine(y).Contains(text, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Gets all lines as a single string separated by newlines.
    /// </summary>
    public static string GetText(this IHex1bTerminalRegion region)
    {
        var lines = new string[region.Height];
        for (int y = 0; y < region.Height; y++)
        {
            lines[y] = region.GetLine(y);
        }
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Gets all non-empty lines from the region.
    /// </summary>
    public static IEnumerable<string> GetNonEmptyLines(this IHex1bTerminalRegion region)
    {
        for (int y = 0; y < region.Height; y++)
        {
            var line = region.GetLineTrimmed(y);
            if (!string.IsNullOrEmpty(line))
                yield return line;
        }
    }

    /// <summary>
    /// Gets all lines as a single string for display/debugging (non-empty lines only).
    /// </summary>
    public static string GetDisplayText(this IHex1bTerminalRegion region)
    {
        return string.Join("\n", region.GetNonEmptyLines());
    }

    /// <summary>
    /// Finds all occurrences of the specified text in the region.
    /// Returns a list of (line, column) positions.
    /// </summary>
    public static List<(int Line, int Column)> FindText(this IHex1bTerminalRegion region, string text)
    {
        var results = new List<(int, int)>();
        if (string.IsNullOrEmpty(text))
            return results;

        for (int y = 0; y < region.Height; y++)
        {
            var line = region.GetLine(y);
            var index = 0;
            while ((index = line.IndexOf(text, index, StringComparison.Ordinal)) >= 0)
            {
                results.Add((y, index));
                index++;
            }
        }
        return results;
    }

    /// <summary>
    /// Compares the content of two regions.
    /// Returns true if they have the same dimensions and identical cell content.
    /// </summary>
    public static bool ContentEquals(this IHex1bTerminalRegion region, IHex1bTerminalRegion other)
    {
        if (other is null)
            return false;

        if (region.Width != other.Width || region.Height != other.Height)
            return false;

        for (int y = 0; y < region.Height; y++)
        {
            for (int x = 0; x < region.Width; x++)
            {
                var thisCell = region.GetCell(x, y);
                var otherCell = other.GetCell(x, y);

                if (!CellsEqual(thisCell, otherCell))
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Compares only the text content of two regions.
    /// Ignores colors and other cell attributes.
    /// </summary>
    public static bool TextEquals(this IHex1bTerminalRegion region, IHex1bTerminalRegion other)
    {
        if (other is null)
            return false;

        if (region.Width != other.Width || region.Height != other.Height)
            return false;

        for (int y = 0; y < region.Height; y++)
        {
            if (region.GetLine(y) != other.GetLine(y))
                return false;
        }
        return true;
    }

    private static bool CellsEqual(TerminalCell a, TerminalCell b)
    {
        // Compare character (treating empty/null as space)
        var charA = string.IsNullOrEmpty(a.Character) || a.Character == "\0" ? " " : a.Character;
        var charB = string.IsNullOrEmpty(b.Character) || b.Character == "\0" ? " " : b.Character;
        if (charA != charB)
            return false;

        // Compare colors
        if (!ColorsEqual(a.Foreground, b.Foreground))
            return false;
        if (!ColorsEqual(a.Background, b.Background))
            return false;

        return true;
    }

    private static bool ColorsEqual(Theming.Hex1bColor? a, Theming.Hex1bColor? b)
    {
        if (a is null && b is null)
            return true;
        if (a is null || b is null)
            return false;
        return a.Value.R == b.Value.R && a.Value.G == b.Value.G && a.Value.B == b.Value.B;
    }

    /// <summary>
    /// Checks if any cell in the region has the specified attribute.
    /// </summary>
    public static bool HasAttribute(this IHex1bTerminalRegion region, CellAttributes attribute)
    {
        for (int y = 0; y < region.Height; y++)
        {
            for (int x = 0; x < region.Width; x++)
            {
                var cell = region.GetCell(x, y);
                if ((cell.Attributes & attribute) != 0)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if any cell in the region has a non-null foreground color.
    /// </summary>
    public static bool HasForegroundColor(this IHex1bTerminalRegion region)
    {
        for (int y = 0; y < region.Height; y++)
        {
            for (int x = 0; x < region.Width; x++)
            {
                if (region.GetCell(x, y).Foreground is not null)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if any cell in the region has a non-null background color.
    /// </summary>
    public static bool HasBackgroundColor(this IHex1bTerminalRegion region)
    {
        for (int y = 0; y < region.Height; y++)
        {
            for (int x = 0; x < region.Width; x++)
            {
                if (region.GetCell(x, y).Background is not null)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if any cell in the region has the specified foreground color.
    /// </summary>
    public static bool HasForegroundColor(this IHex1bTerminalRegion region, Theming.Hex1bColor color)
    {
        for (int y = 0; y < region.Height; y++)
        {
            for (int x = 0; x < region.Width; x++)
            {
                var fg = region.GetCell(x, y).Foreground;
                if (fg is not null && ColorsEqual(fg, color))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if any cell in the region has the specified background color.
    /// </summary>
    public static bool HasBackgroundColor(this IHex1bTerminalRegion region, Theming.Hex1bColor color)
    {
        for (int y = 0; y < region.Height; y++)
        {
            for (int x = 0; x < region.Width; x++)
            {
                var bg = region.GetCell(x, y).Background;
                if (bg is not null && ColorsEqual(bg, color))
                    return true;
            }
        }
        return false;
    }
}
