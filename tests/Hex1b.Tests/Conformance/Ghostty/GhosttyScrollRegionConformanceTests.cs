namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Conformance tests for scroll regions, insertLines, deleteLines, and scrollUp/scrollDown
/// derived from Ghostty's Terminal.zig.
/// </summary>
/// <remarks>
/// Source: https://github.com/ghostty-org/ghostty/blob/main/src/terminal/Terminal.zig
/// Tests exercise DECSTBM (set top/bottom margins), IL, DL, SU, SD, and RI operations.
/// </remarks>
[Trait("Category", "GhosttyConformance")]
public class GhosttyScrollRegionConformanceTests
{
    private static Hex1bTerminal CreateTerminal(int cols = 80, int rows = 24)
        => GhosttyTestFixture.CreateTerminal(cols, rows);

    private static void AssertPlainText(Hex1bTerminal terminal, string expected)
    {
        var expectedLines = expected.Split('\n');
        for (int i = 0; i < expectedLines.Length; i++)
        {
            var actualLine = GhosttyTestFixture.GetLine(terminal, i);
            Assert.Equal(expectedLines[i], actualLine);
        }

        for (int i = expectedLines.Length; i < terminal.Height; i++)
        {
            var line = GhosttyTestFixture.GetLine(terminal, i);
            Assert.Equal("", line);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DECSTBM (Set Top and Bottom Margins) + Scroll Down
    // ──────────────────────────────────────────────────────────────────────────

    #region DECSTBM + ScrollDown

    /// <summary>
    /// Ghostty: "setTopAndBottomMargin simple"
    /// 5×5 terminal. Write ABC, DEF, GHI. Set margins to full screen (0,0). ScrollDown(1).
    /// Content shifts down by 1, blank line at top.
    /// </summary>
    [Fact]
    public void SetTopAndBottomMargin_Simple_ScrollsDownFullScreen()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABC\r\nDEF\r\nGHI");
        // DECSTBM(0,0) resets margins to full screen
        GhosttyTestFixture.Feed(terminal, "\x1b[0;0r");
        // SD(1) — scroll down 1
        GhosttyTestFixture.Feed(terminal, "\x1b[1T");

        AssertPlainText(terminal, "\nABC\nDEF\nGHI");
    }

    /// <summary>
    /// Ghostty: "setTopAndBottomMargin top only"
    /// 5×5 terminal. Write ABC, DEF, GHI. Set margin top=2 (1-based), bottom=full.
    /// ScrollDown(1). Only rows 2–5 shift; row 1 stays.
    /// </summary>
    [Fact]
    public void SetTopAndBottomMargin_TopOnly_ScrollsWithinLowerRegion()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABC\r\nDEF\r\nGHI");
        // DECSTBM(2,0) — top margin at row 2, bottom = full screen
        GhosttyTestFixture.Feed(terminal, "\x1b[2;0r");
        // SD(1)
        GhosttyTestFixture.Feed(terminal, "\x1b[1T");

        AssertPlainText(terminal, "ABC\n\nDEF\nGHI");
    }

    /// <summary>
    /// Ghostty: "setTopAndBottomMargin top and bottom"
    /// 5×5 terminal. Write ABC, DEF, GHI. Set margin rows 1–2.
    /// ScrollDown(1). DEF pushed out of region, GHI untouched below.
    /// </summary>
    [Fact]
    public void SetTopAndBottomMargin_TopAndBottom_ScrollsWithinRegion()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABC\r\nDEF\r\nGHI");
        // DECSTBM(1,2) — scroll region is rows 1–2
        GhosttyTestFixture.Feed(terminal, "\x1b[1;2r");
        // SD(1)
        GhosttyTestFixture.Feed(terminal, "\x1b[1T");

        AssertPlainText(terminal, "\nABC\nGHI");
    }

    /// <summary>
    /// Ghostty: "setTopAndBottomMargin top equal to bottom"
    /// 5×5 terminal. Write ABC, DEF, GHI. Set margin(2,2) — single row region.
    /// Ghostty treats equal margins as full screen. ScrollDown(1).
    /// </summary>
    [Fact]
    public void SetTopAndBottomMargin_TopEqualsBottom_TreatedAsFullScreen()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABC\r\nDEF\r\nGHI");
        // DECSTBM(2,2) — equal margins, treated as full screen
        GhosttyTestFixture.Feed(terminal, "\x1b[2;2r");
        // SD(1)
        GhosttyTestFixture.Feed(terminal, "\x1b[1T");

        AssertPlainText(terminal, "\nABC\nDEF\nGHI");
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────────────
    // Insert Lines (IL — CSI n L)
    // ──────────────────────────────────────────────────────────────────────────

    #region Insert Lines

    /// <summary>
    /// Ghostty: "insertLines simple"
    /// 5×5 terminal. Write ABC, DEF, GHI. CUP(2,2). InsertLines(1).
    /// Blank line inserted at cursor row (row 1), content below shifts down.
    /// </summary>
    [Fact]
    public void InsertLines_Simple_InsertsBlankAndShiftsDown()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABC\r\nDEF\r\nGHI");
        // CUP(2,2) — row 2, col 2 (1-based) → cursor at (1,1) 0-based
        GhosttyTestFixture.Feed(terminal, "\x1b[2;2H");
        // IL(1)
        GhosttyTestFixture.Feed(terminal, "\x1b[1L");

        AssertPlainText(terminal, "ABC\n\nDEF\nGHI");
        // IL resets cursor column to 0
        Assert.Equal(0, terminal.CursorX);
    }

    /// <summary>
    /// Ghostty: "insertLines outside of scroll region"
    /// 5×5 terminal. Write ABC, DEF, GHI. Set margin(3,4). CUP(2,2).
    /// Cursor outside scroll region — insertLines has no effect.
    /// </summary>
    [Fact]
    public void InsertLines_OutsideScrollRegion_NoEffect()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABC\r\nDEF\r\nGHI");
        // DECSTBM(3,4)
        GhosttyTestFixture.Feed(terminal, "\x1b[3;4r");
        // CUP(2,2) — row 2 is outside region 3–4
        GhosttyTestFixture.Feed(terminal, "\x1b[2;2H");
        // IL(1)
        GhosttyTestFixture.Feed(terminal, "\x1b[1L");

        AssertPlainText(terminal, "ABC\nDEF\nGHI");
    }

    /// <summary>
    /// Ghostty: "insertLines top/bottom scroll region"
    /// 5×5 terminal. Write ABC, DEF, GHI, 123. Set margin(1,3). CUP(2,2).
    /// InsertLines(1). GHI pushed out of region (rows 1–3), 123 untouched.
    /// </summary>
    [Fact]
    public void InsertLines_WithTopBottomScrollRegion_PushesOutOfRegion()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABC\r\nDEF\r\nGHI\r\n123");
        // DECSTBM(1,3)
        GhosttyTestFixture.Feed(terminal, "\x1b[1;3r");
        // CUP(2,2)
        GhosttyTestFixture.Feed(terminal, "\x1b[2;2H");
        // IL(1)
        GhosttyTestFixture.Feed(terminal, "\x1b[1L");

        AssertPlainText(terminal, "ABC\n\nDEF\n123");
    }

    /// <summary>
    /// Ghostty: "insertLines zero"
    /// 5×5 terminal. Write ABC, DEF, GHI. CUP(2,2). InsertLines(0).
    /// Zero is treated as 1.
    /// </summary>
    [Fact]
    public void InsertLines_Zero_TreatedAsOne()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABC\r\nDEF\r\nGHI");
        // CUP(2,2)
        GhosttyTestFixture.Feed(terminal, "\x1b[2;2H");
        // IL(0) — treated as IL(1)
        GhosttyTestFixture.Feed(terminal, "\x1b[0L");

        AssertPlainText(terminal, "ABC\n\nDEF\nGHI");
    }

    /// <summary>
    /// Ghostty: "insertLines with scroll region"
    /// 5×5 terminal. Write ABC, DEF, GHI, 123, 456. Set margin(2,4). CUP(3,2).
    /// InsertLines(1). Insert within region rows 2–4, 456 untouched.
    /// </summary>
    [Fact]
    public void InsertLines_WithScrollRegion_InsertsWithinRegion()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABC\r\nDEF\r\nGHI\r\n123\r\n456");
        // DECSTBM(2,4)
        GhosttyTestFixture.Feed(terminal, "\x1b[2;4r");
        // CUP(3,2) — row 3, col 2 (1-based) → cursor at (2,1) 0-based
        GhosttyTestFixture.Feed(terminal, "\x1b[3;2H");
        // IL(1)
        GhosttyTestFixture.Feed(terminal, "\x1b[1L");

        AssertPlainText(terminal, "ABC\nDEF\n\nGHI\n456");
    }

    /// <summary>
    /// Ghostty: "insertLines more than remaining"
    /// 5×5 terminal. Write 5 rows. Set margin(2,4). CUP(3,2). InsertLines(100).
    /// All remaining rows in region blanked.
    /// </summary>
    [Fact]
    public void InsertLines_MoreThanRemaining_BlanksRemainingInRegion()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABCDE\r\nFGHIJ\r\nKLMNO\r\nPQRST\r\nUVWXY");
        // DECSTBM(2,4)
        GhosttyTestFixture.Feed(terminal, "\x1b[2;4r");
        // CUP(3,2) — row 3, col 2 (1-based)
        GhosttyTestFixture.Feed(terminal, "\x1b[3;2H");
        // IL(100) — more than rows remaining in region
        GhosttyTestFixture.Feed(terminal, "\x1b[100L");

        AssertPlainText(terminal, "ABCDE\nFGHIJ\n\n\nUVWXY");
    }

    /// <summary>
    /// Ghostty: "insertLines resets pending wrap"
    /// 5×5 terminal. Write "ABCDE" (fills line, sets pending wrap). InsertLines(1).
    /// Write 'X'. Pending wrap reset, X at col 0 of current row, ABCDE pushed down.
    /// </summary>
    [Fact]
    public void InsertLines_ResetsPendingWrap()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        // Fill line to set pending wrap
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        // IL(1) — inserts blank at row 0, pushes ABCDE to row 1
        GhosttyTestFixture.Feed(terminal, "\x1b[1L");
        // Write X — pending wrap was reset, so X goes to col 0 of current row (row 0)
        GhosttyTestFixture.Feed(terminal, "X");

        AssertPlainText(terminal, "X\nABCDE");
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────────────
    // Delete Lines (DL — CSI n M)
    // ──────────────────────────────────────────────────────────────────────────

    #region Delete Lines

    /// <summary>
    /// Ghostty: "deleteLines simple"
    /// 5×5 terminal. Write ABC, DEF, GHI. CUP(2,2). DeleteLines(1).
    /// Row 1 (DEF) deleted, rows below shift up.
    /// </summary>
    [Fact]
    public void DeleteLines_Simple_DeletesRowAndShiftsUp()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABC\r\nDEF\r\nGHI");
        // CUP(2,2)
        GhosttyTestFixture.Feed(terminal, "\x1b[2;2H");
        // DL(1)
        GhosttyTestFixture.Feed(terminal, "\x1b[1M");

        AssertPlainText(terminal, "ABC\nGHI");
        // DL resets cursor column to 0
        Assert.Equal(0, terminal.CursorX);
    }

    /// <summary>
    /// Ghostty: "deleteLines with scroll region"
    /// 5×5 terminal. Write 5 rows. Set margin(2,4). CUP(3,2). DeleteLines(1).
    /// Within region (rows 2–4), GHI deleted, 123 shifts up, blank at bottom of region.
    /// </summary>
    [Fact]
    public void DeleteLines_WithScrollRegion_DeletesWithinRegion()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABC\r\nDEF\r\nGHI\r\n123\r\n456");
        // DECSTBM(2,4)
        GhosttyTestFixture.Feed(terminal, "\x1b[2;4r");
        // CUP(3,2) — row 3, col 2 (1-based)
        GhosttyTestFixture.Feed(terminal, "\x1b[3;2H");
        // DL(1)
        GhosttyTestFixture.Feed(terminal, "\x1b[1M");

        AssertPlainText(terminal, "ABC\nDEF\n123\n\n456");
    }

    /// <summary>
    /// Ghostty: "deleteLines with scroll region, large count"
    /// 5×5 terminal. Write 5 rows. Set margin(2,4). CUP(3,2). DeleteLines(100).
    /// All rows in region below cursor deleted.
    /// </summary>
    [Fact]
    public void DeleteLines_WithScrollRegion_LargeCount_DeletesAllInRegion()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABCDE\r\nFGHIJ\r\nKLMNO\r\nPQRST\r\nUVWXY");
        // DECSTBM(2,4)
        GhosttyTestFixture.Feed(terminal, "\x1b[2;4r");
        // CUP(3,2)
        GhosttyTestFixture.Feed(terminal, "\x1b[3;2H");
        // DL(100)
        GhosttyTestFixture.Feed(terminal, "\x1b[100M");

        AssertPlainText(terminal, "ABCDE\nFGHIJ\n\n\nUVWXY");
    }

    /// <summary>
    /// Ghostty: "deleteLines with cursor outside region"
    /// 5×5 terminal. Write 3 rows. Set margin(3,4). CUP(2,2).
    /// Cursor outside scroll region — deleteLines has no effect.
    /// </summary>
    [Fact]
    public void DeleteLines_CursorOutsideRegion_NoEffect()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABC\r\nDEF\r\nGHI");
        // DECSTBM(3,4)
        GhosttyTestFixture.Feed(terminal, "\x1b[3;4r");
        // CUP(2,2) — row 2 is outside region 3–4
        GhosttyTestFixture.Feed(terminal, "\x1b[2;2H");
        // DL(1)
        GhosttyTestFixture.Feed(terminal, "\x1b[1M");

        AssertPlainText(terminal, "ABC\nDEF\nGHI");
    }

    /// <summary>
    /// Ghostty: "deleteLines resets pending wrap"
    /// 5×5 terminal. Write "ABCDE" (fills line, sets pending wrap). DeleteLines(1).
    /// Write 'X'. Row 0 deleted, X written on blank row 0.
    /// </summary>
    [Fact]
    public void DeleteLines_ResetsPendingWrap()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        // Fill line to set pending wrap
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        // DL(1) — deletes row 0
        GhosttyTestFixture.Feed(terminal, "\x1b[1M");
        // Write X — pending wrap was reset, X on blank row 0
        GhosttyTestFixture.Feed(terminal, "X");

        AssertPlainText(terminal, "X");
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────────────
    // Scroll Up (SU — CSI n S)
    // ──────────────────────────────────────────────────────────────────────────

    #region Scroll Up

    /// <summary>
    /// Ghostty: "scrollUp simple"
    /// 5×5 terminal. Write ABC, DEF, GHI. ScrollUp(1).
    /// Content shifts up by 1, top line (ABC) disappears, bottom line blank.
    /// </summary>
    [Fact]
    public void ScrollUp_Simple_ShiftsContentUp()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABC\r\nDEF\r\nGHI");
        // SU(1)
        GhosttyTestFixture.Feed(terminal, "\x1b[1S");

        AssertPlainText(terminal, "DEF\nGHI");
    }

    /// <summary>
    /// Ghostty: "scrollUp top/bottom scroll region"
    /// 5×5 terminal. Write ABC, DEF, GHI. Set margin(1,2). ScrollUp(1).
    /// Only rows 1–2 scroll. Row 0 gets DEF, row 1 blank, row 2 (GHI) untouched.
    /// </summary>
    [Fact]
    public void ScrollUp_WithScrollRegion_ScrollsWithinRegion()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABC\r\nDEF\r\nGHI");
        // DECSTBM(1,2) — scroll region is rows 1–2
        GhosttyTestFixture.Feed(terminal, "\x1b[1;2r");
        // SU(1)
        GhosttyTestFixture.Feed(terminal, "\x1b[1S");

        AssertPlainText(terminal, "DEF\n\nGHI");
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────────────
    // Scroll Down (SD — CSI n T)
    // ──────────────────────────────────────────────────────────────────────────

    #region Scroll Down

    /// <summary>
    /// Ghostty: "scrollDown simple"
    /// 5×5 terminal. Write ABC, DEF, GHI. ScrollDown(1).
    /// Content shifts down by 1, blank line at top.
    /// </summary>
    [Fact]
    public void ScrollDown_Simple_ShiftsContentDown()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABC\r\nDEF\r\nGHI");
        // SD(1)
        GhosttyTestFixture.Feed(terminal, "\x1b[1T");

        AssertPlainText(terminal, "\nABC\nDEF\nGHI");
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────────────
    // Reverse Index (RI — ESC M) with Scroll Regions
    // ──────────────────────────────────────────────────────────────────────────

    #region Reverse Index with Scroll Regions

    /// <summary>
    /// Ghostty: "reverseIndex top of scroll region"
    /// 5×5 terminal. Write ABC on rows. Set margin 2–4. CUP(2,1).
    /// Reverse index (ESC M) at top of scroll region scrolls content DOWN within region.
    /// </summary>
    [Fact]
    public void ReverseIndex_AtTopOfScrollRegion_ScrollsDownWithinRegion()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABC\r\nDEF\r\nGHI\r\n123\r\n456");
        // DECSTBM(2,4) — scroll region rows 2–4
        GhosttyTestFixture.Feed(terminal, "\x1b[2;4r");
        // CUP(2,1) — row 2, col 1 (1-based) → at top of scroll region
        GhosttyTestFixture.Feed(terminal, "\x1b[2;1H");
        // RI (ESC M) — reverse index at top of scroll region
        GhosttyTestFixture.Feed(terminal, "\x1bM");

        // DEF, GHI, 123 were in region rows 2–4. RI at top scrolls them down:
        // row 1 = blank (new), row 2 = DEF, row 3 = GHI. 123 pushed out. 456 untouched.
        AssertPlainText(terminal, "ABC\n\nDEF\nGHI\n456");
    }

    #endregion
}
