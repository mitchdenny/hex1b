using System.Text;
using System.Text.RegularExpressions;

namespace Hex1b.Terminal.Automation;

/// <summary>
/// Represents a text match found in a terminal region, with its coordinates.
/// </summary>
/// <param name="Line">The line (Y coordinate) where the match was found.</param>
/// <param name="StartColumn">The starting column (X coordinate) of the match.</param>
/// <param name="EndColumn">The ending column (X coordinate, exclusive) of the match.</param>
/// <param name="Text">The matched text.</param>
public readonly record struct TextMatch(int Line, int StartColumn, int EndColumn, string Text)
{
    /// <summary>
    /// Gets the length of the matched text.
    /// </summary>
    public int Length => EndColumn - StartColumn;
}

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

    /// <summary>
    /// Finds all occurrences of a regular expression pattern in the region.
    /// Returns a list of <see cref="TextMatch"/> objects with start and end coordinates.
    /// </summary>
    /// <param name="region">The terminal region to search.</param>
    /// <param name="pattern">The regular expression pattern to search for.</param>
    /// <param name="options">Regular expression options (default is None).</param>
    /// <returns>A list of matches with their coordinates and matched text.</returns>
    public static List<TextMatch> FindPattern(this IHex1bTerminalRegion region, string pattern, RegexOptions options = RegexOptions.None)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var results = new List<TextMatch>();
        var regex = new Regex(pattern, options);

        for (int y = 0; y < region.Height; y++)
        {
            var line = region.GetLine(y);
            var matches = regex.Matches(line);
            foreach (Match match in matches)
            {
                results.Add(new TextMatch(y, match.Index, match.Index + match.Length, match.Value));
            }
        }
        return results;
    }

    /// <summary>
    /// Finds all occurrences of a compiled regular expression in the region.
    /// Returns a list of <see cref="TextMatch"/> objects with start and end coordinates.
    /// </summary>
    /// <param name="region">The terminal region to search.</param>
    /// <param name="regex">The compiled regular expression to search for.</param>
    /// <returns>A list of matches with their coordinates and matched text.</returns>
    public static List<TextMatch> FindPattern(this IHex1bTerminalRegion region, Regex regex)
    {
        ArgumentNullException.ThrowIfNull(regex);

        var results = new List<TextMatch>();

        for (int y = 0; y < region.Height; y++)
        {
            var line = region.GetLine(y);
            var matches = regex.Matches(line);
            foreach (Match match in matches)
            {
                results.Add(new TextMatch(y, match.Index, match.Index + match.Length, match.Value));
            }
        }
        return results;
    }

    /// <summary>
    /// Finds the first occurrence of a regular expression pattern in the region.
    /// Returns the <see cref="TextMatch"/> if found, or null if not found.
    /// </summary>
    /// <param name="region">The terminal region to search.</param>
    /// <param name="pattern">The regular expression pattern to search for.</param>
    /// <param name="options">Regular expression options (default is None).</param>
    /// <returns>The first match with its coordinates and matched text, or null if not found.</returns>
    public static TextMatch? FindFirstPattern(this IHex1bTerminalRegion region, string pattern, RegexOptions options = RegexOptions.None)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var regex = new Regex(pattern, options);

        for (int y = 0; y < region.Height; y++)
        {
            var line = region.GetLine(y);
            var match = regex.Match(line);
            if (match.Success)
            {
                return new TextMatch(y, match.Index, match.Index + match.Length, match.Value);
            }
        }
        return null;
    }

    /// <summary>
    /// Finds the first occurrence of a compiled regular expression in the region.
    /// Returns the <see cref="TextMatch"/> if found, or null if not found.
    /// </summary>
    /// <param name="region">The terminal region to search.</param>
    /// <param name="regex">The compiled regular expression to search for.</param>
    /// <returns>The first match with its coordinates and matched text, or null if not found.</returns>
    public static TextMatch? FindFirstPattern(this IHex1bTerminalRegion region, Regex regex)
    {
        ArgumentNullException.ThrowIfNull(regex);

        for (int y = 0; y < region.Height; y++)
        {
            var line = region.GetLine(y);
            var match = regex.Match(line);
            if (match.Success)
            {
                return new TextMatch(y, match.Index, match.Index + match.Length, match.Value);
            }
        }
        return null;
    }

    /// <summary>
    /// Gets the text content at the specified coordinates.
    /// </summary>
    /// <param name="region">The terminal region.</param>
    /// <param name="line">The line (Y coordinate) to read from.</param>
    /// <param name="startColumn">The starting column (X coordinate).</param>
    /// <param name="endColumn">The ending column (X coordinate, exclusive).</param>
    /// <returns>The text at the specified coordinates.</returns>
    /// <remarks>
    /// Wide characters (such as emojis) may use continuation cells in the terminal buffer.
    /// This method skips continuation cells, so the returned string length may be shorter 
    /// than (endColumn - startColumn). For text extracted via FindPattern or 
    /// FindFirstPattern, use the <see cref="TextMatch.Text"/> property directly.
    /// </remarks>
    public static string GetTextAt(this IHex1bTerminalRegion region, int line, int startColumn, int endColumn)
    {
        if (line < 0 || line >= region.Height)
            return "";

        var sb = new StringBuilder();
        var start = Math.Max(0, startColumn);
        var end = Math.Min(region.Width, endColumn);

        for (int x = start; x < end; x++)
        {
            var cell = region.GetCell(x, line);
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
    /// Gets the text content at the coordinates specified by a <see cref="TextMatch"/>.
    /// </summary>
    /// <param name="region">The terminal region.</param>
    /// <param name="match">The text match containing the coordinates.</param>
    /// <returns>The text at the match coordinates.</returns>
    public static string GetTextAt(this IHex1bTerminalRegion region, TextMatch match)
    {
        return region.GetTextAt(match.Line, match.StartColumn, match.EndColumn);
    }

    /// <summary>
    /// Checks if the region contains text matching the specified regular expression pattern.
    /// </summary>
    /// <param name="region">The terminal region to search.</param>
    /// <param name="pattern">The regular expression pattern to search for.</param>
    /// <param name="options">Regular expression options (default is None).</param>
    /// <returns>True if a match is found, false otherwise.</returns>
    public static bool ContainsPattern(this IHex1bTerminalRegion region, string pattern, RegexOptions options = RegexOptions.None)
    {
        return region.FindFirstPattern(pattern, options) is not null;
    }

    /// <summary>
    /// Checks if the region contains text matching the specified compiled regular expression.
    /// </summary>
    /// <param name="region">The terminal region to search.</param>
    /// <param name="regex">The compiled regular expression to search for.</param>
    /// <returns>True if a match is found, false otherwise.</returns>
    public static bool ContainsPattern(this IHex1bTerminalRegion region, Regex regex)
    {
        return region.FindFirstPattern(regex) is not null;
    }
}
