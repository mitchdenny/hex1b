using System;
using Hex1b;

namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Ghostty conformance tests — Phase 2 Tier 2 Batch 2:
/// Scroll up/down, index/reverseIndex with margins, setLeftAndRightMargin, insertLines with L/R margins.
/// </summary>
[TestCategory("GhosttyConformance")]
[TestClass]
public class GhosttyScrollMarginConformanceTests
{
    private static Hex1bTerminal CreateTerminal(int cols, int rows) => GhosttyTestFixture.CreateTerminal(cols, rows);
    private static void Feed(Hex1bTerminal t, string s) => GhosttyTestFixture.Feed(t, s);
    private static string GetLine(Hex1bTerminal t, int row) => GhosttyTestFixture.GetLine(t, row);

    #region scrollUp (CSI n S)

    // Ghostty: test "Terminal: scrollUp simple"
    [TestMethod]
    public void ScrollUp_Simple()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "ABC\r\nDEF\r\nGHI");
        Feed(terminal, "\x1b[2;2H"); // CUP(2,2) → row 1, col 1
        Feed(terminal, "\x1b[1S"); // SU(1)

        Assert.AreEqual("DEF", GetLine(terminal, 0));
        Assert.AreEqual("GHI", GetLine(terminal, 1));
        Assert.AreEqual("", GetLine(terminal, 2));

        // Cursor position should be preserved
        Assert.AreEqual(1, terminal.CursorX);
        Assert.AreEqual(1, terminal.CursorY);
    }

    // Ghostty: test "Terminal: scrollUp top/bottom scroll region"
    [TestMethod]
    public void ScrollUp_TopBottomScrollRegion()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "ABC\r\nDEF\r\nGHI");
        Feed(terminal, "\x1b[2;3r"); // DECSTBM(2,3)
        Feed(terminal, "\x1b[1;1H"); // CUP(1,1) → top
        Feed(terminal, "\x1b[1S"); // SU(1)

        Assert.AreEqual("ABC", GetLine(terminal, 0));
        Assert.AreEqual("GHI", GetLine(terminal, 1));
        Assert.AreEqual("", GetLine(terminal, 2));
    }

    // Ghostty: test "Terminal: scrollUp left/right scroll region"
    [TestMethod]
    public void ScrollUp_LeftRightScrollRegion()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 10);
        Feed(terminal, "ABC123\r\nDEF456\r\nGHI789");
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[2;4s"); // DECSLRM(2,4) → left=1, right=3
        Feed(terminal, "\x1b[2;2H"); // CUP(2,2)
        Feed(terminal, "\x1b[1S"); // SU(1)

        Assert.AreEqual("AEF423", GetLine(terminal, 0));
        Assert.AreEqual("DHI756", GetLine(terminal, 1));
        Assert.AreEqual("G   89", GetLine(terminal, 2));
    }

    // Ghostty: test "Terminal: scrollUp preserves pending wrap"
    [TestMethod]
    public void ScrollUp_PreservesPendingWrap()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "\x1b[1;5H"); // CUP(1,5)
        Feed(terminal, "A");
        Feed(terminal, "\x1b[2;5H"); // CUP(2,5)
        Feed(terminal, "B");
        Feed(terminal, "\x1b[3;5H"); // CUP(3,5)
        Feed(terminal, "C");
        Feed(terminal, "\x1b[1S"); // SU(1)
        Feed(terminal, "X"); // Should wrap to next line since pending wrap preserved

        Assert.AreEqual("    B", GetLine(terminal, 0));
        Assert.AreEqual("    C", GetLine(terminal, 1));
        Assert.AreEqual("", GetLine(terminal, 2));
        Assert.AreEqual("X", GetLine(terminal, 3));
    }

    // Ghostty: test "Terminal: scrollUp full top/bottom region"
    [TestMethod]
    public void ScrollUp_FullTopBottomRegion()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "top");
        Feed(terminal, "\x1b[5;1H"); // CUP(5,1) → bottom row
        Feed(terminal, "ABCDE");
        Feed(terminal, "\x1b[2;5r"); // DECSTBM(2,5)
        Feed(terminal, "\x1b[4S"); // SU(4) — scroll up 4 times within region

        Assert.AreEqual("top", GetLine(terminal, 0));
        Assert.AreEqual("", GetLine(terminal, 1));
        Assert.AreEqual("", GetLine(terminal, 2));
    }

    // Ghostty: test "Terminal: scrollUp full top/bottomleft/right scroll region"
    [TestMethod]
    public void ScrollUp_FullTopBottomLeftRightRegion()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "top");
        Feed(terminal, "\x1b[5;1H"); // CUP(5,1)
        Feed(terminal, "ABCDE");
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[2;5r"); // DECSTBM(2,5)
        Feed(terminal, "\x1b[2;4s"); // DECSLRM(2,4)
        Feed(terminal, "\x1b[4S"); // SU(4)

        Assert.AreEqual("top", GetLine(terminal, 0));
        Assert.AreEqual("", GetLine(terminal, 1));
        Assert.AreEqual("", GetLine(terminal, 2));
        Assert.AreEqual("", GetLine(terminal, 3));
        Assert.AreEqual("A   E", GetLine(terminal, 4));
    }

    #endregion

    #region scrollDown (CSI n T)

    // Ghostty: test "Terminal: scrollDown simple"
    [TestMethod]
    public void ScrollDown_Simple()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "ABC\r\nDEF\r\nGHI");
        Feed(terminal, "\x1b[2;2H"); // CUP(2,2)
        Feed(terminal, "\x1b[1T"); // SD(1)

        Assert.AreEqual("", GetLine(terminal, 0));
        Assert.AreEqual("ABC", GetLine(terminal, 1));
        Assert.AreEqual("DEF", GetLine(terminal, 2));
        Assert.AreEqual("GHI", GetLine(terminal, 3));

        // Cursor preserved
        Assert.AreEqual(1, terminal.CursorX);
        Assert.AreEqual(1, terminal.CursorY);
    }

    // Ghostty: test "Terminal: scrollDown outside of scroll region"
    [TestMethod]
    public void ScrollDown_OutsideScrollRegion()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "ABC\r\nDEF\r\nGHI");
        Feed(terminal, "\x1b[3;4r"); // DECSTBM(3,4)
        Feed(terminal, "\x1b[2;2H"); // CUP(2,2) → inside margin
        Feed(terminal, "\x1b[1T"); // SD(1)

        Assert.AreEqual("ABC", GetLine(terminal, 0));
        Assert.AreEqual("DEF", GetLine(terminal, 1));
        Assert.AreEqual("", GetLine(terminal, 2));
        Assert.AreEqual("GHI", GetLine(terminal, 3));
    }

    // Ghostty: test "Terminal: scrollDown left/right scroll region"
    [TestMethod]
    public void ScrollDown_LeftRightScrollRegion()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 10);
        Feed(terminal, "ABC123\r\nDEF456\r\nGHI789");
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[2;4s"); // DECSLRM(2,4) → left=1, right=3
        Feed(terminal, "\x1b[2;2H"); // CUP(2,2)
        Feed(terminal, "\x1b[1T"); // SD(1)

        Assert.AreEqual("A   23", GetLine(terminal, 0));
        Assert.AreEqual("DBC156", GetLine(terminal, 1));
        Assert.AreEqual("GEF489", GetLine(terminal, 2));
        Assert.AreEqual(" HI7", GetLine(terminal, 3));
    }

    // Ghostty: test "Terminal: scrollDown outside of left/right scroll region"
    [TestMethod]
    public void ScrollDown_OutsideLeftRightScrollRegion()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 10);
        Feed(terminal, "ABC123\r\nDEF456\r\nGHI789");
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[2;4s"); // DECSLRM(2,4) → left=1, right=3
        Feed(terminal, "\x1b[1;1H"); // CUP(1,1) — outside L/R margin
        Feed(terminal, "\x1b[1T"); // SD(1)

        // Ghostty: same as inside since scrollDown affects the region regardless of cursor column
        Assert.AreEqual("A   23", GetLine(terminal, 0));
        Assert.AreEqual("DBC156", GetLine(terminal, 1));
        Assert.AreEqual("GEF489", GetLine(terminal, 2));
        Assert.AreEqual(" HI7", GetLine(terminal, 3));
    }

    // Ghostty: test "Terminal: scrollDown preserves pending wrap"
    [TestMethod]
    public void ScrollDown_PreservesPendingWrap()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 10);
        Feed(terminal, "\x1b[1;5H"); // CUP(1,5)
        Feed(terminal, "A");
        Feed(terminal, "\x1b[2;5H"); // CUP(2,5)
        Feed(terminal, "B");
        Feed(terminal, "\x1b[3;5H"); // CUP(3,5)
        Feed(terminal, "C");
        Feed(terminal, "\x1b[1T"); // SD(1)
        Feed(terminal, "X"); // Should wrap due to preserved pending wrap

        Assert.AreEqual("", GetLine(terminal, 0));
        Assert.AreEqual("    A", GetLine(terminal, 1));
        Assert.AreEqual("    B", GetLine(terminal, 2));
        Assert.AreEqual("X   C", GetLine(terminal, 3));
    }

    #endregion

    #region setLeftAndRightMargin (DECSLRM)

    // Ghostty: test "Terminal: setLeftAndRightMargin simple"
    [TestMethod]
    public void SetLeftAndRightMargin_Simple()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "ABC\r\nDEF\r\nGHI");
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[0;0s"); // DECSLRM(0,0) — reset to full width
        Feed(terminal, "\x1b[1;1H"); // CUP(1,1)
        Feed(terminal, "\x1b[1X"); // ECH(1)

        Assert.AreEqual(" BC", GetLine(terminal, 0));
        Assert.AreEqual("DEF", GetLine(terminal, 1));
    }

    // Ghostty: test "Terminal: setLeftAndRightMargin left only"
    [TestMethod]
    public void SetLeftAndRightMargin_LeftOnly()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "ABC\r\nDEF\r\nGHI");
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[2;0s"); // DECSLRM(2,0) — left=1(0-based), right=full
        Feed(terminal, "\x1b[1;2H"); // CUP(1,2) → col 1
        Feed(terminal, "\x1b[1L"); // IL(1)

        Assert.AreEqual("A", GetLine(terminal, 0));
        Assert.AreEqual("DBC", GetLine(terminal, 1));
        Assert.AreEqual("GEF", GetLine(terminal, 2));
        Assert.AreEqual(" HI", GetLine(terminal, 3));
    }

    // Ghostty: test "Terminal: setLeftAndRightMargin left and right"
    [TestMethod]
    public void SetLeftAndRightMargin_LeftAndRight()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "ABC\r\nDEF\r\nGHI");
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[1;2s"); // DECSLRM(1,2) — left=0, right=1
        Feed(terminal, "\x1b[1;2H"); // CUP(1,2) → col 1
        Feed(terminal, "\x1b[1L"); // IL(1)

        Assert.AreEqual("  C", GetLine(terminal, 0));
        Assert.AreEqual("ABF", GetLine(terminal, 1));
        Assert.AreEqual("DEI", GetLine(terminal, 2));
        Assert.AreEqual("GH", GetLine(terminal, 3));
    }

    // Ghostty: test "Terminal: setLeftAndRightMargin left equal right"
    [TestMethod]
    public void SetLeftAndRightMargin_LeftEqualRight()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "ABC\r\nDEF\r\nGHI");
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[2;2s"); // DECSLRM(2,2) — left=right=1
        Feed(terminal, "\x1b[1;2H"); // CUP(1,2)
        Feed(terminal, "\x1b[1L"); // IL(1)

        Assert.AreEqual("", GetLine(terminal, 0));
        Assert.AreEqual("ABC", GetLine(terminal, 1));
        Assert.AreEqual("DEF", GetLine(terminal, 2));
        Assert.AreEqual("GHI", GetLine(terminal, 3));
    }

    // Ghostty: test "Terminal: setLeftAndRightMargin mode 69 unset"
    [TestMethod]
    public void SetLeftAndRightMargin_Mode69Unset()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "ABC\r\nDEF\r\nGHI");
        // Mode 69 NOT enabled — DECSLRM should be ignored
        Feed(terminal, "\x1b[1;2s"); // DECSLRM(1,2) — should be no-op
        Feed(terminal, "\x1b[1;2H"); // CUP(1,2) → col 1
        Feed(terminal, "\x1b[1L"); // IL(1) — should use full width

        Assert.AreEqual("", GetLine(terminal, 0));
        Assert.AreEqual("ABC", GetLine(terminal, 1));
        Assert.AreEqual("DEF", GetLine(terminal, 2));
        Assert.AreEqual("GHI", GetLine(terminal, 3));
    }

    #endregion

    #region insertLines with L/R scroll region

    // Ghostty: test "Terminal: insertLines left/right scroll region"
    [TestMethod]
    public void InsertLines_LeftRightScrollRegion()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 10);
        Feed(terminal, "ABC123\r\nDEF456\r\nGHI789");
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[2;4s"); // DECSLRM(2,4) → left=1, right=3
        Feed(terminal, "\x1b[2;2H"); // CUP(2,2)
        Feed(terminal, "\x1b[1L"); // IL(1)

        Assert.AreEqual("ABC123", GetLine(terminal, 0));
        Assert.AreEqual("D   56", GetLine(terminal, 1));
        Assert.AreEqual("GEF489", GetLine(terminal, 2));
        Assert.AreEqual(" HI7", GetLine(terminal, 3));
    }

    #endregion

    #region index (ESC D / IND)

    // Ghostty: test "Terminal: index no scroll region, top of screen"
    [TestMethod]
    public void Index_NoScrollRegion_TopOfScreen()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "A");
        Feed(terminal, "\u001bD"); // IND
        Feed(terminal, "X");

        Assert.AreEqual("A", GetLine(terminal, 0));
        Assert.AreEqual(" X", GetLine(terminal, 1));
    }

    // Ghostty: test "Terminal: index bottom of primary screen"
    [TestMethod]
    public void Index_BottomOfPrimaryScreen()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "\x1b[5;1H"); // CUP(5,1) → bottom row
        Feed(terminal, "A");
        Feed(terminal, "\u001bD"); // IND — scrolls
        Feed(terminal, "X");

        Assert.AreEqual("", GetLine(terminal, 0));
        Assert.AreEqual("", GetLine(terminal, 1));
        Assert.AreEqual("", GetLine(terminal, 2));
        Assert.AreEqual("A", GetLine(terminal, 3));
        Assert.AreEqual(" X", GetLine(terminal, 4));
    }

    // Ghostty: test "Terminal: index inside scroll region"
    [TestMethod]
    public void Index_InsideScrollRegion()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "\x1b[1;3r"); // DECSTBM(1,3)
        Feed(terminal, "A");
        Feed(terminal, "\u001bD"); // IND
        Feed(terminal, "X");

        Assert.AreEqual("A", GetLine(terminal, 0));
        Assert.AreEqual(" X", GetLine(terminal, 1));
    }

    // Ghostty: test "Terminal: index outside of scrolling region"
    [TestMethod]
    public void Index_OutsideOfScrollingRegion()
    {
        using var terminal = CreateTerminal(cols: 2, rows: 5);
        Feed(terminal, "\x1b[2;5r"); // DECSTBM(2,5) — cursor at row 0 is outside
        Feed(terminal, "\u001bD"); // IND — should just move down (outside region)

        Assert.AreEqual(0, terminal.CursorX);
        Assert.AreEqual(1, terminal.CursorY);
    }

    // Ghostty: test "Terminal: index from the bottom outside of scroll region"
    [TestMethod]
    public void Index_FromBottomOutsideScrollRegion()
    {
        using var terminal = CreateTerminal(cols: 2, rows: 5);
        Feed(terminal, "\x1b[1;2r"); // DECSTBM(1,2)
        Feed(terminal, "\x1b[5;1H"); // CUP(5,1) → bottom row (outside region)
        Feed(terminal, "A");
        Feed(terminal, "\u001bD"); // IND — at bottom, outside scroll region, no scroll
        Feed(terminal, "B");

        Assert.AreEqual("", GetLine(terminal, 0));
        Assert.AreEqual("", GetLine(terminal, 1));
        Assert.AreEqual("", GetLine(terminal, 2));
        Assert.AreEqual("", GetLine(terminal, 3));
        Assert.AreEqual("AB", GetLine(terminal, 4));
    }

    // Ghostty: test "Terminal: index bottom of primary screen with scroll region"
    [TestMethod]
    public void Index_BottomWithScrollRegion()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "\x1b[1;3r"); // DECSTBM(1,3)
        Feed(terminal, "1\r\n2\r\n3");
        Feed(terminal, "\x1b[4;1H"); // CUP(4,1) → row 3
        Feed(terminal, "X");
        Feed(terminal, "\x1b[3;1H"); // CUP(3,1)
        Feed(terminal, "\u001bD"); // IND — at bottom of scroll region, scrolls within region

        Assert.AreEqual("2", GetLine(terminal, 0));
        Assert.AreEqual("3", GetLine(terminal, 1));
        Assert.AreEqual("", GetLine(terminal, 2));
        Assert.AreEqual("X", GetLine(terminal, 3));
    }

    // Ghostty: test "Terminal: index outside left/right margin"
    [TestMethod]
    public void Index_OutsideLeftRightMargin()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        Feed(terminal, "\x1b[1;3r"); // DECSTBM(1,3)
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[4;6s"); // DECSLRM(4,6) → left=3, right=5 (0-based)
        Feed(terminal, "\x1b[3;3H"); // CUP(3,3) → row 2, col 2
        Feed(terminal, "A");
        Feed(terminal, "\x1b[3;1H"); // CUP(3,1) → row 2, col 0 (outside L/R margin)
        Feed(terminal, "\u001bD"); // IND — at bottom of scroll region but outside L/R margin
        Feed(terminal, "X");

        // Cursor stays at row 2 (no scroll since outside L/R), X printed at row 2 col 0
        Assert.AreEqual("", GetLine(terminal, 0));
        Assert.AreEqual("", GetLine(terminal, 1));
        Assert.AreEqual("X A", GetLine(terminal, 2));
    }

    // Ghostty: test "Terminal: index inside left/right margin"
    [TestMethod]
    public void Index_InsideLeftRightMargin()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        Feed(terminal, "AAAAAA\r\nAAAAAA\r\nAAAAAA");
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[1;3r"); // DECSTBM(1,3)
        Feed(terminal, "\x1b[1;3s"); // DECSLRM(1,3) — left=0, right=2
        Feed(terminal, "\x1b[3;1H"); // CUP(3,1) → bottom of scroll region, inside margin
        Feed(terminal, "\u001bD"); // IND — scrolls within L/R margin region

        Assert.AreEqual(2, terminal.CursorY);
        Assert.AreEqual(0, terminal.CursorX);

        Assert.AreEqual("AAAAAA", GetLine(terminal, 0));
        Assert.AreEqual("AAAAAA", GetLine(terminal, 1));
        Assert.AreEqual("   AAA", GetLine(terminal, 2));
    }

    #endregion

    #region reverseIndex (ESC M / RI)

    // Ghostty: test "Terminal: reverseIndex top of scrolling region"
    [TestMethod]
    public void ReverseIndex_TopOfScrollingRegion()
    {
        using var terminal = CreateTerminal(cols: 2, rows: 10);
        Feed(terminal, "\x1b[2;1H"); // CUP(2,1)
        Feed(terminal, "A\r\nB\r\nC\r\n" + "D");
        Feed(terminal, "\x1b[2;5r"); // DECSTBM(2,5)
        Feed(terminal, "\x1b[2;1H"); // CUP(2,1) → top of region
        Feed(terminal, "\x1bM"); // RI — reverse index at top of scroll region → scroll down
        Feed(terminal, "X");

        Assert.AreEqual("", GetLine(terminal, 0));
        Assert.AreEqual("X", GetLine(terminal, 1));
        Assert.AreEqual("A", GetLine(terminal, 2));
        Assert.AreEqual("B", GetLine(terminal, 3));
        Assert.AreEqual("C", GetLine(terminal, 4));
    }

    // Ghostty: test "Terminal: reverseIndex top of screen"
    [TestMethod]
    public void ReverseIndex_TopOfScreen()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "A");
        Feed(terminal, "\x1b[2;1H"); // CUP(2,1)
        Feed(terminal, "B");
        Feed(terminal, "\x1b[3;1H"); // CUP(3,1)
        Feed(terminal, "C");
        Feed(terminal, "\x1b[1;1H"); // CUP(1,1)
        Feed(terminal, "\x1bM"); // RI — at row 0, scrolls down
        Feed(terminal, "X");

        Assert.AreEqual("X", GetLine(terminal, 0));
        Assert.AreEqual("A", GetLine(terminal, 1));
        Assert.AreEqual("B", GetLine(terminal, 2));
        Assert.AreEqual("C", GetLine(terminal, 3));
    }

    // Ghostty: test "Terminal: reverseIndex not top of screen"
    [TestMethod]
    public void ReverseIndex_NotTopOfScreen()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "A");
        Feed(terminal, "\x1b[2;1H"); // CUP(2,1)
        Feed(terminal, "B");
        Feed(terminal, "\x1b[3;1H"); // CUP(3,1)
        Feed(terminal, "C");
        Feed(terminal, "\x1b[2;1H"); // CUP(2,1)
        Feed(terminal, "\x1bM"); // RI — not at top, just moves up
        Feed(terminal, "X");

        Assert.AreEqual("X", GetLine(terminal, 0));
        Assert.AreEqual("B", GetLine(terminal, 1));
        Assert.AreEqual("C", GetLine(terminal, 2));
    }

    // Ghostty: test "Terminal: reverseIndex top/bottom margins"
    [TestMethod]
    public void ReverseIndex_TopBottomMargins()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "A");
        Feed(terminal, "\x1b[2;1H"); // CUP(2,1)
        Feed(terminal, "B");
        Feed(terminal, "\x1b[3;1H"); // CUP(3,1)
        Feed(terminal, "C");
        Feed(terminal, "\x1b[2;3r"); // DECSTBM(2,3)
        Feed(terminal, "\x1b[2;1H"); // CUP(2,1) → top of margin
        Feed(terminal, "\x1bM"); // RI — at top of margin, scrolls within margin

        Assert.AreEqual("A", GetLine(terminal, 0));
        Assert.AreEqual("", GetLine(terminal, 1));
        Assert.AreEqual("B", GetLine(terminal, 2));
    }

    // Ghostty: test "Terminal: reverseIndex outside top/bottom margins"
    [TestMethod]
    public void ReverseIndex_OutsideTopBottomMargins()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "A");
        Feed(terminal, "\x1b[2;1H"); // CUP(2,1)
        Feed(terminal, "B");
        Feed(terminal, "\x1b[3;1H"); // CUP(3,1)
        Feed(terminal, "C");
        Feed(terminal, "\x1b[2;3r"); // DECSTBM(2,3)
        Feed(terminal, "\x1b[1;1H"); // CUP(1,1) → above margin
        Feed(terminal, "\x1bM"); // RI — outside margin, no scroll, just clamp

        Assert.AreEqual("A", GetLine(terminal, 0));
        Assert.AreEqual("B", GetLine(terminal, 1));
        Assert.AreEqual("C", GetLine(terminal, 2));
    }

    // Ghostty: test "Terminal: reverseIndex left/right margins"
    [TestMethod]
    public void ReverseIndex_LeftRightMargins()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "ABC");
        Feed(terminal, "\x1b[2;1H"); // CUP(2,1)
        Feed(terminal, "DEF");
        Feed(terminal, "\x1b[3;1H"); // CUP(3,1)
        Feed(terminal, "GHI");
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[2;3s"); // DECSLRM(2,3) → left=1, right=2
        Feed(terminal, "\x1b[1;2H"); // CUP(1,2) → inside margin, at top
        Feed(terminal, "\x1bM"); // RI — at top with L/R margins

        Assert.AreEqual("A", GetLine(terminal, 0));
        Assert.AreEqual("DBC", GetLine(terminal, 1));
        Assert.AreEqual("GEF", GetLine(terminal, 2));
        Assert.AreEqual(" HI", GetLine(terminal, 3));
    }

    // Ghostty: test "Terminal: reverseIndex outside left/right margins"
    [TestMethod]
    public void ReverseIndex_OutsideLeftRightMargins()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "ABC");
        Feed(terminal, "\x1b[2;1H"); // CUP(2,1)
        Feed(terminal, "DEF");
        Feed(terminal, "\x1b[3;1H"); // CUP(3,1)
        Feed(terminal, "GHI");
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[2;3s"); // DECSLRM(2,3) → left=1, right=2
        Feed(terminal, "\x1b[1;1H"); // CUP(1,1) → outside left margin
        Feed(terminal, "\x1bM"); // RI — outside L/R margin, no scroll

        Assert.AreEqual("ABC", GetLine(terminal, 0));
        Assert.AreEqual("DEF", GetLine(terminal, 1));
        Assert.AreEqual("GHI", GetLine(terminal, 2));
    }

    // Ghostty: test "Terminal: index bottom of scroll region no scrollback"
    [TestMethod]
    public void Index_BottomOfScrollRegion_NoScrollback()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "\x1b[1;3r"); // DECSTBM(1,3)
        Feed(terminal, "\x1b[4;1H"); // CUP(4,1)
        Feed(terminal, "B");
        Feed(terminal, "\x1b[3;1H"); // CUP(3,1)
        Feed(terminal, "A");
        Feed(terminal, "\u001bD"); // IND — at bottom of scroll region
        Feed(terminal, "X");

        Assert.AreEqual("", GetLine(terminal, 0));
        Assert.AreEqual("A", GetLine(terminal, 1));
        Assert.AreEqual(" X", GetLine(terminal, 2));
        Assert.AreEqual("B", GetLine(terminal, 3));
    }

    #endregion
}
