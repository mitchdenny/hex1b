
namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Conformance tests for deleteLines (DL / CSI M) and insertLines (IL / CSI L)
/// focusing on left/right margins and wide character handling,
/// translated from Ghostty's Terminal.zig.
/// </summary>
[TestCategory("GhosttyConformance")]
[TestClass]
public class GhosttyDeleteInsertLinesMarginConformanceTests
{
    private static Hex1bTerminal CreateTerminal(int cols = 80, int rows = 24)
        => GhosttyTestFixture.CreateTerminal(cols, rows);

    #region DeleteLines – wrap and color

    // Ghostty: "Terminal: deleteLines resets wrap"
    [TestMethod]
    public void DeleteLines_ResetsWrap()
    {
        using var t = CreateTerminal(cols: 3, rows: 3);
        GhosttyTestFixture.Feed(t, "1\r\nABCDEF"); // "1" on row 0, "ABC" wraps to row 1, "DEF" on row 2
        GhosttyTestFixture.Feed(t, "\u001b[1;2r");  // setTopAndBottomMargin(1, 2)
        GhosttyTestFixture.Feed(t, "\u001b[1;1H");  // setCursorPos(1, 1)
        GhosttyTestFixture.Feed(t, "\u001b[1M");     // deleteLines(1)
        GhosttyTestFixture.Feed(t, "X");

        Assert.AreEqual("XBC", GhosttyTestFixture.GetLine(t, 0));
        Assert.AreEqual("", GhosttyTestFixture.GetLine(t, 1));
        Assert.AreEqual("DEF", GhosttyTestFixture.GetLine(t, 2));
    }

    // Ghostty: "Terminal: deleteLines colors with bg color"
    [TestMethod]
    public void DeleteLines_ColorsWithBgColor()
    {
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI");
        GhosttyTestFixture.Feed(t, "\u001b[2;2H");        // setCursorPos(2, 2)
        GhosttyTestFixture.Feed(t, "\u001b[48;2;255;0;0m"); // set bg red
        GhosttyTestFixture.Feed(t, "\u001b[1M");            // deleteLines(1)

        Assert.AreEqual("ABC", GhosttyTestFixture.GetLine(t, 0));
        Assert.AreEqual("GHI", GhosttyTestFixture.GetLine(t, 1));
        Assert.AreEqual("", GhosttyTestFixture.GetLine(t, 2));

        // The newly blanked bottom row (row 4) should have the red bg color
        for (int x = 0; x < 5; x++)
        {
            var cell = GhosttyTestFixture.GetCell(t, 4, x);
            Assert.IsNotNull(cell.Background);
            Assert.AreEqual(255, cell.Background!.Value.R);
            Assert.AreEqual(0, cell.Background!.Value.G);
            Assert.AreEqual(0, cell.Background!.Value.B);
        }
    }

    #endregion

    #region DeleteLines – left/right scroll region

    // Ghostty: "Terminal: deleteLines left/right scroll region"
    [TestMethod]
    public void DeleteLines_LeftRightScrollRegion()
    {
        using var t = CreateTerminal(cols: 10, rows: 10);
        GhosttyTestFixture.Feed(t, "ABC123\r\nDEF456\r\nGHI789");
        GhosttyTestFixture.Feed(t, "\u001b[?69h");  // enable left/right margin mode
        GhosttyTestFixture.Feed(t, "\u001b[2;4s");   // setLeftAndRightMargin(2, 4) — cols 1-3 (0-based)
        GhosttyTestFixture.Feed(t, "\u001b[2;2H");   // setCursorPos(2, 2)
        GhosttyTestFixture.Feed(t, "\u001b[1M");      // deleteLines(1)

        Assert.AreEqual("ABC123", GhosttyTestFixture.GetLine(t, 0));
        Assert.AreEqual("DHI756", GhosttyTestFixture.GetLine(t, 1));
        Assert.AreEqual("G   89", GhosttyTestFixture.GetLine(t, 2));
    }

    // Ghostty: "Terminal: deleteLines left/right scroll region from top"
    [TestMethod]
    public void DeleteLines_LeftRightScrollRegion_FromTop()
    {
        using var t = CreateTerminal(cols: 10, rows: 10);
        GhosttyTestFixture.Feed(t, "ABC123\r\nDEF456\r\nGHI789");
        GhosttyTestFixture.Feed(t, "\u001b[?69h");  // enable left/right margin mode
        GhosttyTestFixture.Feed(t, "\u001b[2;4s");   // setLeftAndRightMargin(2, 4)
        GhosttyTestFixture.Feed(t, "\u001b[1;2H");   // setCursorPos(1, 2)
        GhosttyTestFixture.Feed(t, "\u001b[1M");      // deleteLines(1)

        Assert.AreEqual("AEF423", GhosttyTestFixture.GetLine(t, 0));
        Assert.AreEqual("DHI756", GhosttyTestFixture.GetLine(t, 1));
        Assert.AreEqual("G   89", GhosttyTestFixture.GetLine(t, 2));
    }

    // Ghostty: "Terminal: deleteLines left/right scroll region high count"
    [TestMethod]
    public void DeleteLines_LeftRightScrollRegion_HighCount()
    {
        using var t = CreateTerminal(cols: 10, rows: 10);
        GhosttyTestFixture.Feed(t, "ABC123\r\nDEF456\r\nGHI789");
        GhosttyTestFixture.Feed(t, "\u001b[?69h");  // enable left/right margin mode
        GhosttyTestFixture.Feed(t, "\u001b[2;4s");   // setLeftAndRightMargin(2, 4)
        GhosttyTestFixture.Feed(t, "\u001b[2;2H");   // setCursorPos(2, 2)
        GhosttyTestFixture.Feed(t, "\u001b[100M");    // deleteLines(100)

        Assert.AreEqual("ABC123", GhosttyTestFixture.GetLine(t, 0));
        Assert.AreEqual("D   56", GhosttyTestFixture.GetLine(t, 1));
        Assert.AreEqual("G   89", GhosttyTestFixture.GetLine(t, 2));
    }

    #endregion

    #region DeleteLines – wide character spacer head

    // Ghostty: "Terminal: deleteLines wide character spacer head"
    [TestMethod]
    public void DeleteLines_WideCharSpacerHead()
    {
        using var t = CreateTerminal(cols: 5, rows: 3);
        // "AAAAABBBB😀CCC" on a 5-col terminal:
        //   Row 0: AAAAA (wrapped)
        //   Row 1: BBBB* (wrapped, * = spacer head for wide char)
        //   Row 2: 😀CCC
        GhosttyTestFixture.Feed(t, "AAAAABBBB\U0001F600CCC");
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // setCursorPos(1, 1)
        GhosttyTestFixture.Feed(t, "\u001b[1M");    // deleteLines(1)

        // Spacer head should become empty cell, wrap unset
        Assert.AreEqual("BBBB", GhosttyTestFixture.GetLine(t, 0));
        Assert.AreEqual("\U0001F600CCC", GhosttyTestFixture.GetLine(t, 1));
    }

    // Ghostty: "Terminal: deleteLines wide character spacer head left scroll margin"
    [TestMethod]
    public void DeleteLines_WideCharSpacerHead_LeftScrollMargin()
    {
        using var t = CreateTerminal(cols: 5, rows: 3);
        GhosttyTestFixture.Feed(t, "AAAAABBBB\U0001F600CCC");
        GhosttyTestFixture.Feed(t, "\u001b[?69h");  // enable left/right margin mode
        GhosttyTestFixture.Feed(t, "\u001b[3;5s");   // scrolling_region.left=2 (0-based) → left=3,right=5 (1-based)
        GhosttyTestFixture.Feed(t, "\u001b[1;3H");   // setCursorPos(1, 3)
        GhosttyTestFixture.Feed(t, "\u001b[1M");      // deleteLines(1)

        Assert.AreEqual("AABB", GhosttyTestFixture.GetLine(t, 0));
        Assert.AreEqual("BBCCC", GhosttyTestFixture.GetLine(t, 1));
        Assert.AreEqual("\U0001F600", GhosttyTestFixture.GetLine(t, 2));
    }

    // Ghostty: "Terminal: deleteLines wide character spacer head right scroll margin"
    [TestMethod]
    public void DeleteLines_WideCharSpacerHead_RightScrollMargin()
    {
        using var t = CreateTerminal(cols: 5, rows: 3);
        GhosttyTestFixture.Feed(t, "AAAAABBBB\U0001F600CCC");
        GhosttyTestFixture.Feed(t, "\u001b[?69h");  // enable left/right margin mode
        GhosttyTestFixture.Feed(t, "\u001b[1;4s");   // scrolling_region.right=3 (0-based) → left=1,right=4 (1-based)
        GhosttyTestFixture.Feed(t, "\u001b[1;1H");   // setCursorPos(1, 1)
        GhosttyTestFixture.Feed(t, "\u001b[1M");      // deleteLines(1)

        Assert.AreEqual("BBBBA", GhosttyTestFixture.GetLine(t, 0));
        Assert.AreEqual("\U0001F600CC", GhosttyTestFixture.GetLine(t, 1));
        Assert.AreEqual("    C", GhosttyTestFixture.GetLine(t, 2));
    }

    // Ghostty: "Terminal: deleteLines wide character spacer head left and right scroll margin"
    [TestMethod]
    public void DeleteLines_WideCharSpacerHead_LeftAndRightScrollMargin()
    {
        using var t = CreateTerminal(cols: 5, rows: 3);
        GhosttyTestFixture.Feed(t, "AAAAABBBB\U0001F600CCC");
        GhosttyTestFixture.Feed(t, "\u001b[?69h");  // enable left/right margin mode
        GhosttyTestFixture.Feed(t, "\u001b[3;4s");   // left=2,right=3 (0-based) → left=3,right=4 (1-based)
        GhosttyTestFixture.Feed(t, "\u001b[1;3H");   // setCursorPos(1, 3)
        GhosttyTestFixture.Feed(t, "\u001b[1M");      // deleteLines(1)

        Assert.AreEqual("AABBA", GhosttyTestFixture.GetLine(t, 0));
        Assert.AreEqual("BBCC", GhosttyTestFixture.GetLine(t, 1));
        Assert.AreEqual("\U0001F600  C", GhosttyTestFixture.GetLine(t, 2));
    }

    // Ghostty: "Terminal: deleteLines wide character spacer head left (< 2) and right scroll margin"
    [TestMethod]
    [TestCategory("FailureReason:Bug")]
    public void DeleteLines_WideCharSpacerHead_LeftLessThan2_AndRightScrollMargin()
    {
        using var t = CreateTerminal(cols: 5, rows: 3);
        GhosttyTestFixture.Feed(t, "AAAAABBBB\U0001F600CCC");
        GhosttyTestFixture.Feed(t, "\u001b[?69h");  // enable left/right margin mode
        GhosttyTestFixture.Feed(t, "\u001b[2;4s");   // left=1,right=3 (0-based) → left=2,right=4 (1-based)
        GhosttyTestFixture.Feed(t, "\u001b[1;2H");   // setCursorPos(1, 2)
        GhosttyTestFixture.Feed(t, "\u001b[1M");      // deleteLines(1)

        // Wide char at boundary is split and removed
        Assert.AreEqual("ABBBA", GhosttyTestFixture.GetLine(t, 0));
        Assert.AreEqual("B CC", GhosttyTestFixture.GetLine(t, 1));
        Assert.AreEqual("    C", GhosttyTestFixture.GetLine(t, 2));
    }

    // Ghostty: "Terminal: deleteLines wide characters split by left/right scroll region boundaries"
    [TestMethod]
    [TestCategory("FailureReason:Bug")]
    public void DeleteLines_WideCharsSplitByScrollRegionBoundaries()
    {
        using var t = CreateTerminal(cols: 5, rows: 2);
        // Set up content carefully to avoid unwanted scrolling:
        // Row 0: AAAAA
        GhosttyTestFixture.Feed(t, "AAAAA");
        // Row 1: 😀B😀 (wide chars at cols 0-1 and 3-4)
        GhosttyTestFixture.Feed(t, "\u001b[2;1H");  // CUP to row 1, col 0
        GhosttyTestFixture.Feed(t, "\U0001F600B\U0001F600");
        
        GhosttyTestFixture.Feed(t, "\u001b[?69h");  // enable left/right margin mode
        GhosttyTestFixture.Feed(t, "\u001b[2;4s");   // left=1,right=3 (0-based)
        GhosttyTestFixture.Feed(t, "\u001b[1;2H");   // setCursorPos(1, 2)
        GhosttyTestFixture.Feed(t, "\u001b[1M");      // deleteLines(1)

        // Wide chars split by scroll region edges are removed
        Assert.AreEqual("A B A", GhosttyTestFixture.GetLine(t, 0));
    }

    // Ghostty: "Terminal: deleteLines wide char at right margin with full clear"
    [TestMethod]
    public void DeleteLines_WideCharAtRightMarginWithFullClear()
    {
        using var t = CreateTerminal(cols: 80, rows: 24);
        GhosttyTestFixture.Feed(t, "\u001b[10;39H");       // setCursorPos(10, 39)
        GhosttyTestFixture.Feed(t, "\u4E2D");               // print '中' (wide char at col 38-39, 0-based)
        GhosttyTestFixture.Feed(t, "\u001b[?69h");          // enable left/right margin mode
        GhosttyTestFixture.Feed(t, "\u001b[5;39s");          // setLeftAndRightMargin(5, 39)
        GhosttyTestFixture.Feed(t, "\u001b[24S");            // scrollUp(24) — triggers full clear path

        // Should not crash; the orphaned spacer tail at col 39 must be handled
    }

    #endregion

    #region InsertLines – wrap, color, margins, graphemes

    // Ghostty: "Terminal: insertLines resets wrap"
    [TestMethod]
    public void InsertLines_ResetsWrap()
    {
        using var t = CreateTerminal(cols: 3, rows: 3);
        GhosttyTestFixture.Feed(t, "1\r\nABCDEF"); // "1" on row 0, "ABC" wraps to row 1, "DEF" on row 2
        GhosttyTestFixture.Feed(t, "\u001b[1;1H");  // setCursorPos(1, 1)
        GhosttyTestFixture.Feed(t, "\u001b[1L");     // insertLines(1)
        GhosttyTestFixture.Feed(t, "X");

        Assert.AreEqual("X", GhosttyTestFixture.GetLine(t, 0));
        Assert.AreEqual("1", GhosttyTestFixture.GetLine(t, 1));
        Assert.AreEqual("ABC", GhosttyTestFixture.GetLine(t, 2));
    }

    // Ghostty: "Terminal: insertLines colors with bg color"
    [TestMethod]
    public void InsertLines_ColorsWithBgColor()
    {
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI");
        GhosttyTestFixture.Feed(t, "\u001b[2;2H");        // setCursorPos(2, 2)
        GhosttyTestFixture.Feed(t, "\u001b[48;2;255;0;0m"); // set bg red
        GhosttyTestFixture.Feed(t, "\u001b[1L");            // insertLines(1)

        Assert.AreEqual("ABC", GhosttyTestFixture.GetLine(t, 0));
        Assert.AreEqual("", GhosttyTestFixture.GetLine(t, 1));
        Assert.AreEqual("DEF", GhosttyTestFixture.GetLine(t, 2));
        Assert.AreEqual("GHI", GhosttyTestFixture.GetLine(t, 3));

        // The newly inserted blank row (row 1) should have the red bg color
        for (int x = 0; x < 5; x++)
        {
            var cell = GhosttyTestFixture.GetCell(t, 1, x);
            Assert.IsNotNull(cell.Background);
            Assert.AreEqual(255, cell.Background!.Value.R);
            Assert.AreEqual(0, cell.Background!.Value.G);
            Assert.AreEqual(0, cell.Background!.Value.B);
        }
    }

    // Ghostty: "Terminal: insertLines left/right scroll region"
    [TestMethod]
    public void InsertLines_LeftRightScrollRegion()
    {
        using var t = CreateTerminal(cols: 10, rows: 10);
        GhosttyTestFixture.Feed(t, "ABC123\r\nDEF456\r\nGHI789");
        GhosttyTestFixture.Feed(t, "\u001b[?69h");  // enable left/right margin mode
        GhosttyTestFixture.Feed(t, "\u001b[2;4s");   // setLeftAndRightMargin(2, 4) — cols 1-3 (0-based)
        GhosttyTestFixture.Feed(t, "\u001b[2;2H");   // setCursorPos(2, 2)
        GhosttyTestFixture.Feed(t, "\u001b[1L");      // insertLines(1)

        Assert.AreEqual("ABC123", GhosttyTestFixture.GetLine(t, 0));
        Assert.AreEqual("D   56", GhosttyTestFixture.GetLine(t, 1));
        Assert.AreEqual("GEF489", GhosttyTestFixture.GetLine(t, 2));
        Assert.AreEqual(" HI7", GhosttyTestFixture.GetLine(t, 3));
    }

    // Ghostty: "Terminal: insertLines multi-codepoint graphemes"
    [TestMethod]
    public void InsertLines_MultiCodepointGraphemes()
    {
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "\u001b[?2027h"); // enable grapheme cluster mode
        GhosttyTestFixture.Feed(t, "ABC\r\n");
        // Print 👨‍👩‍👧 (family emoji: U+1F468 ZWJ U+1F469 ZWJ U+1F467)
        GhosttyTestFixture.Feed(t, "\U0001F468\u200D\U0001F469\u200D\U0001F467");
        GhosttyTestFixture.Feed(t, "\r\nGHI");
        GhosttyTestFixture.Feed(t, "\u001b[2;2H");   // setCursorPos(2, 2)
        GhosttyTestFixture.Feed(t, "\u001b[1L");      // insertLines(1)

        Assert.AreEqual("ABC", GhosttyTestFixture.GetLine(t, 0));
        Assert.AreEqual("", GhosttyTestFixture.GetLine(t, 1));
        Assert.AreEqual("\U0001F468\u200D\U0001F469\u200D\U0001F467", GhosttyTestFixture.GetLine(t, 2));
        Assert.AreEqual("GHI", GhosttyTestFixture.GetLine(t, 3));
    }

    #endregion
}
