using System.Globalization;

namespace Hex1b.Terminal;

internal static class AnsiString
{
    private const char Escape = '\x1b';

    public static int VisibleLength(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Strip ANSI codes first, then calculate display width
        var stripped = StripAnsiCodes(text);
        return DisplayWidth.GetStringWidth(stripped);
    }

    /// <summary>
    /// Strips all ANSI escape codes (CSI and OSC) from the text.
    /// </summary>
    private static string StripAnsiCodes(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";
            
        var result = new System.Text.StringBuilder();
        for (var i = 0; i < text.Length;)
        {
            if (TryReadCsi(text, i, out var nextIndex))
            {
                i = nextIndex;
                continue;
            }
            if (TryReadOsc(text, i, out nextIndex))
            {
                i = nextIndex;
                continue;
            }
            result.Append(text[i]);
            i++;
        }
        return result.ToString();
    }

    public static string SliceByColumns(string text, int startColumn, int lengthColumns)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        if (lengthColumns <= 0)
            return "";
        if (startColumn < 0)
            startColumn = 0;

        // Collect style codes that appear before the first included visible character.
        var prefix = new System.Text.StringBuilder();
        var output = new System.Text.StringBuilder();

        var currentColumn = 0;
        var started = false;

        var endExclusive = startColumn + lengthColumns;

        // Process the text grapheme by grapheme to handle wide characters correctly
        var i = 0;
        while (i < text.Length)
        {
            // Check for ANSI escape sequences first (CSI and OSC)
            if (TryReadCsi(text, i, out var nextIndex))
            {
                var seq = text.Substring(i, nextIndex - i);
                if (!started)
                    prefix.Append(seq);
                else
                    output.Append(seq);

                i = nextIndex;
                continue;
            }
            
            if (TryReadOsc(text, i, out nextIndex))
            {
                var seq = text.Substring(i, nextIndex - i);
                if (!started)
                    prefix.Append(seq);
                else
                    output.Append(seq);

                i = nextIndex;
                continue;
            }

            // Get the grapheme cluster at this position
            var grapheme = GetGraphemeAt(text, i, out var graphemeLength);
            var graphemeWidth = DisplayWidth.GetGraphemeWidth(grapheme);

            // Skip graphemes that end before our start column
            if (currentColumn + graphemeWidth <= startColumn)
            {
                currentColumn += graphemeWidth;
                i += graphemeLength;
                continue;
            }

            // If we're partially into a wide character (start column is in the middle),
            // skip it but potentially add a space placeholder
            if (currentColumn < startColumn && currentColumn + graphemeWidth > startColumn)
            {
                // Skip this grapheme - it starts before our slice
                currentColumn += graphemeWidth;
                i += graphemeLength;
                continue;
            }

            // Stop if adding this grapheme would exceed our length
            if (currentColumn >= endExclusive)
                break;

            // If the grapheme would extend past our end, we might need to skip it
            if (currentColumn + graphemeWidth > endExclusive)
            {
                // Wide character doesn't fully fit - stop here
                break;
            }

            if (!started)
            {
                output.Append(prefix);
                started = true;
            }

            output.Append(grapheme);
            currentColumn += graphemeWidth;
            i += graphemeLength;
        }

        if (!started)
            return "";

        // Preserve any escape sequences that immediately follow the slice.
        while (i < text.Length)
        {
            if (TryReadCsi(text, i, out var nextIndex))
            {
                output.Append(text.Substring(i, nextIndex - i));
                i = nextIndex;
                continue;
            }
            if (TryReadOsc(text, i, out nextIndex))
            {
                output.Append(text.Substring(i, nextIndex - i));
                i = nextIndex;
                continue;
            }
            break;
        }

        return output.ToString();
    }

    /// <summary>
    /// Gets the grapheme cluster at the specified position in the string.
    /// </summary>
    private static string GetGraphemeAt(string text, int index, out int length)
    {
        if (index >= text.Length)
        {
            length = 0;
            return "";
        }

        // Find the grapheme that contains this index
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            if (enumerator.ElementIndex == index)
            {
                var grapheme = (string)enumerator.Current;
                length = grapheme.Length;
                return grapheme;
            }
            if (enumerator.ElementIndex > index)
            {
                // We're past the index - this shouldn't happen if index is at a grapheme boundary
                break;
            }
        }

        // Fallback: return single character
        if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            length = 2;
            return text.Substring(index, 2);
        }
        
        length = 1;
        return text[index].ToString();
    }

    public static string TrailingEscapeSuffix(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        // Find the end index of the last printable character.
        var lastPrintableEnd = 0;
        for (var i = 0; i < text.Length;)
        {
            if (TryReadCsi(text, i, out var nextIndex))
            {
                i = nextIndex;
                continue;
            }
            if (TryReadOsc(text, i, out nextIndex))
            {
                i = nextIndex;
                continue;
            }

            // Treat any non-escape as printable for our purposes.
            lastPrintableEnd = i + 1;
            i++;
        }

        if (lastPrintableEnd >= text.Length)
            return "";

        // Ensure suffix contains only full escape sequences (CSI or OSC).
        for (var i = lastPrintableEnd; i < text.Length;)
        {
            if (TryReadCsi(text, i, out var nextIndex))
            {
                i = nextIndex;
                continue;
            }
            if (TryReadOsc(text, i, out nextIndex))
            {
                i = nextIndex;
                continue;
            }
            return "";
        }

        return text.Substring(lastPrintableEnd);
    }

    private static bool TryReadCsi(string text, int index, out int nextIndex)
    {
        nextIndex = index;
        if (index < 0 || index >= text.Length)
            return false;

        if (text[index] != Escape)
            return false;
        if (index + 1 >= text.Length || text[index + 1] != '[')
            return false;

        // CSI sequence: ESC [ ... <final byte>
        for (var i = index + 2; i < text.Length; i++)
        {
            var c = text[i];
            if (c >= '@' && c <= '~')
            {
                nextIndex = i + 1;
                return true;
            }
        }

        // Incomplete CSI sequence.
        return false;
    }
    
    /// <summary>
    /// Tries to read an OSC (Operating System Command) sequence.
    /// OSC sequences start with ESC ] and end with ST (String Terminator).
    /// ST can be ESC \ or BEL (\x07).
    /// </summary>
    private static bool TryReadOsc(string text, int index, out int nextIndex)
    {
        nextIndex = index;
        if (index < 0 || index >= text.Length)
            return false;

        if (text[index] != Escape)
            return false;
        if (index + 1 >= text.Length || text[index + 1] != ']')
            return false;

        // OSC sequence: ESC ] ... ST
        // ST can be ESC \ or BEL (\x07)
        for (var i = index + 2; i < text.Length; i++)
        {
            // Check for ST = ESC \ (two characters)
            if (text[i] == Escape && i + 1 < text.Length && text[i + 1] == '\\')
            {
                nextIndex = i + 2;
                return true;
            }
            // Check for ST = BEL (\x07)
            if (text[i] == '\x07')
            {
                nextIndex = i + 1;
                return true;
            }
        }

        // Incomplete OSC sequence.
        return false;
    }
}
