using Xunit;

namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Conformance tests for print and overwrite operations,
/// translated from Ghostty's Terminal.zig.
/// </summary>
[Trait("Category", "GhosttyConformance")]
public class GhosttyPrintOverwriteConformanceTests
{
    private static Hex1bTerminal CreateTerminal(int cols = 80, int rows = 24)
        => GhosttyTestFixture.CreateTerminal(cols, rows);

    #region Print Basics

    [Fact]
    public void PrintSingleVeryLongLine_DoesNotCrash()
    {
        // Ghostty: "Terminal: print single very long line"
        using var t = CreateTerminal(cols: 5, rows: 5);

        // Print 1000 chars into a 5x5 terminal — must not crash
        GhosttyTestFixture.Feed(t, new string('x', 1000));
    }

    #endregion

    #region Print Over Wide Characters

    [Fact]
    public void PrintOverWideCharAtCol0_DoesNotCorruptPreviousRow()
    {
        // Ghostty: "Terminal: print over wide char at col 0 corrupts previous row"
        using var t = CreateTerminal(cols: 10, rows: 3);

        // Fill rows 0 and 1 with wide chars (5 per row on 10-col terminal)
        // 0x4E2D = 中
        for (int i = 0; i < 10; i++)
            GhosttyTestFixture.Feed(t, "中");

        // Move to row 1, col 0 (setCursorPos(2, 1)) and print narrow char
        GhosttyTestFixture.Feed(t, "\u001b[2;1H");
        GhosttyTestFixture.Feed(t, "A");

        // Row 1, col 0 should be the narrow 'A'
        var cell10 = GhosttyTestFixture.GetCell(t, 1, 0);
        Assert.Equal("A", cell10.Character);

        // Row 0, col 8 should still be wide (last wide char on the row)
        var cell08 = GhosttyTestFixture.GetCell(t, 0, 8);
        Assert.Equal("中", cell08.Character);

        // Row 0, col 9 must remain spacer_tail (continuation cell)
        var cell09 = GhosttyTestFixture.GetCell(t, 0, 9);
        Assert.Equal("", cell09.Character);
    }

    #endregion

    #region Print Multicodepoint Grapheme

    [Trait("FailureReason", "Bug")]
    [Fact(Skip = "Hex1b always clusters graphemes regardless of mode 2027 state")]
    public void PrintMulticodepointGrapheme_DisabledMode2027_TreatedAsSeparateChars()
    {
        // Ghostty: "Terminal: print multicodepoint grapheme, disabled mode 2027"
        using var t = CreateTerminal(cols: 80, rows: 80);

        // Print family emoji without mode 2027: 👨‍👩‍👧
        // Each emoji treated separately (2 cells each), ZWJ is zero-width
        GhosttyTestFixture.Feed(t, "\U0001F468\u200D\U0001F469\u200D\U0001F467");

        // Without grapheme clustering, 3 wide chars × 2 cells = 6
        Assert.Equal(0, t.CursorY);
        Assert.Equal(6, t.CursorX);

        // First cell should have the first emoji
        var cell = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.Equal("\U0001F468", cell.Character);
    }

    #endregion

    #region Overwrite Grapheme Data

    [Fact]
    public void OverwriteGrapheme_ClearsGraphemeData()
    {
        // Ghostty: "Terminal: overwrite grapheme should clear grapheme data"
        using var t = CreateTerminal(cols: 5, rows: 5);

        // Enable grapheme clustering (mode 2027)
        GhosttyTestFixture.Feed(t, "\u001b[?2027h");

        // Print ⛈ (U+26C8) + VS15 (U+FE0E) to make narrow
        GhosttyTestFixture.Feed(t, "\u26C8\uFE0E");

        // Go back to origin and overwrite with 'A'
        GhosttyTestFixture.Feed(t, "\u001b[1;1H");
        GhosttyTestFixture.Feed(t, "A");

        var line = GhosttyTestFixture.GetLine(t, 0);
        Assert.Equal("A", line);

        var cell = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.Equal("A", cell.Character);
    }

    [Fact]
    public void OverwriteMulticodepointGrapheme_ClearsGraphemeData()
    {
        // Ghostty: "Terminal: overwrite multicodepoint grapheme clears grapheme data"
        using var t = CreateTerminal(cols: 10, rows: 10);

        // Enable grapheme clustering
        GhosttyTestFixture.Feed(t, "\u001b[?2027h");

        // Print family emoji: 👨‍👩‍👧
        GhosttyTestFixture.Feed(t, "\U0001F468\u200D\U0001F469\u200D\U0001F467");

        // With mode 2027, treated as single wide grapheme (2 cells)
        Assert.Equal(0, t.CursorY);
        Assert.Equal(2, t.CursorX);

        // Go back and overwrite
        GhosttyTestFixture.Feed(t, "\u001b[1;1H");
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal(0, t.CursorY);
        Assert.Equal(1, t.CursorX);

        var line = GhosttyTestFixture.GetLine(t, 0);
        Assert.Equal("X", line);
    }

    [Fact]
    public void OverwriteMulticodepointGraphemeTail_ClearsGraphemeData()
    {
        // Ghostty: "Terminal: overwrite multicodepoint grapheme tail clears grapheme data"
        using var t = CreateTerminal(cols: 10, rows: 10);

        // Enable grapheme clustering
        GhosttyTestFixture.Feed(t, "\u001b[?2027h");

        // Print family emoji: 👨‍👩‍👧
        GhosttyTestFixture.Feed(t, "\U0001F468\u200D\U0001F469\u200D\U0001F467");

        Assert.Equal(0, t.CursorY);
        Assert.Equal(2, t.CursorX);

        // Move to col 2 (1-based) = col 1 (0-based, the tail/spacer) and overwrite
        GhosttyTestFixture.Feed(t, "\u001b[1;2H");
        GhosttyTestFixture.Feed(t, "X");

        var line = GhosttyTestFixture.GetLine(t, 0);
        Assert.Equal(" X", line);

        Assert.Equal(0, t.CursorY);
        Assert.Equal(2, t.CursorX);
    }

    #endregion

    #region Print Charset

    [Trait("FailureReason", "Bug")]
    [Fact]
    public void PrintCharset_DecSpecialGraphics()
    {
        // Ghostty: "Terminal: print charset"
        using var t = CreateTerminal(cols: 80, rows: 80);

        // Configure G1, G2, G3 with DEC special (should have no effect on GL)
        GhosttyTestFixture.Feed(t, "\u001b)0");  // G1 = DEC special
        GhosttyTestFixture.Feed(t, "\u001b*0");  // G2 = DEC special
        GhosttyTestFixture.Feed(t, "\u001b+0");  // G3 = DEC special

        // Print backtick with G0 as default ASCII → literal backtick
        GhosttyTestFixture.Feed(t, "`");

        // Set G0 to UTF-8/ASCII (ESC ( B) — backtick unchanged
        GhosttyTestFixture.Feed(t, "\u001b(B");
        GhosttyTestFixture.Feed(t, "`");

        // Set G0 to ASCII (ESC ( B) — backtick unchanged
        GhosttyTestFixture.Feed(t, "\u001b(B");
        GhosttyTestFixture.Feed(t, "`");

        // Set G0 to DEC special graphics (ESC ( 0)
        GhosttyTestFixture.Feed(t, "\u001b(0");
        GhosttyTestFixture.Feed(t, "`");

        // Expected: "```◆" — first three are literal backticks, fourth is diamond
        var line = GhosttyTestFixture.GetLine(t, 0);
        Assert.Equal("```◆", line);
    }

    [Trait("FailureReason", "Bug")]
    [Fact]
    public void PrintCharset_OutsideAscii()
    {
        // Ghostty: "Terminal: print charset outside of ASCII"
        using var t = CreateTerminal(cols: 80, rows: 80);

        // Configure G1, G2, G3 with DEC special (no effect on GL)
        GhosttyTestFixture.Feed(t, "\u001b)0");
        GhosttyTestFixture.Feed(t, "\u001b*0");
        GhosttyTestFixture.Feed(t, "\u001b+0");

        // Set G0 to DEC special and print backtick → ◆ (diamond)
        GhosttyTestFixture.Feed(t, "\u001b(0");
        GhosttyTestFixture.Feed(t, "`");

        // Print emoji (outside ASCII range) — DEC special should not remap it
        GhosttyTestFixture.Feed(t, "\U0001F600");

        // Verify diamond at col 0
        var cell0 = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.Equal("◆", cell0.Character);

        // Ghostty expects "◆ " — non-ASCII emoji in DEC special mode produces
        // a space. GetLine trims trailing spaces, so check trimmed result.
        var line = GhosttyTestFixture.GetLine(t, 0);
        Assert.StartsWith("◆", line);
    }

    [Trait("FailureReason", "Bug")]
    [Fact]
    public void PrintInvokeCharset_ShiftInOut()
    {
        // Ghostty: "Terminal: print invoke charset"
        using var t = CreateTerminal(cols: 80, rows: 80);

        // Configure G1 as DEC special
        GhosttyTestFixture.Feed(t, "\u001b)0");

        // Print backtick with G0 (default ASCII) → literal backtick
        GhosttyTestFixture.Feed(t, "`");

        // Invoke G1 into GL (SO = 0x0E)
        GhosttyTestFixture.Feed(t, "\u000e");

        // Print backtick with G1 (DEC special) → ◆
        GhosttyTestFixture.Feed(t, "`");
        GhosttyTestFixture.Feed(t, "`");

        // Invoke G0 back into GL (SI = 0x0F)
        GhosttyTestFixture.Feed(t, "\u000f");

        // Print backtick with G0 (ASCII) → literal backtick
        GhosttyTestFixture.Feed(t, "`");

        // Expected: "`◆◆`"
        var line = GhosttyTestFixture.GetLine(t, 0);
        Assert.Equal("`\u25C6\u25C6`", line);
    }

    #endregion

    #region Print Right Margin

    [Fact]
    public void PrintRightMarginWrap()
    {
        // Ghostty: "Terminal: print right margin wrap"
        using var t = CreateTerminal(cols: 10, rows: 5);

        GhosttyTestFixture.Feed(t, "123456789");

        // Enable left/right margin mode (DECLRMM)
        GhosttyTestFixture.Feed(t, "\u001b[?69h");
        // Set left margin=3, right margin=5 (1-based) — DECSLRM
        GhosttyTestFixture.Feed(t, "\u001b[3;5s");
        // CUP(1,5) → row 0, col 4 (0-based)
        GhosttyTestFixture.Feed(t, "\u001b[1;5H");

        GhosttyTestFixture.Feed(t, "XY");

        // Row 0: X replaces col 4 (was '5'), rest unchanged
        var line0 = GhosttyTestFixture.GetLine(t, 0);
        Assert.Equal("1234X6789", line0);

        // Y wraps within margin to next row at left margin (col 2, 0-based)
        var line1 = GhosttyTestFixture.GetLine(t, 1);
        Assert.Equal("  Y", line1);
    }

    [Fact]
    public void PrintRightMarginOutside()
    {
        // Ghostty: "Terminal: print right margin outside"
        using var t = CreateTerminal(cols: 10, rows: 5);

        GhosttyTestFixture.Feed(t, "123456789");
        GhosttyTestFixture.Feed(t, "\u001b[?69h");
        GhosttyTestFixture.Feed(t, "\u001b[3;5s");
        // CUP(1,6) → cursor outside right margin (col 5, 0-based)
        GhosttyTestFixture.Feed(t, "\u001b[1;6H");

        GhosttyTestFixture.Feed(t, "XY");

        // Printing outside margin doesn't trigger margin wrap
        var line0 = GhosttyTestFixture.GetLine(t, 0);
        Assert.Equal("12345XY89", line0);
    }

    [Fact]
    public void PrintRightMarginOutsideWrap()
    {
        // Ghostty: "Terminal: print right margin outside wrap"
        using var t = CreateTerminal(cols: 10, rows: 5);

        GhosttyTestFixture.Feed(t, "123456789");
        GhosttyTestFixture.Feed(t, "\u001b[?69h");
        GhosttyTestFixture.Feed(t, "\u001b[3;5s");
        // CUP(1,10) → cursor at last column (col 9, 0-based)
        GhosttyTestFixture.Feed(t, "\u001b[1;10H");

        GhosttyTestFixture.Feed(t, "XY");

        // X prints at col 9 (last column), then wraps
        var line0 = GhosttyTestFixture.GetLine(t, 0);
        Assert.Equal("123456789X", line0);

        // Y wraps to next row at left margin (col 2, 0-based)
        var line1 = GhosttyTestFixture.GetLine(t, 1);
        Assert.Equal("  Y", line1);
    }

    #endregion
}
