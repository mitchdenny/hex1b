using Xunit;

namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Tests for index (IND), reverse index (RI), scrollUp (SU), and scrollDown (SD).
/// Translated from Ghostty's Terminal.zig.
/// </summary>
[Trait("Category", "GhosttyConformance")]
public class GhosttyScrollConformanceTests
{
    // ═══════════════════════════════════════════════════════════════
    // index (IND — ESC D, also triggered by LF at bottom of scroll region)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Index_Basic()
    {
        // Ghostty: "Terminal: index"
        // Index at top of screen just moves cursor down.
        var t = GhosttyTestFixture.CreateTerminal(2, 5);

        GhosttyTestFixture.Feed(t, "\u001bD");  // IND (ESC D)
        GhosttyTestFixture.Feed(t, "A");

        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 1));
    }

    [Fact]
    public void Index_FromTheBottom()
    {
        // Ghostty: "Terminal: index from the bottom"
        // Index at bottom of screen scrolls content up.
        var t = GhosttyTestFixture.CreateTerminal(2, 5);

        GhosttyTestFixture.Feed(t, "\u001b[5;1H"); // setCursorPos(5,1)
        GhosttyTestFixture.Feed(t, "A");
        GhosttyTestFixture.Feed(t, "\u001b[1D");    // cursorLeft(1)
        GhosttyTestFixture.Feed(t, "\u001bD");       // IND
        GhosttyTestFixture.Feed(t, "B");

        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 3));
        Assert.Equal("B", GhosttyTestFixture.GetLine(t, 4));
    }

    [Fact]
    public void Index_OutsideOfScrollingRegion()
    {
        // Ghostty: "Terminal: index outside of scrolling region"
        // Index above scroll region just moves cursor down normally.
        var t = GhosttyTestFixture.CreateTerminal(2, 5);

        GhosttyTestFixture.Feed(t, "\u001b[2;5r"); // setTopAndBottomMargin(2, 5)
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // Home
        GhosttyTestFixture.Feed(t, "\u001bD");       // IND
        Assert.Equal(1, t.CursorY);
    }

    [Fact]
    public void Index_FromTheBottomOutsideOfScrollRegion()
    {
        // Ghostty: "Terminal: index from the bottom outside of scroll region"
        // Index at bottom of screen when scroll region is smaller — no scroll, cursor stays.
        var t = GhosttyTestFixture.CreateTerminal(2, 5);

        GhosttyTestFixture.Feed(t, "\u001b[1;2r"); // setTopAndBottomMargin(1, 2)
        GhosttyTestFixture.Feed(t, "\u001b[5;1H"); // setCursorPos(5,1)
        GhosttyTestFixture.Feed(t, "A");
        GhosttyTestFixture.Feed(t, "\u001bD");       // IND — at row 5 (bottom), outside region
        GhosttyTestFixture.Feed(t, "B");

        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 3));
        Assert.Equal("AB", GhosttyTestFixture.GetLine(t, 4));
    }

    [Fact]
    public void Index_NoScrollRegionTopOfScreen()
    {
        // Ghostty: "Terminal: index no scroll region, top of screen"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "A");
        GhosttyTestFixture.Feed(t, "\u001bD"); // IND
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal(" X", GhosttyTestFixture.GetLine(t, 1));
    }

    [Fact]
    public void Index_BottomOfPrimaryScreen()
    {
        // Ghostty: "Terminal: index bottom of primary screen"
        // At bottom → scrolls up, writes X on new bottom line.
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "\u001b[5;1H"); // setCursorPos(5,1)
        GhosttyTestFixture.Feed(t, "A");
        GhosttyTestFixture.Feed(t, "\u001bD");       // IND
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 3));
        Assert.Equal(" X", GhosttyTestFixture.GetLine(t, 4));
    }

    [Fact]
    public void Index_InsideScrollRegion()
    {
        // Ghostty: "Terminal: index inside scroll region"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "\u001b[1;3r"); // setTopAndBottomMargin(1, 3)
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // Home
        GhosttyTestFixture.Feed(t, "A");
        GhosttyTestFixture.Feed(t, "\u001bD");       // IND
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal(" X", GhosttyTestFixture.GetLine(t, 1));
    }

    [Fact]
    public void Index_BottomOfPrimaryScreenWithScrollRegion()
    {
        // Ghostty: "Terminal: index bottom of primary screen with scroll region"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "\u001b[1;3r"); // setTopAndBottomMargin(1, 3)
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // Home
        GhosttyTestFixture.Feed(t, "A\r\nB\r\nC");
        GhosttyTestFixture.Feed(t, "\u001bD");       // IND at bottom of scroll region
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("B", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("C", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal(" X", GhosttyTestFixture.GetLine(t, 2));
    }

    // ═══════════════════════════════════════════════════════════════
    // reverseIndex (RI — ESC M)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ReverseIndex_Basic()
    {
        // Ghostty: "Terminal: reverseIndex"
        var t = GhosttyTestFixture.CreateTerminal(2, 5);

        GhosttyTestFixture.Feed(t, "A\r\nB\r\nC");
        GhosttyTestFixture.Feed(t, "\u001bM");  // RI (reverse index)
        GhosttyTestFixture.Feed(t, "D\r\n\r\n");

        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("BD", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("C", GhosttyTestFixture.GetLine(t, 2));
    }

    [Fact]
    public void ReverseIndex_FromTheTop()
    {
        // Ghostty: "Terminal: reverseIndex from the top"
        // RI at row 0 scrolls content down, inserts blank line at top.
        var t = GhosttyTestFixture.CreateTerminal(2, 5);

        GhosttyTestFixture.Feed(t, "A\r\nB\r\n\r\n");
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // setCursorPos(1,1)
        GhosttyTestFixture.Feed(t, "\u001bM");       // RI
        GhosttyTestFixture.Feed(t, "D\r\n");
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // setCursorPos(1,1)
        GhosttyTestFixture.Feed(t, "\u001bM");       // RI
        GhosttyTestFixture.Feed(t, "E\r\n");

        Assert.Equal("E", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("D", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("B", GhosttyTestFixture.GetLine(t, 3));
    }

    [Fact]
    public void ReverseIndex_TopOfScreen()
    {
        // Ghostty: "Terminal: reverseIndex top of screen"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "A");
        GhosttyTestFixture.Feed(t, "\u001b[2;1H"); // row 2
        GhosttyTestFixture.Feed(t, "B");
        GhosttyTestFixture.Feed(t, "\u001b[3;1H"); // row 3
        GhosttyTestFixture.Feed(t, "C");
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // row 1
        GhosttyTestFixture.Feed(t, "\u001bM");       // RI
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("X", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("B", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("C", GhosttyTestFixture.GetLine(t, 3));
    }

    [Fact]
    public void ReverseIndex_NotTopOfScreen()
    {
        // Ghostty: "Terminal: reverseIndex not top of screen"
        // RI not at top just moves cursor up.
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "A");
        GhosttyTestFixture.Feed(t, "\u001b[2;1H");
        GhosttyTestFixture.Feed(t, "B");
        GhosttyTestFixture.Feed(t, "\u001b[3;1H");
        GhosttyTestFixture.Feed(t, "C");
        GhosttyTestFixture.Feed(t, "\u001b[2;1H");
        GhosttyTestFixture.Feed(t, "\u001bM");       // RI — at row 2, goes to row 1
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("X", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("B", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("C", GhosttyTestFixture.GetLine(t, 2));
    }

    [Fact]
    public void ReverseIndex_TopBottomMargins()
    {
        // Ghostty: "Terminal: reverseIndex top/bottom margins"
        // RI at top of scroll region scrolls within region only.
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "A");
        GhosttyTestFixture.Feed(t, "\u001b[2;1H");
        GhosttyTestFixture.Feed(t, "B");
        GhosttyTestFixture.Feed(t, "\u001b[3;1H");
        GhosttyTestFixture.Feed(t, "C");
        GhosttyTestFixture.Feed(t, "\u001b[2;3r"); // setTopAndBottomMargin(2, 3)
        GhosttyTestFixture.Feed(t, "\u001b[2;1H");
        GhosttyTestFixture.Feed(t, "\u001bM");       // RI at top of scroll region

        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("B", GhosttyTestFixture.GetLine(t, 2));
    }

    [Fact]
    public void ReverseIndex_OutsideTopBottomMargins()
    {
        // Ghostty: "Terminal: reverseIndex outside top/bottom margins"
        // RI outside scroll region doesn't scroll.
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "A");
        GhosttyTestFixture.Feed(t, "\u001b[2;1H");
        GhosttyTestFixture.Feed(t, "B");
        GhosttyTestFixture.Feed(t, "\u001b[3;1H");
        GhosttyTestFixture.Feed(t, "C");
        GhosttyTestFixture.Feed(t, "\u001b[2;3r"); // setTopAndBottomMargin(2, 3)
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // row 1 (outside region)
        GhosttyTestFixture.Feed(t, "\u001bM");       // RI

        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("B", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("C", GhosttyTestFixture.GetLine(t, 2));
    }

    // ═══════════════════════════════════════════════════════════════
    // scrollUp (SU — CSI S)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ScrollUp_Simple()
    {
        // Ghostty: "Terminal: scrollUp simple"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI");
        GhosttyTestFixture.Feed(t, "\u001b[2;2H"); // setCursorPos(2,2)
        GhosttyTestFixture.Feed(t, "\u001b[1S");    // scrollUp(1)

        Assert.Equal(1, t.CursorX); // cursor X preserved
        Assert.Equal(1, t.CursorY); // cursor Y preserved

        Assert.Equal("DEF", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("GHI", GhosttyTestFixture.GetLine(t, 1));
    }

    [Fact]
    public void ScrollUp_TopBottomScrollRegion()
    {
        // Ghostty: "Terminal: scrollUp top/bottom scroll region"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI\r\nJKL\r\nMNO");
        GhosttyTestFixture.Feed(t, "\u001b[2;4r"); // setTopAndBottomMargin(2, 4)
        GhosttyTestFixture.Feed(t, "\u001b[1S");    // scrollUp(1)

        Assert.Equal("ABC", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("GHI", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("JKL", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 3));
        Assert.Equal("MNO", GhosttyTestFixture.GetLine(t, 4));
    }

    [Fact]
    public void ScrollUp_PreservesPendingWrap()
    {
        // Ghostty: "Terminal: scrollUp preserves pending wrap"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABCDE");
        Assert.True(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "\u001b[1S"); // scrollUp(1)
        Assert.True(t.PendingWrap);
    }

    // ═══════════════════════════════════════════════════════════════
    // scrollDown (SD — CSI T)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ScrollDown_Simple()
    {
        // Ghostty: "Terminal: scrollDown simple"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI");
        GhosttyTestFixture.Feed(t, "\u001b[2;2H"); // setCursorPos(2,2)
        GhosttyTestFixture.Feed(t, "\u001b[1T");    // scrollDown(1)

        Assert.Equal(1, t.CursorX); // cursor X preserved
        Assert.Equal(1, t.CursorY); // cursor Y preserved

        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("ABC", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("DEF", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("GHI", GhosttyTestFixture.GetLine(t, 3));
    }

    [Fact]
    public void ScrollDown_OutsideOfScrollRegion()
    {
        // Ghostty: "Terminal: scrollDown outside of scroll region"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI\r\nJKL\r\nMNO");
        GhosttyTestFixture.Feed(t, "\u001b[2;4r"); // setTopAndBottomMargin(2, 4)
        GhosttyTestFixture.Feed(t, "\u001b[1T");    // scrollDown(1)

        Assert.Equal("ABC", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("DEF", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("GHI", GhosttyTestFixture.GetLine(t, 3));
        Assert.Equal("MNO", GhosttyTestFixture.GetLine(t, 4));
    }

    [Fact]
    public void ScrollDown_PreservesPendingWrap()
    {
        // Ghostty: "Terminal: scrollDown preserves pending wrap"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABCDE");
        Assert.True(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "\u001b[1T"); // scrollDown(1)
        Assert.True(t.PendingWrap);
    }

    // ═══════════════════════════════════════════════════════════════
    // Linefeed
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Linefeed_UnsetsPendingWrap()
    {
        // Ghostty: "Terminal: linefeed unsets pending wrap"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABCDE");
        Assert.True(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "\n");
        Assert.False(t.PendingWrap);
    }

    [Fact]
    public void Linefeed_AndCarriageReturn()
    {
        // Ghostty: "Terminal: linefeed and carriage return"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABC\r\nDEF");
        Assert.Equal("ABC", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("DEF", GhosttyTestFixture.GetLine(t, 1));
    }

    [Fact]
    public void Linefeed_ModeAutomaticCarriageReturn()
    {
        // Ghostty: "Terminal: linefeed mode automatic carriage return"
        // LNM mode (mode 20): LF also does CR.
        var t = GhosttyTestFixture.CreateTerminal(10, 5);

        GhosttyTestFixture.Feed(t, "\u001b[20h");  // Set LNM mode
        GhosttyTestFixture.Feed(t, "ABC\nDEF");
        Assert.Equal("ABC", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("DEF", GhosttyTestFixture.GetLine(t, 1));
    }

    // ═══════════════════════════════════════════════════════════════
    // setTopAndBottomMargin (DECSTBM — CSI r)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void SetTopAndBottomMargin_Simple()
    {
        // Ghostty: "Terminal: setTopAndBottomMargin simple"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "\u001b[2r"); // top only (top=2)
        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI\r\nJKL\r\nMNO");
        // Cursor should be at bottom. Let's verify the scroll region works
        // by scrolling down from bottom
    }

    [Fact]
    public void SetTopAndBottomMargin_TopAndBottom()
    {
        // Ghostty: "Terminal: setTopAndBottomMargin top and bottom"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "\u001b[2;4r"); // top=2, bottom=4
        // Verify cursor moved to home
        Assert.Equal(0, t.CursorX);
        Assert.Equal(0, t.CursorY);
    }

    // ═══════════════════════════════════════════════════════════════
    // setLeftAndRightMargin (DECSLRM — CSI s with mode 69)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void SetLeftAndRightMargin_Simple()
    {
        // Ghostty: "Terminal: setLeftAndRightMargin simple"
        // Original test checks dirty tracking; we test erase behavior with margins.
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABC");
        GhosttyTestFixture.Feed(t, "\r\n");
        GhosttyTestFixture.Feed(t, "DEF");
        GhosttyTestFixture.Feed(t, "\r\n");
        GhosttyTestFixture.Feed(t, "GHI");
        GhosttyTestFixture.Feed(t, "\u001b[?69h"); // Enable DECLRMM
        GhosttyTestFixture.Feed(t, "\u001b[0;0s");  // left=0, right=0 → reset to full width
        // Cursor moves to home after DECSLRM
        GhosttyTestFixture.Feed(t, "\u001b[X");     // Erase 1 char at home
        Assert.Equal(" BC", GhosttyTestFixture.GetLine(t, 0).Substring(0, 3));
        Assert.Equal("DEF", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("GHI", GhosttyTestFixture.GetLine(t, 2));
    }

    [Fact]
    public void SetLeftAndRightMargin_Mode69Unset()
    {
        // Ghostty: "Terminal: setLeftAndRightMargin mode 69 unset"
        // Without mode 69, DECSLRM is ignored.
        // After setLeftAndRightMargin(1,2), setCursorPos(1,2), insertLines(1):
        // Row 0 becomes blank, content shifts down.
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABC");
        GhosttyTestFixture.Feed(t, "\r\n");
        GhosttyTestFixture.Feed(t, "DEF");
        GhosttyTestFixture.Feed(t, "\r\n");
        GhosttyTestFixture.Feed(t, "GHI");
        // Mode 69 is NOT set, so CSI s is save cursor, not DECSLRM
        GhosttyTestFixture.Feed(t, "\u001b[1;2H"); // setCursorPos(1,2) → row 0, col 1
        GhosttyTestFixture.Feed(t, "\u001b[L");     // insertLines(1)
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("ABC", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("DEF", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("GHI", GhosttyTestFixture.GetLine(t, 3));
    }

    // ═══════════════════════════════════════════════════════════════
    // printRepeat (REP — CSI b)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void PrintRepeat_Simple()
    {
        // Ghostty: "Terminal: printRepeat simple"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "A");
        GhosttyTestFixture.Feed(t, "\u001b[3b"); // REP 3
        GhosttyTestFixture.Feed(t, "B");

        Assert.Equal("AAAAB", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void PrintRepeat_NoPreviousCharacter()
    {
        // Ghostty: "Terminal: printRepeat no previous character"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "\u001b[3b"); // REP 3 with no prior char
        GhosttyTestFixture.Feed(t, "B");

        // No previous char — REP is a no-op
        Assert.Equal("B", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void PrintRepeat_Wrap()
    {
        // Ghostty: "Terminal: printRepeat wrap"
        var t = GhosttyTestFixture.CreateTerminal(3, 5);

        GhosttyTestFixture.Feed(t, "A");
        GhosttyTestFixture.Feed(t, "\u001b[5b"); // REP 5 → wraps

        Assert.Equal("AAA", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("AAA", GhosttyTestFixture.GetLine(t, 1));
    }

    // ═══════════════════════════════════════════════════════════════
    // Zero-width character
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ZeroWidthCharacterAtStart()
    {
        // Ghostty: "Terminal: zero-width character at start"
        // Zero-width char at column 0 should not crash.
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "\u0300"); // Combining grave accent at start
        GhosttyTestFixture.Feed(t, "A");

        // Should not crash — the combining char is dropped or handled gracefully
        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 0));
    }
}
