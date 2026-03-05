using Xunit;

namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Additional erase operation, DECALN, and cursor position tests.
/// Translated from Ghostty's Terminal.zig.
/// </summary>
[Trait("Category", "GhosttyConformance")]
public class GhosttyEraseAndMiscConformanceTests
{
    // ═══════════════════════════════════════════════════════════════
    // eraseChars — basic and edge cases (non-protected)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void EraseChars_SimpleOperation()
    {
        // Ghostty: "Terminal: eraseChars simple operation"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABC");
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // setCursorPos(1,1)
        GhosttyTestFixture.Feed(t, "\u001b[2X");    // eraseChars(2)
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("X C", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void EraseChars_MinimumOne()
    {
        // Ghostty: "Terminal: eraseChars minimum one"
        // ECH with 0 should erase 1 (minimum).
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABC");
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // setCursorPos(1,1)
        GhosttyTestFixture.Feed(t, "\u001b[0X");    // eraseChars(0) → treated as 1
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("XBC", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void EraseChars_BeyondScreenEdge()
    {
        // Ghostty: "Terminal: eraseChars beyond screen edge"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "  ABC");
        GhosttyTestFixture.Feed(t, "\u001b[1;4H"); // setCursorPos(1,4)
        GhosttyTestFixture.Feed(t, "\u001b[10X");   // eraseChars(10)

        Assert.Equal("  A", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void EraseChars_WideCharacter()
    {
        // Ghostty: "Terminal: eraseChars wide character"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "橋BC");
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // setCursorPos(1,1)
        GhosttyTestFixture.Feed(t, "\u001b[1X");    // eraseChars(1) — erases leading cell of wide char
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("X BC", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void EraseChars_ResetsPendingWrap()
    {
        // Ghostty: "Terminal: eraseChars resets pending wrap"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABCDE");
        Assert.True(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "\u001b[1X"); // eraseChars(1)
        Assert.False(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("ABCDX", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void EraseChars_WideCharBoundaryConditions()
    {
        // Ghostty: "Terminal: eraseChars wide char boundary conditions"
        var t = GhosttyTestFixture.CreateTerminal(8, 1);

        GhosttyTestFixture.Feed(t, "😀a😀b😀");
        Assert.Equal("😀a😀b😀", GhosttyTestFixture.GetLine(t, 0));

        GhosttyTestFixture.Feed(t, "\u001b[1;2H"); // setCursorPos(1,2) — on continuation of first emoji
        GhosttyTestFixture.Feed(t, "\u001b[3X");    // eraseChars(3)

        Assert.Equal("     b😀", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void EraseChars_WideCharSplitsProperCellBoundaries()
    {
        // Ghostty: "Terminal: eraseChars wide char splits proper cell boundaries"
        // Regression test for ghostty-org/ghostty#2817
        var t = GhosttyTestFixture.CreateTerminal(30, 1);

        GhosttyTestFixture.Feed(t, "x食べて下さい");
        Assert.Equal("x食べて下さい", GhosttyTestFixture.GetLine(t, 0));

        GhosttyTestFixture.Feed(t, "\u001b[1;6H"); // setCursorPos(1,6) — at て
        GhosttyTestFixture.Feed(t, "\u001b[4X");    // eraseChars(4) — erase て下

        Assert.Equal("x食べ    さい", GhosttyTestFixture.GetLine(t, 0));
    }

    // ═══════════════════════════════════════════════════════════════
    // eraseLine — basic operations (non-protected)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void EraseLine_SimpleEraseRight()
    {
        // Ghostty: "Terminal: eraseLine simple erase right"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABCDE");
        GhosttyTestFixture.Feed(t, "\u001b[1;3H"); // setCursorPos(1,3)
        GhosttyTestFixture.Feed(t, "\u001b[K");     // eraseLine right

        Assert.Equal("AB", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void EraseLine_ResetsPendingWrap()
    {
        // Ghostty: "Terminal: eraseLine resets pending wrap"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABCDE");
        Assert.True(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "\u001b[K"); // eraseLine right
        Assert.False(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "B");

        Assert.Equal("ABCDB", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void EraseLine_RightWideCharacter()
    {
        // Ghostty: "Terminal: eraseLine right wide character"
        // EL right on a wide char continuation cell clears the leading cell too.
        var t = GhosttyTestFixture.CreateTerminal(10, 5);

        GhosttyTestFixture.Feed(t, "AB橋DE");
        GhosttyTestFixture.Feed(t, "\u001b[1;4H"); // setCursorPos(1,4) — on continuation of 橋
        GhosttyTestFixture.Feed(t, "\u001b[K");     // eraseLine right

        Assert.Equal("AB", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void EraseLine_SimpleEraseLeft()
    {
        // Ghostty: "Terminal: eraseLine simple erase left"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABCDE");
        GhosttyTestFixture.Feed(t, "\u001b[1;3H"); // setCursorPos(1,3)
        GhosttyTestFixture.Feed(t, "\u001b[1K");    // eraseLine left

        Assert.Equal("   DE", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void EraseLine_LeftResetsWrap()
    {
        // Ghostty: "Terminal: eraseLine left resets wrap"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABCDE");
        Assert.True(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "\u001b[1K"); // eraseLine left
        Assert.False(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "B");

        Assert.Equal("    B", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void EraseLine_LeftWideCharacter()
    {
        // Ghostty: "Terminal: eraseLine left wide character"
        // EL left on leading cell of wide char clears the continuation too.
        var t = GhosttyTestFixture.CreateTerminal(10, 5);

        GhosttyTestFixture.Feed(t, "AB橋DE");
        GhosttyTestFixture.Feed(t, "\u001b[1;3H"); // setCursorPos(1,3) — on leading cell of 橋
        GhosttyTestFixture.Feed(t, "\u001b[1K");    // eraseLine left

        Assert.Equal("    DE", GhosttyTestFixture.GetLine(t, 0));
    }

    // ═══════════════════════════════════════════════════════════════
    // eraseDisplay — basic operations (non-protected)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void EraseDisplay_SimpleEraseBelow()
    {
        // Ghostty: "Terminal: eraseDisplay simple erase below"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI");
        GhosttyTestFixture.Feed(t, "\u001b[2;2H"); // setCursorPos(2,2)
        GhosttyTestFixture.Feed(t, "\u001b[J");     // eraseDisplay below

        Assert.Equal("ABC", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("D", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 2));
    }

    [Fact]
    public void EraseDisplay_SimpleEraseAbove()
    {
        // Ghostty: "Terminal: eraseDisplay simple erase above"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI");
        GhosttyTestFixture.Feed(t, "\u001b[2;2H"); // setCursorPos(2,2)
        GhosttyTestFixture.Feed(t, "\u001b[1J");    // eraseDisplay above

        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("  F", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("GHI", GhosttyTestFixture.GetLine(t, 2));
    }

    [Fact]
    public void EraseDisplay_BelowSplitMultiCell()
    {
        // Ghostty: "Terminal: eraseDisplay below split multi-cell"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "AB橋C\r\nDE橋F\r\nGH橋I");
        GhosttyTestFixture.Feed(t, "\u001b[2;4H"); // setCursorPos(2,4) — on continuation of 橋
        GhosttyTestFixture.Feed(t, "\u001b[J");     // eraseDisplay below

        Assert.Equal("AB橋C", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("DE", GhosttyTestFixture.GetLine(t, 1));
    }

    [Fact]
    public void EraseDisplay_AboveSplitMultiCell()
    {
        // Ghostty: "Terminal: eraseDisplay above split multi-cell"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "AB橋C\r\nDE橋F\r\nGH橋I");
        GhosttyTestFixture.Feed(t, "\u001b[2;3H"); // setCursorPos(2,3) — on leading cell of 橋
        GhosttyTestFixture.Feed(t, "\u001b[1J");    // eraseDisplay above

        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("    F", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("GH橋I", GhosttyTestFixture.GetLine(t, 2));
    }

    [Fact]
    public void EraseDisplay_ScrollComplete()
    {
        // Ghostty: "Terminal: eraseDisplay scroll complete"
        var t = GhosttyTestFixture.CreateTerminal(10, 5);

        GhosttyTestFixture.Feed(t, "A\r\n");
        GhosttyTestFixture.Feed(t, "\u001b[3J"); // eraseDisplay scroll_complete

        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
    }

    // ═══════════════════════════════════════════════════════════════
    // DECALN
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Decaln_Basic()
    {
        // Ghostty: "Terminal: DECALN"
        var t = GhosttyTestFixture.CreateTerminal(2, 2);

        GhosttyTestFixture.Feed(t, "A\r\nB");
        GhosttyTestFixture.Feed(t, "\u001b#8"); // DECALN

        Assert.Equal(0, t.CursorY);
        Assert.Equal(0, t.CursorX);
        Assert.Equal("EE", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("EE", GhosttyTestFixture.GetLine(t, 1));
    }

    [Fact]
    public void Decaln_ResetMargins()
    {
        // Ghostty: "Terminal: decaln reset margins"
        // DECALN resets scroll margins.
        var t = GhosttyTestFixture.CreateTerminal(3, 3);

        GhosttyTestFixture.Feed(t, "\u001b[?6h");   // origin mode
        GhosttyTestFixture.Feed(t, "\u001b[2;3r");   // set scroll region 2-3
        GhosttyTestFixture.Feed(t, "\u001b#8");       // DECALN — resets margins
        GhosttyTestFixture.Feed(t, "\u001b[T");       // scrollDown(1) — should scroll entire screen now

        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("EEE", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("EEE", GhosttyTestFixture.GetLine(t, 2));
    }

    // ═══════════════════════════════════════════════════════════════
    // saveCursor / restoreCursor
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void SaveCursor_Position()
    {
        // Ghostty: "Terminal: saveCursor position"
        var t = GhosttyTestFixture.CreateTerminal(10, 5);

        GhosttyTestFixture.Feed(t, "\u001b[3;5H"); // setCursorPos(3,5)
        GhosttyTestFixture.Feed(t, "\u001b7");      // saveCursor
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // home
        GhosttyTestFixture.Feed(t, "\u001b8");      // restoreCursor

        Assert.Equal(4, t.CursorX); // col 5, 0-based = 4
        Assert.Equal(2, t.CursorY); // row 3, 0-based = 2
    }

    [Fact]
    public void SaveCursor_PendingWrapState()
    {
        // Ghostty: "Terminal: saveCursor pending wrap state"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABCDE");
        Assert.True(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "\u001b7");      // saveCursor
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // home
        Assert.False(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "\u001b8");      // restoreCursor
        Assert.True(t.PendingWrap);
    }

    [Fact]
    public void SaveCursor_OriginMode()
    {
        // Ghostty: "Terminal: saveCursor origin mode"
        // Save/restore cursor with origin mode — restored position is relative.
        var t = GhosttyTestFixture.CreateTerminal(10, 10);

        GhosttyTestFixture.Feed(t, "\u001b[3;7r");  // set scroll region 3-7
        GhosttyTestFixture.Feed(t, "\u001b[?6h");   // enable origin mode
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // position in origin mode
        GhosttyTestFixture.Feed(t, "\u001b7");      // saveCursor

        Assert.Equal(0, t.CursorX);
        Assert.Equal(2, t.CursorY); // Origin mode maps row 1 to actual row 3 (0-based: 2)
    }

    // ═══════════════════════════════════════════════════════════════
    // Horizontal tabs
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void TabClear_Single()
    {
        // Ghostty: "Terminal: tabClear single"
        var t = GhosttyTestFixture.CreateTerminal(30, 5);

        GhosttyTestFixture.Feed(t, "\t");  // Tab to position 8
        GhosttyTestFixture.Feed(t, "\u001b[0g"); // Clear this tab stop
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // Home
        GhosttyTestFixture.Feed(t, "\t");  // Tab should now skip 8 and go to 16

        Assert.Equal(16, t.CursorX);
    }

    [Fact]
    public void TabClear_All()
    {
        // Ghostty: "Terminal: tabClear all"
        var t = GhosttyTestFixture.CreateTerminal(30, 5);

        GhosttyTestFixture.Feed(t, "\u001b[3g"); // Clear all tab stops
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // Home
        GhosttyTestFixture.Feed(t, "\t");          // Tab with no stops goes to last col

        Assert.Equal(29, t.CursorX); // Last column (0-based)
    }
}
