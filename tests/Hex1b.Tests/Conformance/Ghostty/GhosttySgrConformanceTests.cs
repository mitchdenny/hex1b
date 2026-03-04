using Hex1b.Tokens;

namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// SGR (Select Graphic Rendition) conformance tests derived from Ghostty's sgr.zig.
/// Tests exercise the full pipeline: raw ANSI → AnsiTokenizer → ApplyTokens → cell attributes.
/// </summary>
/// <remarks>
/// Source: https://github.com/ghostty-org/ghostty/blob/main/src/terminal/sgr.zig
/// These tests verify Hex1b handles SGR sequences the same way Ghostty does.
/// </remarks>
[Trait("Category", "GhosttyConformance")]
public class GhosttySgrConformanceTests
{
    private static Hex1bTerminal CreateTerminal(int cols = 20, int rows = 5)
        => GhosttyTestFixture.CreateTerminal(cols, rows);

    #region Basic Attributes

    // Ghostty: test "sgr: bold"
    [Fact]
    public void Sgr_Bold_SetsAndResets()
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal, "\x1b[1mB\x1b[22mN");

        var boldCell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        var normalCell = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.True(boldCell.Attributes.HasFlag(CellAttributes.Bold));
        Assert.False(normalCell.Attributes.HasFlag(CellAttributes.Bold));
    }

    // Ghostty: test "sgr: italic"
    [Fact]
    public void Sgr_Italic_SetsAndResets()
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal, "\x1b[3mI\x1b[23mN");

        var italicCell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        var normalCell = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.True(italicCell.Attributes.HasFlag(CellAttributes.Italic));
        Assert.False(normalCell.Attributes.HasFlag(CellAttributes.Italic));
    }

    // Ghostty: test "sgr: underline"
    [Fact]
    public void Sgr_Underline_SetsAndResets()
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal, "\x1b[4mU\x1b[24mN");

        var underlineCell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        var normalCell = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.True(underlineCell.Attributes.HasFlag(CellAttributes.Underline));
        Assert.False(normalCell.Attributes.HasFlag(CellAttributes.Underline));
    }

    // Ghostty: test "sgr: blink" — codes 5 and 6 both produce blink
    [Fact]
    public void Sgr_Blink_BothCodes5And6()
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal, "\x1b[5mA\x1b[0m\x1b[6mB\x1b[25mC");

        var cell5 = GhosttyTestFixture.GetCell(terminal, 0, 0);
        var cell6 = GhosttyTestFixture.GetCell(terminal, 0, 1);
        var cellReset = GhosttyTestFixture.GetCell(terminal, 0, 2);
        Assert.True(cell5.Attributes.HasFlag(CellAttributes.Blink));
        Assert.True(cell6.Attributes.HasFlag(CellAttributes.Blink));
        Assert.False(cellReset.Attributes.HasFlag(CellAttributes.Blink));
    }

    // Ghostty: test "sgr: inverse"
    [Fact]
    public void Sgr_Inverse_SetsAndResets()
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal, "\x1b[7mR\x1b[27mN");

        var reverseCell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        var normalCell = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.True(reverseCell.Attributes.HasFlag(CellAttributes.Reverse));
        Assert.False(normalCell.Attributes.HasFlag(CellAttributes.Reverse));
    }

    // Ghostty: test "sgr: invisible"
    [Fact]
    public void Sgr_Invisible_SetsAndResets()
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal, "\x1b[8mH\x1b[28mV");

        var hiddenCell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        var visibleCell = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.True(hiddenCell.Attributes.HasFlag(CellAttributes.Hidden));
        Assert.False(visibleCell.Attributes.HasFlag(CellAttributes.Hidden));
    }

    // Ghostty: test "sgr: strikethrough"
    [Fact]
    public void Sgr_Strikethrough_SetsAndResets()
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal, "\x1b[9mS\x1b[29mN");

        var strikeCell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        var normalCell = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.True(strikeCell.Attributes.HasFlag(CellAttributes.Strikethrough));
        Assert.False(normalCell.Attributes.HasFlag(CellAttributes.Strikethrough));
    }

    // Ghostty: test "sgr: faint" + code 22 resets both bold and faint
    [Fact]
    public void Sgr_Faint_SetsAndResetWithCode22()
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal, "\x1b[2mD\x1b[22mN");

        var dimCell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        var normalCell = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.True(dimCell.Attributes.HasFlag(CellAttributes.Dim));
        Assert.False(normalCell.Attributes.HasFlag(CellAttributes.Dim));
    }

    // Ghostty: overline (code 53/55)
    [Fact]
    public void Sgr_Overline_SetsAndResets()
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal, "\x1b[53mO\x1b[55mN");

        var overlineCell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        var normalCell = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.True(overlineCell.Attributes.HasFlag(CellAttributes.Overline));
        Assert.False(normalCell.Attributes.HasFlag(CellAttributes.Overline));
    }

    // Ghostty: code 0 resets all attributes
    [Fact]
    public void Sgr_Reset_ClearsAllAttributes()
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal, "\x1b[1;3;4;7;9mA\x1b[0mB");

        var styledCell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        var resetCell = GhosttyTestFixture.GetCell(terminal, 0, 1);

        Assert.True(styledCell.Attributes.HasFlag(CellAttributes.Bold));
        Assert.True(styledCell.Attributes.HasFlag(CellAttributes.Italic));
        Assert.True(styledCell.Attributes.HasFlag(CellAttributes.Underline));
        Assert.True(styledCell.Attributes.HasFlag(CellAttributes.Reverse));
        Assert.True(styledCell.Attributes.HasFlag(CellAttributes.Strikethrough));

        Assert.Equal(CellAttributes.None, resetCell.Attributes);
    }

    // Ghostty: empty SGR (ESC[m) is equivalent to reset
    [Fact]
    public void Sgr_EmptySequence_IsReset()
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal, "\x1b[1mA\x1b[mB");

        var boldCell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        var resetCell = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.True(boldCell.Attributes.HasFlag(CellAttributes.Bold));
        Assert.Equal(CellAttributes.None, resetCell.Attributes);
    }

    #endregion

    #region Standard 8 Colors

    // Ghostty: test "sgr: 8 color"
    [Fact]
    public void Sgr_8Color_ForegroundAndBackground()
    {
        using var terminal = CreateTerminal();

        // Red foreground (31), Yellow background (43)
        GhosttyTestFixture.Feed(terminal, "\x1b[31;43mA\x1b[0m");

        var cell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.NotNull(cell.Foreground);
        Assert.NotNull(cell.Background);
        // Red = index 1 → (187, 0, 0) or similar depending on palette
        // We just verify colors are set, not exact RGB (palette-dependent)
    }

    // Standard color codes 30-37 (fg) and 40-47 (bg)
    [Theory]
    [InlineData(30, true)]   // black fg
    [InlineData(31, true)]   // red fg
    [InlineData(32, true)]   // green fg
    [InlineData(37, true)]   // white fg
    [InlineData(40, false)]  // black bg
    [InlineData(41, false)]  // red bg
    [InlineData(47, false)]  // white bg
    public void Sgr_StandardColor_SetsColorByCode(int code, bool isForeground)
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal, $"\x1b[{code}mX\x1b[0m");

        var cell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        if (isForeground)
            Assert.NotNull(cell.Foreground);
        else
            Assert.NotNull(cell.Background);
    }

    // Bright colors 90-97 (fg) and 100-107 (bg)
    [Theory]
    [InlineData(90, true)]   // bright black fg
    [InlineData(91, true)]   // bright red fg
    [InlineData(97, true)]   // bright white fg
    [InlineData(100, false)] // bright black bg
    [InlineData(101, false)] // bright red bg
    [InlineData(107, false)] // bright white bg
    public void Sgr_BrightColor_SetsColorByCode(int code, bool isForeground)
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal, $"\x1b[{code}mX\x1b[0m");

        var cell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        if (isForeground)
            Assert.NotNull(cell.Foreground);
        else
            Assert.NotNull(cell.Background);
    }

    // Reset fg (39) and bg (49)
    [Fact]
    public void Sgr_ResetColor_ResetsToDefault()
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal, "\x1b[31mR\x1b[39mD");

        var redCell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        var defaultCell = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.NotNull(redCell.Foreground);
        Assert.Null(defaultCell.Foreground); // null = default color
    }

    #endregion

    #region 256 Color

    // Ghostty: test "sgr: 256 color"
    [Fact]
    public void Sgr_256Color_ForegroundAndBackground()
    {
        using var terminal = CreateTerminal();
        // ESC[38;5;161m = 256-color fg, ESC[48;5;236m = 256-color bg
        GhosttyTestFixture.Feed(terminal, "\x1b[38;5;161mF\x1b[48;5;236mB\x1b[0m");

        var fgCell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        var bothCell = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.NotNull(fgCell.Foreground);
        Assert.NotNull(bothCell.Background);
    }

    #endregion

    #region 24-bit True Color (RGB)

    // Ghostty: test "sgr: Parser" — direct color fg with semicolons
    [Fact]
    public void Sgr_DirectColorFg_Semicolons()
    {
        using var terminal = CreateTerminal();
        // ESC[38;2;40;44;52m = RGB fg (40,44,52)
        GhosttyTestFixture.Feed(terminal, "\x1b[38;2;40;44;52mX");

        var cell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.NotNull(cell.Foreground);
        Assert.Equal(40, cell.Foreground!.Value.R);
        Assert.Equal(44, cell.Foreground!.Value.G);
        Assert.Equal(52, cell.Foreground!.Value.B);
    }

    // Ghostty: test "sgr: Parser" — direct color bg with semicolons
    [Fact]
    public void Sgr_DirectColorBg_Semicolons()
    {
        using var terminal = CreateTerminal();
        // ESC[48;2;40;44;52m = RGB bg (40,44,52)
        GhosttyTestFixture.Feed(terminal, "\x1b[48;2;40;44;52mX");

        var cell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.NotNull(cell.Background);
        Assert.Equal(40, cell.Background!.Value.R);
        Assert.Equal(44, cell.Background!.Value.G);
        Assert.Equal(52, cell.Background!.Value.B);
    }

    // Ghostty: test "sgr: Parser multiple" — reset then direct color
    [Fact]
    public void Sgr_ResetThenDirectColor_MultipleParams()
    {
        using var terminal = CreateTerminal();
        // ESC[0;38;2;40;44;52m = reset, then RGB fg
        GhosttyTestFixture.Feed(terminal, "\x1b[1mB\x1b[0;38;2;40;44;52mX");

        var colorCell = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.False(colorCell.Attributes.HasFlag(CellAttributes.Bold)); // reset cleared bold
        Assert.NotNull(colorCell.Foreground);
        Assert.Equal(40, colorCell.Foreground!.Value.R);
    }

    // Ghostty: test "sgr: direct fg/bg/underline ignore optional color space"
    // Semicolon version: ESC[38;2;R;G;B;m — no colorspace, uses first 3 values as RGB
    [Fact]
    public void Sgr_DirectColorFg_SemicolonSkipsValues()
    {
        using var terminal = CreateTerminal();
        // With semicolons: 38;2;0;1;2 → R=0, G=1, B=2 (no colorspace skip)
        GhosttyTestFixture.Feed(terminal, "\x1b[38;2;0;1;2mX");

        var cell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.NotNull(cell.Foreground);
        Assert.Equal(0, cell.Foreground!.Value.R);
        Assert.Equal(1, cell.Foreground!.Value.G);
        Assert.Equal(2, cell.Foreground!.Value.B);
    }

    #endregion

    #region Complex Multi-Attribute Sequences

    // Ghostty: test "sgr: underline, bg, and fg"
    // ESC[4;38;2;255;247;219;48;2;242;93;147;4m
    [Fact]
    public void Sgr_ComplexSequence_UnderlineFgBg()
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal,
            "\x1b[4;38;2;255;247;219;48;2;242;93;147mX");

        var cell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Underline));
        Assert.NotNull(cell.Foreground);
        Assert.Equal(255, cell.Foreground!.Value.R);
        Assert.Equal(247, cell.Foreground!.Value.G);
        Assert.Equal(219, cell.Foreground!.Value.B);
        Assert.NotNull(cell.Background);
        Assert.Equal(242, cell.Background!.Value.R);
        Assert.Equal(93, cell.Background!.Value.G);
        Assert.Equal(147, cell.Background!.Value.B);
    }

    // Ghostty: test "sgr: direct color fg missing color" — should not crash
    [Fact]
    public void Sgr_DirectColorFg_MissingValues_NoCrash()
    {
        using var terminal = CreateTerminal();
        // ESC[38;5m — missing the actual color index
        GhosttyTestFixture.Feed(terminal, "\x1b[38;5mX");
        // Should not throw — just verify terminal is still functional
        Assert.Equal("X", GhosttyTestFixture.GetLine(terminal, 0));
    }

    // Ghostty: test "sgr: direct color bg missing color" — should not crash
    [Fact]
    public void Sgr_DirectColorBg_MissingValues_NoCrash()
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal, "\x1b[48;5mX");
        Assert.Equal("X", GhosttyTestFixture.GetLine(terminal, 0));
    }

    // Ghostty: code 21 = double underline per ECMA-48
    // BUG FOUND: Hex1b treats code 21 as "reset bold" (same as 22), but ECMA-48
    // and Ghostty both define it as "double underline". This is a conformance bug.
    [Fact]
    public void Sgr_Code21_DoubleUnderline()
    {
        using var terminal = CreateTerminal();
        // Code 21 is double underline in Ghostty/ECMA-48
        GhosttyTestFixture.Feed(terminal, "\x1b[21mX\x1b[24mY");

        var underlinedCell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        var normalCell = GhosttyTestFixture.GetCell(terminal, 0, 1);

        // BUG: Hex1b currently treats 21 as reset bold (like 22), not as double underline.
        // Ghostty and ECMA-48 say 21 = doubly underlined.
        // Hex1bTerminal.cs:2651 — case 21 falls through to case 22 (reset bold).
        // Fix: case 21 should set underline, not reset bold.
        Assert.True(underlinedCell.Attributes.HasFlag(CellAttributes.Underline));
        Assert.False(normalCell.Attributes.HasFlag(CellAttributes.Underline));
    }

    #endregion

    #region Colon-Separated Sub-Parameters (Ghostty-specific handling)

    // Ghostty: test "sgr: underline styles" — colon-separated 4:N
    // These test colon sub-params: ESC[4:0m through ESC[4:5m
    // NOTE: Hex1b may not support colon-separated params yet.
    // Tests are included to identify this gap.

    [Fact(Skip = "Colon-separated SGR sub-params not yet implemented in Hex1b")]
    public void Sgr_UnderlineStyle_Curly_ColonSeparated()
    {
        using var terminal = CreateTerminal();
        // ESC[4:3m = curly underline (colon separator)
        GhosttyTestFixture.Feed(terminal, "\x1b[4:3mX\x1b[24mY");

        var curlyCell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.True(curlyCell.Attributes.HasFlag(CellAttributes.Underline));
    }

    [Fact(Skip = "Colon-separated SGR sub-params not yet implemented in Hex1b")]
    public void Sgr_UnderlineStyle_None_ColonSeparated()
    {
        using var terminal = CreateTerminal();
        // ESC[4:0m = no underline
        GhosttyTestFixture.Feed(terminal, "\x1b[4mU\x1b[4:0mN");

        var noUnderline = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.False(noUnderline.Attributes.HasFlag(CellAttributes.Underline));
    }

    // Ghostty: test "sgr: underline color" — colon-separated 58:2:R:G:B
    [Fact(Skip = "Underline color (SGR 58) not yet implemented in Hex1b")]
    public void Sgr_UnderlineColor_ColonSeparated()
    {
        using var terminal = CreateTerminal();
        // ESC[58:2:1:2:3m = underline color RGB(1,2,3)
        GhosttyTestFixture.Feed(terminal, "\x1b[4;58:2:1:2:3mX");

        // Would need underline color support in TerminalCell
        var cell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Underline));
    }

    // Ghostty: test "sgr: 24-bit bg color" with colon separator
    [Fact(Skip = "Colon-separated SGR sub-params not yet implemented in Hex1b")]
    public void Sgr_DirectColorBg_ColonSeparated()
    {
        using var terminal = CreateTerminal();
        // ESC[48:2:1:2:3m = RGB bg with colon separators (no colorspace)
        GhosttyTestFixture.Feed(terminal, "\x1b[48:2:1:2:3mX");

        var cell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.NotNull(cell.Background);
        Assert.Equal(1, cell.Background!.Value.R);
        Assert.Equal(2, cell.Background!.Value.G);
        Assert.Equal(3, cell.Background!.Value.B);
    }

    // Ghostty: test "sgr: direct fg/bg/underline ignore optional color space"
    // Colon version with colorspace: 38:2:Pi:R:G:B (6 values → skip Pi)
    [Fact(Skip = "Colon-separated SGR sub-params not yet implemented in Hex1b")]
    public void Sgr_DirectColorFg_ColonWithColorspace()
    {
        using var terminal = CreateTerminal();
        // ESC[38:2:0:1:2:3m = 38:2:colorspace:R:G:B → R=1,G=2,B=3
        GhosttyTestFixture.Feed(terminal, "\x1b[38:2:0:1:2:3mX");

        var cell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.NotNull(cell.Foreground);
        Assert.Equal(1, cell.Foreground!.Value.R);
        Assert.Equal(2, cell.Foreground!.Value.G);
        Assert.Equal(3, cell.Foreground!.Value.B);
    }

    // Ghostty: test "sgr: kakoune input" — complex real-world sequence from Kakoune editor
    // ESC[0;4:3;38;2;175;175;215;58:2:0:190:80:70m
    [Fact(Skip = "Colon-separated SGR sub-params not yet implemented in Hex1b")]
    public void Sgr_KakouneInput_ComplexMixedSequence()
    {
        using var terminal = CreateTerminal();
        // Mixed colon and semicolon: reset, curly underline, RGB fg, underline color
        GhosttyTestFixture.Feed(terminal,
            "\x1b[0;4:3;38;2;175;175;215;58:2:0:190:80:70mX");

        var cell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Underline));
        Assert.NotNull(cell.Foreground);
        Assert.Equal(175, cell.Foreground!.Value.R);
        Assert.Equal(175, cell.Foreground!.Value.G);
        Assert.Equal(215, cell.Foreground!.Value.B);
    }

    // Ghostty: test "sgr: kakoune input issue underline, fg, and bg"
    // ESC[4:3;38;2;51;51;51;48;2;170;170;170;58;2;255;97;136m
    [Fact(Skip = "Colon-separated SGR sub-params not yet implemented in Hex1b")]
    public void Sgr_KakouneInput_UnderlineFgBgUnderlineColor()
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal,
            "\x1b[4:3;38;2;51;51;51;48;2;170;170;170;58;2;255;97;136mX");

        var cell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Underline));
        Assert.NotNull(cell.Foreground);
        Assert.Equal(51, cell.Foreground!.Value.R);
        Assert.NotNull(cell.Background);
        Assert.Equal(170, cell.Background!.Value.R);
    }

    #endregion

    #region Edge Cases

    // Ghostty: test "sgr: Parser" — too few params for direct color is unknown
    [Fact]
    public void Sgr_DirectColorFg_TooFewParams_IsUnknown()
    {
        using var terminal = CreateTerminal();
        // ESC[38;2;44;52m — only 2 color values instead of 3
        GhosttyTestFixture.Feed(terminal, "\x1b[38;2;44;52mX");

        // Should not crash; terminal should still be functional
        Assert.Equal("X", GhosttyTestFixture.GetLine(terminal, 0));
    }

    // Ghostty: code 22 resets both bold AND faint
    [Fact]
    public void Sgr_Code22_ResetsBoldAndFaint()
    {
        using var terminal = CreateTerminal();
        // Bold, write A, then faint, write B, then code 22 (resets both), write C
        GhosttyTestFixture.Feed(terminal, "\x1b[1mA\x1b[2mB\x1b[22mC");

        var boldCell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        var boldDimCell = GhosttyTestFixture.GetCell(terminal, 0, 1);
        var normalCell = GhosttyTestFixture.GetCell(terminal, 0, 2);

        Assert.True(boldCell.Attributes.HasFlag(CellAttributes.Bold));
        Assert.True(boldDimCell.Attributes.HasFlag(CellAttributes.Dim));
        Assert.False(normalCell.Attributes.HasFlag(CellAttributes.Bold));
        Assert.False(normalCell.Attributes.HasFlag(CellAttributes.Dim));
    }

    // Multiple SGR attributes in single sequence
    [Fact]
    public void Sgr_MultipleAttributes_SingleSequence()
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal, "\x1b[1;3;4;9mX");

        var cell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Bold));
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Italic));
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Underline));
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Strikethrough));
    }

    #endregion
}
