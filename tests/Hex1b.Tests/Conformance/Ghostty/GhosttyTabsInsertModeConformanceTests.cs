using Xunit;

namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Conformance tests for horizontal tab (HT), backward tab (CBT),
/// and insert mode (IRM) behavior.
/// Translated from Ghostty's Terminal.zig.
/// </summary>
[Trait("Category", "GhosttyConformance")]
public class GhosttyTabsInsertModeConformanceTests
{
    private static Hex1bTerminal CreateTerminal(int cols = 80, int rows = 24)
        => GhosttyTestFixture.CreateTerminal(cols, rows);

    #region Horizontal Tabs (HT)

    [Fact]
    public void HorizontalTabs_Basic()
    {
        // Ghostty: "Terminal: horizontal tabs"
        using var t = CreateTerminal(20, 5);

        GhosttyTestFixture.Feed(t, "1");
        GhosttyTestFixture.Feed(t, "\t");
        Assert.Equal(8, t.CursorX);

        GhosttyTestFixture.Feed(t, "\t");
        Assert.Equal(16, t.CursorX);

        // HT at the end
        GhosttyTestFixture.Feed(t, "\t");
        Assert.Equal(19, t.CursorX);
        GhosttyTestFixture.Feed(t, "\t");
        Assert.Equal(19, t.CursorX);
    }

    [Fact]
    public void HorizontalTabs_StartingOnTabStop()
    {
        // Ghostty: "Terminal: horizontal tabs starting on tabstop"
        using var t = CreateTerminal(20, 5);

        GhosttyTestFixture.Feed(t, "\u001b[1;9H"); // setCursorPos(y, 9) — col 8 (0-based)
        GhosttyTestFixture.Feed(t, "X");
        GhosttyTestFixture.Feed(t, "\u001b[1;9H"); // back to col 8
        GhosttyTestFixture.Feed(t, "\t");
        GhosttyTestFixture.Feed(t, "A");

        Assert.Equal("        X       A", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void HorizontalTabs_WithRightMargin()
    {
        // Ghostty: "Terminal: horizontal tabs with right margin"
        using var t = CreateTerminal(20, 5);

        // Set left/right margins: left=2(0-based)=3(1-based), right=5(0-based)=6(1-based)
        GhosttyTestFixture.Feed(t, "\u001b[?69h");  // enable DECLRMM
        GhosttyTestFixture.Feed(t, "\u001b[3;6s");   // DECSLRM(3,6)
        GhosttyTestFixture.Feed(t, "\u001b[1;1H");   // setCursorPos(1,1) — col 0
        GhosttyTestFixture.Feed(t, "X");
        GhosttyTestFixture.Feed(t, "\t");
        GhosttyTestFixture.Feed(t, "A");

        Assert.Equal("X    A", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void HorizontalTabsBack_Basic()
    {
        // Ghostty: "Terminal: horizontal tabs back"
        using var t = CreateTerminal(20, 5);

        GhosttyTestFixture.Feed(t, "\u001b[1;20H"); // setCursorPos(y, 20) — col 19

        GhosttyTestFixture.Feed(t, "\u001b[Z"); // CBT
        Assert.Equal(16, t.CursorX);

        GhosttyTestFixture.Feed(t, "\u001b[Z"); // CBT
        Assert.Equal(8, t.CursorX);

        GhosttyTestFixture.Feed(t, "\u001b[Z"); // CBT
        Assert.Equal(0, t.CursorX);
        GhosttyTestFixture.Feed(t, "\u001b[Z"); // CBT again at start
        Assert.Equal(0, t.CursorX);
    }

    [Fact]
    public void HorizontalTabsBack_StartingOnTabStop()
    {
        // Ghostty: "Terminal: horizontal tabs back starting on tabstop"
        using var t = CreateTerminal(20, 5);

        GhosttyTestFixture.Feed(t, "\u001b[1;9H"); // setCursorPos(y, 9) — col 8 (tab stop)
        GhosttyTestFixture.Feed(t, "X");
        GhosttyTestFixture.Feed(t, "\u001b[1;9H"); // back to col 8
        GhosttyTestFixture.Feed(t, "\u001b[Z");     // CBT — back to col 0
        GhosttyTestFixture.Feed(t, "A");

        Assert.Equal("A       X", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact(Skip = "Hex1b CBT does not clamp to left margin in origin mode")]
    [Trait("FailureReason", "Bug")]
    public void HorizontalTabsBack_WithLeftMarginInOriginMode()
    {
        // Ghostty: "Terminal: horizontal tabs with left margin in origin mode"
        using var t = CreateTerminal(20, 5);

        // Enable origin mode and set left/right margins
        GhosttyTestFixture.Feed(t, "\u001b[?69h");  // enable DECLRMM
        GhosttyTestFixture.Feed(t, "\u001b[3;6s");   // DECSLRM(3,6) — left=2, right=5 (0-based)
        GhosttyTestFixture.Feed(t, "\u001b[?6h");    // enable origin mode (DECOM)
        // In origin mode, CUP(1,2) → row 0, col = left_margin + 1 = col 3 (absolute)
        GhosttyTestFixture.Feed(t, "\u001b[1;2H");
        GhosttyTestFixture.Feed(t, "X");
        GhosttyTestFixture.Feed(t, "\u001b[Z");      // CBT — back to left margin (col 2)
        GhosttyTestFixture.Feed(t, "A");

        Assert.Equal("  AX", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void HorizontalTabBack_CursorBeforeLeftMargin()
    {
        // Ghostty: "Terminal: horizontal tab back with cursor before left margin"
        using var t = CreateTerminal(20, 5);

        GhosttyTestFixture.Feed(t, "\u001b[?6h");    // enable origin mode (DECOM)
        GhosttyTestFixture.Feed(t, "\u001b7");        // DECSC — save cursor
        GhosttyTestFixture.Feed(t, "\u001b[?69h");    // enable DECLRMM
        GhosttyTestFixture.Feed(t, "\u001b[5;0s");    // DECSLRM(5, 0) — left=5(1-based), right=default
        GhosttyTestFixture.Feed(t, "\u001b8");        // DECRC — restore cursor (col 0, before left margin)
        GhosttyTestFixture.Feed(t, "\u001b[Z");       // CBT
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("X", GhosttyTestFixture.GetLine(t, 0));
    }

    #endregion

    #region Insert Mode (IRM)

    [Fact]
    public void InsertMode_WithSpace()
    {
        // Ghostty: "Terminal: insert mode with space"
        using var t = CreateTerminal(10, 2);

        GhosttyTestFixture.Feed(t, "hello");
        GhosttyTestFixture.Feed(t, "\u001b[1;2H");  // setCursorPos(1, 2) — col 1 (0-based)
        GhosttyTestFixture.Feed(t, "\u001b[4h");     // enable IRM
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("hXello", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void InsertMode_DoesNotWrapPushedCharacters()
    {
        // Ghostty: "Terminal: insert mode doesn't wrap pushed characters"
        using var t = CreateTerminal(5, 2);

        GhosttyTestFixture.Feed(t, "hello");
        GhosttyTestFixture.Feed(t, "\u001b[1;2H");  // setCursorPos(1, 2) — col 1
        GhosttyTestFixture.Feed(t, "\u001b[4h");     // enable IRM
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("hXell", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void InsertMode_DoesNothingAtEndOfLine()
    {
        // Ghostty: "Terminal: insert mode does nothing at the end of the line"
        using var t = CreateTerminal(5, 2);

        GhosttyTestFixture.Feed(t, "hello");
        GhosttyTestFixture.Feed(t, "\u001b[4h");     // enable IRM
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("hello", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("X", GhosttyTestFixture.GetLine(t, 1));
    }

    [Fact]
    public void InsertMode_WithWideCharacters()
    {
        // Ghostty: "Terminal: insert mode with wide characters"
        using var t = CreateTerminal(5, 2);

        GhosttyTestFixture.Feed(t, "hello");
        GhosttyTestFixture.Feed(t, "\u001b[1;2H");  // setCursorPos(1, 2) — col 1
        GhosttyTestFixture.Feed(t, "\u001b[4h");     // enable IRM
        GhosttyTestFixture.Feed(t, "\U0001F600");     // 😀

        Assert.Equal("h😀el", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void InsertMode_WideCharAtEnd()
    {
        // Ghostty: "Terminal: insert mode with wide characters at end"
        using var t = CreateTerminal(5, 2);

        GhosttyTestFixture.Feed(t, "well");
        GhosttyTestFixture.Feed(t, "\u001b[4h");     // enable IRM
        GhosttyTestFixture.Feed(t, "\U0001F600");     // 😀

        Assert.Equal("well", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("😀", GhosttyTestFixture.GetLine(t, 1));
    }

    [Fact]
    public void InsertMode_PushingOffWideCharacter()
    {
        // Ghostty: "Terminal: insert mode pushing off wide character"
        using var t = CreateTerminal(5, 2);

        GhosttyTestFixture.Feed(t, "123");
        GhosttyTestFixture.Feed(t, "\U0001F600");     // 😀 at cols 3-4
        GhosttyTestFixture.Feed(t, "\u001b[4h");      // enable IRM
        GhosttyTestFixture.Feed(t, "\u001b[1;1H");    // setCursorPos(1, 1) — col 0
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("X123", GhosttyTestFixture.GetLine(t, 0));
    }

    #endregion
}
