using System.Globalization;
using System.Text;

namespace Hex1b;

/// <summary>
/// Helper methods for working with grapheme clusters (user-perceived characters).
/// 
/// A grapheme cluster is what users perceive as a single "character" but may be
/// composed of multiple Unicode code points (and thus multiple C# chars).
/// 
/// This class provides methods to navigate text by grapheme cluster boundaries,
/// ensuring operations like delete, cursor movement, and selection work correctly
/// with emojis, combining characters, and other complex Unicode sequences.
/// </summary>
public static class GraphemeHelper
{
    /// <summary>
    /// Gets the string index of the start of the grapheme cluster that ends at or before the given index.
    /// Used for "move left" and "delete backward" operations.
    /// </summary>
    /// <param name="text">The text to navigate</param>
    /// <param name="index">The current cursor position (0 to text.Length)</param>
    /// <returns>The index of the start of the previous grapheme cluster, or the current index if already at start</returns>
    public static int GetPreviousClusterBoundary(string text, int index)
    {
        if (string.IsNullOrEmpty(text) || index <= 0)
            return 0;

        if (index > text.Length)
            index = text.Length;

        // Find all cluster boundaries
        var boundaries = GetClusterBoundaries(text);
        
        // Find the largest boundary that is less than index
        int result = 0;
        foreach (var boundary in boundaries)
        {
            if (boundary < index)
                result = boundary;
            else
                break;
        }
        return result;
    }

    /// <summary>
    /// Gets the string index of the end of the grapheme cluster that starts at or after the given index.
    /// Used for "move right" and "delete forward" operations.
    /// </summary>
    /// <param name="text">The text to navigate</param>
    /// <param name="index">The current cursor position (0 to text.Length)</param>
    /// <returns>The index after the current grapheme cluster, or the current index if already at end</returns>
    public static int GetNextClusterBoundary(string text, int index)
    {
        if (string.IsNullOrEmpty(text) || index >= text.Length)
            return text?.Length ?? 0;

        if (index < 0)
            index = 0;

        // Find all cluster boundaries
        var boundaries = GetClusterBoundaries(text);
        
        // Find the smallest boundary that is greater than index
        foreach (var boundary in boundaries)
        {
            if (boundary > index)
                return boundary;
        }
        return text.Length;
    }

    /// <summary>
    /// Gets all grapheme cluster boundary positions in the text.
    /// This includes 0 (start) and text.Length (end), plus the index after each cluster.
    /// </summary>
    public static List<int> GetClusterBoundaries(string text)
    {
        var boundaries = new List<int> { 0 };
        
        if (string.IsNullOrEmpty(text))
            return boundaries;

        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            var clusterStart = enumerator.ElementIndex;
            var cluster = (string)enumerator.Current;
            boundaries.Add(clusterStart + cluster.Length);
        }
        
        return boundaries;
    }

    /// <summary>
    /// Snaps a cursor position to the nearest valid grapheme cluster boundary.
    /// If the position is already at a boundary, returns it unchanged.
    /// Otherwise, snaps to the start of the containing cluster.
    /// </summary>
    public static int SnapToClusterBoundary(string text, int index)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        if (index <= 0)
            return 0;

        if (index >= text.Length)
            return text.Length;

        var boundaries = GetClusterBoundaries(text);
        
        // If already at a boundary, return as-is
        if (boundaries.Contains(index))
            return index;

        // Otherwise, snap to the previous boundary (start of containing cluster)
        return GetPreviousClusterBoundary(text, index);
    }

    /// <summary>
    /// Gets the length (in chars) of the grapheme cluster that starts at the given index.
    /// </summary>
    public static int GetClusterLength(string text, int index)
    {
        if (string.IsNullOrEmpty(text) || index < 0 || index >= text.Length)
            return 0;

        return GetNextClusterBoundary(text, index) - index;
    }

    /// <summary>
    /// Gets the grapheme cluster at the specified index.
    /// </summary>
    public static string GetClusterAt(string text, int index)
    {
        if (string.IsNullOrEmpty(text) || index < 0 || index >= text.Length)
            return string.Empty;

        var length = GetClusterLength(text, index);
        return text.Substring(index, length);
    }

    /// <summary>
    /// Counts the number of grapheme clusters in the text.
    /// </summary>
    public static int GetClusterCount(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        var info = new StringInfo(text);
        return info.LengthInTextElements;
    }

    /// <summary>
    /// Gets the terminal display width of the text (accounting for wide characters like emoji and CJK).
    /// </summary>
    public static int GetDisplayWidth(string text)
    {
        return DisplayWidth.GetStringWidth(text);
    }

    /// <summary>
    /// Gets the terminal display width of a single grapheme cluster.
    /// </summary>
    public static int GetClusterDisplayWidth(string grapheme)
    {
        return DisplayWidth.GetGraphemeWidth(grapheme);
    }

    /// <summary>
    /// Converts a string index (cursor position) to a display column position.
    /// </summary>
    public static int IndexToDisplayColumn(string text, int index)
    {
        if (string.IsNullOrEmpty(text) || index <= 0)
            return 0;

        if (index >= text.Length)
            return DisplayWidth.GetStringWidth(text);

        // Sum the display widths of all graphemes before this index
        int column = 0;
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            if (enumerator.ElementIndex >= index)
                break;
            
            var grapheme = (string)enumerator.Current;
            column += DisplayWidth.GetGraphemeWidth(grapheme);
        }
        
        return column;
    }

    /// <summary>
    /// Converts a display column position to a string index (cursor position).
    /// Returns the index at or after the specified column.
    /// </summary>
    public static int DisplayColumnToIndex(string text, int column)
    {
        if (string.IsNullOrEmpty(text) || column <= 0)
            return 0;

        int currentColumn = 0;
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            var grapheme = (string)enumerator.Current;
            var width = DisplayWidth.GetGraphemeWidth(grapheme);
            
            if (currentColumn + width > column)
            {
                // This grapheme spans the target column
                return enumerator.ElementIndex;
            }
            
            currentColumn += width;
            
            if (currentColumn >= column)
            {
                // Reached or passed the target column
                return enumerator.ElementIndex + grapheme.Length;
            }
        }
        
        return text.Length;
    }

    /// <summary>
    /// Gets the string index of the start of the previous word.
    /// Used for Ctrl+Left navigation. Skips backward over non-word chars, then word chars.
    /// </summary>
    /// <param name="text">The text to navigate</param>
    /// <param name="index">The current cursor position (0 to text.Length)</param>
    /// <returns>The index of the start of the previous word, or 0 if at beginning</returns>
    public static int GetPreviousWordBoundary(string text, int index)
    {
        if (string.IsNullOrEmpty(text) || index <= 0)
            return 0;

        if (index > text.Length)
            index = text.Length;

        // Get all grapheme clusters with their positions
        var clusters = GetGraphemeClusters(text);
        
        // Find the cluster index we're at or just before
        int clusterIdx = clusters.Count;
        for (int i = 0; i < clusters.Count; i++)
        {
            if (clusters[i].EndIndex >= index)
            {
                clusterIdx = clusters[i].EndIndex == index ? i + 1 : i;
                break;
            }
        }

        if (clusterIdx == 0)
            return 0;

        // Phase 1: Skip backward over non-word characters (whitespace, punctuation)
        while (clusterIdx > 0 && !IsWordCluster(clusters[clusterIdx - 1].Text))
        {
            clusterIdx--;
        }

        // Phase 2: Skip backward over word characters
        while (clusterIdx > 0 && IsWordCluster(clusters[clusterIdx - 1].Text))
        {
            clusterIdx--;
        }

        return clusterIdx == 0 ? 0 : clusters[clusterIdx].StartIndex;
    }

    /// <summary>
    /// Gets the string index of the start of the next word.
    /// Used for Ctrl+Right navigation. Skips forward over word chars, then non-word chars.
    /// </summary>
    /// <param name="text">The text to navigate</param>
    /// <param name="index">The current cursor position (0 to text.Length)</param>
    /// <returns>The index of the start of the next word, or text.Length if at end</returns>
    public static int GetNextWordBoundary(string text, int index)
    {
        if (string.IsNullOrEmpty(text) || index >= text.Length)
            return text?.Length ?? 0;

        if (index < 0)
            index = 0;

        // Get all grapheme clusters with their positions
        var clusters = GetGraphemeClusters(text);
        
        if (clusters.Count == 0)
            return text.Length;

        // Find the cluster index we're at or just after
        int clusterIdx = 0;
        for (int i = 0; i < clusters.Count; i++)
        {
            if (clusters[i].StartIndex >= index)
            {
                clusterIdx = i;
                break;
            }
            if (clusters[i].EndIndex > index)
            {
                clusterIdx = i;
                break;
            }
            clusterIdx = i + 1;
        }

        if (clusterIdx >= clusters.Count)
            return text.Length;

        // Phase 1: Skip forward over word characters
        while (clusterIdx < clusters.Count && IsWordCluster(clusters[clusterIdx].Text))
        {
            clusterIdx++;
        }

        // Phase 2: Skip forward over non-word characters (whitespace, punctuation)
        while (clusterIdx < clusters.Count && !IsWordCluster(clusters[clusterIdx].Text))
        {
            clusterIdx++;
        }

        return clusterIdx >= clusters.Count ? text.Length : clusters[clusterIdx].StartIndex;
    }

    /// <summary>
    /// Determines if a grapheme cluster is a "word" character (letter, digit, or underscore).
    /// </summary>
    private static bool IsWordCluster(string cluster)
    {
        if (string.IsNullOrEmpty(cluster))
            return false;

        // Check the first character's Unicode category
        // For multi-char clusters (emojis, combining marks), use first base char
        foreach (var rune in cluster.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            return category switch
            {
                UnicodeCategory.UppercaseLetter => true,
                UnicodeCategory.LowercaseLetter => true,
                UnicodeCategory.TitlecaseLetter => true,
                UnicodeCategory.ModifierLetter => true,
                UnicodeCategory.OtherLetter => true,
                UnicodeCategory.DecimalDigitNumber => true,
                UnicodeCategory.LetterNumber => true,
                UnicodeCategory.OtherNumber => true,
                UnicodeCategory.ConnectorPunctuation => true, // Includes underscore
                _ => false
            };
        }
        return false;
    }

    /// <summary>
    /// Gets all grapheme clusters with their start and end indices.
    /// </summary>
    private static List<(string Text, int StartIndex, int EndIndex)> GetGraphemeClusters(string text)
    {
        var result = new List<(string Text, int StartIndex, int EndIndex)>();
        
        if (string.IsNullOrEmpty(text))
            return result;

        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            var cluster = (string)enumerator.Current;
            var start = enumerator.ElementIndex;
            result.Add((cluster, start, start + cluster.Length));
        }
        
        return result;
    }
}
