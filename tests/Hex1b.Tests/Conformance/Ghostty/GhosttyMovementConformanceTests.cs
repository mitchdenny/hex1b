using Xunit;

namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Tests for cursor movement conformance.
/// Translated from Ghostty's Terminal.zig cursor tests.
/// </summary>
[Trait("Category", "GhosttyConformance")]
public class GhosttyMovementConformanceTests
{
    // ═══════════════════════════════════════════════════════════════
    // Carriage Return
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void CarriageReturn_UnsetsPendingWrap()
    {
        // Ghostty: "Terminal: carriage return unsets pending wrap"
        var t = GhosttyTestFixture.CreateTerminal(5, 80);

        GhosttyTestFixture.Feed(t, "hello");
        Assert.True(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "\r");
        Assert.False(t.PendingWrap);
    }

    [Fact]
    public void CarriageReturn_OriginMode_MovesToLeftMargin()
    {
        // Ghostty: "Terminal: carriage return origin mode moves to left margin"
        // In origin mode with left margin, CR goes to left margin.
        var t = GhosttyTestFixture.CreateTerminal(5, 80);

        // Enable DECLRMM (mode 69) + set left margin to 3 (1-based col 3 = index 2)
        GhosttyTestFixture.Feed(t, "\u001b[?69h");      // Enable DECLRMM
        GhosttyTestFixture.Feed(t, "\u001b[3;5s");       // Set left=3, right=5
        GhosttyTestFixture.Feed(t, "\u001b[?6h");        // Enable origin mode
        GhosttyTestFixture.Feed(t, "\u001b[1;1H");       // Move to origin (which is now at left margin)
        GhosttyTestFixture.Feed(t, "\r");
        Assert.Equal(2, t.CursorX); // Left margin at column 2 (0-based)
    }

    [Fact]
    public void CarriageReturn_LeftOfLeftMargin_MovesToZero()
    {
        // Ghostty: "Terminal: carriage return left of left margin moves to zero"
        // When cursor is left of left margin, CR goes to column 0.
        var t = GhosttyTestFixture.CreateTerminal(5, 80);

        GhosttyTestFixture.Feed(t, "\u001b[?69h");      // Enable DECLRMM
        GhosttyTestFixture.Feed(t, "\u001b[3;5s");       // Set left=3, right=5
        // Cursor starts at 0, which is left of margin (2)
        GhosttyTestFixture.Feed(t, "\u001b[1;2H");       // Move to col 2 (0-based: 1), left of margin
        GhosttyTestFixture.Feed(t, "\r");
        Assert.Equal(0, t.CursorX);
    }

    [Fact]
    public void CarriageReturn_RightOfLeftMargin_MovesToLeftMargin()
    {
        // Ghostty: "Terminal: carriage return right of left margin moves to left margin"
        // When cursor is right of left margin, CR goes to left margin.
        var t = GhosttyTestFixture.CreateTerminal(5, 80);

        GhosttyTestFixture.Feed(t, "\u001b[?69h");      // Enable DECLRMM
        GhosttyTestFixture.Feed(t, "\u001b[3;5s");       // Set left=3, right=5
        GhosttyTestFixture.Feed(t, "\u001b[1;4H");       // Move to col 4 (0-based: 3), right of margin
        GhosttyTestFixture.Feed(t, "\r");
        Assert.Equal(2, t.CursorX); // Left margin at column 2 (0-based)
    }

    // ═══════════════════════════════════════════════════════════════
    // Backspace
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Backspace_Basic()
    {
        // Ghostty: "Terminal: backspace"
        var t = GhosttyTestFixture.CreateTerminal(80, 80);

        GhosttyTestFixture.Feed(t, "hello");
        GhosttyTestFixture.Feed(t, "\b"); // BS
        GhosttyTestFixture.Feed(t, "y");
        Assert.Equal(0, t.CursorY);
        Assert.Equal(5, t.CursorX);
        Assert.Equal("helly", GhosttyTestFixture.GetLine(t, 0));
    }

    // ═══════════════════════════════════════════════════════════════
    // cursorPos (CUP — CSI H)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void CursorPos_ResetsWrap()
    {
        // Ghostty: "Terminal: cursorPos resets wrap"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABCDE");
        Assert.True(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "\u001b[1;1H");
        Assert.False(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("XBCDE", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void CursorPos_OffTheScreen()
    {
        // Ghostty: "Terminal: cursorPos off the screen"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "\u001b[500;500H");
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 3));
        Assert.Equal("    X", GhosttyTestFixture.GetLine(t, 4));
    }

    [Fact]
    public void CursorPos_RelativeToOrigin()
    {
        // Ghostty: "Terminal: cursorPos relative to origin"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "\u001b[3;4r");  // Set scroll region top=3, bottom=4
        GhosttyTestFixture.Feed(t, "\u001b[?6h");   // Enable origin mode
        GhosttyTestFixture.Feed(t, "\u001b[1;1H");  // CUP(1,1) — relative to origin
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("X", GhosttyTestFixture.GetLine(t, 2));
    }

    [Fact]
    public void CursorPos_RelativeToOriginWithLeftRight()
    {
        // Ghostty: "Terminal: cursorPos relative to origin with left/right"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "\u001b[3;4r");    // Set scroll region top=3, bottom=4
        GhosttyTestFixture.Feed(t, "\u001b[?69h");    // Enable DECLRMM
        GhosttyTestFixture.Feed(t, "\u001b[3;5s");    // Set left=3, right=5
        GhosttyTestFixture.Feed(t, "\u001b[?6h");     // Enable origin mode
        GhosttyTestFixture.Feed(t, "\u001b[1;1H");    // CUP(1,1) — relative to origin
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("  X", GhosttyTestFixture.GetLine(t, 2));
    }

    [Fact]
    public void CursorPos_LimitsWithFullScrollRegion()
    {
        // Ghostty: "Terminal: cursorPos limits with full scroll region"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "\u001b[3;4r");    // Set scroll region top=3, bottom=4
        GhosttyTestFixture.Feed(t, "\u001b[?69h");    // Enable DECLRMM
        GhosttyTestFixture.Feed(t, "\u001b[3;5s");    // Set left=3, right=5
        GhosttyTestFixture.Feed(t, "\u001b[?6h");     // Enable origin mode
        GhosttyTestFixture.Feed(t, "\u001b[500;500H"); // CUP beyond limits
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("    X", GhosttyTestFixture.GetLine(t, 3));
    }

    [Fact]
    public void SetCursorPos_Original()
    {
        // Ghostty: "Terminal: setCursorPos (original test)"
        var t = GhosttyTestFixture.CreateTerminal(80, 80);

        Assert.Equal(0, t.CursorX);
        Assert.Equal(0, t.CursorY);

        // Setting to 0 should keep it at 0 (1-based, 0 treated as 1)
        GhosttyTestFixture.Feed(t, "\u001b[0;0H");
        Assert.Equal(0, t.CursorX);
        Assert.Equal(0, t.CursorY);

        // Should clamp to size
        GhosttyTestFixture.Feed(t, "\u001b[81;81H");
        Assert.Equal(79, t.CursorX);
        Assert.Equal(79, t.CursorY);

        // Should reset pending wrap
        GhosttyTestFixture.Feed(t, "\u001b[1;80H"); // Row 1, Col 80 (last col)
        GhosttyTestFixture.Feed(t, "c");
        Assert.True(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "\u001b[1;80H");
        Assert.False(t.PendingWrap);
    }

    // ═══════════════════════════════════════════════════════════════
    // cursorUp (CUU — CSI A)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void CursorUp_Basic()
    {
        // Ghostty: "Terminal: cursorUp basic"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "\u001b[3;1H"); // setCursorPos(3,1)
        GhosttyTestFixture.Feed(t, "A");
        GhosttyTestFixture.Feed(t, "\u001b[10A");  // cursorUp(10)
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal(" X", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 2));
    }

    [Fact]
    public void CursorUp_BelowTopScrollMargin()
    {
        // Ghostty: "Terminal: cursorUp below top scroll margin"
        // Cursor stops at top margin if inside scroll region.
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "\u001b[2;4r");  // setTopAndBottomMargin(2, 4)
        GhosttyTestFixture.Feed(t, "\u001b[3;1H");  // setCursorPos(3,1)
        GhosttyTestFixture.Feed(t, "A");
        GhosttyTestFixture.Feed(t, "\u001b[5A");    // cursorUp(5) — clamps to top margin
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal(" X", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 2));
    }

    [Fact]
    public void CursorUp_AboveTopScrollMargin()
    {
        // Ghostty: "Terminal: cursorUp above top scroll margin"
        // Cursor already above scroll region can go to row 0.
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "\u001b[3;5r");  // setTopAndBottomMargin(3, 5)
        GhosttyTestFixture.Feed(t, "\u001b[3;1H");  // setCursorPos(3,1)
        GhosttyTestFixture.Feed(t, "A");
        GhosttyTestFixture.Feed(t, "\u001b[2;1H");  // setCursorPos(2,1) — above scroll region
        GhosttyTestFixture.Feed(t, "\u001b[10A");   // cursorUp(10)
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("X", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 2));
    }

    [Fact]
    public void CursorUp_ResetsWrap()
    {
        // Ghostty: "Terminal: cursorUp resets wrap"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABCDE");
        Assert.True(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "\u001b[1A"); // cursorUp(1)
        Assert.False(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("ABCDX", GhosttyTestFixture.GetLine(t, 0));
    }

    // ═══════════════════════════════════════════════════════════════
    // cursorDown (CUD — CSI B)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void CursorDown_Basic()
    {
        // Ghostty: "Terminal: cursorDown basic"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "A");
        GhosttyTestFixture.Feed(t, "\u001b[10B"); // cursorDown(10)
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 3));
        Assert.Equal(" X", GhosttyTestFixture.GetLine(t, 4));
    }

    [Fact]
    public void CursorDown_AboveBottomScrollMargin()
    {
        // Ghostty: "Terminal: cursorDown above bottom scroll margin"
        // Cursor inside scroll region stops at bottom margin.
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "\u001b[1;3r"); // setTopAndBottomMargin(1, 3)
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // Home
        GhosttyTestFixture.Feed(t, "A");
        GhosttyTestFixture.Feed(t, "\u001b[10B");  // cursorDown(10)
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal(" X", GhosttyTestFixture.GetLine(t, 2));
    }

    [Fact]
    public void CursorDown_BelowBottomScrollMargin()
    {
        // Ghostty: "Terminal: cursorDown below bottom scroll margin"
        // Cursor below scroll region can go to bottom of screen.
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "\u001b[1;3r"); // setTopAndBottomMargin(1, 3)
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // Home
        GhosttyTestFixture.Feed(t, "A");
        GhosttyTestFixture.Feed(t, "\u001b[4;1H"); // setCursorPos(4,1) — below scroll region
        GhosttyTestFixture.Feed(t, "\u001b[10B");  // cursorDown(10)
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 3));
        Assert.Equal("X", GhosttyTestFixture.GetLine(t, 4));
    }

    [Fact]
    public void CursorDown_ResetsWrap()
    {
        // Ghostty: "Terminal: cursorDown resets wrap"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABCDE");
        Assert.True(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "\u001b[1B"); // cursorDown(1)
        Assert.False(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("ABCDE", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("    X", GhosttyTestFixture.GetLine(t, 1));
    }

    // ═══════════════════════════════════════════════════════════════
    // cursorLeft (CUB — CSI D) — non-reverse-wrap tests only
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void CursorLeft_NoWrap()
    {
        // Ghostty: "Terminal: cursorLeft no wrap"
        var t = GhosttyTestFixture.CreateTerminal(10, 5);

        GhosttyTestFixture.Feed(t, "A");
        GhosttyTestFixture.Feed(t, "\r\n");
        GhosttyTestFixture.Feed(t, "B");
        GhosttyTestFixture.Feed(t, "\u001b[10D"); // cursorLeft(10)

        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("B", GhosttyTestFixture.GetLine(t, 1));
    }

    [Fact]
    public void CursorLeft_UnsetsPendingWrapState()
    {
        // Ghostty: "Terminal: cursorLeft unsets pending wrap state"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABCDE");
        Assert.True(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "\u001b[1D"); // cursorLeft(1)
        Assert.False(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("ABCXE", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void CursorLeft_UnsetsPendingWrapStateWithLongerJump()
    {
        // Ghostty: "Terminal: cursorLeft unsets pending wrap state with longer jump"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABCDE");
        Assert.True(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "\u001b[3D"); // cursorLeft(3)
        Assert.False(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("AXCDE", GhosttyTestFixture.GetLine(t, 0));
    }

    // ═══════════════════════════════════════════════════════════════
    // cursorRight (CUF — CSI C)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void CursorRight_ResetsWrap()
    {
        // Ghostty: "Terminal: cursorRight resets wrap"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "ABCDE");
        Assert.True(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "\u001b[1C"); // cursorRight(1)
        Assert.False(t.PendingWrap);
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("ABCDX", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void CursorRight_ToEdgeOfScreen()
    {
        // Ghostty: "Terminal: cursorRight to the edge of screen"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "\u001b[100C"); // cursorRight(100)
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("    X", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void CursorRight_LeftOfRightMargin()
    {
        // Ghostty: "Terminal: cursorRight left of right margin"
        // When cursor is left of right margin, CUF stops at right margin.
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "\u001b[?69h");    // Enable DECLRMM
        GhosttyTestFixture.Feed(t, "\u001b[1;3s");    // Set left=1, right=3
        GhosttyTestFixture.Feed(t, "\u001b[100C");    // cursorRight(100)
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("  X", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void CursorRight_RightOfRightMargin()
    {
        // Ghostty: "Terminal: cursorRight right of right margin"
        // When cursor is right of right margin, CUF goes to right edge of screen.
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "\u001b[?69h");    // Enable DECLRMM
        GhosttyTestFixture.Feed(t, "\u001b[1;3s");    // Set left=1, right=3
        GhosttyTestFixture.Feed(t, "\u001b[1;4H");    // setCursorPos(1,4) — right of margin
        GhosttyTestFixture.Feed(t, "\u001b[100C");    // cursorRight(100)
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("    X", GhosttyTestFixture.GetLine(t, 0));
    }
}
