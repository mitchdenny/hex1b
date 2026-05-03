using System.Globalization;
using System.Text;

namespace Hex1b.Widgets;

/// <summary>
/// Internal parser for FIGfont 2.0 (<c>.flf</c>) files. Produces the inputs required to construct
/// a <see cref="FigletFont"/>: the header, the resolved <see cref="FigletLayoutInfo"/>, and the
/// glyph dictionary keyed by code point.
/// </summary>
/// <remarks>
/// <para>
/// The parser is strict about header structure but tolerant about codetag_count (advisory; we
/// parse glyphs until end-of-stream regardless). Lines are read using ISO-8859-1 (Latin-1) so
/// that hardblanks and sub-characters in the 128–255 range are preserved exactly as authored.
/// </para>
/// <para>
/// FIGcharacter rows are terminated by an "endmark" sub-character. The endmark is the LAST
/// character on each row, and any run of trailing endmarks (one or two, typically) is stripped.
/// The endmark may differ from row to row within a glyph — we look at the actual last character
/// each time. This matches reference figlet behavior.
/// </para>
/// </remarks>
internal static class FigletFontParser
{
    private static readonly Encoding s_latin1 = Encoding.GetEncoding("ISO-8859-1");

    private static readonly int[] s_requiredCodePoints =
    [
        // ASCII printable 32-126
        32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47,
        48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63,
        64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79,
        80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95,
        96, 97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111,
        112, 113, 114, 115, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126,

        // German block (Latin-1 supplement)
        196, // Ä
        214, // Ö
        220, // Ü
        228, // ä
        246, // ö
        252, // ü
        223, // ß
    ];

    /// <summary>
    /// Parses a complete FIGfont from a stream. The stream is read as Latin-1.
    /// </summary>
    public static FontData Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // Don't dispose the stream — caller owns it. leaveOpen: true.
        using var reader = new StreamReader(stream, s_latin1, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
        return ParseFromReader(reader);
    }

    /// <summary>
    /// Parses a complete FIGfont from an in-memory string. Used by <see cref="FigletFont.Parse(string)"/>.
    /// </summary>
    public static FontData Parse(string flfContent)
    {
        ArgumentNullException.ThrowIfNull(flfContent);

        using var reader = new StringReader(flfContent);
        return ParseFromReader(reader);
    }

    private static FontData ParseFromReader(TextReader reader)
    {
        // ---- Header line ----
        var headerLine = reader.ReadLine() ?? throw new FigletFontFormatException("FIGfont is empty.");
        var header = ParseHeader(headerLine);
        var layout = ResolveLayout(header);

        // ---- Comment block ----
        for (var i = 0; i < header.CommentLines; i++)
        {
            // Reading the line is enough; we don't store comment text. EOF mid-block is an error.
            if (reader.ReadLine() is null)
            {
                throw new FigletFontFormatException(
                    $"FIGfont ended before all {header.CommentLines} comment line(s) were read (got {i}).");
            }
        }

        // ---- Required FIGcharacters (in order) ----
        var glyphs = new Dictionary<int, FigletGlyph>(capacity: 128);
        for (var i = 0; i < s_requiredCodePoints.Length; i++)
        {
            var codePoint = s_requiredCodePoints[i];
            var glyph = ReadGlyph(reader, header.Height, codePoint, isCodeTagged: false)
                ?? throw new FigletFontFormatException(
                    $"FIGfont ended before required FIGcharacter U+{codePoint:X4} was read.");
            glyphs[codePoint] = glyph;
        }

        // ---- Code-tagged FIGcharacters (parse to EOF; codetag_count is advisory) ----
        while (true)
        {
            var tagLine = reader.ReadLine();
            if (tagLine is null)
            {
                break;
            }

            // Allow blank lines between code-tagged glyphs (some hand-edited fonts include them).
            if (string.IsNullOrWhiteSpace(tagLine))
            {
                continue;
            }

            var (codePoint, skip) = ParseCodeTag(tagLine);
            var glyph = ReadGlyph(reader, header.Height, codePoint, isCodeTagged: true)
                ?? throw new FigletFontFormatException(
                    $"FIGfont ended in the middle of code-tagged FIGcharacter {codePoint}.");

            if (!skip)
            {
                glyphs[codePoint] = glyph;
            }
        }

        return new FontData(header, layout, glyphs);
    }

    // ----- Header --------------------------------------------------------------------------

    private static FigletFontHeader ParseHeader(string headerLine)
    {
        // Format: "flf2a$ 6 5 16 15 15 0 24463 229"
        // The signature is the first 5 chars and the 6th char is the hardblank.
        if (headerLine.Length < 6)
        {
            throw new FigletFontFormatException(
                $"FIGfont header is too short: '{headerLine}'.");
        }

        if (!headerLine.StartsWith("flf2a", StringComparison.Ordinal))
        {
            throw new FigletFontFormatException(
                $"FIGfont header signature must be 'flf2a' (got '{headerLine[..Math.Min(5, headerLine.Length)]}').");
        }

        var hardblank = headerLine[5];

        // Parameters start after the hardblank, separated by whitespace.
        var rest = headerLine[6..].Trim();
        var parts = rest.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 5)
        {
            throw new FigletFontFormatException(
                $"FIGfont header must have at least 5 numeric parameters after the signature/hardblank (got {parts.Length}): '{headerLine}'.");
        }

        var height = ParseInt(parts[0], "height");
        var baseline = ParseInt(parts[1], "baseline");
        var maxLength = ParseInt(parts[2], "max_length");
        var oldLayout = ParseInt(parts[3], "old_layout");
        var commentLines = ParseInt(parts[4], "comment_lines");

        if (height < 1)
        {
            throw new FigletFontFormatException($"FIGfont height must be >= 1 (got {height}).");
        }
        if (baseline < 1 || baseline > height)
        {
            throw new FigletFontFormatException(
                $"FIGfont baseline must be in [1, height] (got baseline={baseline}, height={height}).");
        }
        if (oldLayout < -1 || oldLayout > 63)
        {
            throw new FigletFontFormatException(
                $"FIGfont old_layout must be in [-1, 63] (got {oldLayout}).");
        }
        if (commentLines < 0)
        {
            throw new FigletFontFormatException(
                $"FIGfont comment_lines must be >= 0 (got {commentLines}).");
        }

        int? printDirection = parts.Length > 5 ? ParseInt(parts[5], "print_direction") : null;
        int? fullLayout = parts.Length > 6 ? ParseInt(parts[6], "full_layout") : null;
        int? codetagCount = parts.Length > 7 ? ParseInt(parts[7], "codetag_count") : null;

        if (fullLayout is < 0 or > 32767)
        {
            throw new FigletFontFormatException(
                $"FIGfont full_layout must be in [0, 32767] (got {fullLayout}).");
        }

        return new FigletFontHeader
        {
            Hardblank = hardblank,
            Height = height,
            Baseline = baseline,
            MaxLength = maxLength,
            OldLayout = oldLayout,
            CommentLines = commentLines,
            PrintDirection = printDirection,
            FullLayout = fullLayout,
            CodetagCount = codetagCount,
        };
    }

    private static int ParseInt(string text, string fieldName)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new FigletFontFormatException(
                $"FIGfont header field '{fieldName}' must be an integer (got '{text}').");
        }
        return value;
    }

    // ----- Layout resolution ---------------------------------------------------------------

    private static FigletLayoutInfo ResolveLayout(FigletFontHeader header)
    {
        // Per the spec ("INTERPRETATION OF LAYOUT PARAMETERS"):
        //
        //   - If full_layout is present, IT is the source of truth.
        //   - Otherwise derive from old_layout:
        //       -1  -> full width
        //        0  -> horizontal fitting (kerning)
        //       >0  -> bitmask of horizontal smushing rules + smushing enabled
        //
        // We also compute vertical-axis flags only from full_layout (old_layout has no vertical
        // information at all).

        if (header.FullLayout is { } full)
        {
            var hRules = full & 0x3F;        // bits 1..32
            var hFitting = (full & 64) != 0;
            var hSmushing = (full & 128) != 0;
            var vRules = (full >> 8) & 0x1F; // bits 256..4096 shifted to bits 1..16
            var vFitting = (full & 8192) != 0;
            var vSmushing = (full & 16384) != 0;

            return new FigletLayoutInfo
            {
                HorizontalSmushingRules = hRules,
                HorizontalSmushing = hSmushing,
                HorizontalFitting = hFitting,
                VerticalSmushingRules = vRules,
                VerticalSmushing = vSmushing,
                VerticalFitting = vFitting,
            };
        }

        // Derive from old_layout only.
        if (header.OldLayout == -1)
        {
            return new FigletLayoutInfo();
        }

        if (header.OldLayout == 0)
        {
            return new FigletLayoutInfo { HorizontalFitting = true };
        }

        return new FigletLayoutInfo
        {
            HorizontalSmushingRules = header.OldLayout & 0x3F,
            HorizontalSmushing = true,
        };
    }

    // ----- FIGcharacter glyphs -------------------------------------------------------------

    private static FigletGlyph? ReadGlyph(TextReader reader, int height, int codePoint, bool isCodeTagged)
    {
        var rows = new string[height];
        for (var i = 0; i < height; i++)
        {
            var line = reader.ReadLine();
            if (line is null)
            {
                if (i == 0)
                {
                    return null;
                }
                throw new FigletFontFormatException(
                    $"FIGcharacter {codePoint} ended after {i} of {height} rows.");
            }
            rows[i] = StripEndmarks(line);
        }

        // The rule "all rows have the same length once endmarks are removed" is recommended but
        // not enforced strictly by reference figlet (it just left-aligns and ignores the rest).
        // We follow the same lenient policy: keep rows as-parsed and let FigletGlyph compute the
        // max width. This avoids rejecting otherwise-valid hand-authored fonts.
        _ = isCodeTagged;
        return new FigletGlyph(rows);
    }

    private static string StripEndmarks(string line)
    {
        if (line.Length == 0)
        {
            return string.Empty;
        }

        var endmark = line[^1];
        var end = line.Length;
        while (end > 0 && line[end - 1] == endmark)
        {
            end--;
        }
        return end == line.Length ? line : line[..end];
    }

    // ----- Code tag parsing ---------------------------------------------------------------

    private static (int CodePoint, bool Skip) ParseCodeTag(string line)
    {
        // A code tag is a number followed by whitespace and an optional comment.
        // Number forms (per the spec): decimal "65", hex "0x41" or "0X41", octal "0101"
        // (a leading "0" with no x). Negative tags use a leading "-".
        // Negative codes other than -1 are font-specific tags; we skip the glyph that follows
        // (parse it but don't store it). -1 is illegal per the spec.

        var trimmed = line.TrimStart();
        if (trimmed.Length == 0)
        {
            throw new FigletFontFormatException("Empty code tag line.");
        }

        // Find the end of the number token.
        var spaceIdx = trimmed.IndexOf(' ');
        var tabIdx = trimmed.IndexOf('\t');
        var endIdx = spaceIdx switch
        {
            < 0 => tabIdx,
            _ => tabIdx < 0 ? spaceIdx : Math.Min(spaceIdx, tabIdx)
        };
        var token = endIdx < 0 ? trimmed : trimmed[..endIdx];

        var negative = false;
        if (token.StartsWith('-'))
        {
            negative = true;
            token = token[1..];
        }

        int value;
        try
        {
            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                value = int.Parse(token.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            else if (token.Length > 1 && token[0] == '0')
            {
                // Octal: convert manually because int.Parse doesn't support base 8.
                value = 0;
                for (var i = 0; i < token.Length; i++)
                {
                    var c = token[i];
                    if (c < '0' || c > '7')
                    {
                        throw new FigletFontFormatException($"Invalid octal digit '{c}' in code tag '{line.Trim()}'.");
                    }
                    value = checked(value * 8 + (c - '0'));
                }
            }
            else
            {
                value = int.Parse(token, NumberStyles.Integer, CultureInfo.InvariantCulture);
            }
        }
        catch (FormatException ex)
        {
            throw new FigletFontFormatException($"Invalid character code in code tag '{line.Trim()}'.", ex);
        }
        catch (OverflowException ex)
        {
            throw new FigletFontFormatException($"Character code in code tag '{line.Trim()}' overflows int.", ex);
        }

        if (negative)
        {
            value = -value;
        }

        if (value == -1)
        {
            throw new FigletFontFormatException("Character code -1 is reserved and not allowed in FIGfonts.");
        }

        // Negative codes (other than -1) are font-internal tags; skip the glyph data that follows.
        return (value, Skip: value < 0);
    }

    // ----- Result -------------------------------------------------------------------------

    /// <summary>The complete parser output: header, resolved layout, and the glyph dictionary.</summary>
    internal sealed record FontData(
        FigletFontHeader Header,
        FigletLayoutInfo Layout,
        IReadOnlyDictionary<int, FigletGlyph> Glyphs);
}
