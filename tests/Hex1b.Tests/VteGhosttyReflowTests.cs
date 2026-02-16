using Hex1b.Reflow;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Reflow tests for VTE and Ghostty terminal emulator strategies, including
/// saved cursor (DECSC) reflow behavior.
/// </summary>
/// <remarks>
/// <para><b>Provenance:</b> Tests in this class are adapted from:</para>
/// <list type="bullet">
///   <item><b>VTE</b>: <c>GNOME/vte</c> — <c>doc/rewrap.txt</c> specification for reflow behavior</item>
///   <item><b>Ghostty</b>: <c>ghostty-org/ghostty</c> — <c>src/terminal/Screen.zig</c> test cases</item>
/// </list>
///
/// <para><b>Key difference from Kitty/Xterm:</b> VTE and Ghostty reflow the DECSC saved cursor
/// position alongside the primary cursor. Kitty and Xterm do not.</para>
/// </remarks>
public class VteGhosttyReflowTests
{
    #region Helper Methods

    /// <summary>
    /// Creates a row of terminal cells from a string, with optional soft-wrap on last cell.
    /// </summary>
    private static TerminalCell[] MakeRow(string text, int width, bool softWrap = false)
    {
        var cells = new TerminalCell[width];
        for (int i = 0; i < width; i++)
        {
            var ch = i < text.Length ? text[i].ToString() : " ";
            var attrs = (i == width - 1 && softWrap) ? CellAttributes.SoftWrap : CellAttributes.None;
            cells[i] = new TerminalCell(ch, null, null, attrs);
        }
        return cells;
    }

    /// <summary>
    /// Extracts visible text from a row, trimming trailing spaces.
    /// </summary>
    private static string GetRowText(TerminalCell[] row)
    {
        var chars = row.Select(c => c.Character ?? " ").ToArray();
        return string.Concat(chars).TrimEnd();
    }

    /// <summary>
    /// Creates a terminal with the specified strategy, writes text, and returns the terminal.
    /// </summary>
    private static Hex1bTerminal CreateTerminal(ITerminalReflowProvider strategy, int cols, int rows, int scrollback = 0)
    {
        var adapter = new HeadlessPresentationAdapter(cols, rows).WithReflow(strategy);
        using var workload = new Hex1bAppWorkloadAdapter();
        return Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(cols, rows)
            .WithScrollback(scrollback).Build();
    }

    #endregion

    #region Ghostty Screen.zig Tests

    /// <summary>
    /// Resize wider: reflow that fits full width merges wrapped rows.
    /// </summary>
    /// <remarks>
    /// <b>Upstream:</b> <c>ghostty-org/ghostty src/terminal/Screen.zig</c>
    /// — "resize more cols with reflow that fits full width"
    ///
    /// <b>Setup:</b> "1ABCD2EFGH\n3IJKL" at cols=5 → rows: "1ABCD" (soft), "2EFGH" (hard), "3IJKL" (hard).
    /// Cursor on '2' at (0,1). Resize to cols=10.
    /// Logical line "1ABCD2EFGH" re-wraps to one row. Cursor moves to (5,0).
    /// </remarks>
    [Fact]
    public void Ghostty_ResizeMoreCols_ReflowFitsFullWidth()
    {
        using var terminal = CreateTerminal(GhosttyReflowStrategy.Instance, 5, 5, scrollback: 5);

        // Write "1ABCD2EFGH" (10 chars, soft-wraps at col 5) then newline, then "3IJKL"
        terminal.ApplyTokens([new TextToken("1ABCD2EFGH")]);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\r\n"));
        terminal.ApplyTokens([new TextToken("3IJKL")]);

        // Before resize: cursor after "3IJKL" at col 4 pending or col 5 (past end), row 2
        // Move cursor to '2' at (0,1)
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[2;1H")); // Move to row 2, col 1 = '2'

        Assert.Equal(0, terminal.CursorX);
        Assert.Equal(1, terminal.CursorY);

        terminal.Resize(10, 5);

        // After reflow: "1ABCD2EFGH" becomes one row, cursor on '2' moves to (5,0)
        var snap = terminal.CreateSnapshot();
        Assert.Equal("1ABCD2EFGH", snap.GetLine(0).TrimEnd());
        Assert.Equal("3IJKL", snap.GetLine(1).TrimEnd());
        Assert.Equal(5, snap.CursorX);
        Assert.Equal(0, snap.CursorY);
    }

    /// <summary>
    /// Resize wider: reflow that ends in newline.
    /// </summary>
    /// <remarks>
    /// <b>Upstream:</b> <c>ghostty-org/ghostty src/terminal/Screen.zig</c>
    /// — "resize more cols with reflow that ends in newline"
    ///
    /// <b>Setup:</b> "1ABCD2EFGH\n3IJKL" at cols=6 → rows: "1ABCD2" (soft), "EFGH" (hard), "3IJKL" (hard).
    /// Cursor on '3' at (0,2). Resize to cols=10.
    /// Logical line "1ABCD2EFGH" re-wraps to one row at width 10. Cursor on '3' stays on '3'.
    /// </remarks>
    [Fact]
    public void Ghostty_ResizeMoreCols_ReflowEndsInNewline()
    {
        using var terminal = CreateTerminal(GhosttyReflowStrategy.Instance, 6, 5, scrollback: 5);

        terminal.ApplyTokens([new TextToken("1ABCD2EFGH")]);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\r\n"));
        terminal.ApplyTokens([new TextToken("3IJKL")]);

        // Move cursor to '3' at row 3 col 1 (which is screen row 2, col 0 in 0-based)
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3;1H"));

        Assert.Equal(0, terminal.CursorX);
        Assert.Equal(2, terminal.CursorY);

        terminal.Resize(10, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("1ABCD2EFGH", snap.GetLine(0).TrimEnd());
        Assert.Equal("3IJKL", snap.GetLine(1).TrimEnd());
        // Cursor on '3' moved from row 2 to row 1
        Assert.Equal(0, snap.CursorX);
        Assert.Equal(1, snap.CursorY);
    }

    /// <summary>
    /// Resize wider: reflow that forces more wrapping at intermediate width.
    /// </summary>
    /// <remarks>
    /// <b>Upstream:</b> <c>ghostty-org/ghostty src/terminal/Screen.zig</c>
    /// — "resize more cols with reflow that forces more wrapping"
    ///
    /// <b>Setup:</b> "1ABCD2EFGH\n3IJKL" at cols=5. Cursor on '2' at (0,1).
    /// Resize to cols=7. "1ABCD2EFGH" → "1ABCD2E" (soft) + "FGH" → 2 rows.
    /// Cursor on '2' was at cell offset 5, maps to (5,0).
    /// </remarks>
    [Fact]
    public void Ghostty_ResizeMoreCols_ForcesMoreWrapping()
    {
        using var terminal = CreateTerminal(GhosttyReflowStrategy.Instance, 5, 5, scrollback: 5);

        terminal.ApplyTokens([new TextToken("1ABCD2EFGH")]);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\r\n"));
        terminal.ApplyTokens([new TextToken("3IJKL")]);

        // Move cursor to '2' at (0,1)
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[2;1H"));

        terminal.Resize(7, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("1ABCD2E", snap.GetLine(0).TrimEnd());
        Assert.Equal("FGH", snap.GetLine(1).TrimEnd());
        Assert.Equal("3IJKL", snap.GetLine(2).TrimEnd());
        Assert.Equal(5, snap.CursorX);
        Assert.Equal(0, snap.CursorY);
    }

    /// <summary>
    /// Resize wider: reflow unwraps multiple times into a single row.
    /// </summary>
    /// <remarks>
    /// <b>Upstream:</b> <c>ghostty-org/ghostty src/terminal/Screen.zig</c>
    /// — "resize more cols with reflow that unwraps multiple times"
    ///
    /// <b>Setup:</b> "1ABCD2EFGH3IJKL" (15 chars) at cols=5 → 3 rows.
    /// Cursor on '3' at (0,2). Resize to cols=15. All one row.
    /// Cursor on '3' was at cell offset 10, maps to (10,0).
    /// </remarks>
    [Fact]
    public void Ghostty_ResizeMoreCols_UnwrapsMultipleTimes()
    {
        using var terminal = CreateTerminal(GhosttyReflowStrategy.Instance, 5, 5, scrollback: 5);

        terminal.ApplyTokens([new TextToken("1ABCD2EFGH3IJKL")]);

        // Move cursor to '3' at (0,2)
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3;1H"));

        Assert.Equal(0, terminal.CursorX);
        Assert.Equal(2, terminal.CursorY);

        terminal.Resize(15, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("1ABCD2EFGH3IJKL", snap.GetLine(0).TrimEnd());
        Assert.Equal(10, snap.CursorX);
        Assert.Equal(0, snap.CursorY);
    }

    /// <summary>
    /// Resize narrower: previously wrapped content re-wraps to narrower width.
    /// </summary>
    /// <remarks>
    /// <b>Upstream:</b> <c>ghostty-org/ghostty src/terminal/Screen.zig</c>
    /// — "resize less cols with reflow previously wrapped"
    ///
    /// <b>Setup:</b> "3IJKL4ABCD5EFGH" at cols=5. All one logical line (soft-wrapped).
    /// Resize to cols=3. 15 chars → 5 rows at width 3.
    /// Screen shows: "3IJ" "KL4" "ABC" "D5E" "FGH"
    /// </remarks>
    [Fact]
    public void Ghostty_ResizeLessCols_ReflowPreviouslyWrapped()
    {
        using var terminal = CreateTerminal(GhosttyReflowStrategy.Instance, 5, 5, scrollback: 5);

        terminal.ApplyTokens([new TextToken("3IJKL4ABCD5EFGH")]);

        terminal.Resize(3, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("3IJ", snap.GetLine(0).TrimEnd());
        Assert.Equal("KL4", snap.GetLine(1).TrimEnd());
        Assert.Equal("ABC", snap.GetLine(2).TrimEnd());
        Assert.Equal("D5E", snap.GetLine(3).TrimEnd());
        Assert.Equal("FGH", snap.GetLine(4).TrimEnd());
    }

    /// <summary>
    /// Resize narrower: reflow with scrollback, cursor stays on correct character.
    /// </summary>
    /// <remarks>
    /// <b>Upstream:</b> <c>ghostty-org/ghostty src/terminal/Screen.zig</c>
    /// — "resize less cols with reflow and scrollback"
    ///
    /// <b>Setup:</b> "1A\n2B\n3C\n4D\n5E" at cols=5, rows=3, scrollback=5.
    /// Cursor at (1,2) on 'E'. Resize to cols=3. Each line is hard-wrapped (no merging).
    /// The hard-wrapped lines stay as separate logical lines. Cursor stays at (1,2).
    /// </remarks>
    [Fact]
    public void Ghostty_ResizeLessCols_ReflowWithScrollback()
    {
        using var terminal = CreateTerminal(GhosttyReflowStrategy.Instance, 5, 3, scrollback: 5);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("1A\r\n2B\r\n3C\r\n4D\r\n5E"));

        // Cursor should be after "5E" at col 2, row 2
        Assert.Equal(2, terminal.CursorX);
        Assert.Equal(2, terminal.CursorY);

        terminal.Resize(3, 3);

        // Content is short enough that no re-wrapping needed (each line < 3 chars).
        // Cursor should still be at (2, 2) on 'E' row.
        Assert.Equal(2, terminal.CursorX);
        Assert.Equal(2, terminal.CursorY);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("3C", snap.GetLine(0).TrimEnd());
        Assert.Equal("4D", snap.GetLine(1).TrimEnd());
        Assert.Equal("5E", snap.GetLine(2).TrimEnd());
    }

    /// <summary>
    /// Resize narrower: previously wrapped content with scrollback, cursor tracks content.
    /// </summary>
    /// <remarks>
    /// <b>Upstream:</b> <c>ghostty-org/ghostty src/terminal/Screen.zig</c>
    /// — "resize less cols previously wrapped and scrollback"
    ///
    /// <b>Setup:</b> "1ABCD2EFGH3IJKL4ABCD5EFGH" (25 chars) at cols=5, rows=3, scrollback=2.
    /// All one logical line. At cols=5: 5 rows, screen shows last 3 (rows 2-4): "3IJKL", "4ABCD", "5EFGH".
    /// Cursor on 'H' at (4,2). Resize to cols=3.
    /// 25 chars → ceil(25/3)=9 rows at width 3. Last row has "H" at col 0.
    /// Screen shows last 3 rows: "EFG", "H" — wait, need to trace more carefully.
    /// 25 chars at width 3: "1AB"(0) "CD2"(1) "EFG"(2) "H3I"(3) "JKL"(4) "4AB"(5) "CD5"(6) "EFG"(7) "H"(8)
    /// Cursor was on 'H' at cell offset 4*5+4=24. At width 3: row=24/3=8, col=24%3=0.
    /// Screen (rows=3): shows rows 6-8: "CD5", "EFG", "H". Cursor at (0, 2).
    /// </remarks>
    [Fact]
    public void Ghostty_ResizeLessCols_PreviouslyWrappedWithScrollback()
    {
        using var terminal = CreateTerminal(GhosttyReflowStrategy.Instance, 5, 3, scrollback: 2);

        terminal.ApplyTokens([new TextToken("1ABCD2EFGH3IJKL4ABCD5EFGH")]);

        // After writing 25 chars at width 5: 5 rows, screen shows last 3.
        // Cursor wraps to end of content. After pending wrap, cursor is at col 4 row 4 (last screen row = 2)
        Assert.Equal(4, terminal.CursorX);
        Assert.Equal(2, terminal.CursorY);

        terminal.Resize(3, 3);

        var snap = terminal.CreateSnapshot();
        // Cursor was on 'H' (last char), should track to its new position
        Assert.Equal(0, snap.CursorX);
        Assert.Equal(2, snap.CursorY);
    }

    #endregion

    #region VTE Saved Cursor Tests

    /// <summary>
    /// VTE reflows the saved cursor: save cursor on a character, resize narrower,
    /// restore cursor — it points to the same character's new position.
    /// </summary>
    /// <remarks>
    /// <b>Upstream:</b> <c>GNOME/vte doc/rewrap.txt</c> — saved cursor stays on same character after reflow.
    ///
    /// <b>Setup:</b> "12345\n67890" at cols=10. Save cursor on '5' at (4,0).
    /// Resize to cols=5. "12345" is only 5 chars so stays on one row. "67890" same.
    /// Saved cursor should be at (4,0) — same position since the line fits.
    /// </remarks>
    [Fact]
    public void Vte_SavedCursor_TracksCharacterAfterNarrowResize()
    {
        using var terminal = CreateTerminal(VteReflowStrategy.Instance, 10, 5, scrollback: 5);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("12345\r\n67890"));

        // Move cursor to '5' at (4,0) and save
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;5H")); // row 1, col 5 = (4, 0)
        Assert.Equal(4, terminal.CursorX);
        Assert.Equal(0, terminal.CursorY);
        terminal.ApplyTokens([SaveCursorToken.Dec]);

        // Move cursor away
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[2;3H")); // row 2, col 3

        terminal.Resize(5, 5);

        // Restore saved cursor and check — should still be on '5'
        terminal.ApplyTokens([RestoreCursorToken.Dec]);
        Assert.Equal(4, terminal.CursorX);
        Assert.Equal(0, terminal.CursorY);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("12345", snap.GetLine(0).TrimEnd());
    }

    /// <summary>
    /// VTE reflows the saved cursor when soft-wrapped content is widened.
    /// </summary>
    /// <remarks>
    /// <b>Upstream:</b> <c>GNOME/vte doc/rewrap.txt</c> — saved cursor stays on same character after reflow.
    ///
    /// <b>Setup:</b> Write "ABCDEFGHIJ" at cols=5. This creates 2 soft-wrapped rows:
    /// "ABCDE" (soft) + "FGHIJ" (hard). Save cursor on 'F' at (0,1).
    /// Write more content, resize to cols=10.
    /// After reflow: "ABCDEFGHIJ" becomes one row. 'F' is at (5,0).
    /// Restore and verify.
    /// </remarks>
    [Fact]
    public void Vte_SavedCursor_TracksCharacterAfterWiderResize()
    {
        using var terminal = CreateTerminal(VteReflowStrategy.Instance, 5, 5, scrollback: 5);

        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        // Move cursor to 'F' at (0,1) and save
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[2;1H"));
        Assert.Equal(0, terminal.CursorX);
        Assert.Equal(1, terminal.CursorY);
        terminal.ApplyTokens([SaveCursorToken.Dec]);

        // Write more content on a new line
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\r\n"));
        terminal.ApplyTokens([new TextToken("KLMNO")]);

        terminal.Resize(10, 5);

        // Restore saved cursor — should be at (5, 0) where 'F' now is
        terminal.ApplyTokens([RestoreCursorToken.Dec]);
        Assert.Equal(5, terminal.CursorX);
        Assert.Equal(0, terminal.CursorY);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGHIJ", snap.GetLine(0).TrimEnd());
    }

    /// <summary>
    /// Kitty does NOT update saved cursor on reflow, but VTE DOES.
    /// </summary>
    /// <remarks>
    /// <b>Provenance:</b> Comparison test to demonstrate the behavioral difference between
    /// Kitty and VTE/Ghostty reflow strategies regarding DECSC saved cursor.
    ///
    /// <b>Setup:</b> Write "ABCDEFGHIJ" at cols=5, creating soft-wrapped rows.
    /// Save cursor on 'F' at (0,1). Resize to cols=10 with both strategies.
    /// Kitty: saved cursor stays at original (0,1) — NOT reflowed.
    /// VTE: saved cursor moves to (5,0) — reflowed.
    /// </remarks>
    [Fact]
    public void Vte_SavedCursor_DiffersFromKitty()
    {
        // Build the same context for both strategies
        int width = 5, height = 5;

        var screen = new TerminalCell[][] {
            MakeRow("ABCDE", width, softWrap: true),
            MakeRow("FGHIJ", width),
            MakeRow("", width),
            MakeRow("", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 10, height,
            0, 1, false,
            SavedCursorX: 0, SavedCursorY: 1);

        // VTE strategy reflowed the saved cursor
        var vteResult = VteReflowStrategy.Instance.Reflow(context);
        Assert.NotNull(vteResult.NewSavedCursorX);
        Assert.NotNull(vteResult.NewSavedCursorY);
        Assert.Equal(5, vteResult.NewSavedCursorX!.Value);
        Assert.Equal(0, vteResult.NewSavedCursorY!.Value);

        // Kitty strategy does NOT reflow the saved cursor
        var kittyResult = KittyReflowStrategy.Instance.Reflow(context);
        Assert.Null(kittyResult.NewSavedCursorX);
        Assert.Null(kittyResult.NewSavedCursorY);
    }

    /// <summary>
    /// Ghostty saved cursor reflow matches VTE behavior.
    /// </summary>
    /// <remarks>
    /// <b>Provenance:</b> Verification that <see cref="GhosttyReflowStrategy"/> produces
    /// identical saved cursor behavior to <see cref="VteReflowStrategy"/>.
    /// </remarks>
    [Fact]
    public void Ghostty_SavedCursor_MatchesVteBehavior()
    {
        int width = 5, height = 5;

        var screen = new TerminalCell[][] {
            MakeRow("ABCDE", width, softWrap: true),
            MakeRow("FGHIJ", width),
            MakeRow("", width),
            MakeRow("", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 10, height,
            0, 1, false,
            SavedCursorX: 0, SavedCursorY: 1);

        var vteResult = VteReflowStrategy.Instance.Reflow(context);
        var ghosttyResult = GhosttyReflowStrategy.Instance.Reflow(context);

        Assert.Equal(vteResult.NewSavedCursorX, ghosttyResult.NewSavedCursorX);
        Assert.Equal(vteResult.NewSavedCursorY, ghosttyResult.NewSavedCursorY);
        Assert.Equal(vteResult.CursorX, ghosttyResult.CursorX);
        Assert.Equal(vteResult.CursorY, ghosttyResult.CursorY);
    }

    #endregion

    #region VTE rewrap.txt Specification Tests

    /// <summary>
    /// Hard-wrapped lines are not merged during reflow (VTE rewrap.txt rule).
    /// </summary>
    /// <remarks>
    /// <b>Upstream:</b> <c>GNOME/vte doc/rewrap.txt</c> — hard-wrapped lines must not be joined.
    ///
    /// <b>Setup:</b> "Hello\nWorld" (hard-wrapped via newlines). Resize wider.
    /// The two lines must remain separate.
    /// </remarks>
    [Fact]
    public void Vte_HardWrappedLines_NotMerged()
    {
        using var terminal = CreateTerminal(VteReflowStrategy.Instance, 10, 5, scrollback: 5);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("Hello\r\nWorld"));

        terminal.Resize(20, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("Hello", snap.GetLine(0).TrimEnd());
        Assert.Equal("World", snap.GetLine(1).TrimEnd());
    }

    /// <summary>
    /// Saved cursor at end of line stays at end of line after reflow.
    /// </summary>
    /// <remarks>
    /// <b>Upstream:</b> <c>GNOME/vte doc/rewrap.txt</c> — saved cursor at EOL stays at EOL,
    /// not merging into joined text from the next soft-wrapped row.
    ///
    /// <b>Setup:</b> "ABCDE" (5 chars) at cols=5 → 1 row. Save cursor at (4,0) (on 'E').
    /// Resize to cols=10. Line is still "ABCDE" (no soft-wrap to join).
    /// Saved cursor should be at (4,0).
    /// </remarks>
    [Fact]
    public void Vte_SavedCursorAtEOL_StaysAtEOL()
    {
        using var terminal = CreateTerminal(VteReflowStrategy.Instance, 5, 5, scrollback: 5);

        terminal.ApplyTokens([new TextToken("ABCDE")]);

        // Move to 'E' at (4,0) and save
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;5H"));
        terminal.ApplyTokens([SaveCursorToken.Dec]);

        // Move away
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;1H"));

        terminal.Resize(10, 5);

        // Restore and verify cursor is still on 'E'
        terminal.ApplyTokens([RestoreCursorToken.Dec]);
        Assert.Equal(4, terminal.CursorX);
        Assert.Equal(0, terminal.CursorY);
    }

    #endregion

    #region Strategy Property Tests

    /// <summary>
    /// VTE strategy sets ShouldClearSoftWrapOnAbsolutePosition to true.
    /// </summary>
    [Fact]
    public void VteStrategy_ShouldClearSoftWrapOnAbsolutePosition_IsTrue()
    {
        Assert.True(VteReflowStrategy.Instance.ShouldClearSoftWrapOnAbsolutePosition);
    }

    /// <summary>
    /// Ghostty strategy sets ShouldClearSoftWrapOnAbsolutePosition to true.
    /// </summary>
    [Fact]
    public void GhosttyStrategy_ShouldClearSoftWrapOnAbsolutePosition_IsTrue()
    {
        Assert.True(GhosttyReflowStrategy.Instance.ShouldClearSoftWrapOnAbsolutePosition);
    }

    /// <summary>
    /// VTE does not reflow alternate screen buffer.
    /// </summary>
    [Fact]
    public void VteStrategy_AlternateScreen_NoReflow()
    {
        int width = 5, height = 3;
        var screen = new TerminalCell[][] {
            MakeRow("ABCDE", width, softWrap: true),
            MakeRow("FGHIJ", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 10, height,
            0, 1, InAlternateScreen: true);

        var result = VteReflowStrategy.Instance.Reflow(context);

        // No reflow in alt screen — content is cropped/extended, not re-wrapped
        Assert.Equal("ABCDE", GetRowText(result.ScreenRows[0]));
    }

    /// <summary>
    /// Ghostty does not reflow alternate screen buffer.
    /// </summary>
    [Fact]
    public void GhosttyStrategy_AlternateScreen_NoReflow()
    {
        int width = 5, height = 3;
        var screen = new TerminalCell[][] {
            MakeRow("ABCDE", width, softWrap: true),
            MakeRow("FGHIJ", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 10, height,
            0, 1, InAlternateScreen: true);

        var result = GhosttyReflowStrategy.Instance.Reflow(context);

        Assert.Equal("ABCDE", GetRowText(result.ScreenRows[0]));
    }

    #endregion

    #region ReflowHelper Saved Cursor Tests (Direct)

    /// <summary>
    /// When reflowSavedCursor is false, saved cursor fields are null in result.
    /// </summary>
    [Fact]
    public void ReflowHelper_NoSavedCursorReflow_ReturnsNull()
    {
        int width = 5, height = 3;
        var screen = new TerminalCell[][] {
            MakeRow("ABCDE", width, softWrap: true),
            MakeRow("FGHIJ", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 10, height,
            0, 1, false,
            SavedCursorX: 2, SavedCursorY: 0);

        var result = ReflowHelper.PerformReflow(context, preserveCursorRow: true, reflowSavedCursor: false);

        Assert.Null(result.NewSavedCursorX);
        Assert.Null(result.NewSavedCursorY);
    }

    /// <summary>
    /// When reflowSavedCursor is true but saved cursor is null, result fields are null.
    /// </summary>
    [Fact]
    public void ReflowHelper_SavedCursorNull_ReturnsNull()
    {
        int width = 5, height = 3;
        var screen = new TerminalCell[][] {
            MakeRow("ABCDE", width, softWrap: true),
            MakeRow("FGHIJ", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 10, height,
            0, 1, false);

        var result = ReflowHelper.PerformReflow(context, preserveCursorRow: true, reflowSavedCursor: true);

        Assert.Null(result.NewSavedCursorX);
        Assert.Null(result.NewSavedCursorY);
    }

    /// <summary>
    /// Saved cursor on soft-wrapped content tracks through reflow when widening.
    /// </summary>
    /// <remarks>
    /// <b>Provenance:</b> Direct ReflowHelper test verifying saved cursor tracking
    /// through the logical line grouping and re-wrapping pipeline.
    ///
    /// <b>Setup:</b> "ABCDE" (soft-wrap) + "FGHIJ" at width 5.
    /// Primary cursor at (0,1), saved cursor at (2,0) on 'C'.
    /// Resize to width 10: "ABCDEFGHIJ". 'C' moves to (2,0).
    /// </remarks>
    [Fact]
    public void ReflowHelper_SavedCursor_TracksOnWiden()
    {
        int width = 5, height = 3;
        var screen = new TerminalCell[][] {
            MakeRow("ABCDE", width, softWrap: true),
            MakeRow("FGHIJ", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 10, height,
            0, 1, false,
            SavedCursorX: 2, SavedCursorY: 0);

        var result = ReflowHelper.PerformReflow(context, preserveCursorRow: true, reflowSavedCursor: true);

        Assert.Equal(2, result.NewSavedCursorX);
        Assert.Equal(0, result.NewSavedCursorY);

        // Verify content
        Assert.Equal("ABCDEFGHIJ", GetRowText(result.ScreenRows[0]));
    }

    /// <summary>
    /// Saved cursor on second soft-wrapped row tracks through reflow when widening.
    /// </summary>
    /// <remarks>
    /// <b>Provenance:</b> Direct ReflowHelper test.
    /// Saved cursor at (2,1) = 'H' in "ABCDE" + "FGHIJ". Cell offset = 1*5+2 = 7.
    /// At width 10: row=7/10=0, col=7%10=7. Saved cursor → (7,0).
    /// </remarks>
    [Fact]
    public void ReflowHelper_SavedCursor_TracksSecondRowOnWiden()
    {
        int width = 5, height = 3;
        var screen = new TerminalCell[][] {
            MakeRow("ABCDE", width, softWrap: true),
            MakeRow("FGHIJ", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 10, height,
            0, 0, false,
            SavedCursorX: 2, SavedCursorY: 1);

        var result = ReflowHelper.PerformReflow(context, preserveCursorRow: true, reflowSavedCursor: true);

        Assert.Equal(7, result.NewSavedCursorX);
        Assert.Equal(0, result.NewSavedCursorY);
    }

    /// <summary>
    /// Saved cursor tracks correctly when narrowing causes more wrapping.
    /// </summary>
    /// <remarks>
    /// <b>Provenance:</b> Direct ReflowHelper test.
    /// "ABCDEFGHIJ" at width 10, saved cursor at (7,0) = 'H'.
    /// Resize to width 5: "ABCDE" + "FGHIJ". Cell offset 7: row=7/5=1, col=7%5=2. → (2,1) = 'H'.
    /// </remarks>
    [Fact]
    public void ReflowHelper_SavedCursor_TracksOnNarrow()
    {
        int width = 10, height = 3;
        var screen = new TerminalCell[][] {
            MakeRow("ABCDEFGHIJ", width),
            MakeRow("", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 5, height,
            0, 0, false,
            SavedCursorX: 7, SavedCursorY: 0);

        var result = ReflowHelper.PerformReflow(context, preserveCursorRow: true, reflowSavedCursor: true);

        Assert.Equal(2, result.NewSavedCursorX);
        Assert.Equal(1, result.NewSavedCursorY);

        Assert.Equal("ABCDE", GetRowText(result.ScreenRows[0]));
        Assert.Equal("FGHIJ", GetRowText(result.ScreenRows[1]));
    }

    /// <summary>
    /// Same-size resize returns saved cursor unchanged.
    /// </summary>
    [Fact]
    public void ReflowHelper_SameSize_SavedCursorUnchanged()
    {
        int width = 5, height = 3;
        var screen = new TerminalCell[][] {
            MakeRow("ABCDE", width),
            MakeRow("FGHIJ", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, width, height,
            0, 0, false,
            SavedCursorX: 3, SavedCursorY: 1);

        var result = ReflowHelper.PerformReflow(context, preserveCursorRow: true, reflowSavedCursor: true);

        Assert.Equal(3, result.NewSavedCursorX);
        Assert.Equal(1, result.NewSavedCursorY);
    }

    #endregion
}
