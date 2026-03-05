using System;
using Hex1b;
using Xunit;

namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Ghostty conformance tests for hyperlink (OSC 8) handling.
/// Tests hyperlink attachment to cells, preservation through scrolling,
/// clearing on overwrite, and interaction with insert/delete operations.
/// </summary>
[Trait("Category", "GhosttyConformance")]
public class GhosttyHyperlinkConformanceTests
{
    private static Hex1bTerminal CreateTerminal(int cols, int rows) => GhosttyTestFixture.CreateTerminal(cols, rows);
    private static void Feed(Hex1bTerminal t, string s) => GhosttyTestFixture.Feed(t, s);
    private static string GetLine(Hex1bTerminal t, int row) => GhosttyTestFixture.GetLine(t, row);
    private static TerminalCell GetCell(Hex1bTerminal t, int row, int col) => GhosttyTestFixture.GetCell(t, row, col);

    // Helper: send OSC 8 to start a hyperlink
    private static void StartHyperlink(Hex1bTerminal t, string uri, string? param = null)
    {
        Feed(t, $"\u001b]8;{param ?? ""};{uri}\u001b\\");
    }

    // Helper: send OSC 8 to end the current hyperlink
    private static void EndHyperlink(Hex1bTerminal t)
    {
        Feed(t, "\u001b]8;;\u001b\\");
    }

    #region Basic Hyperlink Printing

    // Ghostty: "Terminal: print with hyperlink"
    [Fact]
    public void PrintWithHyperlink()
    {
        using var t = CreateTerminal(cols: 80, rows: 80);

        StartHyperlink(t, "http://example.com");
        Feed(t, "123456");

        // All 6 cells should have the hyperlink
        for (int x = 0; x < 6; x++)
        {
            var cell = GetCell(t, 0, x);
            Assert.True(cell.HasHyperlinkData, $"Cell at col {x} should have hyperlink");
            Assert.Equal("http://example.com", cell.HyperlinkData!.Uri);
        }
    }

    // Ghostty: "Terminal: print over cell with same hyperlink"
    [Fact]
    public void PrintOverCellWithSameHyperlink()
    {
        using var t = CreateTerminal(cols: 80, rows: 80);

        StartHyperlink(t, "http://example.com");
        Feed(t, "123456");
        Feed(t, "\u001b[1;1H"); // CUP(1,1) — back to start
        Feed(t, "123456"); // Overwrite with same hyperlink active

        for (int x = 0; x < 6; x++)
        {
            var cell = GetCell(t, 0, x);
            Assert.True(cell.HasHyperlinkData, $"Cell at col {x} should have hyperlink");
            Assert.Equal("http://example.com", cell.HyperlinkData!.Uri);
        }
    }

    // Ghostty: "Terminal: print and end hyperlink"
    [Fact]
    public void PrintAndEndHyperlink()
    {
        using var t = CreateTerminal(cols: 80, rows: 80);

        StartHyperlink(t, "http://example.com");
        Feed(t, "123");
        EndHyperlink(t);
        Feed(t, "456");

        // First 3 cells have hyperlink
        for (int x = 0; x < 3; x++)
        {
            var cell = GetCell(t, 0, x);
            Assert.True(cell.HasHyperlinkData, $"Cell at col {x} should have hyperlink");
            Assert.Equal("http://example.com", cell.HyperlinkData!.Uri);
        }

        // Next 3 cells do NOT have hyperlink
        for (int x = 3; x < 6; x++)
        {
            var cell = GetCell(t, 0, x);
            Assert.False(cell.HasHyperlinkData, $"Cell at col {x} should NOT have hyperlink");
        }
    }

    // Ghostty: "Terminal: print and change hyperlink"
    [Fact]
    public void PrintAndChangeHyperlink()
    {
        using var t = CreateTerminal(cols: 80, rows: 80);

        StartHyperlink(t, "http://one.example.com");
        Feed(t, "123");
        StartHyperlink(t, "http://two.example.com");
        Feed(t, "456");

        // First 3 cells have first hyperlink
        for (int x = 0; x < 3; x++)
        {
            var cell = GetCell(t, 0, x);
            Assert.True(cell.HasHyperlinkData, $"Cell at col {x} should have hyperlink");
            Assert.Equal("http://one.example.com", cell.HyperlinkData!.Uri);
        }

        // Next 3 cells have second hyperlink
        for (int x = 3; x < 6; x++)
        {
            var cell = GetCell(t, 0, x);
            Assert.True(cell.HasHyperlinkData, $"Cell at col {x} should have hyperlink");
            Assert.Equal("http://two.example.com", cell.HyperlinkData!.Uri);
        }
    }

    // Ghostty: "Terminal: overwrite hyperlink"
    // Writing non-hyperlinked text over hyperlinked cells clears the hyperlink.
    [Fact]
    public void OverwriteHyperlink()
    {
        using var t = CreateTerminal(cols: 80, rows: 80);

        StartHyperlink(t, "http://one.example.com");
        Feed(t, "123");
        Feed(t, "\u001b[1;1H"); // CUP back to start
        EndHyperlink(t);
        Feed(t, "456"); // Overwrite with no hyperlink

        // All cells should have NO hyperlink
        for (int x = 0; x < 3; x++)
        {
            var cell = GetCell(t, 0, x);
            Assert.False(cell.HasHyperlinkData, $"Cell at col {x} should NOT have hyperlink after overwrite");
        }
    }

    // Ghostty: "Terminal: print wide char at right edge with hyperlink"
    [Fact]
    public void PrintWideCharAtRightEdgeWithHyperlink()
    {
        using var t = CreateTerminal(cols: 10, rows: 5);

        StartHyperlink(t, "http://example.com");
        Feed(t, "\u001b[1;10H"); // CUP to last column

        // Print wide character 中 (U+4E2D) — wraps to next line
        Feed(t, "\u4E2D");

        // Cursor should be on row 1 after the wide char
        Assert.Equal(1, t.CursorY);
        Assert.Equal(2, t.CursorX);

        // Row 0, col 9: spacer head should have hyperlink
        var spacerHead = GetCell(t, 0, 9);
        Assert.True(spacerHead.HasHyperlinkData,
            "Spacer head at right edge should have hyperlink");

        // Row 1, col 0: the wide char should have hyperlink
        var wideCell = GetCell(t, 1, 0);
        Assert.True(wideCell.HasHyperlinkData,
            "Wide char should have hyperlink");
        Assert.Equal("http://example.com", wideCell.HyperlinkData!.Uri);

        // Row 1, col 1: spacer tail should have hyperlink
        var spacerTail = GetCell(t, 1, 1);
        Assert.True(spacerTail.HasHyperlinkData,
            "Spacer tail should have hyperlink");
    }

    #endregion

    #region Scrolling with Hyperlinks

    // Ghostty: "Terminal: scrollUp moves hyperlink"
    [Fact]
    public void ScrollUp_MovesHyperlink()
    {
        using var t = CreateTerminal(cols: 5, rows: 5);

        Feed(t, "ABC");
        Feed(t, "\r\n");
        StartHyperlink(t, "http://example.com");
        Feed(t, "DEF");
        EndHyperlink(t);
        Feed(t, "\r\n");
        Feed(t, "GHI");
        Feed(t, "\u001b[2;2H"); // CUP(2,2)
        Feed(t, "\u001b[S");     // SU 1 (scroll up)

        Assert.Equal("DEF", GetLine(t, 0).TrimEnd());
        Assert.Equal("GHI", GetLine(t, 1).TrimEnd());

        // Row 0 should have hyperlinks (the DEF row moved up)
        for (int x = 0; x < 3; x++)
        {
            var cell = GetCell(t, 0, x);
            Assert.True(cell.HasHyperlinkData,
                $"Cell at (0,{x}) should have hyperlink after scroll");
            Assert.Equal("http://example.com", cell.HyperlinkData!.Uri);
        }

        // Row 1 should NOT have hyperlinks (GHI was plain text)
        for (int x = 0; x < 3; x++)
        {
            var cell = GetCell(t, 1, x);
            Assert.False(cell.HasHyperlinkData,
                $"Cell at (1,{x}) should NOT have hyperlink");
        }
    }

    // Ghostty: "Terminal: scrollUp clears hyperlink"
    // When a row with hyperlinks scrolls off the top, the hyperlinks are cleared.
    [Fact]
    public void ScrollUp_ClearsHyperlink()
    {
        using var t = CreateTerminal(cols: 5, rows: 5);

        StartHyperlink(t, "http://example.com");
        Feed(t, "ABC");
        EndHyperlink(t);
        Feed(t, "\r\n");
        Feed(t, "DEF");
        Feed(t, "\r\n");
        Feed(t, "GHI");
        Feed(t, "\u001b[2;2H"); // CUP(2,2)
        Feed(t, "\u001b[S");     // SU 1

        Assert.Equal("DEF", GetLine(t, 0).TrimEnd());
        Assert.Equal("GHI", GetLine(t, 1).TrimEnd());

        // Row 0 (was DEF, no hyperlink) should not have hyperlinks
        for (int x = 0; x < 3; x++)
        {
            var cell = GetCell(t, 0, x);
            Assert.False(cell.HasHyperlinkData,
                $"Cell at (0,{x}) should NOT have hyperlink");
        }
    }

    // Ghostty: "Terminal: scrollDown hyperlink moves"
    [Fact]
    public void ScrollDown_HyperlinkMoves()
    {
        using var t = CreateTerminal(cols: 5, rows: 5);

        StartHyperlink(t, "http://example.com");
        Feed(t, "ABC");
        EndHyperlink(t);
        Feed(t, "\r\n");
        Feed(t, "DEF");
        Feed(t, "\r\n");
        Feed(t, "GHI");
        Feed(t, "\u001b[2;2H"); // CUP(2,2)
        Feed(t, "\u001b[T");     // SD 1 (scroll down)

        Assert.Equal("ABC", GetLine(t, 1).TrimEnd());
        Assert.Equal("DEF", GetLine(t, 2).TrimEnd());

        // Row 1 should have hyperlinks (ABC moved down)
        for (int x = 0; x < 3; x++)
        {
            var cell = GetCell(t, 1, x);
            Assert.True(cell.HasHyperlinkData,
                $"Cell at (1,{x}) should have hyperlink after scroll down");
            Assert.Equal("http://example.com", cell.HyperlinkData!.Uri);
        }

        // Row 0 (new blank row) should NOT have hyperlinks
        for (int x = 0; x < 3; x++)
        {
            var cell = GetCell(t, 0, x);
            Assert.False(cell.HasHyperlinkData,
                $"Cell at (0,{x}) should NOT have hyperlink (new blank row)");
        }
    }

    #endregion

    #region Index with Hyperlinks

    // Ghostty: "Terminal: index scrolling with hyperlink"
    [Fact]
    public void Index_ScrollingWithHyperlink()
    {
        using var t = CreateTerminal(cols: 2, rows: 5);

        Feed(t, "\u001b[5;1H"); // CUP(5,1) — row 4, col 0
        StartHyperlink(t, "http://example.com");
        Feed(t, "A");
        EndHyperlink(t);
        Feed(t, "\u001b[D"); // CUB 1 (undo right movement)
        Feed(t, "\u001bD");   // IND (index — scroll up if at bottom)
        Feed(t, "B");

        Assert.Equal("A", GetLine(t, 3).TrimEnd());
        Assert.Equal("B", GetLine(t, 4).TrimEnd());

        // Row 3, col 0 should have hyperlink (A moved up by index)
        var cellA = GetCell(t, 3, 0);
        Assert.True(cellA.HasHyperlinkData, "Cell A should have hyperlink");
        Assert.Equal("http://example.com", cellA.HyperlinkData!.Uri);

        // Row 4, col 0 should NOT have hyperlink (B printed without hyperlink)
        var cellB = GetCell(t, 4, 0);
        Assert.False(cellB.HasHyperlinkData, "Cell B should NOT have hyperlink");
    }

    // Ghostty: "Terminal: index bottom of scroll region with hyperlinks"
    [Fact]
    public void Index_BottomOfScrollRegion_WithHyperlinks()
    {
        using var t = CreateTerminal(cols: 5, rows: 5);

        Feed(t, "\u001b[1;2r"); // DECSTBM: scroll region rows 1-2
        Feed(t, "A");
        Feed(t, "\u001bD");     // IND
        Feed(t, "\r");          // CR
        StartHyperlink(t, "http://example.com");
        Feed(t, "B");
        EndHyperlink(t);
        Feed(t, "\u001bD");     // IND — scrolls within region
        Feed(t, "\r");          // CR
        Feed(t, "C");

        Assert.Equal("B", GetLine(t, 0).TrimEnd());
        Assert.Equal("C", GetLine(t, 1).TrimEnd());

        // Row 0 (B) should have hyperlink
        var cellB = GetCell(t, 0, 0);
        Assert.True(cellB.HasHyperlinkData, "Cell B should have hyperlink");
        Assert.Equal("http://example.com", cellB.HyperlinkData!.Uri);

        // Row 1 (C) should NOT have hyperlink
        var cellC = GetCell(t, 1, 0);
        Assert.False(cellC.HasHyperlinkData, "Cell C should NOT have hyperlink");
    }

    // Ghostty: "Terminal: index bottom of scroll region clear hyperlinks"
    [Fact]
    public void Index_BottomOfScrollRegion_ClearsHyperlinks()
    {
        using var t = CreateTerminal(cols: 5, rows: 5);

        Feed(t, "\u001b[2;3r");  // DECSTBM: scroll region rows 2-3
        Feed(t, "\u001b[2;1H");  // CUP(2,1)
        StartHyperlink(t, "http://example.com");
        Feed(t, "A");
        EndHyperlink(t);
        Feed(t, "\u001bD");      // IND
        Feed(t, "\r");
        Feed(t, "B");
        Feed(t, "\u001bD");      // IND — scrolls A out of region
        Feed(t, "\r");
        Feed(t, "C");

        Assert.Equal("B", GetLine(t, 1).TrimEnd());
        Assert.Equal("C", GetLine(t, 2).TrimEnd());

        // Rows 1-2 should NOT have hyperlinks (A scrolled off)
        for (int y = 1; y <= 2; y++)
        {
            var cell = GetCell(t, y, 0);
            Assert.False(cell.HasHyperlinkData,
                $"Cell at ({y},0) should NOT have hyperlink");
        }
    }

    #endregion

    #region InsertBlanks with Hyperlinks

    // Ghostty: "Terminal: insertBlanks shifts hyperlinks"
    [Fact]
    public void InsertBlanks_ShiftsHyperlinks()
    {
        using var t = CreateTerminal(cols: 10, rows: 2);

        StartHyperlink(t, "http://example.com");
        Feed(t, "ABC");
        Feed(t, "\u001b[1;1H"); // CUP back to start
        Feed(t, "\u001b[2@");    // ICH 2 (insert 2 blanks)

        Assert.Equal("  ABC", GetLine(t, 0).TrimEnd());

        // Shifted cells (cols 2-4) should have hyperlink
        for (int x = 2; x < 5; x++)
        {
            var cell = GetCell(t, 0, x);
            Assert.True(cell.HasHyperlinkData,
                $"Shifted cell at col {x} should have hyperlink");
            Assert.Equal("http://example.com", cell.HyperlinkData!.Uri);
        }

        // Inserted blank cells (cols 0-1) should NOT have hyperlink
        for (int x = 0; x < 2; x++)
        {
            var cell = GetCell(t, 0, x);
            Assert.False(cell.HasHyperlinkData,
                $"Blank cell at col {x} should NOT have hyperlink");
        }
    }

    // Ghostty: "Terminal: insertBlanks pushes hyperlink off end completely"
    [Fact]
    public void InsertBlanks_PushesHyperlinkOffEndCompletely()
    {
        using var t = CreateTerminal(cols: 3, rows: 2);

        StartHyperlink(t, "http://example.com");
        Feed(t, "ABC");
        Feed(t, "\u001b[1;1H"); // CUP back to start
        Feed(t, "\u001b[3@");    // ICH 3 — pushes all hyperlinked chars off

        Assert.Equal("", GetLine(t, 0).TrimEnd());

        // All cells should have NO hyperlink
        for (int x = 0; x < 3; x++)
        {
            var cell = GetCell(t, 0, x);
            Assert.False(cell.HasHyperlinkData,
                $"Cell at col {x} should NOT have hyperlink after push-off");
        }
    }

    #endregion

    #region Save/Restore and Reset

    // Ghostty: "Terminal: saveCursor doesn't modify hyperlink state"
    [Fact]
    public void SaveCursor_DoesNotModifyHyperlinkState()
    {
        using var t = CreateTerminal(cols: 3, rows: 3);

        StartHyperlink(t, "http://example.com");
        // Save and restore cursor should not affect the active hyperlink
        Feed(t, "\u001b7");  // DECSC (save cursor)
        Feed(t, "\u001b8");  // DECRC (restore cursor)

        // Hyperlink should still be active — print and verify
        Feed(t, "A");
        var cell = GetCell(t, 0, 0);
        Assert.True(cell.HasHyperlinkData, "Hyperlink should still be active after save/restore");
        Assert.Equal("http://example.com", cell.HyperlinkData!.Uri);
    }

    // Ghostty: "Terminal: fullReset hyperlink"
    [Fact]
    public void FullReset_ClearsHyperlink()
    {
        using var t = CreateTerminal(cols: 80, rows: 80);

        StartHyperlink(t, "http://example.com");
        Feed(t, "\u001bc"); // RIS (full reset)

        // After reset, hyperlink should be cleared — printing should not have hyperlink
        Feed(t, "A");
        var cell = GetCell(t, 0, 0);
        Assert.False(cell.HasHyperlinkData, "Hyperlink should be cleared after RIS");
    }

    #endregion
}
