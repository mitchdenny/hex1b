using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the internal FIGfont (.flf) parser. Verifies header parsing, comment handling,
/// FIGcharacter data, hardblank/endmark handling, and code-tag parsing.
/// </summary>
[TestClass]
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

    [TestMethod]
    public void Parse_RejectsMissingMagic()
    {
        var ex = Assert.ThrowsExactly<FigletFontFormatException>(() => FigletFont.Parse("nope$ 1 1 4 0 0\n##@@\n"));
        Assert.Contains("flf2a", ex.Message);
    }

    [TestMethod]
    public void Parse_RejectsTooFewHeaderFields()
    {
        var ex = Assert.ThrowsExactly<FigletFontFormatException>(() => FigletFont.Parse("flf2a$ 1 1 4\n##@@\n"));
        Assert.Contains("at least 5", ex.Message);
    }

    [TestMethod]
    public void Parse_RejectsZeroHeight()
    {
        var ex = Assert.ThrowsExactly<FigletFontFormatException>(() => FigletFont.Parse("flf2a$ 0 1 4 0 0\n##@@\n"));
        Assert.Contains("height", ex.Message);
    }

    [TestMethod]
    public void Parse_RejectsBaselineGreaterThanHeight()
    {
        var ex = Assert.ThrowsExactly<FigletFontFormatException>(() => FigletFont.Parse("flf2a$ 3 4 4 0 0\n##@@\n"));
        Assert.Contains("baseline", ex.Message);
    }

    [TestMethod]
    public void Parse_AcceptsAllOptionalHeaderFields()
    {
        var content = MinimalFont(headerExtras: "0 24463 102");
        var font = FigletFont.Parse(content);
        Assert.AreEqual(1, font.Height);
        Assert.AreEqual(1, font.Baseline);
        Assert.AreEqual('$', font.Hardblank);
    }

    [TestMethod]
    public void Parse_UsesHardblankFromHeader()
    {
        var content = MinimalFont();
        var font = FigletFont.Parse(content);
        Assert.AreEqual('$', font.Hardblank);
    }

    [TestMethod]
    public void Parse_NonStandardHardblank_IsHonored()
    {
        var content = "flf2a! 1 1 4 0 0\n" + string.Join("\n", System.Linq.Enumerable.Repeat("##@@", 102)) + "\n";
        var font = FigletFont.Parse(content);
        Assert.AreEqual('!', font.Hardblank);
    }

    // ----- Comment block ---------------------------------------------------------------

    [TestMethod]
    public void Parse_SkipsCommentLines()
    {
        var content = "flf2a$ 1 1 4 0 3\nfont author\nlicense\nmore comments\n"
            + string.Join("\n", System.Linq.Enumerable.Repeat("##@@", 102)) + "\n";
        var font = FigletFont.Parse(content);
        Assert.IsTrue(font.TryGetGlyph(' ', out _));
    }

    [TestMethod]
    public void Parse_FailsWhenCommentBlockTruncated()
    {
        var content = "flf2a$ 1 1 4 0 5\ncomment1\ncomment2\n";
        Assert.ThrowsExactly<FigletFontFormatException>(() => FigletFont.Parse(content));
    }

    // ----- Endmarks --------------------------------------------------------------------

    [TestMethod]
    public void Parse_StripsTrailingEndmarks()
    {
        var content = "flf2a$ 1 1 4 0 0\n" + string.Join("\n", System.Linq.Enumerable.Repeat("##@@", 102)) + "\n";
        var font = FigletFont.Parse(content);
        Assert.IsTrue(font.TryGetGlyph('!', out var glyph));
        Assert.AreEqual("##", glyph.GetRow(0));
    }

    [TestMethod]
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
        Assert.IsTrue(font.TryGetGlyph('!', out var glyph));
        Assert.AreEqual(2, glyph.Height);
        Assert.AreEqual("ab", glyph.GetRow(0));
        Assert.AreEqual("cd", glyph.GetRow(1));
    }

    // ----- Required glyphs -------------------------------------------------------------

    [TestMethod]
    public void Parse_FailsWhenRequiredGlyphsMissing()
    {
        // Only 50 glyphs.
        var content = "flf2a$ 1 1 4 0 0\n" + string.Join("\n", System.Linq.Enumerable.Repeat("##@@", 50)) + "\n";
        Assert.ThrowsExactly<FigletFontFormatException>(() => FigletFont.Parse(content));
    }

    [TestMethod]
    public void Parse_StoresGermanBlockAtCorrectCodePoints()
    {
        var content = MinimalFont();
        var font = FigletFont.Parse(content);
        Assert.IsTrue(font.TryGetGlyph(196, out _)); // Ä
        Assert.IsTrue(font.TryGetGlyph(214, out _)); // Ö
        Assert.IsTrue(font.TryGetGlyph(220, out _)); // Ü
        Assert.IsTrue(font.TryGetGlyph(228, out _)); // ä
        Assert.IsTrue(font.TryGetGlyph(246, out _)); // ö
        Assert.IsTrue(font.TryGetGlyph(252, out _)); // ü
        Assert.IsTrue(font.TryGetGlyph(223, out _)); // ß
    }

    // ----- Code tags --------------------------------------------------------------------

    [TestMethod]
    public void Parse_AcceptsDecimalCodeTag()
    {
        var content = MinimalFont() + "256  EXTRA\n@@\n";
        // Wait — the glyph data row needs the right shape. Header height=1 so just one row.
        var fixed_ = MinimalFont() + "256  EXTRA\nXX@@\n";
        var font = FigletFont.Parse(fixed_);
        Assert.IsTrue(font.TryGetGlyph(256, out var g));
        Assert.AreEqual("XX", g.GetRow(0));
        _ = content;
    }

    [TestMethod]
    public void Parse_AcceptsHexCodeTag()
    {
        var content = MinimalFont() + "0x100  EXTRA\nYY@@\n";
        var font = FigletFont.Parse(content);
        Assert.IsTrue(font.TryGetGlyph(0x100, out var g));
        Assert.AreEqual("YY", g.GetRow(0));
    }

    [TestMethod]
    public void Parse_AcceptsOctalCodeTag()
    {
        var content = MinimalFont() + "0400  EXTRA\nZZ@@\n";
        var font = FigletFont.Parse(content);
        Assert.IsTrue(font.TryGetGlyph(256, out var g));
        Assert.AreEqual("ZZ", g.GetRow(0));
    }

    [TestMethod]
    public void Parse_SkipsNegativeCodeTags()
    {
        var content = MinimalFont() + "-2  TAG\nQQ@@\n";
        var font = FigletFont.Parse(content);
        Assert.IsFalse(font.TryGetGlyph(-2, out _));
    }

    [TestMethod]
    public void Parse_RejectsCodeTagMinusOne()
    {
        var content = MinimalFont() + "-1  forbidden\nXX@@\n";
        Assert.ThrowsExactly<FigletFontFormatException>(() => FigletFont.Parse(content));
    }

    [TestMethod]
    public void Parse_HonorsLastDuplicateCodeTag()
    {
        // Spec: "If two or more FIGcharacters have the same character code, the last one wins."
        var content = MinimalFont() + "256\nFIRST@@\n256\nSECOND@@\n";
        var font = FigletFont.Parse(content);
        Assert.IsTrue(font.TryGetGlyph(256, out var g));
        Assert.AreEqual("SECOND", g.GetRow(0));
    }

    // ----- Layout resolution -----------------------------------------------------------

    [TestMethod]
    public void Parse_OldLayoutMinusOne_FullWidth()
    {
        var content = "flf2a$ 1 1 4 -1 0\n" + string.Join("\n", System.Linq.Enumerable.Repeat("##@@", 102)) + "\n";
        var font = FigletFont.Parse(content);
        Assert.IsFalse(font.HorizontalSmushing);
        Assert.IsFalse(font.HorizontalFitting);
        Assert.AreEqual(0, font.HorizontalSmushingRules);
    }

    [TestMethod]
    public void Parse_OldLayoutZero_Fitting()
    {
        var content = MinimalFont();
        var font = FigletFont.Parse(content);
        Assert.IsTrue(font.HorizontalFitting);
        Assert.IsFalse(font.HorizontalSmushing);
    }

    [TestMethod]
    public void Parse_OldLayoutPositive_ControlledSmushing()
    {
        var content = "flf2a$ 1 1 4 15 0\n" + string.Join("\n", System.Linq.Enumerable.Repeat("##@@", 102)) + "\n";
        var font = FigletFont.Parse(content);
        Assert.IsTrue(font.HorizontalSmushing);
        Assert.AreEqual(15, font.HorizontalSmushingRules);
    }

    [TestMethod]
    public void Parse_FullLayout_OverridesOldLayout()
    {
        // old_layout=0 (fitting), full_layout=24463 = 128|64|... ⇒ smushing
        var content = "flf2a$ 1 1 4 0 0 0 24463\n" + string.Join("\n", System.Linq.Enumerable.Repeat("##@@", 102)) + "\n";
        var font = FigletFont.Parse(content);
        Assert.IsTrue(font.HorizontalSmushing);
        Assert.AreEqual(24463 & 0x3F, font.HorizontalSmushingRules);
    }

    [TestMethod]
    public void Parse_FullLayout_VerticalFlags()
    {
        // 16384 = vertical smushing default; 256 = vertical rule 1
        var fullLayout = 16384 + 256;
        var content = $"flf2a$ 1 1 4 -1 0 0 {fullLayout}\n" + string.Join("\n", System.Linq.Enumerable.Repeat("##@@", 102)) + "\n";
        var font = FigletFont.Parse(content);
        Assert.IsTrue(font.VerticalSmushing);
        Assert.AreEqual(1, font.VerticalSmushingRules);
    }
}
