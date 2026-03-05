using System;
using Hex1b;
using Xunit;

namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Additional Ghostty conformance tests covering miscellaneous terminal behaviors:
/// LNM mode, zero-width characters, wide char at right margin with DECLRMM,
/// bold style application, and basic LF+CR interaction.
/// </summary>
[Trait("Category", "GhosttyConformance")]
public class GhosttyMiscConformance2Tests
{
    private static Hex1bTerminal CreateTerminal(int cols, int rows) => GhosttyTestFixture.CreateTerminal(cols, rows);
    private static void Feed(Hex1bTerminal t, string s) => GhosttyTestFixture.Feed(t, s);
    private static string GetLine(Hex1bTerminal t, int row) => GhosttyTestFixture.GetLine(t, row);
    private static TerminalCell GetCell(Hex1bTerminal t, int row, int col) => GhosttyTestFixture.GetCell(t, row, col);

    #region Linefeed Mode (LNM)

    /// <summary>
    /// Ghostty: "Terminal: linefeed mode automatic carriage return"
    /// When LNM (mode 20) is set, LF automatically performs CR.
    /// </summary>
    [Fact]
    public void LinefeedMode_AutomaticCarriageReturn()
    {
        using var t = CreateTerminal(10, 10);

        // Enable LNM mode (standard mode 20)
        Feed(t, "\x1b[20h");
        Feed(t, "123456");
        Feed(t, "\n"); // LF — with LNM, should also do CR
        Feed(t, "X");

        Assert.Equal("123456", GetLine(t, 0));
        Assert.Equal("X", GetLine(t, 1));
    }

    /// <summary>
    /// Ghostty: "Terminal: linefeed and carriage return"
    /// Basic test: print, CR, LF, print produces correct output.
    /// </summary>
    [Fact]
    public void LinefeedAndCarriageReturn()
    {
        using var t = CreateTerminal(80, 80);

        Feed(t, "hello");
        Feed(t, "\r");     // CR
        Feed(t, "\n");     // LF
        Feed(t, "world");

        Assert.Equal(1, t.CursorY);
        Assert.Equal(5, t.CursorX);
        Assert.Equal("hello", GetLine(t, 0));
        Assert.Equal("world", GetLine(t, 1));
    }

    /// <summary>
    /// Ghostty: "Terminal: linefeed unsets pending wrap"
    /// LF clears pending wrap state.
    /// </summary>
    [Fact]
    public void Linefeed_UnsetsPendingWrap()
    {
        using var t = CreateTerminal(5, 80);

        Feed(t, "hello"); // Fills 5 cols, sets pending wrap
        Assert.True(t.PendingWrap);

        Feed(t, "\n"); // LF should clear pending wrap
        Assert.False(t.PendingWrap);
    }

    #endregion

    #region Zero-Width Characters

    /// <summary>
    /// Ghostty: "Terminal: zero-width character at start"
    /// A zero-width character (ZWJ) at position 0,0 should be ignored, not crash.
    /// </summary>
    [Fact]
    public void ZeroWidth_CharacterAtStart()
    {
        using var t = CreateTerminal(80, 80);

        // ZWJ at start should be silently ignored
        Feed(t, "\u200D");

        Assert.Equal(0, t.CursorY);
        Assert.Equal(0, t.CursorX);
    }

    #endregion

    #region Wide Char at Right Margin (DECLRMM)

    /// <summary>
    /// Ghostty: "Terminal: print wide char at right margin does not create spacer head"
    /// With DECLRMM active and right margin set, a wide char at the right margin
    /// should wrap to left margin on next row without creating a spacer head.
    /// </summary>
    [Fact]
    public void WideChar_AtRightMargin_NoSpacerHead_WithDECLRMM()
    {
        using var t = CreateTerminal(10, 10);

        // Enable left/right margin mode and set margins 3-5 (1-based)
        Feed(t, "\x1b[?69h");    // DECLRMM
        Feed(t, "\x1b[3;5s");     // DECSLRM left=3, right=5
        Feed(t, "\x1b[1;5H");     // Move to row 1, col 5 (right margin)
        Feed(t, "\U0001F600");    // Smiley face (wide char) — doesn't fit at col 5

        // Should have wrapped to next row, at left margin + 2 (after wide char)
        Assert.Equal(1, t.CursorY);

        // Col 4 (0-based) on row 0 should be empty (no spacer head)
        var cell = GetCell(t, 0, 4);
        Assert.True(string.IsNullOrWhiteSpace(cell.Character),
            $"Expected empty cell at [0,4] but got '{cell.Character}'");

        // Wide char should be on row 1 at left margin (col 2, 0-based)
        var wideCell = GetCell(t, 1, 2);
        Assert.Equal("\U0001F600", wideCell.Character);
    }

    #endregion

    #region Bold Style

    /// <summary>
    /// Ghostty: "Terminal: bold style"
    /// Printing with bold SGR attribute marks the cell with Bold attribute.
    /// </summary>
    [Fact]
    public void BoldStyle_AppliedToCell()
    {
        using var t = CreateTerminal(5, 5);

        Feed(t, "\x1b[1m"); // SGR bold
        Feed(t, "A");

        var cell = GetCell(t, 0, 0);
        Assert.Equal("A", cell.Character);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Bold));
    }

    /// <summary>
    /// Printing with bold then reset — second char should not be bold.
    /// </summary>
    [Fact]
    public void BoldStyle_ResetClearsAttribute()
    {
        using var t = CreateTerminal(5, 5);

        Feed(t, "\x1b[1m"); // SGR bold
        Feed(t, "A");
        Feed(t, "\x1b[0m"); // SGR reset
        Feed(t, "B");

        var boldCell = GetCell(t, 0, 0);
        Assert.True(boldCell.Attributes.HasFlag(CellAttributes.Bold));

        var normalCell = GetCell(t, 0, 1);
        Assert.False(normalCell.Attributes.HasFlag(CellAttributes.Bold));
    }

    #endregion

    #region Carriage Return with Margins

    /// <summary>
    /// Ghostty: "Terminal: carriage return unsets pending wrap"
    /// CR clears pending wrap state.
    /// </summary>
    [Fact]
    public void CarriageReturn_UnsetsPendingWrap()
    {
        using var t = CreateTerminal(5, 80);

        Feed(t, "hello"); // Fills 5 cols, sets pending wrap
        Assert.True(t.PendingWrap);

        Feed(t, "\r"); // CR should clear pending wrap
        Assert.False(t.PendingWrap);
    }

    /// <summary>
    /// Ghostty: "Terminal: carriage return origin mode moves to left margin"
    /// With origin mode enabled, CR moves to the left margin.
    /// </summary>
    [Fact]
    public void CarriageReturn_OriginMode_MovesToLeftMargin()
    {
        using var t = CreateTerminal(5, 80);

        // Enable DECLRMM and set left margin, then enable origin mode
        Feed(t, "\x1b[?69h");    // DECLRMM
        Feed(t, "\x1b[3;5s");     // DECSLRM left=3 right=5 (1-based)
        Feed(t, "\x1b[?6h");      // Origin mode

        // In origin mode, cursor should be at left margin
        Feed(t, "\r");
        Assert.Equal(2, t.CursorX); // 0-based: left margin is col 2
    }

    /// <summary>
    /// Ghostty: "Terminal: carriage return left of left margin moves to zero"
    /// When cursor is left of the left margin, CR moves to column 0 (not the margin).
    /// </summary>
    [Fact]
    public void CarriageReturn_LeftOfLeftMargin_MovesToZero()
    {
        using var t = CreateTerminal(5, 80);

        // Set left margin without enabling DECLRMM via direct mode
        // Actually Ghostty sets scrolling_region.left directly — we use DECSLRM
        Feed(t, "\x1b[?69h");    // DECLRMM
        Feed(t, "\x1b[3;5s");     // DECSLRM left=3 right=5 (1-based)
        Feed(t, "\x1b[1;2H");     // Move to row 1, col 2 (left of margin, 1-based)

        Feed(t, "\r");
        Assert.Equal(0, t.CursorX); // Should go to col 0, not margin
    }

    /// <summary>
    /// Ghostty: "Terminal: carriage return right of left margin moves to left margin"
    /// When cursor is right of (or at) the left margin, CR moves to left margin.
    /// </summary>
    [Fact]
    public void CarriageReturn_RightOfLeftMargin_MovesToLeftMargin()
    {
        using var t = CreateTerminal(5, 80);

        Feed(t, "\x1b[?69h");    // DECLRMM
        Feed(t, "\x1b[3;5s");     // DECSLRM left=3 right=5 (1-based)
        Feed(t, "\x1b[1;4H");     // Move to row 1, col 4 (right of left margin, 1-based)

        Feed(t, "\r");
        Assert.Equal(2, t.CursorX); // Should go to left margin (col 2, 0-based)
    }

    #endregion

    #region Print Right Margin (DECLRMM) - Wide Char

    /// <summary>
    /// Ghostty: "Terminal: print right margin wrap"
    /// With DECLRMM, printing past right margin wraps to left margin on next row.
    /// </summary>
    [Fact]
    public void PrintRightMarginWrap_NoSoftWrap()
    {
        using var t = CreateTerminal(10, 5);

        Feed(t, "123456789"); // Fill row (only 9 of 10 cols)
        Feed(t, "\x1b[?69h");    // DECLRMM
        Feed(t, "\x1b[3;5s");     // DECSLRM left=3 right=5 (1-based)
        Feed(t, "\x1b[1;5H");     // Move to row 1, col 5 (right margin)
        Feed(t, "XY");            // X at col 5 (right margin), Y wraps to next row

        Assert.Equal("1234X6789", GetLine(t, 0));
        Assert.Equal("  Y", GetLine(t, 1));

        // Row 0 should NOT have soft wrap flag
        var cell = GetCell(t, 0, 9);
        Assert.False(cell.Attributes.HasFlag(CellAttributes.SoftWrap));
    }

    #endregion

    #region Soft Wrap

    /// <summary>
    /// Ghostty: "Terminal: soft wrap"
    /// Writing beyond the terminal width wraps to the next line.
    /// </summary>
    [Fact]
    public void SoftWrap_Basic()
    {
        using var t = CreateTerminal(3, 80);

        Feed(t, "hello"); // 5 chars in 3-col terminal
        Assert.Equal(1, t.CursorY);
        Assert.Equal(2, t.CursorX); // After 'o' on second row

        Assert.Equal("hel", GetLine(t, 0));
        Assert.Equal("lo", GetLine(t, 1));
    }

    #endregion

    #region Disabled Wraparound with Wide Char

    /// <summary>
    /// Ghostty: "Terminal: disabled wraparound with wide char and no space"
    /// With wraparound disabled, wide char at last col with existing content is not printed.
    /// </summary>
    [Fact]
    public void DisabledWraparound_WideChar_NoSpace()
    {
        using var t = CreateTerminal(5, 5);

        Feed(t, "\x1b[?7l"); // Disable wraparound
        Feed(t, "AAAAA");    // Fill all 5 cols
        Feed(t, "\U0001F6A8"); // Police car light (wide) — no space

        Assert.Equal(0, t.CursorY);
        Assert.Equal(4, t.CursorX);
        Assert.Equal("AAAAA", GetLine(t, 0));

        // Last cell should still be 'A' — wide char was not printed
        var cell = GetCell(t, 0, 4);
        Assert.Equal("A", cell.Character);
    }

    /// <summary>
    /// Ghostty: "Terminal: disabled wraparound with wide char and one space"
    /// With wraparound disabled, wide char at second-to-last col (1 space left)
    /// is not printed because it needs 2 columns.
    /// </summary>
    [Fact]
    public void DisabledWraparound_WideChar_OneSpace()
    {
        using var t = CreateTerminal(5, 5);

        Feed(t, "\x1b[?7l"); // Disable wraparound
        Feed(t, "AAAA");     // Fill 4 cols, cursor at col 4
        Feed(t, "\U0001F6A8"); // Police car light (wide) — only 1 space left

        Assert.Equal(0, t.CursorY);
        Assert.Equal(4, t.CursorX);

        // The wide char should NOT have been printed — cell should remain empty
        var cell = GetCell(t, 0, 4);
        Assert.True(string.IsNullOrWhiteSpace(cell.Character),
            $"Expected empty cell at [0,4] but got '{cell.Character}'");
    }

    /// <summary>
    /// Ghostty: "Terminal: disabled wraparound with wide grapheme and half space"
    /// With wraparound disabled and mode 2027, a VS16 widening at the edge
    /// should leave the narrow char as-is since there's no room for wide.
    /// </summary>
    [Fact]
    public void DisabledWraparound_WideGrapheme_HalfSpace()
    {
        using var t = CreateTerminal(5, 5);

        Feed(t, "\x1b[?2027h"); // Enable grapheme cluster mode
        Feed(t, "\x1b[?7l");    // Disable wraparound
        Feed(t, "AAAA");        // Fill 4 cols
        Feed(t, "\u2764");      // Heart (narrow by default)
        Feed(t, "\uFE0F");      // VS16 — try to make wide, but no room

        Assert.Equal(0, t.CursorY);
        Assert.Equal(4, t.CursorX);

        // Heart should remain narrow since VS16 widening has no room
        Assert.Equal("AAAA❤", GetLine(t, 0));
    }

    #endregion

    #region Print Very Long Line

    /// <summary>
    /// Ghostty: "Terminal: print single very long line"
    /// Printing a very long line (1000 chars) should not crash.
    /// </summary>
    [Fact]
    public void PrintSingleVeryLongLine_DoesNotCrash()
    {
        using var t = CreateTerminal(5, 5);

        // Print 1000 characters — should wrap many times without crashing
        Feed(t, new string('x', 1000));

        // If we got here without exception, the test passes
        Assert.True(true);
    }

    #endregion
}
