using System;
using Hex1b;
using Xunit;

namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Ghostty conformance tests for reverse wrap (DECSET 45) and
/// extended reverse wrap (DECSET 1045 / XTREVWRAP2).
/// </summary>
[Trait("Category", "GhosttyConformance")]
public class GhosttyReverseWrapConformanceTests
{
    private static Hex1bTerminal CreateTerminal(int cols, int rows) => GhosttyTestFixture.CreateTerminal(cols, rows);
    private static void Feed(Hex1bTerminal t, string s) => GhosttyTestFixture.Feed(t, s);
    private static string GetLine(Hex1bTerminal t, int row) => GhosttyTestFixture.GetLine(t, row);
    private static TerminalCell GetCell(Hex1bTerminal t, int row, int col) => GhosttyTestFixture.GetCell(t, row, col);

    #region Reverse Wrap (Mode 45)

    // Ghostty: "Terminal: cursorLeft reverse wrap with pending wrap state"
    // When pending wrap is set and reverse wrap is enabled, CUB 1 just
    // clears the pending wrap without moving the cursor.
    [Fact]
    public void CursorLeft_ReverseWrap_PendingWrapState()
    {
        using var t = CreateTerminal(cols: 5, rows: 5);
        Feed(t, "\u001b[?7h");  // Enable DECAWM (wraparound)
        Feed(t, "\u001b[?45h"); // Enable reverse wrap

        Feed(t, "ABCDE"); // Fill row, pending wrap set
        Assert.True(t.PendingWrap);

        Feed(t, "\u001b[D"); // CUB 1 — clears pending wrap
        Assert.False(t.PendingWrap);

        Feed(t, "X"); // Overwrites last char
        Assert.Equal("ABCDX", GetLine(t, 0));
    }

    // Ghostty: "Terminal: cursorLeft reverse wrap extended with pending wrap state"
    [Fact]
    public void CursorLeft_ReverseWrapExtended_PendingWrapState()
    {
        using var t = CreateTerminal(cols: 5, rows: 5);
        Feed(t, "\u001b[?7h");    // Enable DECAWM
        Feed(t, "\u001b[?1045h"); // Enable extended reverse wrap

        Feed(t, "ABCDE"); // Fill row, pending wrap set
        Assert.True(t.PendingWrap);

        Feed(t, "\u001b[D"); // CUB 1 — clears pending wrap
        Assert.False(t.PendingWrap);

        Feed(t, "X"); // Overwrites last char
        Assert.Equal("ABCDX", GetLine(t, 0));
    }

    // Ghostty: "Terminal: cursorLeft reverse wrap"
    // CUB wraps to previous line's right margin when reverse wrap is enabled
    // and the previous line was soft-wrapped.
    [Fact]
    public void CursorLeft_ReverseWrap()
    {
        using var t = CreateTerminal(cols: 5, rows: 5);
        Feed(t, "\u001b[?7h");  // Enable DECAWM
        Feed(t, "\u001b[?45h"); // Enable reverse wrap

        Feed(t, "ABCDE1"); // Wraps: "ABCDE" on row 0 (soft-wrapped), "1" on row 1
        Feed(t, "\u001b[2D"); // CUB 2: from col 1 → col 0 → wrap to row 0, col 4

        Feed(t, "X"); // Overwrites 'E' at row 0, col 4
        Assert.True(t.PendingWrap); // Now at col 4 with pending wrap

        Assert.Equal("ABCDX", GetLine(t, 0));
        Assert.Equal("1", GetLine(t, 1).TrimEnd());
    }

    // Ghostty: "Terminal: cursorLeft reverse wrap with no soft wrap"
    // When the previous line was NOT soft-wrapped (i.e., hard newline),
    // mode 45 reverse wrap does NOT cross the boundary.
    [Fact]
    public void CursorLeft_ReverseWrap_NoSoftWrap()
    {
        using var t = CreateTerminal(cols: 5, rows: 5);
        Feed(t, "\u001b[?7h");  // Enable DECAWM
        Feed(t, "\u001b[?45h"); // Enable reverse wrap

        Feed(t, "ABCDE"); // Fill row 0
        Feed(t, "\r\n");  // Hard newline to row 1 — row 0 is NOT soft-wrapped
        Feed(t, "1");

        Feed(t, "\u001b[2D"); // CUB 2: from col 1 → col 0 → can't wrap (no soft wrap) → stays col 0

        Feed(t, "X"); // Overwrites at row 1, col 0
        Assert.Equal("ABCDE", GetLine(t, 0));
        Assert.Equal("X", GetLine(t, 1).TrimEnd());
    }

    // Ghostty: "Terminal: cursorLeft reverse wrap before left margin"
    // When cursor is within a scroll region and reverse wrap is enabled,
    // it should stop at the top of the scroll region.
    [Fact]
    public void CursorLeft_ReverseWrap_BeforeLeftMargin()
    {
        using var t = CreateTerminal(cols: 5, rows: 5);
        Feed(t, "\u001b[?7h");  // Enable DECAWM
        Feed(t, "\u001b[?45h"); // Enable reverse wrap

        Feed(t, "\u001b[3;5r"); // DECSTBM: scroll region rows 3-5 (0-indexed: 2-4)
        // Cursor starts at row 0, col 0 (outside scroll region top)
        Feed(t, "\u001b[D"); // CUB 1: at col 0 already, try to wrap → stops at (0,0)

        Feed(t, "X");
        // Should print at row 2 (top of scroll region) col 0
        // Wait — Ghostty's test says: cursor starts at 0,0 after DECSTBM.
        // DECSTBM homes cursor. So cursor is at 0,0.
        // CUB 1 at col 0 with no soft wrap → stays at 0,0.
        // print 'X' → at col 0, row 0
        Assert.Equal("X", GetLine(t, 2).TrimEnd());
    }

    // Ghostty: "Terminal: cursorLeft reverse wrap on first row"
    // When at the first row of the scroll region, reverse wrap stops.
    [Fact]
    public void CursorLeft_ReverseWrap_FirstRow()
    {
        using var t = CreateTerminal(cols: 5, rows: 5);
        Feed(t, "\u001b[?7h");  // Enable DECAWM
        Feed(t, "\u001b[?45h"); // Enable reverse wrap

        Feed(t, "\u001b[3;5r");  // DECSTBM: scroll region rows 3-5
        Feed(t, "\u001b[1;2H");  // CUP: row 1, col 2 (0-indexed: row 0, col 1)
        Feed(t, "\u001b[1000D"); // CUB 1000: massive left move, should clamp

        Assert.Equal(0, t.CursorX);
        Assert.Equal(0, t.CursorY);
    }

    #endregion

    #region Extended Reverse Wrap (Mode 1045)

    // Ghostty: "Terminal: cursorLeft extended reverse wrap"
    // Extended mode wraps across hard line boundaries (unlike mode 45).
    [Fact]
    public void CursorLeft_ExtendedReverseWrap()
    {
        using var t = CreateTerminal(cols: 5, rows: 5);
        Feed(t, "\u001b[?7h");    // Enable DECAWM
        Feed(t, "\u001b[?1045h"); // Enable extended reverse wrap

        Feed(t, "ABCDE"); // Fill row 0
        Feed(t, "\r\n");  // Hard newline — row 0 NOT soft-wrapped
        Feed(t, "1");

        Feed(t, "\u001b[2D"); // CUB 2: col 1 → col 0 → wraps to row 0, col 4 (even without soft wrap!)

        Feed(t, "X"); // Overwrites 'E' at row 0, col 4
        Assert.Equal("ABCDX", GetLine(t, 0));
        Assert.Equal("1", GetLine(t, 1).TrimEnd());
    }

    // Ghostty: "Terminal: cursorLeft extended reverse wrap bottom wraparound"
    // Extended mode wraps from the top of the scroll region to the bottom.
    [Fact]
    public void CursorLeft_ExtendedReverseWrap_BottomWraparound()
    {
        using var t = CreateTerminal(cols: 5, rows: 3);
        Feed(t, "\u001b[?7h");    // Enable DECAWM
        Feed(t, "\u001b[?1045h"); // Enable extended reverse wrap

        Feed(t, "ABCDE"); // Fill row 0 (soft-wrapped to row 1)
        Feed(t, "\r\n");  // Hard newline
        Feed(t, "1");

        // CUB by 1 + cols + 1 = 7: from (1,1) → (1,0) → (0,4) → ... → wraps to bottom
        Feed(t, "\u001b[7D");

        Feed(t, "X");
        Assert.Equal("ABCDE", GetLine(t, 0));
        Assert.Equal("1", GetLine(t, 1).TrimEnd());
        Assert.Equal("    X", GetLine(t, 2));
    }

    // Ghostty: "Terminal: cursorLeft extended reverse wrap is priority if both set"
    // When both mode 45 and 1045 are set, 1045 (extended) takes priority.
    [Fact]
    public void CursorLeft_ExtendedReverseWrap_PriorityOverMode45()
    {
        using var t = CreateTerminal(cols: 5, rows: 3);
        Feed(t, "\u001b[?7h");    // Enable DECAWM
        Feed(t, "\u001b[?45h");   // Enable reverse wrap
        Feed(t, "\u001b[?1045h"); // Enable extended reverse wrap (takes priority)

        Feed(t, "ABCDE"); // Fill row 0
        Feed(t, "\r\n");
        Feed(t, "1");

        Feed(t, "\u001b[7D"); // CUB 7: same as bottom wraparound test

        Feed(t, "X");
        Assert.Equal("ABCDE", GetLine(t, 0));
        Assert.Equal("1", GetLine(t, 1).TrimEnd());
        Assert.Equal("    X", GetLine(t, 2));
    }

    // Ghostty: "Terminal: cursorLeft extended reverse wrap above top scroll region"
    // In extended mode, massive CUB above scroll region stops at (0, 0).
    [Fact]
    public void CursorLeft_ExtendedReverseWrap_AboveTopScrollRegion()
    {
        using var t = CreateTerminal(cols: 5, rows: 5);
        Feed(t, "\u001b[?7h");    // Enable DECAWM
        Feed(t, "\u001b[?1045h"); // Enable extended reverse wrap

        Feed(t, "\u001b[3;5r");   // DECSTBM: scroll region rows 3-5
        Feed(t, "\u001b[2;1H");   // CUP: row 2, col 1 (0-indexed: row 1, col 0)
        Feed(t, "\u001b[1000D");  // CUB 1000

        Assert.Equal(0, t.CursorX);
        Assert.Equal(0, t.CursorY);
    }

    #endregion
}
