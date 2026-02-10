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
        // These are BMP characters with Emoji_Presentation=Yes in Unicode data
        // They render as emoji (width 2) by default in terminals
        // See: https://unicode.org/Public/emoji/latest/emoji-data.txt
        
        // Misc Symbols with default emoji presentation
        return codePoint switch
        {
            // Dingbats with Emoji_Presentation
            0x2705 => true,  // ✅ White Heavy Check Mark
            0x2714 => true,  // ✔ Heavy Check Mark (note: some terminals differ)
            0x2716 => true,  // ✖ Heavy Multiplication X
            0x274C => true,  // ❌ Cross Mark
            0x274E => true,  // ❎ Negative Squared Cross Mark
            0x2753 => true,  // ❓ Question Mark Ornament
            0x2754 => true,  // ❔ White Question Mark Ornament
            0x2755 => true,  // ❕ White Exclamation Mark Ornament
            0x2757 => true,  // ❗ Exclamation Mark
            0x2763 => true,  // ❣ Heavy Heart Exclamation Mark Ornament
            0x2764 => true,  // ❤ Heavy Black Heart
            0x2795 => true,  // ➕ Heavy Plus Sign
            0x2796 => true,  // ➖ Heavy Minus Sign
            0x2797 => true,  // ➗ Heavy Division Sign
            0x27A1 => true,  // ➡ Black Rightwards Arrow
            0x27B0 => true,  // ➰ Curly Loop
            0x27BF => true,  // ➿ Double Curly Loop
            
            // Miscellaneous Symbols with Emoji_Presentation
            0x2600 => true,  // ☀ Black Sun With Rays
            0x2601 => true,  // ☁ Cloud
            0x260E => true,  // ☎ Black Telephone
            0x2611 => true,  // ☑ Ballot Box With Check
            0x2614 => true,  // ☔ Umbrella With Rain Drops
            0x2615 => true,  // ☕ Hot Beverage
            0x2618 => true,  // ☘ Shamrock
            0x261D => true,  // ☝ White Up Pointing Index
            0x2620 => true,  // ☠ Skull and Crossbones
            0x2622 => true,  // ☢ Radioactive Sign
            0x2623 => true,  // ☣ Biohazard Sign
            0x2626 => true,  // ☦ Orthodox Cross
            0x262A => true,  // ☪ Star and Crescent
            0x262E => true,  // ☮ Peace Symbol
            0x262F => true,  // ☯ Yin Yang
            0x2638 => true,  // ☸ Wheel of Dharma
            0x2639 => true,  // ☹ White Frowning Face
            0x263A => true,  // ☺ White Smiling Face
            0x2640 => true,  // ♀ Female Sign
            0x2642 => true,  // ♂ Male Sign
            0x2648 => true,  // ♈ Aries
            0x2649 => true,  // ♉ Taurus
            0x264A => true,  // ♊ Gemini
            0x264B => true,  // ♋ Cancer
            0x264C => true,  // ♌ Leo
            0x264D => true,  // ♍ Virgo
            0x264E => true,  // ♎ Libra
            0x264F => true,  // ♏ Scorpius
            0x2650 => true,  // ♐ Sagittarius
            0x2651 => true,  // ♑ Capricorn
            0x2652 => true,  // ♒ Aquarius
            0x2653 => true,  // ♓ Pisces
            0x265F => true,  // ♟ Black Chess Pawn
            0x2660 => true,  // ♠ Black Spade Suit
            0x2663 => true,  // ♣ Black Club Suit
            0x2665 => true,  // ♥ Black Heart Suit
            0x2666 => true,  // ♦ Black Diamond Suit
            0x2668 => true,  // ♨ Hot Springs
            0x267B => true,  // ♻ Black Universal Recycling Symbol
            0x267E => true,  // ♾ Permanent Paper Sign
            0x267F => true,  // ♿ Wheelchair Symbol
            0x2692 => true,  // ⚒ Hammer and Pick
            0x2693 => true,  // ⚓ Anchor
            0x2694 => true,  // ⚔ Crossed Swords
            0x2695 => true,  // ⚕ Staff of Aesculapius
            0x2696 => true,  // ⚖ Scales
            0x2697 => true,  // ⚗ Alembic
            0x2699 => true,  // ⚙ Gear
            0x269B => true,  // ⚛ Atom Symbol
            0x269C => true,  // ⚜ Fleur-de-lis
            0x26A0 => true,  // ⚠ Warning Sign
            0x26A1 => true,  // ⚡ High Voltage Sign
            0x26AA => true,  // ⚪ Medium White Circle
            0x26AB => true,  // ⚫ Medium Black Circle
            0x26B0 => true,  // ⚰ Coffin
            0x26B1 => true,  // ⚱ Funeral Urn
            0x26BD => true,  // ⚽ Soccer Ball
            0x26BE => true,  // ⚾ Baseball
            0x26C4 => true,  // ⛄ Snowman Without Snow
            0x26C5 => true,  // ⛅ Sun Behind Cloud
            0x26CE => true,  // ⛎ Ophiuchus
            0x26D4 => true,  // ⛔ No Entry
            0x26EA => true,  // ⛪ Church
            0x26F2 => true,  // ⛲ Fountain
            0x26F3 => true,  // ⛳ Flag in Hole
            0x26F5 => true,  // ⛵ Sailboat
            0x26FA => true,  // ⛺ Tent
            0x26FD => true,  // ⛽ Fuel Pump
            
            // Geometric shapes
            0x2B05 => true,  // ⬅ Leftwards Black Arrow
            0x2B06 => true,  // ⬆ Upwards Black Arrow
            0x2B07 => true,  // ⬇ Downwards Black Arrow
            0x2B1B => true,  // ⬛ Black Large Square
            0x2B1C => true,  // ⬜ White Large Square
            0x2B50 => true,  // ⭐ White Medium Star
            0x2B55 => true,  // ⭕ Heavy Large Circle
            
            // Miscellaneous Technical
            0x231A => true,  // ⌚ Watch
            0x231B => true,  // ⌛ Hourglass
            0x23E9 => true,  // ⏩ Fast-forward
            0x23EA => true,  // ⏪ Rewind
            0x23EB => true,  // ⏫ Fast Up
            0x23EC => true,  // ⏬ Fast Down
            0x23F0 => true,  // ⏰ Alarm Clock
            0x23F3 => true,  // ⏳ Hourglass Not Done
            
            _ => false
        };
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

    /// <summary>
    /// Checks if a code point is an emoji that typically displays as 2 cells.
    /// This uses a comprehensive approach covering all known emoji blocks.
    /// </summary>
    private static bool IsEmojiPresentation(int codePoint)
    {
        // SMP Emoji Blocks (U+1F000 - U+1FFFF range)
        // Using broader ranges to be more future-proof
        
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
        // Alchemical Symbols - skip (U+1F700-1F77F, not emoji)
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
        
        // BMP Emoji Blocks
        
        // Miscellaneous Symbols (☀️⚡⚠️ etc)
        if (codePoint >= 0x2600 && codePoint <= 0x26FF)
            return true;
        // Dingbats (✂️✈️✉️ etc)
        if (codePoint >= 0x2700 && codePoint <= 0x27BF)
            return true;
        // Supplemental Arrows-B (some arrow emoji)
        if (codePoint >= 0x2900 && codePoint <= 0x297F)
            return true;
        // Miscellaneous Symbols and Arrows
        if (codePoint >= 0x2B00 && codePoint <= 0x2BFF)
            return true;
        // CJK Symbols (some emoji like ㊗️㊙️)
        if (codePoint >= 0x3200 && codePoint <= 0x32FF)
            return true;
        // Enclosed CJK Letters and Months
        if (codePoint >= 0x3300 && codePoint <= 0x33FF)
            return true;
            
        // Specific standalone emoji characters
        // Copyright, Registered, Trademark
        if (codePoint == 0x00A9 || codePoint == 0x00AE || codePoint == 0x2122)
            return true;
        // Information source (ℹ️)
        if (codePoint == 0x2139)
            return true;
        // Left/right arrows (↩️↪️)
        if (codePoint == 0x21A9 || codePoint == 0x21AA)
            return true;
        // Watch and hourglass
        if (codePoint == 0x231A || codePoint == 0x231B)
            return true;
        // Keyboard (⌨️)
        if (codePoint == 0x2328)
            return true;
        // Eject symbol (⏏️)
        if (codePoint >= 0x23CF && codePoint <= 0x23F3)
            return true;
        // Media control symbols (⏩⏪⏫⏬ etc)
        if (codePoint >= 0x23E9 && codePoint <= 0x23F3)
            return true;
        // Alarm clock, stopwatch, timer
        if (codePoint >= 0x23F0 && codePoint <= 0x23F3)
            return true;
        // Additional media controls
        if (codePoint >= 0x23F8 && codePoint <= 0x23FA)
            return true;
        // Scales, alembic, etc
        if (codePoint == 0x2696 || codePoint == 0x2697 || codePoint == 0x2699)
            return true;
        // NOTE: The following characters have text presentation as their default.
        // They are only wide (2 cells) when followed by U+FE0F (variation selector-16).
        // Since GetGraphemeWidth already handles VS16 by returning 2, we DON'T list them here.
        // - U+25AA, U+25AB (black/white small square)
        // - U+25B6, U+25C0 (play/reverse triangles - used as scroll arrows)
        // - U+25FB-U+25FE (medium squares)
        // If they were listed here, they'd incorrectly be width 2 in text mode.
            
        return false;
    }
}
