using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for <see cref="FigletRenderer"/> that exercise layout and overlap behavior using
/// hand-crafted single-row synthetic fonts. By keeping the fonts tiny we can read the expected
/// outputs straight off the page and assert byte-for-byte.
/// </summary>
public class FigletRendererTests
{
    private const char Hb = '$';

    /// <summary>
    /// Tiny single-row test font where each ASCII letter is a 3-cell "X.X" stamp (X being the
    /// letter, . a space). Hardblank is '$'. Layout flags vary by subclass.
    /// </summary>
    private sealed class StampFont : FigletFont
    {
        private readonly Dictionary<int, FigletGlyph> _glyphs;

        public StampFont(
            int hRules,
            bool hSmush,
            bool hFit,
            int vRules = 0,
            bool vSmush = false,
            bool vFit = false,
            string letterPattern = "X.X")
            : base(
                height: 1,
                baseline: 1,
                hardblank: Hb,
                horizontalSmushingRules: hRules,
                horizontalSmushing: hSmush,
                horizontalFitting: hFit,
                verticalSmushingRules: vRules,
                verticalSmushing: vSmush,
                verticalFitting: vFit)
        {
            _glyphs = new Dictionary<int, FigletGlyph>
            {
                [' '] = new FigletGlyph([" "]),
            };
            for (var c = 'A'; c <= 'Z'; c++)
            {
                _glyphs[c] = new FigletGlyph([letterPattern.Replace('X', c).Replace('.', ' ')]);
            }
        }

        public override bool TryGetGlyph(int codePoint, out FigletGlyph glyph)
        {
            if (_glyphs.TryGetValue(codePoint, out var found))
            {
                glyph = found;
                return true;
            }
            glyph = null!;
            return false;
        }
    }

    /// <summary>Multi-row font that paints each letter in a vertical stripe pattern.</summary>
    private sealed class StripeFont : FigletFont
    {
        private readonly Dictionary<int, FigletGlyph> _glyphs;

        public StripeFont(int height = 3, int vRules = 0, bool vSmush = false, bool vFit = false)
            : base(
                height: height,
                baseline: height,
                hardblank: Hb,
                horizontalSmushingRules: 0,
                horizontalSmushing: false,
                horizontalFitting: false,
                verticalSmushingRules: vRules,
                verticalSmushing: vSmush,
                verticalFitting: vFit)
        {
            _glyphs = new Dictionary<int, FigletGlyph>();
            var spaceRows = new string[height];
            for (var i = 0; i < height; i++) spaceRows[i] = " ";
            _glyphs[' '] = new FigletGlyph(spaceRows);
            for (var c = 'A'; c <= 'Z'; c++)
            {
                var rows = new string[height];
                for (var r = 0; r < height; r++)
                {
                    rows[r] = c.ToString();
                }
                _glyphs[c] = new FigletGlyph(rows);
            }
        }

        public override bool TryGetGlyph(int codePoint, out FigletGlyph glyph)
        {
            if (_glyphs.TryGetValue(codePoint, out var found))
            {
                glyph = found;
                return true;
            }
            glyph = null!;
            return false;
        }
    }

    // ----- Empty / single-glyph behavior ------------------------------------------------

    [Fact]
    public void Render_EmptyText_ProducesEmptyRowsAtFontHeight()
    {
        var font = new StampFont(0, false, false);
        var lines = FigletRenderer.Render("", font, FigletLayoutMode.Default, FigletLayoutMode.Default,
            FigletHorizontalOverflow.Clip, int.MaxValue);

        Assert.Equal(font.Height, lines.Count);
        foreach (var l in lines) Assert.Equal(string.Empty, l);
    }

    [Fact]
    public void Render_SingleCharacter_ReturnsThatGlyph()
    {
        var font = new StampFont(0, false, false);
        var lines = FigletRenderer.Render("A", font, FigletLayoutMode.Default, FigletLayoutMode.Default,
            FigletHorizontalOverflow.Clip, int.MaxValue);

        Assert.Single(lines);
        Assert.Equal("A A", lines[0]);
    }

    // ----- Horizontal layout modes ------------------------------------------------------

    [Fact]
    public void Render_FullWidth_NoOverlap()
    {
        var font = new StampFont(0, false, false);
        var lines = FigletRenderer.Render("AB", font, FigletLayoutMode.FullWidth, FigletLayoutMode.Default,
            FigletHorizontalOverflow.Clip, int.MaxValue);

        Assert.Equal("A AB B", lines[0]);
    }

    [Fact]
    public void Render_Fitted_OverlapAtBoundaryBlanks()
    {
        // Each glyph is "X X" (3 wide, trailing & leading 0 visible chars but middle blank).
        // Trailing blanks of A = 0, leading blanks of B = 0 → fitted overlap = 0.
        var font = new StampFont(0, false, true);
        var lines = FigletRenderer.Render("AB", font, FigletLayoutMode.Fitted, FigletLayoutMode.Default,
            FigletHorizontalOverflow.Clip, int.MaxValue);

        Assert.Equal("A AB B", lines[0]);
    }

    [Fact]
    public void Render_Fitted_WithLeadingBlankInGlyph_OverlapsByOne()
    {
        // Letters use ".X." pattern (space-letter-space). Trailing blanks of " A " = 1; leading
        // blanks of " B " = 1; fitted overlap = 2. Result: " A " + " B " with 2 overlap → " AB ".
        var font = new StampFont(0, false, true, letterPattern: ".X.");
        var lines = FigletRenderer.Render("AB", font, FigletLayoutMode.Fitted, FigletLayoutMode.Default,
            FigletHorizontalOverflow.Clip, int.MaxValue);

        Assert.Equal(" AB", lines[0]);
    }

    [Fact]
    public void Render_Smushed_UniversalRightWins()
    {
        // No leading/trailing blanks ("XYX") so fitted = 0. Smushed = +1: A's 'A' vs B's 'B' →
        // universal smushing → right wins → 'B'. Result: "ABA" + "YBY"? Let me think...
        // Glyph A = "AAA", B = "BBB". block after A = "AAA", overlap 1 means last col of A ('A')
        // smushes with first col of B ('B') → 'B'. Then append remaining "BB". Result: "AABBB".
        var font = new StampFont(0, true, false, letterPattern: "XXX");
        var lines = FigletRenderer.Render("AB", font, FigletLayoutMode.Smushed, FigletLayoutMode.Default,
            FigletHorizontalOverflow.Clip, int.MaxValue);

        Assert.Equal("AABBB", lines[0]);
    }

    [Fact]
    public void Render_Smushed_HardblankProtectsColumn()
    {
        // Glyph A = "AA", glyph B = "$B$". After A: block = "AA". With B:
        //   Trailing blanks of "AA" = 0; leading blanks of "$B$" = 0 (hardblank not blank).
        //   fitted = 0. Try +1: 'A' vs '$' → universal "visible+hardblank" → REJECT.
        //   Backoff to 0. Block: "AA" + "$B$" = "AA$B$".
        // Display: hardblanks → spaces → "AA B " → trim → "AA B".
        var font = new SyntheticFont(
            hRules: 0,
            hSmush: true,
            hFit: false,
            glyphs: new Dictionary<int, FigletGlyph>
            {
                ['A'] = new FigletGlyph(["AA"]),
                ['B'] = new FigletGlyph(["$B$"]),
                [' '] = new FigletGlyph([" "]),
            });
        var lines = FigletRenderer.Render("AB", font, FigletLayoutMode.Smushed, FigletLayoutMode.Default,
            FigletHorizontalOverflow.Clip, int.MaxValue);

        Assert.Equal("AA B", lines[0]);
    }

    [Fact]
    public void Render_Smushed_HardblankBoth_KeepsHardblank()
    {
        // Glyph A = "A$", glyph B = "$B". Smushed +1: '$' vs '$' → both hardblanks (universal) →
        // hardblank. Block: "A$B" → "A B".
        var font = new SyntheticFont(
            hRules: 0,
            hSmush: true,
            hFit: false,
            glyphs: new Dictionary<int, FigletGlyph>
            {
                ['A'] = new FigletGlyph(["A$"]),
                ['B'] = new FigletGlyph(["$B"]),
                [' '] = new FigletGlyph([" "]),
            });
        var lines = FigletRenderer.Render("AB", font, FigletLayoutMode.Smushed, FigletLayoutMode.Default,
            FigletHorizontalOverflow.Clip, int.MaxValue);

        Assert.Equal("A B", lines[0]);
    }

    [Fact]
    public void Render_Smushed_ControlledRule1_EqualCharSmushes()
    {
        // Glyph A = "AA" (no spaces), so for "AA" input: trailing blanks = 0, leading blanks = 0
        // fitted = 0; smushed +1: 'A' vs 'A' → controlled rule 1 → 'A'. Result "AAA".
        var font = new SyntheticFont(
            hRules: FigletSmushingRules.HorizontalRuleEqual,
            hSmush: true,
            hFit: false,
            glyphs: new Dictionary<int, FigletGlyph>
            {
                ['A'] = new FigletGlyph(["AA"]),
                [' '] = new FigletGlyph([" "]),
            });
        var lines = FigletRenderer.Render("AA", font, FigletLayoutMode.Smushed, FigletLayoutMode.Default,
            FigletHorizontalOverflow.Clip, int.MaxValue);

        Assert.Equal("AAA", lines[0]);
    }

    [Fact]
    public void Render_Default_PrefersFontFlags()
    {
        // Font opts in to fitting only; Default should resolve to Fitted.
        var font = new StampFont(0, false, true, letterPattern: ".X.");
        var lines = FigletRenderer.Render("AB", font, FigletLayoutMode.Default, FigletLayoutMode.Default,
            FigletHorizontalOverflow.Clip, int.MaxValue);

        // .A. .B. fitted by 2 → " AB"
        Assert.Equal(" AB", lines[0]);
    }

    [Fact]
    public void Render_Default_NoFlags_FallsBackToFullWidth()
    {
        // Font has neither smushing nor fitting → full width.
        var font = new StampFont(0, false, false, letterPattern: ".X.");
        var lines = FigletRenderer.Render("AB", font, FigletLayoutMode.Default, FigletLayoutMode.Default,
            FigletHorizontalOverflow.Clip, int.MaxValue);

        // " A " + " B " concatenated, trailing trimmed → " A  B"
        Assert.Equal(" A  B", lines[0]);
    }

    // ----- Hard line breaks (\n) --------------------------------------------------------

    [Fact]
    public void Render_HardNewline_StacksBlocksVertically()
    {
        var font = new StripeFont(height: 2);
        var lines = FigletRenderer.Render("A\nB", font, FigletLayoutMode.Default, FigletLayoutMode.FullWidth,
            FigletHorizontalOverflow.Clip, int.MaxValue);

        // Two paragraphs of height 2, full vertical width, no overlap → 4 rows.
        Assert.Equal(4, lines.Count);
        Assert.Equal("A", lines[0]);
        Assert.Equal("A", lines[1]);
        Assert.Equal("B", lines[2]);
        Assert.Equal("B", lines[3]);
    }

    // ----- Word wrap --------------------------------------------------------------------

    [Fact]
    public void Render_Wrap_BreaksOnSpacesWhenWidthExceeded()
    {
        // Each letter renders to "X" (1 wide). Each rendered word is letterCount columns; no
        // smushing. "AB CD" = 5 chars rendered. wrapWidth=3 forces second word to next row.
        var font = new StampFont(0, false, false, letterPattern: "X");
        var lines = FigletRenderer.Render("AB CD", font, FigletLayoutMode.FullWidth, FigletLayoutMode.FullWidth,
            FigletHorizontalOverflow.Wrap, wrapWidth: 3);

        // Height=1 per paragraph, two paragraphs → 2 lines.
        Assert.Equal(2, lines.Count);
        Assert.Equal("AB", lines[0]);
        Assert.Equal("CD", lines[1]);
    }

    [Fact]
    public void Render_Wrap_LongWord_BreaksAtCharacters()
    {
        var font = new StampFont(0, false, false, letterPattern: "X");
        var lines = FigletRenderer.Render("ABCDE", font, FigletLayoutMode.FullWidth, FigletLayoutMode.FullWidth,
            FigletHorizontalOverflow.Wrap, wrapWidth: 3);

        // Single word longer than wrapWidth → broken at char boundaries: "ABC" (3 wide) + "DE" (2 wide).
        Assert.Equal(2, lines.Count);
        Assert.Equal("ABC", lines[0]);
        Assert.Equal("DE", lines[1]);
    }

    [Fact]
    public void Render_Wrap_InfiniteWidth_NoOpsToClipBehavior()
    {
        var font = new StampFont(0, false, false, letterPattern: "X");
        var lines = FigletRenderer.Render("AB CD", font, FigletLayoutMode.FullWidth, FigletLayoutMode.FullWidth,
            FigletHorizontalOverflow.Wrap, wrapWidth: int.MaxValue);

        Assert.Single(lines);
        Assert.Equal("AB CD", lines[0]);
    }

    [Fact]
    public void Render_Wrap_StacksParagraphsWithoutVerticalSmushing()
    {
        // Stripe font: two-row glyph "X"/"X" with smushable rows. If wrap-induced paragraph
        // breaks were vertically smushed, we'd see fewer rows than (paragraphs * fontHeight).
        // The renderer must always FullWidth-stack wrap-produced paragraphs even when the font
        // (or the caller) prefers vertical Smushed/Fitted, so wrapped output remains visually
        // distinct.
        var font = new StripeFont(height: 2, vRules: 0, vSmush: true);
        var lines = FigletRenderer.Render("AB CD", font, FigletLayoutMode.FullWidth, FigletLayoutMode.Smushed,
            FigletHorizontalOverflow.Wrap, wrapWidth: 3);

        // 2 paragraphs × 2 rows each = 4 rows, no smushing collapse.
        Assert.Equal(4, lines.Count);
        Assert.Equal("AB", lines[0]);
        Assert.Equal("AB", lines[1]);
        Assert.Equal("CD", lines[2]);
        Assert.Equal("CD", lines[3]);
    }

    // ----- Vertical layout --------------------------------------------------------------

    [Fact]
    public void Render_VerticalFullWidth_StacksWithoutOverlap()
    {
        var font = new StripeFont(height: 2, vRules: 0, vSmush: false, vFit: false);
        var lines = FigletRenderer.Render("A\nB", font, FigletLayoutMode.Default,
            FigletLayoutMode.FullWidth, FigletHorizontalOverflow.Clip, int.MaxValue);

        Assert.Equal(4, lines.Count);
    }

    // ----- Hardblanks disappear from output ---------------------------------------------

    [Fact]
    public void Render_OutputContainsNoHardblanks()
    {
        var font = new SyntheticFont(
            hRules: 0,
            hSmush: false,
            hFit: false,
            glyphs: new Dictionary<int, FigletGlyph>
            {
                ['A'] = new FigletGlyph(["$A$"]),
                [' '] = new FigletGlyph([" "]),
            });
        var lines = FigletRenderer.Render("A", font, FigletLayoutMode.FullWidth, FigletLayoutMode.Default,
            FigletHorizontalOverflow.Clip, int.MaxValue);

        Assert.DoesNotContain('$', lines[0]);
        Assert.Equal(" A", lines[0]); // " A$" trimmed of trailing blanks (incl. the hardblank-as-space)
    }

    // ----- Helper: synthetic font with explicit glyph table -----------------------------

    private sealed class SyntheticFont : FigletFont
    {
        private readonly Dictionary<int, FigletGlyph> _glyphs;

        public SyntheticFont(
            int hRules,
            bool hSmush,
            bool hFit,
            Dictionary<int, FigletGlyph> glyphs)
            : base(
                height: glyphs.First().Value.Height,
                baseline: glyphs.First().Value.Height,
                hardblank: Hb,
                horizontalSmushingRules: hRules,
                horizontalSmushing: hSmush,
                horizontalFitting: hFit,
                verticalSmushingRules: 0,
                verticalSmushing: false,
                verticalFitting: false)
        {
            _glyphs = glyphs;
        }

        public override bool TryGetGlyph(int codePoint, out FigletGlyph glyph)
        {
            if (_glyphs.TryGetValue(codePoint, out var found))
            {
                glyph = found;
                return true;
            }
            glyph = null!;
            return false;
        }
    }
}
