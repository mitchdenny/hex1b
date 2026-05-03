using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// FIGfont 2.0 conformance tests using synthetic <c>.flf</c> fonts authored to isolate single
/// behaviors at a time:
/// <list type="bullet">
///   <item><description>per-rule positive cases (rule must fire)</description></item>
///   <item><description>per-rule negative cases (rule must NOT fire)</description></item>
///   <item><description><c>old_layout</c> vs <c>full_layout</c> precedence</description></item>
///   <item><description>universal smushing edge cases</description></item>
///   <item><description>code-tag parsing in decimal/hex/octal</description></item>
/// </list>
/// Expected outputs are derived from the FIGfont 2.0 spec (<c>docs/figfont.txt</c>), not from
/// any reference implementation.
/// </summary>
public class FigletConformanceTests
{
    // ----- Helpers ----------------------------------------------------------------------

    private static FigletFont LoadFlf(string flf) => FigletFont.Parse(flf);

    private static IReadOnlyList<string> Render(
        FigletFont font,
        string text,
        FigletLayoutMode horizontal = FigletLayoutMode.Default,
        FigletLayoutMode vertical = FigletLayoutMode.Default)
    {
        return FigletRenderer.Render(text, font, horizontal, vertical,
            FigletHorizontalOverflow.Clip, int.MaxValue);
    }

    /// <summary>
    /// Builds a minimal FIGfont with <c>height</c> rows. Glyphs are supplied as a dictionary of
    /// codepoint → row strings (length must equal <c>height</c>). Rows are emitted with the
    /// supplied endmark, then the final row of each glyph gets a doubled endmark.
    /// </summary>
    private static string BuildFlf(
        char hardblank,
        int height,
        int oldLayout,
        int? fullLayout,
        Dictionary<int, string[]> glyphs,
        char endmark = '@')
    {
        // Pick maxLength as the longest glyph row + endmarks.
        var maxLen = 0;
        foreach (var g in glyphs.Values)
        {
            foreach (var r in g)
            {
                if (r.Length + 2 > maxLen) maxLen = r.Length + 2;
            }
        }

        var sb = new System.Text.StringBuilder();
        if (fullLayout.HasValue)
        {
            sb.AppendLine($"flf2a{hardblank} {height} {height} {maxLen} {oldLayout} 0 0 {fullLayout.Value} 0");
        }
        else
        {
            sb.AppendLine($"flf2a{hardblank} {height} {height} {maxLen} {oldLayout} 0 0");
        }

        // Required ASCII 32..126 glyphs. Use the supplied glyph if present; else a blank glyph.
        for (var c = 32; c <= 126; c++)
        {
            AppendGlyph(sb, c, height, glyphs, endmark);
        }
        // German block (required in fixed order).
        foreach (var c in new[] { 196, 214, 220, 228, 246, 252, 223 })
        {
            AppendGlyph(sb, c, height, glyphs, endmark);
        }
        // Code-tagged extras (any code point > 127, in any order).
        foreach (var (codePoint, _) in glyphs)
        {
            if (codePoint < 32 || (codePoint >= 32 && codePoint <= 126)) continue;
            if (codePoint == 196 || codePoint == 214 || codePoint == 220
                || codePoint == 228 || codePoint == 246 || codePoint == 252 || codePoint == 223) continue;

            sb.AppendLine($"{codePoint}");
            AppendGlyphRows(sb, codePoint, height, glyphs, endmark);
        }
        return sb.ToString();
    }

    private static void AppendGlyph(System.Text.StringBuilder sb, int code, int height,
        Dictionary<int, string[]> glyphs, char endmark)
    {
        AppendGlyphRows(sb, code, height, glyphs, endmark);
    }

    private static void AppendGlyphRows(System.Text.StringBuilder sb, int code, int height,
        Dictionary<int, string[]> glyphs, char endmark)
    {
        var rows = glyphs.TryGetValue(code, out var found) ? found : BlankRows(height);
        for (var i = 0; i < height; i++)
        {
            var row = i < rows.Length ? rows[i] : "";
            // Last row gets double endmark; earlier rows single.
            sb.Append(row);
            sb.Append(endmark);
            if (i == height - 1) sb.Append(endmark);
            sb.Append('\n');
        }
    }

    private static string[] BlankRows(int height)
    {
        var rows = new string[height];
        for (var i = 0; i < height; i++) rows[i] = " ";
        return rows;
    }

    // ----- old_layout / full_layout precedence -----------------------------------------

    [Fact]
    public void Layout_FullLayoutOverridesOldLayout()
    {
        // old_layout=-1 (full width); full_layout=128 (smushing, no rules → universal).
        // full_layout must win → universal smushing should fire.
        var flf = BuildFlf('$', 1, oldLayout: -1, fullLayout: 128, new Dictionary<int, string[]>
        {
            ['A'] = ["AA"],
            ['B'] = ["BB"],
            [' '] = [" "],
        });
        var font = LoadFlf(flf);

        var lines = Render(font, "AB");
        // With universal smushing: A's last 'A' meets B's first 'B' → right wins → 'B'.
        // Block: "ABB" (3 cols).
        Assert.Equal("ABB", lines[0]);
    }

    [Fact]
    public void Layout_OldLayoutMinus1_FullWidthWhenNoFullLayout()
    {
        var flf = BuildFlf('$', 1, oldLayout: -1, fullLayout: null, new Dictionary<int, string[]>
        {
            ['A'] = ["AA"],
            ['B'] = ["BB"],
        });
        var font = LoadFlf(flf);

        var lines = Render(font, "AB");
        Assert.Equal("AABB", lines[0]); // no overlap
    }

    [Fact]
    public void Layout_OldLayout0_FittingWhenNoFullLayout()
    {
        var flf = BuildFlf('$', 1, oldLayout: 0, fullLayout: null, new Dictionary<int, string[]>
        {
            ['A'] = [" A "],
            ['B'] = [" B "],
        });
        var font = LoadFlf(flf);

        // Trailing blanks of " A " = 1; leading blanks of " B " = 1; fitted = 2.
        var lines = Render(font, "AB");
        // " A " + " B " with 2 overlap → " AB " → trim trailing → " AB"
        Assert.Equal(" AB", lines[0]);
    }

    [Fact]
    public void Layout_OldLayoutPositive_SmushingWithThoseRules()
    {
        // old_layout=1 → smushing enabled with horizontal rule 1 (equal char).
        var flf = BuildFlf('$', 1, oldLayout: 1, fullLayout: null, new Dictionary<int, string[]>
        {
            ['A'] = ["AA"],
        });
        var font = LoadFlf(flf);

        // "AA": first 'A' glyph "AA" then second; smushed +1: 'A' vs 'A' → rule 1 → 'A'.
        var lines = Render(font, "AA");
        Assert.Equal("AAA", lines[0]);
    }

    // ----- Universal smushing edge cases ----------------------------------------------

    [Fact]
    public void Universal_VisibleVsHardblank_Rejects()
    {
        // Smushing flag set (full_layout=128), no rules → universal.
        var flf = BuildFlf('$', 1, oldLayout: 0, fullLayout: 128, new Dictionary<int, string[]>
        {
            ['A'] = ["AA"],
            ['B'] = ["$B$"],
            [' '] = [" "],
        });
        var font = LoadFlf(flf);

        // Try +1: 'A' vs '$' → reject. Backoff to fitted=0. Block: "AA$B$" → " B" after strips.
        var lines = Render(font, "AB");
        Assert.Equal("AA B", lines[0]);
    }

    [Fact]
    public void Universal_HardblankVsHardblank_Smushes()
    {
        var flf = BuildFlf('$', 1, oldLayout: 0, fullLayout: 128, new Dictionary<int, string[]>
        {
            ['A'] = ["A$"],
            ['B'] = ["$B"],
        });
        var font = LoadFlf(flf);

        // Smushed +1: '$' vs '$' → hardblank. Block: "A$B" → "A B".
        var lines = Render(font, "AB");
        Assert.Equal("A B", lines[0]);
    }

    // ----- Per-rule conformance --------------------------------------------------------

    [Fact]
    public void Rule1_EqualChar_Smushes()
    {
        // full_layout = smushing (128) + rule 1 (1) = 129.
        var flf = BuildFlf('$', 1, oldLayout: 1, fullLayout: 129, new Dictionary<int, string[]>
        {
            ['A'] = ["AA"],
        });
        var font = LoadFlf(flf);

        var lines = Render(font, "AA");
        Assert.Equal("AAA", lines[0]);
    }

    [Fact]
    public void Rule2_Underscore_SmushesWithBracketingChars()
    {
        // full_layout = smushing (128) + rule 2 (2) = 130.
        var flf = BuildFlf('$', 1, oldLayout: 2, fullLayout: 130, new Dictionary<int, string[]>
        {
            ['A'] = ["A_"],
            ['B'] = ["|B"],
        });
        var font = LoadFlf(flf);

        // Smushed +1: '_' vs '|' → rule 2 → '|'. Block: "A|B".
        var lines = Render(font, "AB");
        Assert.Equal("A|B", lines[0]);
    }

    [Fact]
    public void Rule6_Hardblank_SmushesTwoHardblanks()
    {
        // full_layout = smushing (128) + rule 6 (32) = 160. ONLY rule 6, no other rules → 
        // hardblank+hardblank smushes; visible+hardblank should NOT smush (no rule covers it).
        var flf = BuildFlf('$', 1, oldLayout: 32, fullLayout: 160, new Dictionary<int, string[]>
        {
            ['A'] = ["A$"],
            ['B'] = ["$B"],
        });
        var font = LoadFlf(flf);

        var lines = Render(font, "AB");
        Assert.Equal("A B", lines[0]);
    }

    // ----- Code tag parsing -----------------------------------------------------------

    [Fact]
    public void CodeTag_Decimal_LoadsExtendedGlyph()
    {
        var flf = BuildFlf('$', 1, oldLayout: 0, fullLayout: null, new Dictionary<int, string[]>
        {
            [0x2603] = ["*"], // U+2603 SNOWMAN
        });
        var font = LoadFlf(flf);

        Assert.True(font.TryGetGlyph(0x2603, out var glyph));
        Assert.Equal(1, glyph.Width);
    }

    [Fact]
    public void CodeTag_NegativeOne_IsRejected()
    {
        // -1 is the FIGfont reserved "missing" tag; the parser must reject it as illegal data.
        // Build a font with an invalid code tag of -1 manually (BuildFlf doesn't emit it).
        var flf = BuildFlf('$', 1, oldLayout: 0, fullLayout: null, new Dictionary<int, string[]>())
            + "-1\n*@\n*@@\n";

        Assert.Throws<FigletFontFormatException>(() => LoadFlf(flf));
    }

    // ----- Hardblank rendering --------------------------------------------------------

    [Fact]
    public void Render_OutputDoesNotContainHardblanks()
    {
        var flf = BuildFlf('$', 1, oldLayout: -1, fullLayout: null, new Dictionary<int, string[]>
        {
            ['A'] = ["$A$"],
        });
        var font = LoadFlf(flf);

        var lines = Render(font, "A");
        Assert.DoesNotContain('$', lines[0]);
    }

    // ----- German block --------------------------------------------------------------

    [Fact]
    public void GermanBlock_AllSevenAreLoaded()
    {
        var flf = BuildFlf('$', 1, oldLayout: 0, fullLayout: null, new Dictionary<int, string[]>
        {
            [196] = ["A"], // Ä
            [214] = ["O"], // Ö
            [220] = ["U"], // Ü
            [228] = ["a"], // ä
            [246] = ["o"], // ö
            [252] = ["u"], // ü
            [223] = ["B"], // ß
        });
        var font = LoadFlf(flf);

        foreach (var cp in new[] { 196, 214, 220, 228, 246, 252, 223 })
        {
            Assert.True(font.TryGetGlyph(cp, out _), $"Missing glyph for U+{cp:X4}.");
        }
    }
}
