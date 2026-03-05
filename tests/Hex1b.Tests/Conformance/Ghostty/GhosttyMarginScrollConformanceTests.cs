using Xunit;

namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Additional conformance tests for index, reverseIndex, scrollUp, scrollDown
/// with left/right margins (DECSLRM), and erase reset-wrap/SGR behavior.
/// Translated from Ghostty's Terminal.zig.
/// </summary>
[Trait("Category", "GhosttyConformance")]
public class GhosttyMarginScrollConformanceTests
{
    private static Hex1bTerminal CreateTerminal(int cols = 80, int rows = 24)
        => GhosttyTestFixture.CreateTerminal(cols, rows);

    #region EraseChars — reset wrap and preserves SGR

    [Fact]
    public void EraseChars_ResetsPendingWrap()
    {
        // Ghostty: "Terminal: eraseChars resets pending wrap"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABCDE");
        Assert.True(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "\u001b[X"); // eraseChars(1)
        Assert.False(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "X");
        Assert.Equal("ABCDX", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void EraseChars_ResetsWrap()
    {
        // Ghostty: "Terminal: eraseChars resets wrap"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABCDE123");
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // setCursorPos(1,1)
        GhosttyTestFixture.Feed(t, "\u001b[X");     // eraseChars(1)
        GhosttyTestFixture.Feed(t, "X");
        Assert.Equal("XBCDE", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("123", GhosttyTestFixture.GetLine(t, 1));
    }

    [Fact]
    public void EraseChars_PreservesBackgroundSgr()
    {
        // Ghostty: "Terminal: eraseChars preserves background sgr"
        using var t = CreateTerminal(cols: 10, rows: 10);
        GhosttyTestFixture.Feed(t, "ABC");
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // setCursorPos(1,1)
        GhosttyTestFixture.Feed(t, "\u001b[48;2;255;0;0m"); // bg = red
        GhosttyTestFixture.Feed(t, "\u001b[2X"); // eraseChars(2)

        // First two cells erased, third remains 'C'
        var cell0 = GhosttyTestFixture.GetCell(t, 0, 0);
        var cell1 = GhosttyTestFixture.GetCell(t, 0, 1);
        var cell2 = GhosttyTestFixture.GetCell(t, 0, 2);
        Assert.Equal(" ", cell0.Character);
        Assert.Equal(" ", cell1.Character);
        Assert.Equal("C", cell2.Character);
        // Erased cells should have the background color set
        Assert.Equal(255, cell0.Background!.Value.R);
        Assert.Equal(0, cell0.Background!.Value.G);
        Assert.Equal(0, cell0.Background!.Value.B);
        Assert.Equal(255, cell1.Background!.Value.R);
    }

    #endregion

    #region EraseLine — reset wrap and preserves SGR

    [Fact]
    public void EraseLine_ResetsPendingWrap()
    {
        // Ghostty: "Terminal: eraseLine resets pending wrap"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABCDE");
        Assert.True(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "\u001b[K"); // eraseLine(.right)
        Assert.False(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "B");
        Assert.Equal("ABCDB", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void EraseLine_ResetsWrap()
    {
        // Ghostty: "Terminal: eraseLine resets wrap"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABCDE123");
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // setCursorPos(1,1)
        GhosttyTestFixture.Feed(t, "\u001b[K");     // eraseLine(.right)
        GhosttyTestFixture.Feed(t, "X");
        Assert.Equal("X", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("123", GhosttyTestFixture.GetLine(t, 1));
    }

    [Fact]
    public void EraseLine_RightPreservesBackgroundSgr()
    {
        // Ghostty: "Terminal: eraseLine right preserves background sgr"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABCDE");
        GhosttyTestFixture.Feed(t, "\u001b[1;2H"); // setCursorPos(1,2) → col 1
        GhosttyTestFixture.Feed(t, "\u001b[48;2;255;0;0m"); // bg = red
        GhosttyTestFixture.Feed(t, "\u001b[K"); // eraseLine(.right)

        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 0));
        for (int x = 1; x < 5; x++)
        {
            var cell = GhosttyTestFixture.GetCell(t, 0, x);
            Assert.Equal(255, cell.Background!.Value.R);
            Assert.Equal(0, cell.Background!.Value.G);
        }
    }

    [Fact]
    public void EraseLine_LeftResetsWrap()
    {
        // Ghostty: "Terminal: eraseLine left resets wrap" (adapted — skip dirty check)
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABCDE");
        Assert.True(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "\u001b[1K"); // eraseLine(.left)
        Assert.False(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "B");
        Assert.Equal("    B", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void EraseLine_LeftPreservesBackgroundSgr()
    {
        // Ghostty: "Terminal: eraseLine left preserves background sgr"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABCDE");
        GhosttyTestFixture.Feed(t, "\u001b[1;2H"); // setCursorPos(1,2) → col 1
        GhosttyTestFixture.Feed(t, "\u001b[48;2;255;0;0m"); // bg = red
        GhosttyTestFixture.Feed(t, "\u001b[1K"); // eraseLine(.left)

        // First two cells erased (cols 0-1), rest remain
        Assert.Equal("  CDE", GhosttyTestFixture.GetLine(t, 0));
        for (int x = 0; x < 2; x++)
        {
            var cell = GhosttyTestFixture.GetCell(t, 0, x);
            Assert.Equal(255, cell.Background!.Value.R);
        }
    }

    [Fact]
    public void EraseLine_CompletePreservesBackgroundSgr()
    {
        // Ghostty: "Terminal: eraseLine complete preserves background sgr"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABCDE");
        GhosttyTestFixture.Feed(t, "\u001b[1;2H"); // setCursorPos(1,2)
        GhosttyTestFixture.Feed(t, "\u001b[48;2;255;0;0m"); // bg = red
        GhosttyTestFixture.Feed(t, "\u001b[2K"); // eraseLine(.complete)

        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
        for (int x = 0; x < 5; x++)
        {
            var cell = GhosttyTestFixture.GetCell(t, 0, x);
            Assert.Equal(255, cell.Background!.Value.R);
        }
    }

    #endregion

    #region EraseDisplay — preserves SGR and cursor

    [Fact]
    public void EraseDisplay_BelowPreservesSgrBg()
    {
        // Ghostty: "Terminal: eraseDisplay erase below preserves SGR bg"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI");
        GhosttyTestFixture.Feed(t, "\u001b[2;2H"); // setCursorPos(2,2) → row 1, col 1
        GhosttyTestFixture.Feed(t, "\u001b[48;2;255;0;0m"); // bg = red
        GhosttyTestFixture.Feed(t, "\u001b[J"); // eraseDisplay(.below)

        Assert.Equal("ABC", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("D", GhosttyTestFixture.GetLine(t, 1));
        // Erased cells on row 1 (cols 1-4) should have red bg
        for (int x = 1; x < 5; x++)
        {
            var cell = GhosttyTestFixture.GetCell(t, 1, x);
            Assert.Equal(255, cell.Background!.Value.R);
        }
    }

    [Fact]
    public void EraseDisplay_AbovePreservesSgrBg()
    {
        // Ghostty: "Terminal: eraseDisplay erase above preserves SGR bg"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI");
        GhosttyTestFixture.Feed(t, "\u001b[2;2H"); // setCursorPos(2,2) → row 1, col 1
        GhosttyTestFixture.Feed(t, "\u001b[48;2;255;0;0m"); // bg = red
        GhosttyTestFixture.Feed(t, "\u001b[1J"); // eraseDisplay(.above)

        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
        // Row 1: first two cells erased, "F" remains
        Assert.Equal("  F", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("GHI", GhosttyTestFixture.GetLine(t, 2));
        // Erased cells on row 1 (cols 0-1) should have red bg
        for (int x = 0; x < 2; x++)
        {
            var cell = GhosttyTestFixture.GetCell(t, 1, x);
            Assert.Equal(255, cell.Background!.Value.R);
        }
    }

    [Fact]
    public void EraseDisplay_CompletePreservesCursor()
    {
        // Ghostty: "Terminal: eraseDisplay complete preserves cursor"
        // After erasing entire display, the cursor position should be preserved
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "\u001b[1m"); // bold
        GhosttyTestFixture.Feed(t, "AAAA");
        var cursorX = t.CursorX;
        var cursorY = t.CursorY;
        GhosttyTestFixture.Feed(t, "\u001b[2J"); // eraseDisplay(.complete)
        // Cursor position should be preserved
        Assert.Equal(cursorX, t.CursorX);
        Assert.Equal(cursorY, t.CursorY);
    }

    #endregion

    #region Index — SGR bg and left/right margins

    [Fact]
    public void Index_BottomOfPrimaryScreen_BackgroundSgr()
    {
        // Ghostty: "Terminal: index bottom of primary screen background sgr"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "\u001b[5;1H"); // setCursorPos(5,1) → row 4
        GhosttyTestFixture.Feed(t, "A");
        GhosttyTestFixture.Feed(t, "\u001b[48;2;255;0;0m"); // bg = red
        GhosttyTestFixture.Feed(t, "\u001bD"); // index

        // After scrolling, 'A' moves to row 3, row 4 is blank with red bg
        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 3));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 4));
        for (int x = 0; x < 5; x++)
        {
            var cell = GhosttyTestFixture.GetCell(t, 4, x);
            Assert.Equal(255, cell.Background!.Value.R);
        }
    }

    [Fact]
    public void Index_BottomOfScrollRegion_BackgroundSgr()
    {
        // Ghostty: "Terminal: index bottom of scroll region with background SGR"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "\u001b[1;3r"); // setTopAndBottomMargin(1,3)
        GhosttyTestFixture.Feed(t, "\u001b[4;1H"); // setCursorPos(4,1) → row 3 (outside)
        GhosttyTestFixture.Feed(t, "B");
        GhosttyTestFixture.Feed(t, "\u001b[3;1H"); // setCursorPos(3,1) → row 2
        GhosttyTestFixture.Feed(t, "A");
        GhosttyTestFixture.Feed(t, "\u001b[48;2;255;0;0m"); // bg = red
        GhosttyTestFixture.Feed(t, "\u001bD"); // index (scrolls within region)

        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("B", GhosttyTestFixture.GetLine(t, 3));
        // New blank line (row 2) should have red bg
        for (int x = 0; x < 5; x++)
        {
            var cell = GhosttyTestFixture.GetCell(t, 2, x);
            Assert.Equal(255, cell.Background!.Value.R);
        }
    }

    [Fact]
    public void Index_BottomOfScrollRegion_BlankLinePreservesSgr()
    {
        // Ghostty: "Terminal: index bottom of scroll region blank line preserves SGR"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "\u001b[1;3r"); // setTopAndBottomMargin(1,3)
        GhosttyTestFixture.Feed(t, "1\r\n2\r\n3");
        GhosttyTestFixture.Feed(t, "\u001b[4;1H"); // setCursorPos(4,1)
        GhosttyTestFixture.Feed(t, "X");
        GhosttyTestFixture.Feed(t, "\u001b[3;1H"); // setCursorPos(3,1)
        GhosttyTestFixture.Feed(t, "\u001b[48;2;255;0;0m"); // bg = red
        GhosttyTestFixture.Feed(t, "\u001bD"); // index

        Assert.Equal("2", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("3", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("X", GhosttyTestFixture.GetLine(t, 3));
        // Blank line (row 2) should preserve red bg
        for (int x = 0; x < 5; x++)
        {
            var cell = GhosttyTestFixture.GetCell(t, 2, x);
            Assert.Equal(255, cell.Background!.Value.R);
        }
    }

    [Fact]
    public void Index_OutsideLeftRightMargin()
    {
        // Ghostty: "Terminal: index outside left/right margin"
        // When cursor is outside left/right margins, index at bottom of scroll
        // region just moves cursor down without scrolling the margin region.
        using var t = CreateTerminal(cols: 10, rows: 5);
        GhosttyTestFixture.Feed(t, "\u001b[1;3r"); // setTopAndBottomMargin(1,3)
        GhosttyTestFixture.Feed(t, "\u001b[?69h"); // Enable DECLRMM
        GhosttyTestFixture.Feed(t, "\u001b[4;6s"); // setLeftAndRightMargin(4,6) → cols 3-5
        GhosttyTestFixture.Feed(t, "\u001b[3;3H"); // setCursorPos(3,3) → row 2, col 2
        GhosttyTestFixture.Feed(t, "A");
        GhosttyTestFixture.Feed(t, "\u001b[3;1H"); // setCursorPos(3,1) → row 2, col 0 (outside margin)
        GhosttyTestFixture.Feed(t, "\u001bD"); // index
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 1));
        Assert.StartsWith("X A", GhosttyTestFixture.GetLine(t, 2));
    }

    [Fact]
    public void Index_InsideLeftRightMargin()
    {
        // Ghostty: "Terminal: index inside left/right margin"
        using var t = CreateTerminal(cols: 10, rows: 5);
        GhosttyTestFixture.Feed(t, "AAAAAA\r\nAAAAAA\r\nAAAAAA");
        GhosttyTestFixture.Feed(t, "\u001b[?69h"); // Enable DECLRMM
        GhosttyTestFixture.Feed(t, "\u001b[1;3r"); // setTopAndBottomMargin(1,3)
        GhosttyTestFixture.Feed(t, "\u001b[1;3s"); // setLeftAndRightMargin(1,3)
        GhosttyTestFixture.Feed(t, "\u001b[3;1H"); // setCursorPos(3,1) → row 2, col 0
        GhosttyTestFixture.Feed(t, "\u001bD"); // index

        Assert.Equal(2, t.CursorY);
        Assert.Equal(0, t.CursorX);
        Assert.Equal("AAAAAA", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("AAAAAA", GhosttyTestFixture.GetLine(t, 1));
        // After scrolling within L/R margins, the margin region shifts up
        Assert.Equal("   AAA", GhosttyTestFixture.GetLine(t, 2));
    }

    #endregion

    #region ReverseIndex — left/right margins

    [Fact]
    public void ReverseIndex_LeftRightMargins()
    {
        // Ghostty: "Terminal: reverseIndex left/right margins"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABC");
        GhosttyTestFixture.Feed(t, "\u001b[2;1H"); // setCursorPos(2,1)
        GhosttyTestFixture.Feed(t, "DEF");
        GhosttyTestFixture.Feed(t, "\u001b[3;1H"); // setCursorPos(3,1)
        GhosttyTestFixture.Feed(t, "GHI");
        GhosttyTestFixture.Feed(t, "\u001b[?69h"); // Enable DECLRMM
        GhosttyTestFixture.Feed(t, "\u001b[2;3s"); // setLeftAndRightMargin(2,3)
        GhosttyTestFixture.Feed(t, "\u001b[1;2H"); // setCursorPos(1,2) → row 0, col 1 (inside margin)
        GhosttyTestFixture.Feed(t, "\u001bM"); // reverseIndex

        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("DBC", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("GEF", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal(" HI", GhosttyTestFixture.GetLine(t, 3));
    }

    [Fact]
    public void ReverseIndex_OutsideLeftRightMargins()
    {
        // Ghostty: "Terminal: reverseIndex outside left/right margins"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABC");
        GhosttyTestFixture.Feed(t, "\u001b[2;1H"); // setCursorPos(2,1)
        GhosttyTestFixture.Feed(t, "DEF");
        GhosttyTestFixture.Feed(t, "\u001b[3;1H"); // setCursorPos(3,1)
        GhosttyTestFixture.Feed(t, "GHI");
        GhosttyTestFixture.Feed(t, "\u001b[?69h"); // Enable DECLRMM
        GhosttyTestFixture.Feed(t, "\u001b[2;3s"); // setLeftAndRightMargin(2,3)
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // setCursorPos(1,1) → row 0, col 0 (outside margin)
        GhosttyTestFixture.Feed(t, "\u001bM"); // reverseIndex

        // Outside margin — no scrolling happens, content unchanged
        Assert.Equal("ABC", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("DEF", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("GHI", GhosttyTestFixture.GetLine(t, 2));
    }

    [Fact]
    public void ReverseIndex_TopOfScrollingRegion()
    {
        // Ghostty: "Terminal: reverseIndex top of scrolling region"
        using var t = CreateTerminal(cols: 2, rows: 10);
        GhosttyTestFixture.Feed(t, "\u001b[2;1H"); // setCursorPos(2,1)
        GhosttyTestFixture.Feed(t, "A\r\nB\r\nC\r\nD\r\n");
        // Set scroll region
        GhosttyTestFixture.Feed(t, "\u001b[2;5r"); // setTopAndBottomMargin(2,5)
        GhosttyTestFixture.Feed(t, "\u001b[2;1H"); // setCursorPos(2,1)
        GhosttyTestFixture.Feed(t, "\u001bM"); // reverseIndex
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("X", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("B", GhosttyTestFixture.GetLine(t, 3));
        Assert.Equal("C", GhosttyTestFixture.GetLine(t, 4));
    }

    #endregion

    #region ScrollUp — left/right margins

    [Fact]
    public void ScrollUp_LeftRightScrollRegion()
    {
        // Ghostty: "Terminal: scrollUp left/right scroll region"
        using var t = CreateTerminal(cols: 10, rows: 10);
        GhosttyTestFixture.Feed(t, "ABC123\r\nDEF456\r\nGHI789");
        GhosttyTestFixture.Feed(t, "\u001b[?69h"); // Enable DECLRMM
        GhosttyTestFixture.Feed(t, "\u001b[2;4s"); // setLeftAndRightMargin(2,4) → cols 1-3
        GhosttyTestFixture.Feed(t, "\u001b[2;2H"); // setCursorPos(2,2)
        GhosttyTestFixture.Feed(t, "\u001b[S"); // scrollUp(1)

        Assert.Equal("AEF423", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("DHI756", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("G   89", GhosttyTestFixture.GetLine(t, 2));
    }

    [Fact]
    public void ScrollUp_FullTopBottomRegion()
    {
        // Ghostty: "Terminal: scrollUp full top/bottom region"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "top");
        GhosttyTestFixture.Feed(t, "\u001b[5;1H"); // setCursorPos(5,1) → row 4
        GhosttyTestFixture.Feed(t, "ABCDE");
        GhosttyTestFixture.Feed(t, "\u001b[2;5r"); // setTopAndBottomMargin(2,5)
        GhosttyTestFixture.Feed(t, "\u001b[4S"); // scrollUp(4)

        Assert.Equal("top", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 1));
    }

    [Fact]
    public void ScrollUp_FullTopBottomLeftRightRegion()
    {
        // Ghostty: "Terminal: scrollUp full top/bottomleft/right scroll region"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "top");
        GhosttyTestFixture.Feed(t, "\u001b[5;1H"); // setCursorPos(5,1)
        GhosttyTestFixture.Feed(t, "ABCDE");
        GhosttyTestFixture.Feed(t, "\u001b[?69h"); // Enable DECLRMM
        GhosttyTestFixture.Feed(t, "\u001b[2;5r"); // setTopAndBottomMargin(2,5)
        GhosttyTestFixture.Feed(t, "\u001b[2;4s"); // setLeftAndRightMargin(2,4)
        GhosttyTestFixture.Feed(t, "\u001b[4S"); // scrollUp(4)

        Assert.Equal("top", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 3));
        Assert.Equal("A   E", GhosttyTestFixture.GetLine(t, 4));
    }

    #endregion

    #region ScrollDown — left/right margins

    [Fact]
    public void ScrollDown_LeftRightScrollRegion()
    {
        // Ghostty: "Terminal: scrollDown left/right scroll region"
        using var t = CreateTerminal(cols: 10, rows: 10);
        GhosttyTestFixture.Feed(t, "ABC123\r\nDEF456\r\nGHI789");
        GhosttyTestFixture.Feed(t, "\u001b[?69h"); // Enable DECLRMM
        GhosttyTestFixture.Feed(t, "\u001b[2;4s"); // setLeftAndRightMargin(2,4) → cols 1-3
        GhosttyTestFixture.Feed(t, "\u001b[2;2H"); // setCursorPos(2,2)
        GhosttyTestFixture.Feed(t, "\u001b[T"); // scrollDown(1)

        Assert.Equal("A   23", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("DBC156", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("GEF489", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal(" HI7", GhosttyTestFixture.GetLine(t, 3));
    }

    [Fact]
    public void ScrollDown_OutsideLeftRightScrollRegion()
    {
        // Ghostty: "Terminal: scrollDown outside of left/right scroll region"
        using var t = CreateTerminal(cols: 10, rows: 10);
        GhosttyTestFixture.Feed(t, "ABC123\r\nDEF456\r\nGHI789");
        GhosttyTestFixture.Feed(t, "\u001b[?69h"); // Enable DECLRMM
        GhosttyTestFixture.Feed(t, "\u001b[2;4s"); // setLeftAndRightMargin(2,4)
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // setCursorPos(1,1) → col 0 (outside margin)
        GhosttyTestFixture.Feed(t, "\u001b[T"); // scrollDown(1)

        // Cursor outside margin — scroll still affects the margin region
        Assert.Equal("A   23", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("DBC156", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("GEF489", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal(" HI7", GhosttyTestFixture.GetLine(t, 3));
    }

    #endregion

    #region FullReset — origin mode

    [Fact]
    public void FullReset_OriginMode()
    {
        // Ghostty: "Terminal: fullReset origin mode"
        using var t = CreateTerminal(cols: 10, rows: 10);
        GhosttyTestFixture.Feed(t, "\u001b[3;5H"); // setCursorPos(3,5) → row 2, col 4
        GhosttyTestFixture.Feed(t, "\u001b[?6h"); // origin mode on
        GhosttyTestFixture.Feed(t, "\u001bc"); // fullReset (RIS)

        // Origin mode should be reset and cursor should be at home
        Assert.Equal(0, t.CursorY);
        Assert.Equal(0, t.CursorX);
    }

    [Fact]
    public void FullReset_NonEmptyPen()
    {
        // Ghostty: "Terminal: fullReset with a non-empty pen"
        // After RIS, cursor style should be reset
        using var t = CreateTerminal(cols: 80, rows: 80);
        GhosttyTestFixture.Feed(t, "\u001b[38;2;255;0;127m"); // fg color
        GhosttyTestFixture.Feed(t, "\u001b[48;2;255;0;127m"); // bg color
        GhosttyTestFixture.Feed(t, "\u001bc"); // fullReset (RIS)
        GhosttyTestFixture.Feed(t, "A");

        // After reset, 'A' should have default styling
        var cell = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.Equal("A", cell.Character);
        Assert.False(cell.IsBold);
    }

    [Fact]
    public void FullReset_NonEmptySavedCursor()
    {
        // Ghostty: "Terminal: fullReset with a non-empty saved cursor"
        using var t = CreateTerminal(cols: 80, rows: 80);
        GhosttyTestFixture.Feed(t, "\u001b[38;2;255;0;127m"); // fg color
        GhosttyTestFixture.Feed(t, "\u001b7"); // save cursor
        GhosttyTestFixture.Feed(t, "\u001bc"); // fullReset (RIS)
        // After reset, saved cursor should also be reset
        GhosttyTestFixture.Feed(t, "\u001b8"); // restore cursor — should restore to defaults
        // Cursor should be at origin after reset
        Assert.Equal(0, t.CursorX);
        Assert.Equal(0, t.CursorY);
    }

    #endregion

    #region SetTopAndBottomMargin — additional edge cases

    [Fact]
    public void SetTopAndBottomMargin_TopOnly()
    {
        // Ghostty: "Terminal: setTopAndBottomMargin top only"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI\r\nJKL\r\nMNO");
        GhosttyTestFixture.Feed(t, "\u001b[3r"); // top=3, bottom defaults to rows
        GhosttyTestFixture.Feed(t, "\u001b[3;1H"); // setCursorPos(3,1)
        GhosttyTestFixture.Feed(t, "\u001b[M"); // deleteLines(1)

        Assert.Equal("ABC", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("DEF", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("JKL", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("MNO", GhosttyTestFixture.GetLine(t, 3));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 4));
    }

    [Fact]
    public void SetTopAndBottomMargin_TopEqualBottom()
    {
        // Ghostty: "Terminal: setTopAndBottomMargin top equal to bottom"
        // When top equals bottom, the margin should be ignored (reset to full)
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI\r\nJKL\r\nMNO");
        GhosttyTestFixture.Feed(t, "\u001b[3;3r"); // top=3, bottom=3 → should reset
        GhosttyTestFixture.Feed(t, "\u001b[3;1H"); // setCursorPos(3,1)
        GhosttyTestFixture.Feed(t, "\u001b[M"); // deleteLines(1)

        Assert.Equal("ABC", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("DEF", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("JKL", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("MNO", GhosttyTestFixture.GetLine(t, 3));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 4));
    }

    #endregion

    #region SetLeftAndRightMargin — edge cases

    [Fact]
    public void SetLeftAndRightMargin_LeftAndRight()
    {
        // Ghostty: "Terminal: setLeftAndRightMargin left and right"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI");
        GhosttyTestFixture.Feed(t, "\u001b[?69h"); // Enable DECLRMM
        GhosttyTestFixture.Feed(t, "\u001b[1;3s"); // setLeftAndRightMargin(1,3)
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // home
        GhosttyTestFixture.Feed(t, "\u001b[M"); // deleteLines(1) — within margin

        Assert.Equal("DEF", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("GHI", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 2));
    }

    [Fact]
    public void SetLeftAndRightMargin_LeftEqualRight()
    {
        // Ghostty: "Terminal: setLeftAndRightMargin left equal right"
        // When left == right, margin should reset to full width
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI");
        GhosttyTestFixture.Feed(t, "\u001b[?69h"); // Enable DECLRMM
        GhosttyTestFixture.Feed(t, "\u001b[3;3s"); // left=3, right=3 → should reset
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // home
        GhosttyTestFixture.Feed(t, "\u001b[M"); // deleteLines(1)

        Assert.Equal("DEF", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("GHI", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 2));
    }

    [Fact]
    public void SetLeftAndRightMargin_LeftOnly()
    {
        // Ghostty: "Terminal: setLeftAndRightMargin left only"
        // Left=2, right defaults to cols
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABCDE\r\nFGHIJ\r\nKLMNO");
        GhosttyTestFixture.Feed(t, "\u001b[?69h"); // Enable DECLRMM
        GhosttyTestFixture.Feed(t, "\u001b[2s");   // left=2, right=default (cols)
        GhosttyTestFixture.Feed(t, "\u001b[1;2H"); // setCursorPos(1,2) → col 1 (inside margin)
        GhosttyTestFixture.Feed(t, "\u001b[M");     // deleteLines(1) — within margin

        // Only cols 1-4 should be affected
        Assert.Equal("AGHIJ", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("FLMNO", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("K", GhosttyTestFixture.GetLine(t, 2));
    }

    #endregion

    #region SaveCursor — additional tests

    [Fact]
    public void SaveCursor_Basic()
    {
        // Ghostty: "Terminal: saveCursor"
        using var t = CreateTerminal(cols: 10, rows: 5);
        GhosttyTestFixture.Feed(t, "ABCDE");
        GhosttyTestFixture.Feed(t, "\u001b7"); // saveCursor
        GhosttyTestFixture.Feed(t, "\u001b[2;3H"); // move cursor
        GhosttyTestFixture.Feed(t, "\u001b8"); // restoreCursor
        Assert.Equal(5, t.CursorX); // cursor was at col 5 before save
        Assert.Equal(0, t.CursorY);
    }

    #endregion
}
