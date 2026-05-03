using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the internal FIGfont (.flf) parser. Verifies header parsing, comment handling,
/// FIGcharacter data, hardblank/endmark handling, and code-tag parsing.
/// </summary>
public class FigletFontParserTests
{
    // ----- Helpers ---------------------------------------------------------------------

    private static string Flf(string header, int comments, params string[] glyphs)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(header);
        for (var i = 0; i < comments; i++)
        {
            sb.AppendLine($"comment {i}");
        }
        foreach (var g in glyphs)
        {
            sb.Append(g);
            if (!g.EndsWith("\n")) sb.AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>
    /// Returns a one-row-per-line glyph rendered as height N rows of `body` followed by an endmark.
    /// The last row gets a double endmark.
    /// </summary>
    private static string Glyph(int height, char endmark, params string[] rows)
    {
        if (rows.Length != height)
        {
            throw new System.ArgumentException($"Expected {height} rows, got {rows.Length}.");
        }
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < height; i++)
        {
            sb.Append(rows[i]);
            sb.Append(endmark);
            if (i == height - 1) sb.Append(endmark);
            sb.Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>
    /// Builds a minimal valid FIGfont with the 102 required FIGcharacters all set to a single-row
    /// hash-pattern glyph. Used as the base for tests that exercise specific behaviors.
    /// </summary>
    private static string MinimalFont(string headerExtras = "")
    {
        // 95 ASCII printable + 7 German = 102 required glyphs.
        var sb = new System.Text.StringBuilder();
        sb.Append("flf2a$ 1 1 4 0 0 ").AppendLine(headerExtras.Trim());
        // Each glyph: one row "##" then double endmark on last row.
        for (var i = 0; i < 102; i++)
        {
            sb.AppendLine("##@@");
        }
        return sb.ToString();
    }

    // ----- Header parsing --------------------------------------------------------------

    [Fact]
    public void Parse_RejectsMissingMagic()
    {
        var ex = Assert.Throws<FigletFontFormatException>(() => FigletFont.Parse("nope$ 1 1 4 0 0\n##@@\n"));
        Assert.Contains("flf2a", ex.Message);
    }

    [Fact]
    public void Parse_RejectsTooFewHeaderFields()
    {
        var ex = Assert.Throws<FigletFontFormatException>(() => FigletFont.Parse("flf2a$ 1 1 4\n##@@\n"));
        Assert.Contains("at least 5", ex.Message);
    }

    [Fact]
    public void Parse_RejectsZeroHeight()
    {
        var ex = Assert.Throws<FigletFontFormatException>(() => FigletFont.Parse("flf2a$ 0 1 4 0 0\n##@@\n"));
        Assert.Contains("height", ex.Message);
    }

    [Fact]
    public void Parse_RejectsBaselineGreaterThanHeight()
    {
        var ex = Assert.Throws<FigletFontFormatException>(() => FigletFont.Parse("flf2a$ 3 4 4 0 0\n##@@\n"));
        Assert.Contains("baseline", ex.Message);
    }

    [Fact]
    public void Parse_AcceptsAllOptionalHeaderFields()
    {
        var content = MinimalFont(headerExtras: "0 24463 102");
        var font = FigletFont.Parse(content);
        Assert.Equal(1, font.Height);
        Assert.Equal(1, font.Baseline);
        Assert.Equal('$', font.Hardblank);
    }

    [Fact]
    public void Parse_UsesHardblankFromHeader()
    {
        var content = MinimalFont();
        var font = FigletFont.Parse(content);
        Assert.Equal('$', font.Hardblank);
    }

    [Fact]
    public void Parse_NonStandardHardblank_IsHonored()
    {
        var content = "flf2a! 1 1 4 0 0\n" + string.Join("\n", System.Linq.Enumerable.Repeat("##@@", 102)) + "\n";
        var font = FigletFont.Parse(content);
        Assert.Equal('!', font.Hardblank);
    }

    // ----- Comment block ---------------------------------------------------------------

    [Fact]
    public void Parse_SkipsCommentLines()
    {
        var content = "flf2a$ 1 1 4 0 3\nfont author\nlicense\nmore comments\n"
            + string.Join("\n", System.Linq.Enumerable.Repeat("##@@", 102)) + "\n";
        var font = FigletFont.Parse(content);
        Assert.True(font.TryGetGlyph(' ', out _));
    }

    [Fact]
    public void Parse_FailsWhenCommentBlockTruncated()
    {
        var content = "flf2a$ 1 1 4 0 5\ncomment1\ncomment2\n";
        Assert.Throws<FigletFontFormatException>(() => FigletFont.Parse(content));
    }

    // ----- Endmarks --------------------------------------------------------------------

    [Fact]
    public void Parse_StripsTrailingEndmarks()
    {
        var content = "flf2a$ 1 1 4 0 0\n" + string.Join("\n", System.Linq.Enumerable.Repeat("##@@", 102)) + "\n";
        var font = FigletFont.Parse(content);
        Assert.True(font.TryGetGlyph('!', out var glyph));
        Assert.Equal("##", glyph.GetRow(0));
    }

    [Fact]
    public void Parse_AllowsDifferentEndmarkPerRow()
    {
        // Height 2; first row uses '@', second uses '#' (which becomes endmark).
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("flf2a$ 2 1 4 0 0");
        for (var i = 0; i < 102; i++)
        {
            sb.AppendLine("ab@");
            sb.AppendLine("cd##");
        }
        var font = FigletFont.Parse(sb.ToString());
        Assert.True(font.TryGetGlyph('!', out var glyph));
        Assert.Equal(2, glyph.Height);
        Assert.Equal("ab", glyph.GetRow(0));
        Assert.Equal("cd", glyph.GetRow(1));
    }

    // ----- Required glyphs -------------------------------------------------------------

    [Fact]
    public void Parse_FailsWhenRequiredGlyphsMissing()
    {
        // Only 50 glyphs.
        var content = "flf2a$ 1 1 4 0 0\n" + string.Join("\n", System.Linq.Enumerable.Repeat("##@@", 50)) + "\n";
        Assert.Throws<FigletFontFormatException>(() => FigletFont.Parse(content));
    }

    [Fact]
    public void Parse_StoresGermanBlockAtCorrectCodePoints()
    {
        var content = MinimalFont();
        var font = FigletFont.Parse(content);
        Assert.True(font.TryGetGlyph(196, out _)); // Ä
        Assert.True(font.TryGetGlyph(214, out _)); // Ö
        Assert.True(font.TryGetGlyph(220, out _)); // Ü
        Assert.True(font.TryGetGlyph(228, out _)); // ä
        Assert.True(font.TryGetGlyph(246, out _)); // ö
        Assert.True(font.TryGetGlyph(252, out _)); // ü
        Assert.True(font.TryGetGlyph(223, out _)); // ß
    }

    // ----- Code tags --------------------------------------------------------------------

    [Fact]
    public void Parse_AcceptsDecimalCodeTag()
    {
        var content = MinimalFont() + "256  EXTRA\n@@\n";
        // Wait — the glyph data row needs the right shape. Header height=1 so just one row.
        var fixed_ = MinimalFont() + "256  EXTRA\nXX@@\n";
        var font = FigletFont.Parse(fixed_);
        Assert.True(font.TryGetGlyph(256, out var g));
        Assert.Equal("XX", g.GetRow(0));
        _ = content;
    }

    [Fact]
    public void Parse_AcceptsHexCodeTag()
    {
        var content = MinimalFont() + "0x100  EXTRA\nYY@@\n";
        var font = FigletFont.Parse(content);
        Assert.True(font.TryGetGlyph(0x100, out var g));
        Assert.Equal("YY", g.GetRow(0));
    }

    [Fact]
    public void Parse_AcceptsOctalCodeTag()
    {
        var content = MinimalFont() + "0400  EXTRA\nZZ@@\n";
        var font = FigletFont.Parse(content);
        Assert.True(font.TryGetGlyph(256, out var g));
        Assert.Equal("ZZ", g.GetRow(0));
    }

    [Fact]
    public void Parse_SkipsNegativeCodeTags()
    {
        var content = MinimalFont() + "-2  TAG\nQQ@@\n";
        var font = FigletFont.Parse(content);
        Assert.False(font.TryGetGlyph(-2, out _));
    }

    [Fact]
    public void Parse_RejectsCodeTagMinusOne()
    {
        var content = MinimalFont() + "-1  forbidden\nXX@@\n";
        Assert.Throws<FigletFontFormatException>(() => FigletFont.Parse(content));
    }

    [Fact]
    public void Parse_HonorsLastDuplicateCodeTag()
    {
        // Spec: "If two or more FIGcharacters have the same character code, the last one wins."
        var content = MinimalFont() + "256\nFIRST@@\n256\nSECOND@@\n";
        var font = FigletFont.Parse(content);
        Assert.True(font.TryGetGlyph(256, out var g));
        Assert.Equal("SECOND", g.GetRow(0));
    }

    // ----- Layout resolution -----------------------------------------------------------

    [Fact]
    public void Parse_OldLayoutMinusOne_FullWidth()
    {
        var content = "flf2a$ 1 1 4 -1 0\n" + string.Join("\n", System.Linq.Enumerable.Repeat("##@@", 102)) + "\n";
        var font = FigletFont.Parse(content);
        Assert.False(font.HorizontalSmushing);
        Assert.False(font.HorizontalFitting);
        Assert.Equal(0, font.HorizontalSmushingRules);
    }

    [Fact]
    public void Parse_OldLayoutZero_Fitting()
    {
        var content = MinimalFont();
        var font = FigletFont.Parse(content);
        Assert.True(font.HorizontalFitting);
        Assert.False(font.HorizontalSmushing);
    }

    [Fact]
    public void Parse_OldLayoutPositive_ControlledSmushing()
    {
        var content = "flf2a$ 1 1 4 15 0\n" + string.Join("\n", System.Linq.Enumerable.Repeat("##@@", 102)) + "\n";
        var font = FigletFont.Parse(content);
        Assert.True(font.HorizontalSmushing);
        Assert.Equal(15, font.HorizontalSmushingRules);
    }

    [Fact]
    public void Parse_FullLayout_OverridesOldLayout()
    {
        // old_layout=0 (fitting), full_layout=24463 = 128|64|... ⇒ smushing
        var content = "flf2a$ 1 1 4 0 0 0 24463\n" + string.Join("\n", System.Linq.Enumerable.Repeat("##@@", 102)) + "\n";
        var font = FigletFont.Parse(content);
        Assert.True(font.HorizontalSmushing);
        Assert.Equal(24463 & 0x3F, font.HorizontalSmushingRules);
    }

    [Fact]
    public void Parse_FullLayout_VerticalFlags()
    {
        // 16384 = vertical smushing default; 256 = vertical rule 1
        var fullLayout = 16384 + 256;
        var content = $"flf2a$ 1 1 4 -1 0 0 {fullLayout}\n" + string.Join("\n", System.Linq.Enumerable.Repeat("##@@", 102)) + "\n";
        var font = FigletFont.Parse(content);
        Assert.True(font.VerticalSmushing);
        Assert.Equal(1, font.VerticalSmushingRules);
    }
}
