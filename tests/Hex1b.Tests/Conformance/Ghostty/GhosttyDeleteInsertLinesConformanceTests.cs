using Xunit;

namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Conformance tests for deleteLines (DL / CSI M) and insertLines (IL / CSI L),
/// translated from Ghostty's Terminal.zig.
/// </summary>
[Trait("Category", "GhosttyConformance")]
public class GhosttyDeleteInsertLinesConformanceTests
{
    private static Hex1bTerminal CreateTerminal(int cols = 80, int rows = 24)
        => GhosttyTestFixture.CreateTerminal(cols, rows);

    #region DeleteLines (DL / CSI M)

    // Ghostty: "Terminal: deleteLines simple"
    [Fact]
    public void DeleteLines_Simple()
    {
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI");
        GhosttyTestFixture.Feed(t, "\u001b[2;2H"); // setCursorPos(2, 2)
        GhosttyTestFixture.Feed(t, "\u001b[1M");    // deleteLines(1)

        Assert.Equal("ABC", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("GHI", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 2));
    }

    // Ghostty: "Terminal: deleteLines zero"
    [Fact]
    public void DeleteLines_Zero_NoOp()
    {
        using var t = CreateTerminal(cols: 2, rows: 5);
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // setCursorPos(1, 1)
        GhosttyTestFixture.Feed(t, "\u001b[0M");    // deleteLines(0)
        // Should not crash; no content to verify
    }

    // Ghostty: "Terminal: deleteLines resets pending wrap"
    [Fact]
    public void DeleteLines_ResetsPendingWrap()
    {
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABCDE");
        Assert.True(t.PendingWrap);

        GhosttyTestFixture.Feed(t, "\u001b[1M"); // deleteLines(1)
        Assert.False(t.PendingWrap);

        GhosttyTestFixture.Feed(t, "B");
        Assert.Equal("B", GhosttyTestFixture.GetLine(t, 0));
    }

    // Ghostty: "Terminal: deleteLines with scroll region"
    [Fact]
    public void DeleteLines_WithScrollRegion()
    {
        using var t = CreateTerminal(cols: 80, rows: 80);
        GhosttyTestFixture.Feed(t, "A\r\nB\r\nC\r\nD");
        GhosttyTestFixture.Feed(t, "\u001b[1;3r");  // setTopAndBottomMargin(1, 3)
        GhosttyTestFixture.Feed(t, "\u001b[1;1H");  // setCursorPos(1, 1)
        GhosttyTestFixture.Feed(t, "\u001b[1M");     // deleteLines(1)
        GhosttyTestFixture.Feed(t, "E\r\n");

        Assert.Equal("E", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("C", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("D", GhosttyTestFixture.GetLine(t, 3));
    }

    // Ghostty: "Terminal: deleteLines with scroll region, cursor outside of region"
    [Fact]
    public void DeleteLines_WithScrollRegion_CursorOutside()
    {
        using var t = CreateTerminal(cols: 80, rows: 80);
        GhosttyTestFixture.Feed(t, "A\r\nB\r\nC\r\nD");
        GhosttyTestFixture.Feed(t, "\u001b[1;3r");  // setTopAndBottomMargin(1, 3)
        GhosttyTestFixture.Feed(t, "\u001b[4;1H");  // setCursorPos(4, 1) — outside region
        GhosttyTestFixture.Feed(t, "\u001b[1M");     // deleteLines(1)

        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("B", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("C", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("D", GhosttyTestFixture.GetLine(t, 3));
    }

    // Ghostty: "Terminal: deleteLines with scroll region, large count"
    [Fact]
    public void DeleteLines_WithScrollRegion_LargeCount()
    {
        using var t = CreateTerminal(cols: 80, rows: 80);
        GhosttyTestFixture.Feed(t, "A\r\nB\r\nC\r\nD");
        GhosttyTestFixture.Feed(t, "\u001b[1;3r");  // setTopAndBottomMargin(1, 3)
        GhosttyTestFixture.Feed(t, "\u001b[1;1H");  // setCursorPos(1, 1)
        GhosttyTestFixture.Feed(t, "\u001b[5M");     // deleteLines(5)
        GhosttyTestFixture.Feed(t, "E\r\n");

        Assert.Equal("E", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("D", GhosttyTestFixture.GetLine(t, 3));
    }

    // Ghostty: "Terminal: deleteLines (legacy)"
    [Fact]
    public void DeleteLines_Legacy()
    {
        using var t = CreateTerminal(cols: 80, rows: 80);
        GhosttyTestFixture.Feed(t, "A\r\nB\r\nC\r\nD");
        GhosttyTestFixture.Feed(t, "\u001b[2A");     // cursorUp(2)
        GhosttyTestFixture.Feed(t, "\u001b[1M");     // deleteLines(1)
        GhosttyTestFixture.Feed(t, "E\r\n");

        Assert.Equal(0, t.CursorX);
        Assert.Equal(2, t.CursorY);

        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("E", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("D", GhosttyTestFixture.GetLine(t, 2));
    }

    #endregion

    #region InsertLines (IL / CSI L)

    // Ghostty: "Terminal: insertLines simple"
    [Fact]
    public void InsertLines_Simple()
    {
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI");
        GhosttyTestFixture.Feed(t, "\u001b[2;2H"); // setCursorPos(2, 2)
        GhosttyTestFixture.Feed(t, "\u001b[1L");    // insertLines(1)

        Assert.Equal("ABC", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("DEF", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("GHI", GhosttyTestFixture.GetLine(t, 3));
    }

    // Ghostty: "Terminal: insertLines zero"
    [Fact]
    public void InsertLines_Zero_NoOp()
    {
        using var t = CreateTerminal(cols: 2, rows: 5);
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // setCursorPos(1, 1)
        GhosttyTestFixture.Feed(t, "\u001b[0L");    // insertLines(0)
        // Should not crash; no content to verify
    }

    // Ghostty: "Terminal: insertLines resets pending wrap"
    [Fact]
    public void InsertLines_ResetsPendingWrap()
    {
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABCDE");
        Assert.True(t.PendingWrap);

        GhosttyTestFixture.Feed(t, "\u001b[1L"); // insertLines(1)
        Assert.False(t.PendingWrap);

        GhosttyTestFixture.Feed(t, "B");
        Assert.Equal("B", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("ABCDE", GhosttyTestFixture.GetLine(t, 1));
    }

    // Ghostty: "Terminal: insertLines with scroll region"
    [Fact]
    public void InsertLines_WithScrollRegion()
    {
        using var t = CreateTerminal(cols: 2, rows: 6);
        GhosttyTestFixture.Feed(t, "A\r\nB\r\nC\r\nD\r\nE");
        GhosttyTestFixture.Feed(t, "\u001b[1;2r");  // setTopAndBottomMargin(1, 2)
        GhosttyTestFixture.Feed(t, "\u001b[1;1H");  // setCursorPos(1, 1)
        GhosttyTestFixture.Feed(t, "\u001b[1L");     // insertLines(1)
        GhosttyTestFixture.Feed(t, "X");

        Assert.Equal("X", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("C", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("D", GhosttyTestFixture.GetLine(t, 3));
        Assert.Equal("E", GhosttyTestFixture.GetLine(t, 4));
    }

    // Ghostty: "Terminal: insertLines more than remaining"
    [Fact]
    public void InsertLines_MoreThanRemaining()
    {
        using var t = CreateTerminal(cols: 2, rows: 5);
        GhosttyTestFixture.Feed(t, "A\r\nB\r\nC\r\nD\r\nE");
        GhosttyTestFixture.Feed(t, "\u001b[2;1H");  // setCursorPos(2, 1)
        GhosttyTestFixture.Feed(t, "\u001b[20L");    // insertLines(20)

        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 3));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 4));
    }

    // Ghostty: "Terminal: insertLines outside of scroll region"
    [Fact]
    public void InsertLines_OutsideScrollRegion()
    {
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI");
        GhosttyTestFixture.Feed(t, "\u001b[3;4r");  // setTopAndBottomMargin(3, 4)
        GhosttyTestFixture.Feed(t, "\u001b[2;2H");  // setCursorPos(2, 2) — outside region
        GhosttyTestFixture.Feed(t, "\u001b[1L");     // insertLines(1)

        Assert.Equal("ABC", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("DEF", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("GHI", GhosttyTestFixture.GetLine(t, 2));
    }

    // Ghostty: "Terminal: insertLines (legacy test)"
    [Fact]
    public void InsertLines_Legacy()
    {
        using var t = CreateTerminal(cols: 2, rows: 5);
        GhosttyTestFixture.Feed(t, "A\r\nB\r\nC\r\nD\r\nE");
        GhosttyTestFixture.Feed(t, "\u001b[2;1H");  // setCursorPos(2, 1)
        GhosttyTestFixture.Feed(t, "\u001b[2L");     // insertLines(2)

        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("B", GhosttyTestFixture.GetLine(t, 3));
        Assert.Equal("C", GhosttyTestFixture.GetLine(t, 4));
    }

    // Ghostty: "Terminal: insertLines top/bottom scroll region"
    [Fact]
    public void InsertLines_TopBottomScrollRegion()
    {
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI\r\n123");
        GhosttyTestFixture.Feed(t, "\u001b[1;3r");  // setTopAndBottomMargin(1, 3)
        GhosttyTestFixture.Feed(t, "\u001b[2;2H");  // setCursorPos(2, 2)
        GhosttyTestFixture.Feed(t, "\u001b[1L");     // insertLines(1)

        Assert.Equal("ABC", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("DEF", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("123", GhosttyTestFixture.GetLine(t, 3));
    }

    #endregion
}
