using System;
using Hex1b;
using Xunit;

namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Ghostty conformance tests — Phase 2 Tier 2 Batch 3:
/// deleteLines with margins, insertLines extras, print wrapping with L/R margins,
/// linefeed mode, insertLines/deleteLines bg color preservation.
/// </summary>
[Trait("Category", "GhosttyConformance")]
public class GhosttyMiscConformanceTests
{
    private static Hex1bTerminal CreateTerminal(int cols, int rows) => GhosttyTestFixture.CreateTerminal(cols, rows);
    private static void Feed(Hex1bTerminal t, string s) => GhosttyTestFixture.Feed(t, s);
    private static string GetLine(Hex1bTerminal t, int row) => GhosttyTestFixture.GetLine(t, row);
    private static TerminalCell GetCell(Hex1bTerminal t, int row, int col) => GhosttyTestFixture.GetCell(t, row, col);

    #region deleteLines with L/R scroll region

    // Ghostty: test "Terminal: deleteLines left/right scroll region"
    [Fact]
    public void DeleteLines_LeftRightScrollRegion()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 10);
        Feed(terminal, "ABC123\r\nDEF456\r\nGHI789");
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[2;4s"); // DECSLRM(2,4) → left=1, right=3
        Feed(terminal, "\x1b[2;2H"); // CUP(2,2)
        Feed(terminal, "\x1b[1M"); // DL(1)

        Assert.Equal("ABC123", GetLine(terminal, 0));
        Assert.Equal("DHI756", GetLine(terminal, 1));
        Assert.Equal("G   89", GetLine(terminal, 2));
    }

    // Ghostty: test "Terminal: deleteLines left/right scroll region from top"
    [Fact]
    public void DeleteLines_LeftRightScrollRegion_FromTop()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 10);
        Feed(terminal, "ABC123\r\nDEF456\r\nGHI789");
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[2;4s"); // DECSLRM(2,4) → left=1, right=3
        Feed(terminal, "\x1b[1;2H"); // CUP(1,2)
        Feed(terminal, "\x1b[1M"); // DL(1)

        Assert.Equal("AEF423", GetLine(terminal, 0));
        Assert.Equal("DHI756", GetLine(terminal, 1));
        Assert.Equal("G   89", GetLine(terminal, 2));
    }

    // Ghostty: test "Terminal: deleteLines left/right scroll region high count"
    [Fact]
    public void DeleteLines_LeftRightScrollRegion_HighCount()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 10);
        Feed(terminal, "ABC123\r\nDEF456\r\nGHI789");
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[2;4s"); // DECSLRM(2,4)
        Feed(terminal, "\x1b[2;2H"); // CUP(2,2)
        Feed(terminal, "\x1b[100M"); // DL(100)

        Assert.Equal("ABC123", GetLine(terminal, 0));
        Assert.Equal("D   56", GetLine(terminal, 1));
        Assert.Equal("G   89", GetLine(terminal, 2));
    }

    // Ghostty: test "Terminal: deleteLines with scroll region, cursor outside of region"
    [Fact]
    public void DeleteLines_CursorOutsideScrollRegion()
    {
        using var terminal = CreateTerminal(cols: 80, rows: 80);
        Feed(terminal, "A\r\nB\r\nC\r\n" + "D");
        Feed(terminal, "\x1b[1;3r"); // DECSTBM(1,3)
        Feed(terminal, "\x1b[4;1H"); // CUP(4,1) → outside scroll region
        Feed(terminal, "\x1b[1M"); // DL(1) — no-op since outside region

        Assert.Equal("A", GetLine(terminal, 0));
        Assert.Equal("B", GetLine(terminal, 1));
        Assert.Equal("C", GetLine(terminal, 2));
        Assert.Equal("D", GetLine(terminal, 3));
    }

    #endregion

    #region insertLines extras

    // Ghostty: test "Terminal: insertLines outside of scroll region"
    [Fact]
    public void InsertLines_OutsideScrollRegion()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "ABC\r\nDEF\r\nGHI");
        Feed(terminal, "\x1b[3;4r"); // DECSTBM(3,4) — scroll region rows 2-3
        Feed(terminal, "\x1b[2;2H"); // CUP(2,2) → row 1 (outside region)
        Feed(terminal, "\x1b[1L"); // IL(1) — no-op since outside

        Assert.Equal("ABC", GetLine(terminal, 0));
        Assert.Equal("DEF", GetLine(terminal, 1));
        Assert.Equal("GHI", GetLine(terminal, 2));
    }

    // Ghostty: test "Terminal: insertLines top/bottom scroll region"
    [Fact]
    public void InsertLines_TopBottomScrollRegion()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "ABC\r\nDEF\r\nGHI\r\n123");
        Feed(terminal, "\x1b[1;3r"); // DECSTBM(1,3)
        Feed(terminal, "\x1b[2;2H"); // CUP(2,2)
        Feed(terminal, "\x1b[1L"); // IL(1)

        Assert.Equal("ABC", GetLine(terminal, 0));
        Assert.Equal("", GetLine(terminal, 1));
        Assert.Equal("DEF", GetLine(terminal, 2));
        Assert.Equal("123", GetLine(terminal, 3));
    }

    // Ghostty: test "Terminal: insertLines colors with bg color"
    [Fact]
    public void InsertLines_ColorsWithBgColor()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "ABC\r\nDEF\r\nGHI");
        Feed(terminal, "\x1b[2;2H"); // CUP(2,2)
        Feed(terminal, "\x1b[48;2;255;0;0m"); // Set bg to red
        Feed(terminal, "\x1b[1L"); // IL(1)

        Assert.Equal("ABC", GetLine(terminal, 0));
        Assert.Equal("", GetLine(terminal, 1));
        Assert.Equal("DEF", GetLine(terminal, 2));
        Assert.Equal("GHI", GetLine(terminal, 3));

        // Verify the inserted blank line has the bg color
        var cell = GetCell(terminal, 1, 0);
        Assert.NotNull(cell.Background);
        var bg = cell.Background!.Value;
        Assert.Equal((byte)255, bg.R);
        Assert.Equal((byte)0, bg.G);
        Assert.Equal((byte)0, bg.B);
    }

    // Ghostty: test "Terminal: deleteLines colors with bg color"
    [Fact]
    public void DeleteLines_ColorsWithBgColor()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "ABC\r\nDEF\r\nGHI");
        Feed(terminal, "\x1b[2;2H"); // CUP(2,2)
        Feed(terminal, "\x1b[48;2;255;0;0m"); // Set bg to red
        Feed(terminal, "\x1b[1M"); // DL(1)

        Assert.Equal("ABC", GetLine(terminal, 0));
        Assert.Equal("GHI", GetLine(terminal, 1));

        // Verify the newly blank bottom line has the bg color
        var cell = GetCell(terminal, 4, 0);
        Assert.NotNull(cell.Background);
        var bg = cell.Background!.Value;
        Assert.Equal((byte)255, bg.R);
        Assert.Equal((byte)0, bg.G);
        Assert.Equal((byte)0, bg.B);
    }

    #endregion

    #region Print wrapping with L/R margins

    // Ghostty: test "Terminal: print right margin wrap"
    [Fact]
    public void Print_RightMarginWrap()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        Feed(terminal, "123456789");
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[3;5s"); // DECSLRM(3,5) → left=2, right=4
        Feed(terminal, "\x1b[1;5H"); // CUP(1,5) → col 4 (right margin)
        Feed(terminal, "XY"); // X at col 4, Y wraps to col 2 of row 1

        Assert.Equal("1234X6789", GetLine(terminal, 0));
        Assert.Equal("  Y", GetLine(terminal, 1));
    }

    // Ghostty: test "Terminal: print right margin outside"
    [Fact]
    public void Print_RightMarginOutside()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        Feed(terminal, "123456789");
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[3;5s"); // DECSLRM(3,5) → left=2, right=4
        Feed(terminal, "\x1b[1;6H"); // CUP(1,6) → col 5 (outside right margin)
        Feed(terminal, "XY");

        Assert.Equal("12345XY89", GetLine(terminal, 0));
    }

    // Ghostty: test "Terminal: print right margin outside wrap"
    [Fact]
    public void Print_RightMarginOutsideWrap()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        Feed(terminal, "123456789");
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[3;5s"); // DECSLRM(3,5)
        Feed(terminal, "\x1b[1;10H"); // CUP(1,10) → col 9 (far right, outside margin)
        Feed(terminal, "XY"); // X at col 9, Y wraps to left margin col 2

        Assert.Equal("123456789X", GetLine(terminal, 0));
        Assert.Equal("  Y", GetLine(terminal, 1));
    }

    #endregion

    #region Linefeed behavior

    // Ghostty: test "Terminal: linefeed and carriage return"
    [Fact]
    public void Linefeed_AndCarriageReturn()
    {
        using var terminal = CreateTerminal(cols: 80, rows: 80);
        Feed(terminal, "hello\r\nworld");

        Assert.Equal(1, terminal.CursorY);
        Assert.Equal(5, terminal.CursorX);
        Assert.Equal("hello", GetLine(terminal, 0));
        Assert.Equal("world", GetLine(terminal, 1));
    }

    // Ghostty: test "Terminal: linefeed mode automatic carriage return"
    [Fact]
    public void Linefeed_AutomaticCarriageReturn()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 10);
        Feed(terminal, "\x1b[20h"); // Set LNM (linefeed mode) — LF does CR+LF
        Feed(terminal, "123456");
        Feed(terminal, "\n"); // LF — in LNM, this also does CR
        Feed(terminal, "X");

        Assert.Equal("123456", GetLine(terminal, 0));
        Assert.Equal("X", GetLine(terminal, 1));
    }

    // Ghostty: test "Terminal: carriage return origin mode moves to left margin"
    [Fact]
    public void CarriageReturn_OriginMode_MovesToLeftMargin_Direct()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 80);
        Feed(terminal, "\x1b[?6h"); // Enable DECOM
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[3;5s"); // DECSLRM(3,5) → left margin at col 2
        Feed(terminal, "\x1b[1;1H"); // CUP — goes to origin (margin left=2)
        Feed(terminal, "\r"); // CR — should go to left margin

        Assert.Equal(2, terminal.CursorX);
    }

    #endregion

    #region Backspace

    // Ghostty: test "Terminal: backspace"
    [Fact]
    public void Backspace_Basic()
    {
        using var terminal = CreateTerminal(cols: 80, rows: 80);
        Feed(terminal, "A\b");

        Assert.Equal(0, terminal.CursorX);
        Assert.Equal(0, terminal.CursorY);
    }

    // Ghostty: test "Terminal: backspace at left margin"
    [Fact]
    public void Backspace_AtLeftMargin()
    {
        using var terminal = CreateTerminal(cols: 80, rows: 80);
        Feed(terminal, "\b"); // Backspace at col 0 — should be no-op

        Assert.Equal(0, terminal.CursorX);
        Assert.Equal(0, terminal.CursorY);
    }

    #endregion

    #region Additional index/scroll behavior

    // Ghostty: test "Terminal: index bottom of primary screen background sgr"
    [Fact]
    public void Index_BottomOfScreen_BackgroundSgr()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "\x1b[5;1H"); // CUP(5,1) → bottom row
        Feed(terminal, "A");
        Feed(terminal, "\x1b[48;2;255;0;0m"); // Set bg to red
        Feed(terminal, "\u001bD"); // IND — scrolls, new blank line has bg color

        Assert.Equal("", GetLine(terminal, 0));
        Assert.Equal("", GetLine(terminal, 1));
        Assert.Equal("", GetLine(terminal, 2));
        Assert.Equal("A", GetLine(terminal, 3));

        // Verify the new blank bottom line has red background
        var cell = GetCell(terminal, 4, 0);
        Assert.NotNull(cell.Background);
        var bg = cell.Background!.Value;
        Assert.Equal((byte)255, bg.R);
        Assert.Equal((byte)0, bg.G);
        Assert.Equal((byte)0, bg.B);
    }

    // Ghostty: test "Terminal: scrollUp preserves pending wrap" (from scrollUp tests)
    // Already covered in GhosttyScrollMarginConformanceTests — verify cursor positioning
    
    // Ghostty: test "Terminal: input that forces scroll"
    [Fact]
    public void Input_ForcesScroll()
    {
        using var terminal = CreateTerminal(cols: 80, rows: 5);
        // Fill all rows
        Feed(terminal, "1\r\n2\r\n3\r\n4\r\n5");
        // Now print another char which forces scroll
        Feed(terminal, "\r\n6");

        Assert.Equal("2", GetLine(terminal, 0));
        Assert.Equal("3", GetLine(terminal, 1));
        Assert.Equal("4", GetLine(terminal, 2));
        Assert.Equal("5", GetLine(terminal, 3));
        Assert.Equal("6", GetLine(terminal, 4));
    }

    #endregion

    #region setTopAndBottomMargin extras

    // Ghostty: test "Terminal: setTopAndBottomMargin top only"
    [Fact]
    public void SetTopAndBottomMargin_TopOnly()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "ABC\r\nDEF\r\nGHI");
        Feed(terminal, "\x1b[2r"); // DECSTBM(2) — top=2, bottom=default (5)
        Feed(terminal, "\x1b[2;1H"); // CUP(2,1)
        Feed(terminal, "\x1b[1L"); // IL(1) — within scroll region

        Assert.Equal("ABC", GetLine(terminal, 0));
        Assert.Equal("", GetLine(terminal, 1));
        Assert.Equal("DEF", GetLine(terminal, 2));
        Assert.Equal("GHI", GetLine(terminal, 3));
    }

    // Ghostty: test "Terminal: setTopAndBottomMargin top and bottom"
    [Fact]
    public void SetTopAndBottomMargin_TopAndBottom()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "ABC\r\nDEF\r\nGHI\r\n123");
        Feed(terminal, "\x1b[2;3r"); // DECSTBM(2,3)
        Feed(terminal, "\x1b[2;1H"); // CUP(2,1)
        Feed(terminal, "\x1b[1L"); // IL(1)

        Assert.Equal("ABC", GetLine(terminal, 0));
        Assert.Equal("", GetLine(terminal, 1));
        Assert.Equal("DEF", GetLine(terminal, 2));
        Assert.Equal("123", GetLine(terminal, 3));
    }

    // Ghostty: test "Terminal: setTopAndBottomMargin top equal to bottom"
    [Fact]
    public void SetTopAndBottomMargin_TopEqualBottom()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "ABC\r\nDEF\r\nGHI");
        Feed(terminal, "\x1b[2;2r"); // DECSTBM(2,2) — top=bottom → should reset to full
        Feed(terminal, "\x1b[2;1H"); // CUP(2,1)
        Feed(terminal, "\x1b[1L"); // IL(1)

        // With top=bottom, DECSTBM should be treated as invalid → full height
        Assert.Equal("ABC", GetLine(terminal, 0));
        Assert.Equal("", GetLine(terminal, 1));
        Assert.Equal("DEF", GetLine(terminal, 2));
        Assert.Equal("GHI", GetLine(terminal, 3));
    }

    #endregion

    #region Print wrapping edge cases

    // Ghostty: test "Terminal: printRepeat wrap"
    [Fact]
    public void PrintRepeat_Wrap()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "    A");
        Feed(terminal, "\x1b[b"); // REP(1) — repeat last char

        Assert.Equal("    A", GetLine(terminal, 0));
        Assert.Equal("A", GetLine(terminal, 1));
    }

    // Ghostty: test "Terminal: printRepeat no previous character"
    [Fact]
    public void PrintRepeat_NoPreviousCharacter()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "\x1b[b"); // REP(1) — no previous char, should be no-op

        Assert.Equal("", GetLine(terminal, 0));
    }

    #endregion
}
