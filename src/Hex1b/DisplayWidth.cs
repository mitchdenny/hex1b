using System.Globalization;
using System.Text;

namespace Hex1b;

/// <summary>
/// Provides methods to calculate the terminal display width of Unicode text.
/// 
/// In terminal emulators, most characters occupy 1 cell, but some characters
/// (like CJK ideographs, emojis, and certain other symbols) occupy 2 cells.
/// Combining characters (accents, etc.) occupy 0 cells as they combine with
/// the previous character.
/// 
/// This is based on the Unicode East Asian Width property and wcwidth behavior.
/// </summary>
public static class DisplayWidth
{
    /// <summary>
    /// Gets the terminal display width of a single Unicode code point.
    /// Returns 0 for combining characters, 2 for wide characters, 1 for others.
    /// </summary>
    public static int GetCodePointWidth(int codePoint)
    {
        // Control characters and null
        if (codePoint < 32 || codePoint == 0x7F)
            return 0;

        // Combining characters have zero width
        if (IsCombiningCharacter(codePoint))
            return 0;

        // Wide characters (East Asian Wide/Fullwidth, most emoji, etc.)
        if (IsWideCharacter(codePoint))
            return 2;

        return 1;
    }

    /// <summary>
    /// Gets the terminal display width of a single Rune.
    /// </summary>
    public static int GetRuneWidth(Rune rune)
    {
        return GetCodePointWidth(rune.Value);
    }

    /// <summary>
    /// Gets the terminal display width of a grapheme cluster.
    /// For a cluster like "👨‍👩‍👧" or "é" (e + combining accent), returns the total display width.
    /// </summary>
    public static int GetGraphemeWidth(string grapheme)
    {
        if (string.IsNullOrEmpty(grapheme))
            return 0;

        // Most grapheme clusters display as a single unit.
        // Emoji sequences (ZWJ, skin tones, flags) typically display as 2 cells.
        // Combining sequences display as the width of their base character.
        
        // Check for variation selectors first - they explicitly set the presentation
        bool hasVS16 = false;  // U+FE0F - emoji presentation (wide)
        bool hasVS15 = false;  // U+FE0E - text presentation (narrow)
        bool hasKeycap = false; // U+20E3 - combining enclosing keycap
        
        foreach (var rune in grapheme.EnumerateRunes())
        {
            if (rune.Value == 0x20E3) // Combining enclosing keycap
                hasKeycap = true;
            if (rune.Value == 0xFE0F) // Variation selector-16 (emoji presentation)
                hasVS16 = true;
            if (rune.Value == 0xFE0E) // Variation selector-15 (text presentation)
                hasVS15 = true;
        }
        
        // Keycap sequences are always 2 cells wide (like 1️⃣)
        if (hasKeycap)
            return 2;
        
        // VS16 forces emoji presentation (2 cells)
        if (hasVS16)
            return 2;
        
        // VS15 forces text presentation - use GetRuneWidth for the base character
        // (fall through to normal width calculation)
        
        int width = 0;
        foreach (var rune in grapheme.EnumerateRunes())
        {
            var runeWidth = GetRuneWidth(rune);
            if (runeWidth > 0)
            {
                // For SMP emoji (U+1F000+), default to emoji presentation (2 cells)
                if (!hasVS15 && IsSmpEmoji(rune.Value))
                {
                    return 2;
                }
                
                // For BMP characters with Emoji_Presentation property (like ✅❌),
                // default to emoji presentation (2 cells) unless VS15 is present
                if (!hasVS15 && HasDefaultEmojiPresentation(rune.Value))
                {
                    return 2;
                }
                
                // For other BMP characters (including those that CAN be emoji like ✓),
                // default to text presentation (1 cell) unless VS16 was present
                width = Math.Max(width, runeWidth);
            }
        }
        
        // If no visible characters, return 0
        return width;
    }
    
    /// <summary>
    /// Checks if a BMP codepoint has the Emoji_Presentation property,
    /// meaning it defaults to emoji (wide) presentation without needing VS16.
    /// </summary>
    private static bool HasDefaultEmojiPresentation(int codePoint)
    {
        // Unicode defines Emoji_Presentation=Yes for many BMP characters, but
        // terminal emulators do NOT use that property for width. All major terminals
        // (GNOME Terminal, Kitty, Alacritty, Windows Terminal, xterm.js, Ghostty)
        // render these BMP characters as 1-cell wide in Western locales.
        //
        // Characters that NEED emoji presentation (2-wide) get it via:
        // - VS16 (U+FE0F) — already handled in GetGraphemeWidth at line 80
        // - SMP emoji (U+1F000+) — handled by IsSmpEmoji in IsWideCharacter
        //
        // Returning true here was causing ♠♣♥♦⚡☀☁ etc. to be measured as
        // 2-wide, mismatching the terminal's actual 1-wide rendering and
        // causing cursor drift / stale cell artifacts in the editor.
        return false;
    }
    
    /// <summary>
    /// Checks if a codepoint is in the Supplementary Multilingual Plane (SMP) emoji ranges.
    /// These characters default to emoji presentation (width 2).
    /// BMP characters (U+0000-U+FFFF) that can be emoji default to text presentation.
    /// </summary>
    private static bool IsSmpEmoji(int codePoint)
    {
        // SMP Emoji Blocks (U+1F000 - U+1FFFF range)
        // These default to emoji presentation
        
        // Mahjong Tiles and Domino Tiles
        if (codePoint >= 0x1F000 && codePoint <= 0x1F0FF)
            return true;
        // Playing Cards
        if (codePoint >= 0x1F0A0 && codePoint <= 0x1F0FF)
            return true;
        // Enclosed Alphanumeric Supplement (some emoji like 🅰️🅱️)
        if (codePoint >= 0x1F100 && codePoint <= 0x1F1FF)
            return true;
        // Enclosed Ideographic Supplement
        if (codePoint >= 0x1F200 && codePoint <= 0x1F2FF)
            return true;
        // Miscellaneous Symbols and Pictographs
        if (codePoint >= 0x1F300 && codePoint <= 0x1F5FF)
            return true;
        // Emoticons
        if (codePoint >= 0x1F600 && codePoint <= 0x1F64F)
            return true;
        // Ornamental Dingbats
        if (codePoint >= 0x1F650 && codePoint <= 0x1F67F)
            return true;
        // Transport and Map Symbols
        if (codePoint >= 0x1F680 && codePoint <= 0x1F6FF)
            return true;
        // Geometric Shapes Extended (colored circles, squares like 🟠🟡🟢🔵)
        if (codePoint >= 0x1F780 && codePoint <= 0x1F7FF)
            return true;
        // Supplemental Arrows-C
        if (codePoint >= 0x1F800 && codePoint <= 0x1F8FF)
            return true;
        // Supplemental Symbols and Pictographs
        if (codePoint >= 0x1F900 && codePoint <= 0x1F9FF)
            return true;
        // Chess Symbols, Symbols and Pictographs Extended-A/B
        if (codePoint >= 0x1FA00 && codePoint <= 0x1FAFF)
            return true;
            
        return false;
    }

    /// <summary>
    /// Gets the total terminal display width of a string, respecting grapheme clusters.
    /// </summary>
    public static int GetStringWidth(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // PERF: StringInfo.GetTextElementEnumerator allocates a new string per grapheme cluster.
        // For printable ASCII (0x20–0x7E), every char is exactly one display column and one
        // grapheme cluster, so we can return text.Length directly. This fast-path avoids all
        // allocations for the common case of ASCII-only content (labels, borders, padding).
        //
        // PITFALL: Control chars (< 0x20) and DEL (0x7F) are zero-width; chars >= 0x80 may be
        // multi-column (CJK) or combining marks. Any such char forces the slow path.
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c < 0x20 || c == 0x7F || c >= 0x80)
            {
                goto SlowPath;
            }
        }

        return text.Length;

        SlowPath:
        int totalWidth = 0;
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            var grapheme = (string)enumerator.Current;
            totalWidth += GetGraphemeWidth(grapheme);
        }
        
        return totalWidth;
    }

    /// <summary>
    /// Slices a string by display width columns, returning the substring that fits.
    /// </summary>
    /// <param name="text">The text to slice.</param>
    /// <param name="startColumn">The starting column (0-based).</param>
    /// <param name="maxColumns">Maximum number of columns to include.</param>
    /// <returns>
    /// A tuple of (slicedText, columnsUsed, paddingNeeded).
    /// paddingNeeded is the number of spaces needed if a wide character was cut.
    /// </returns>
    public static (string text, int columns, int paddingBefore, int paddingAfter) SliceByDisplayWidth(
        string text, int startColumn, int maxColumns)
    {
        if (string.IsNullOrEmpty(text) || maxColumns <= 0)
            return ("", 0, 0, 0);

        var result = new System.Text.StringBuilder();
        int currentColumn = 0;
        int columnsUsed = 0;
        int paddingBefore = 0;
        int paddingAfter = 0;
        
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            var grapheme = (string)enumerator.Current;
            var graphemeWidth = GetGraphemeWidth(grapheme);
            
            // Skip graphemes before start column
            if (currentColumn + graphemeWidth <= startColumn)
            {
                currentColumn += graphemeWidth;
                continue;
            }
            
            // Check if we're starting in the middle of a wide character
            if (currentColumn < startColumn && currentColumn + graphemeWidth > startColumn)
            {
                // We're cutting into a wide character - need padding
                paddingBefore = graphemeWidth - (startColumn - currentColumn);
                currentColumn += graphemeWidth;
                continue;
            }
            
            // Check if adding this grapheme would exceed our limit
            if (columnsUsed + graphemeWidth > maxColumns)
            {
                // Would this grapheme partially fit?
                if (columnsUsed < maxColumns && graphemeWidth > 1)
                {
                    // Wide character doesn't fit - need padding after
                    paddingAfter = maxColumns - columnsUsed;
                }
                break;
            }
            
            result.Append(grapheme);
            columnsUsed += graphemeWidth;
            currentColumn += graphemeWidth;
        }
        
        return (result.ToString(), columnsUsed, paddingBefore, paddingAfter);
    }

    /// <summary>
    /// Slices a string by display width columns, handling ANSI escape sequences.
    /// ANSI escape sequences are preserved and don't count towards the column width.
    /// </summary>
    /// <param name="text">The text to slice (may contain ANSI escape sequences).</param>
    /// <param name="startColumn">The starting column (0-based).</param>
    /// <param name="maxColumns">Maximum number of columns to include.</param>
    /// <returns>
    /// A tuple of (slicedText, columnsUsed, paddingBefore, paddingAfter).
    /// The sliced text includes any ANSI codes that precede included characters.
    /// </returns>
    public static (string text, int columns, int paddingBefore, int paddingAfter) SliceByDisplayWidthWithAnsi(
        string text, int startColumn, int maxColumns)
    {
        if (string.IsNullOrEmpty(text) || maxColumns <= 0)
            return ("", 0, 0, 0);

        var prefix = new StringBuilder(); // ANSI codes before first visible char
        var result = new StringBuilder();
        int currentColumn = 0;
        int columnsUsed = 0;
        int paddingBefore = 0;
        int paddingAfter = 0;
        bool started = false;

        int i = 0;
        while (i < text.Length)
        {
            // Check for ANSI escape sequence (ESC [ ... final byte) - CSI sequences
            if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == '[')
            {
                // Find the end of the CSI sequence
                var seqStart = i;
                i += 2; // Skip ESC [
                while (i < text.Length)
                {
                    var c = text[i];
                    if (c >= '@' && c <= '~')
                    {
                        i++; // Include final byte
                        break;
                    }
                    i++;
                }
                var seq = text.Substring(seqStart, i - seqStart);
                
                // Add to prefix or result depending on whether we've started
                if (!started)
                    prefix.Append(seq);
                else
                    result.Append(seq);
                continue;
            }
            
            // Check for OSC escape sequence (ESC ] ... ST) - OSC sequences like OSC 8 hyperlinks
            // ST (String Terminator) can be ESC \ or BEL (\x07)
            if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == ']')
            {
                var seqStart = i;
                i += 2; // Skip ESC ]
                while (i < text.Length)
                {
                    // Check for ST = ESC \ (two characters)
                    if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == '\\')
                    {
                        i += 2; // Include ESC \
                        break;
                    }
                    // Check for ST = BEL (single character \x07)
                    if (text[i] == '\x07')
                    {
                        i++; // Include BEL
                        break;
                    }
                    i++;
                }
                var seq = text.Substring(seqStart, i - seqStart);
                
                // Add to prefix or result depending on whether we've started
                if (!started)
                    prefix.Append(seq);
                else
                    result.Append(seq);
                continue;
            }

            // Get the grapheme cluster at this position
            var grapheme = GetGraphemeAtIndex(text, i, out var graphemeLength);
            var graphemeWidth = GetGraphemeWidth(grapheme);

            // Skip graphemes before start column
            if (currentColumn + graphemeWidth <= startColumn)
            {
                currentColumn += graphemeWidth;
                i += graphemeLength;
                continue;
            }

            // Check if we're starting in the middle of a wide character
            if (currentColumn < startColumn && currentColumn + graphemeWidth > startColumn)
            {
                // We're cutting into a wide character - need padding
                paddingBefore = graphemeWidth - (startColumn - currentColumn);
                currentColumn += graphemeWidth;
                i += graphemeLength;
                continue;
            }

            // Check if adding this grapheme would exceed our limit
            if (columnsUsed + graphemeWidth > maxColumns)
            {
                // Would this grapheme partially fit?
                if (columnsUsed < maxColumns && graphemeWidth > 1)
                {
                    // Wide character doesn't fit - need padding after
                    paddingAfter = maxColumns - columnsUsed;
                }
                break;
            }

            if (!started)
            {
                result.Append(prefix);
                started = true;
            }

            result.Append(grapheme);
            columnsUsed += graphemeWidth;
            currentColumn += graphemeWidth;
            i += graphemeLength;
        }

        // Collect any trailing ANSI sequences (CSI and OSC)
        while (i < text.Length)
        {
            // CSI sequences
            if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == '[')
            {
                var seqStart = i;
                i += 2;
                while (i < text.Length)
                {
                    var c = text[i];
                    if (c >= '@' && c <= '~')
                    {
                        i++;
                        break;
                    }
                    i++;
                }
                result.Append(text.Substring(seqStart, i - seqStart));
                continue;
            }
            // OSC sequences
            if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == ']')
            {
                var seqStart = i;
                i += 2;
                while (i < text.Length)
                {
                    if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == '\\')
                    {
                        i += 2;
                        break;
                    }
                    if (text[i] == '\x07')
                    {
                        i++;
                        break;
                    }
                    i++;
                }
                result.Append(text.Substring(seqStart, i - seqStart));
                continue;
            }
            break;
        }

        return (result.ToString(), columnsUsed, paddingBefore, paddingAfter);
    }

    /// <summary>
    /// Gets the grapheme cluster at the specified index in a string.
    /// </summary>
    private static string GetGraphemeAtIndex(string text, int index, out int length)
    {
        if (index >= text.Length)
        {
            length = 0;
            return "";
        }

        // Use StringInfo to find the grapheme at this position
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
                // Index is in the middle of a grapheme - shouldn't happen with proper navigation
                break;
            }
        }

        // Fallback: handle surrogate pairs
        if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            length = 2;
            return text.Substring(index, 2);
        }

        length = 1;
        return text[index].ToString();
    }

    /// <summary>
    /// Checks if a code point is a combining character (zero-width).
    /// </summary>
    private static bool IsCombiningCharacter(int codePoint)
    {
        // Unicode combining character ranges
        // Combining Diacritical Marks: U+0300-U+036F
        if (codePoint >= 0x0300 && codePoint <= 0x036F)
            return true;
        // Combining Diacritical Marks Extended: U+1AB0-U+1AFF
        if (codePoint >= 0x1AB0 && codePoint <= 0x1AFF)
            return true;
        // Combining Diacritical Marks Supplement: U+1DC0-U+1DFF
        if (codePoint >= 0x1DC0 && codePoint <= 0x1DFF)
            return true;
        // Combining Diacritical Marks for Symbols: U+20D0-U+20FF
        if (codePoint >= 0x20D0 && codePoint <= 0x20FF)
            return true;
        // Combining Half Marks: U+FE20-U+FE2F
        if (codePoint >= 0xFE20 && codePoint <= 0xFE2F)
            return true;
        // Zero Width Joiner and Zero Width Non-Joiner
        if (codePoint == 0x200D || codePoint == 0x200C)
            return true;
        // Variation selectors
        if (codePoint >= 0xFE00 && codePoint <= 0xFE0F)
            return true;
        // Skin tone modifiers (Fitzpatrick modifiers)
        if (codePoint >= 0x1F3FB && codePoint <= 0x1F3FF)
            return true;
            
        return false;
    }

    /// <summary>
    /// Checks if a code point is a wide character (2 cells in terminal).
    /// </summary>
    private static bool IsWideCharacter(int codePoint)
    {
        // CJK Unified Ideographs and related
        if (codePoint >= 0x4E00 && codePoint <= 0x9FFF)
            return true;
        if (codePoint >= 0x3400 && codePoint <= 0x4DBF) // CJK Extension A
            return true;
        if (codePoint >= 0x20000 && codePoint <= 0x2A6DF) // CJK Extension B
            return true;
        if (codePoint >= 0x2A700 && codePoint <= 0x2B73F) // CJK Extension C
            return true;
        if (codePoint >= 0x2B740 && codePoint <= 0x2B81F) // CJK Extension D
            return true;
        if (codePoint >= 0x2B820 && codePoint <= 0x2CEAF) // CJK Extension E
            return true;
        if (codePoint >= 0x2CEB0 && codePoint <= 0x2EBEF) // CJK Extension F
            return true;
        if (codePoint >= 0x30000 && codePoint <= 0x3134F) // CJK Extension G
            return true;
            
        // CJK Compatibility Ideographs
        if (codePoint >= 0xF900 && codePoint <= 0xFAFF)
            return true;
        if (codePoint >= 0x2F800 && codePoint <= 0x2FA1F)
            return true;
            
        // Hangul Syllables
        if (codePoint >= 0xAC00 && codePoint <= 0xD7AF)
            return true;
            
        // Hangul Jamo Extended
        if (codePoint >= 0xA960 && codePoint <= 0xA97F)
            return true;
        if (codePoint >= 0xD7B0 && codePoint <= 0xD7FF)
            return true;
            
        // Katakana and Hiragana
        if (codePoint >= 0x3040 && codePoint <= 0x30FF)
            return true;
        if (codePoint >= 0x31F0 && codePoint <= 0x31FF) // Katakana Phonetic Extensions
            return true;
            
        // Fullwidth Forms
        if (codePoint >= 0xFF00 && codePoint <= 0xFF60)
            return true;
        if (codePoint >= 0xFFE0 && codePoint <= 0xFFE6)
            return true;
            
        // SMP Emoji are wide (but BMP emoji default to text presentation unless VS16)
        // Note: BMP characters like ✓ (U+2713) are handled by GetGraphemeWidth
        // which checks for VS16 to determine emoji vs text presentation
        if (IsSmpEmoji(codePoint))
            return true;
            
        return false;
    }
}
