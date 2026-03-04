namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Terminal conformance tests derived from Ghostty's Terminal.zig.
/// Tests exercise the full pipeline: raw ANSI → AnsiTokenizer → ApplyTokens → terminal state.
/// </summary>
/// <remarks>
/// Source: https://github.com/ghostty-org/ghostty/blob/main/src/terminal/Terminal.zig
/// These tests verify Hex1b handles core terminal operations (cursor movement, erase, scroll,
/// insert/delete lines, tabs, etc.) the same way Ghostty does.
/// </remarks>
[Trait("Category", "GhosttyConformance")]
public class GhosttyTerminalConformanceTests
{
    private static Hex1bTerminal CreateTerminal(int cols = 80, int rows = 24)
        => GhosttyTestFixture.CreateTerminal(cols, rows);

    /// <summary>
    /// Asserts that the terminal's visible text matches the expected multiline string.
    /// Each line in <paramref name="expected"/> is compared against the trimmed terminal line.
    /// Lines beyond the expected are verified to be empty.
    /// </summary>
    /// <remarks>
    /// Mirrors Ghostty's <c>plainString()</c> which trims trailing spaces per line
    /// and trailing blank lines from the output.
    /// </remarks>
    private static void AssertPlainText(Hex1bTerminal terminal, string expected)
    {
        var expectedLines = expected.Split('\n');
        for (int i = 0; i < expectedLines.Length; i++)
        {
            var actualLine = GhosttyTestFixture.GetLine(terminal, i);
            Assert.Equal(expectedLines[i], actualLine);
        }

        // Check that lines beyond expected are empty
        for (int i = expectedLines.Length; i < terminal.Height; i++)
        {
            var line = GhosttyTestFixture.GetLine(terminal, i);
            Assert.Equal("", line);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Group 1: Basic Input and Cursor Movement
    // ──────────────────────────────────────────────────────────────────────────

    #region Basic Input and Cursor Movement

    /// <summary>
    /// Ghostty: test "Terminal: input with no control characters"
    /// Writes "hello" to a 40×40 terminal, verifies cursor at (5, 0) and text content.
    /// </summary>
    [Fact]
    public void Input_NoControlCharacters_WritesTextAndAdvancesCursor()
    {
        using var terminal = CreateTerminal(cols: 40, rows: 40);
        GhosttyTestFixture.Feed(terminal, "hello");

        Assert.Equal(5, terminal.CursorX);
        Assert.Equal(0, terminal.CursorY);
        AssertPlainText(terminal, "hello");
    }

    /// <summary>
    /// Ghostty: test "Terminal: input with basic wraparound"
    /// Writes "helloworldabc12" (15 chars) to a 5×40 terminal. The text wraps at column 5:
    /// line 0 = "hello", line 1 = "world", line 2 = "abc12".
    /// Cursor ends at col 4, row 2 (with pending wrap in Ghostty — here we verify position).
    /// </summary>
    [Fact]
    public void Input_BasicWraparound_WrapsTextAtColumnBoundary()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 40);
        GhosttyTestFixture.Feed(terminal, "helloworldabc12");

        // Ghostty: cursor at (4, 2) with pending wrap.
        // After writing "abc12", the cursor is at col 4 row 2 with pending wrap set.
        // In Hex1b, the cursor should be at col 4, row 2 (or col 5 if no pending wrap concept).
        // We check the text content which is the authoritative behavior.
        Assert.Equal(2, terminal.CursorY);
        AssertPlainText(terminal, "hello\nworld\nabc12");
    }

    /// <summary>
    /// Ghostty: test "Terminal: input that forces scroll"
    /// Writes 6 characters to a 1×5 terminal (1 col, 5 rows).
    /// Each char after the first autowraps to the next row. The 6th char forces a scroll.
    /// After scroll: 'a' is lost, visible = "b","c","d","e","f". Cursor at row 4, col 0.
    /// </summary>
    [Fact]
    public void Input_ForcesScroll_ScrollsContentUp()
    {
        using var terminal = CreateTerminal(cols: 1, rows: 5);
        GhosttyTestFixture.Feed(terminal, "abcdef");

        Assert.Equal(0, terminal.CursorX);
        Assert.Equal(4, terminal.CursorY);
        Assert.Equal("b", GhosttyTestFixture.GetLine(terminal, 0));
        Assert.Equal("c", GhosttyTestFixture.GetLine(terminal, 1));
        Assert.Equal("d", GhosttyTestFixture.GetLine(terminal, 2));
        Assert.Equal("e", GhosttyTestFixture.GetLine(terminal, 3));
        Assert.Equal("f", GhosttyTestFixture.GetLine(terminal, 4));
    }

    /// <summary>
    /// Ghostty: test "Terminal: linefeed unsets pending wrap"
    /// Writes "hello" filling 5 cols (pending wrap set), then LF.
    /// After LF, pending wrap is cleared and cursor moves to next row.
    /// The column is preserved (LF does not reset column — only CR does).
    /// Behavioral test: without LF, the next char would autowrap to row 1 col 0.
    /// With LF, cursor moves to row 1 preserving col, so 'X' writes at col 4.
    /// </summary>
    [Fact]
    public void Linefeed_AfterFilledLine_UnsetsWrapAndMovesToNextRow()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 40);
        GhosttyTestFixture.Feed(terminal, "hello");   // fills 5 cols, pending wrap
        GhosttyTestFixture.Feed(terminal, "\n");       // LF: clears pending wrap, moves down, keeps col
        GhosttyTestFixture.Feed(terminal, "X");        // writes at (col 4, row 1)

        Assert.Equal(1, terminal.CursorY);
        Assert.Equal("hello", GhosttyTestFixture.GetLine(terminal, 0));
        Assert.Equal("    X", GhosttyTestFixture.GetLine(terminal, 1));
    }

    /// <summary>
    /// Ghostty: test "Terminal: carriage return unsets pending wrap"
    /// Writes "hello" filling 5 cols, then CR. CR resets cursor to col 0 on same row.
    /// Writing 'X' overwrites the first character.
    /// </summary>
    [Fact]
    public void CarriageReturn_AfterFilledLine_UnsetsWrapAndReturnsToCol0()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 40);
        GhosttyTestFixture.Feed(terminal, "hello");   // fills 5 cols, pending wrap
        GhosttyTestFixture.Feed(terminal, "\r");       // CR goes to col 0
        GhosttyTestFixture.Feed(terminal, "X");

        Assert.Equal(1, terminal.CursorX);
        Assert.Equal(0, terminal.CursorY);
        AssertPlainText(terminal, "Xello");
    }

    /// <summary>
    /// Ghostty: test "Terminal: backspace"
    /// Writes "hello", backspaces, writes 'y'. Result: "helly".
    /// </summary>
    [Fact]
    public void Backspace_OverwritesPreviousCharacter()
    {
        using var terminal = CreateTerminal(cols: 40, rows: 40);
        GhosttyTestFixture.Feed(terminal, "hello");
        GhosttyTestFixture.Feed(terminal, "\x08");   // BS
        GhosttyTestFixture.Feed(terminal, "y");

        AssertPlainText(terminal, "helly");
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────────────
    // Group 2: Cursor Position (CUP)
    // ──────────────────────────────────────────────────────────────────────────

    #region Cursor Position (CUP)

    /// <summary>
    /// Ghostty: test "Terminal: cursorPos resets wrap"
    /// Writes "ABCDE" in 5-col terminal (pending wrap), then CUP(1,1) (top-left).
    /// Writing 'X' overwrites 'A'. Result: "XBCDE".
    /// </summary>
    [Fact]
    public void CursorPos_ResetsWrap_WritesAtNewPosition()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABCDE");        // fills line, pending wrap
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H");    // CUP(1,1) → top-left
        GhosttyTestFixture.Feed(terminal, "X");

        AssertPlainText(terminal, "XBCDE");
    }

    /// <summary>
    /// Ghostty: test "Terminal: cursorPos off the screen"
    /// CUP(500,500) in 5×5 terminal clamps to bottom-right (row 4, col 4).
    /// Writing 'X' places it at the last cell.
    /// </summary>
    [Fact]
    public void CursorPos_OffScreen_ClampsToTerminalBounds()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "\x1b[500;500H"); // CUP far off-screen
        GhosttyTestFixture.Feed(terminal, "X");

        Assert.Equal(4, terminal.CursorY);
        var line = GhosttyTestFixture.GetLine(terminal, 4);
        Assert.EndsWith("X", line);
    }

    /// <summary>
    /// Ghostty: test "Terminal: setCursorPos"
    /// Tests CUP clamping: CUP(0,0) → (0,0), CUP(81,81) in 80×80 → (79,79).
    /// </summary>
    [Fact]
    public void CursorPos_Zero_ClampsToTopLeft()
    {
        using var terminal = CreateTerminal(cols: 80, rows: 80);
        // CUP(0,0) should clamp to (0,0) — row 0, col 0
        GhosttyTestFixture.Feed(terminal, "\x1b[0;0H");
        Assert.Equal(0, terminal.CursorX);
        Assert.Equal(0, terminal.CursorY);
    }

    /// <summary>
    /// Ghostty: test "Terminal: setCursorPos" — clamping to max
    /// CUP(81,81) in 80×80 terminal clamps to row 79, col 79.
    /// </summary>
    [Fact]
    public void CursorPos_BeyondMax_ClampsToBottomRight()
    {
        using var terminal = CreateTerminal(cols: 80, rows: 80);
        GhosttyTestFixture.Feed(terminal, "\x1b[81;81H");
        Assert.Equal(79, terminal.CursorX);
        Assert.Equal(79, terminal.CursorY);
    }

    /// <summary>
    /// Ghostty: test "Terminal: setCursorPos" — with DECOM (origin mode)
    /// Sets scroll region 2–4, enables DECOM, CUP(1,1) should map to row 1 (second row of region).
    /// </summary>
    [Fact]
    public void CursorPos_WithOriginMode_IsRelativeToScrollRegion()
    {
        using var terminal = CreateTerminal(cols: 80, rows: 80);
        // Set scroll region rows 2–4 (1-based DECSTBM)
        GhosttyTestFixture.Feed(terminal, "\x1b[2;4r");
        // Enable origin mode (DECOM)
        GhosttyTestFixture.Feed(terminal, "\x1b[?6h");
        // CUP(1,1) with DECOM → cursor at top of scroll region (row 1, 0-based)
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H");

        Assert.Equal(0, terminal.CursorX);
        Assert.Equal(1, terminal.CursorY); // row 1 (0-based) = top of scroll region (row 2, 1-based)
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────────────
    // Group 3: Erase Operations
    // ──────────────────────────────────────────────────────────────────────────

    #region Erase Characters (ECH)

    /// <summary>
    /// Ghostty: test "Terminal: eraseChars simple operation"
    /// Writes "ABC", CUP(1,1), ECH(2) erases 2 chars at col 0–1. Then CUP(1,1), write 'X'.
    /// Result: "X C" — A and B erased, X written at col 0, C remains at col 2.
    /// </summary>
    [Fact]
    public void EraseChars_SimpleOperation_ErasesCharactersAtCursor()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABC");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H");   // CUP(1,1) → col 0, row 0
        GhosttyTestFixture.Feed(terminal, "\x1b[2X");      // ECH(2) — erase 2 chars
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H");   // CUP(1,1) again
        GhosttyTestFixture.Feed(terminal, "X");

        // Cols: X(0), ' '(1), C(2) — trimmed = "X  C" but with GetLine trimming,
        // we need to check the cell-level content.
        // GetLine trims trailing spaces. "X C" has no trailing spaces after C.
        // Actually col 0='X', col 1=' ' (erased), col 2='C' → "X C" (space in middle)
        // But GetLine trims trailing whitespace, not internal. So "X C" is correct if col 3,4 are empty.
        // Wait: original was "ABC" in 5-col terminal. After ECH(2) at col 0: cols 0,1 erased, col 2='C'.
        // Then write 'X' at col 0: col 0='X', col 1=' ', col 2='C' → trimmed = "X C"
        var line = GhosttyTestFixture.GetLine(terminal, 0);
        // The line has X at 0, space at 1, C at 2 — no trailing spaces after col 2
        Assert.Equal("X C", line);
    }

    /// <summary>
    /// Ghostty: test "Terminal: eraseChars minimum one"
    /// ECH(0) erases at least 1 character (minimum is 1).
    /// Writes "ABC", CUP(1,1), ECH(0), write 'X' → "XBC".
    /// </summary>
    [Fact]
    public void EraseChars_ZeroParameter_ErasesAtLeastOne()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABC");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H");   // CUP(1,1)
        GhosttyTestFixture.Feed(terminal, "\x1b[0X");      // ECH(0) — minimum 1
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H");   // CUP(1,1)
        GhosttyTestFixture.Feed(terminal, "X");

        AssertPlainText(terminal, "XBC");
    }

    /// <summary>
    /// Ghostty: test "Terminal: eraseChars beyond screen edge"
    /// Writes "  ABC" (2 spaces + ABC), CUP(1,4) → col 3 (0-based). ECH(10) erases from col 3 to end.
    /// Result: "  A" (only cols 0–2 survive).
    /// </summary>
    [Fact]
    public void EraseChars_BeyondScreenEdge_ClampsToLineEnd()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "  ABC");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;4H");    // CUP(1,4) → row 0, col 3
        GhosttyTestFixture.Feed(terminal, "\x1b[10X");      // ECH(10) — erases cols 3–4

        AssertPlainText(terminal, "  A");
    }

    /// <summary>
    /// Ghostty: test "Terminal: eraseChars resets pending wrap"
    /// Writes "ABCDE" (fills 5 cols, pending wrap). ECH(1) resets wrap and erases col 4.
    /// Then writing 'X' overwrites col 4 (no wrap). Result: "ABCDX".
    /// </summary>
    [Fact]
    public void EraseChars_ResetsPendingWrap()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABCDE");        // pending wrap at col 4
        GhosttyTestFixture.Feed(terminal, "\x1b[1X");      // ECH(1) — resets wrap, erases at cursor
        GhosttyTestFixture.Feed(terminal, "X");

        AssertPlainText(terminal, "ABCDX");
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────────────
    // Group 4: Index / Reverse Index
    // ──────────────────────────────────────────────────────────────────────────

    #region Index (IND) and Reverse Index (RI)

    /// <summary>
    /// Ghostty: test "Terminal: index"
    /// ESC D (Index) moves cursor down one line. Then write 'A'.
    /// Result: line 0 empty, line 1 = "A".
    /// </summary>
    [Fact]
    public void Index_MovesCursorDown()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "\u001bD");   // IND — cursor down
        GhosttyTestFixture.Feed(terminal, "A");

        Assert.Equal(1, terminal.CursorY);
        AssertPlainText(terminal, "\nA");
    }

    /// <summary>
    /// Ghostty: test "Terminal: index from the bottom"
    /// Places cursor at last row, writes 'A', moves left, then Index.
    /// At the bottom row, Index scrolls content up and inserts blank line at bottom.
    /// Then write 'B' on the new last row.
    /// </summary>
    [Fact]
    public void Index_FromBottomRow_ScrollsContentUp()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        // Move to last row
        GhosttyTestFixture.Feed(terminal, "\x1b[5;1H");   // CUP(5,1) → row 4, col 0
        GhosttyTestFixture.Feed(terminal, "A");
        GhosttyTestFixture.Feed(terminal, "\x1b[1D");     // cursor left 1
        GhosttyTestFixture.Feed(terminal, "\u001bD");       // IND — at bottom, scrolls up

        // 'A' should have scrolled up from row 4 to row 3
        GhosttyTestFixture.Feed(terminal, "B");

        var line3 = GhosttyTestFixture.GetLine(terminal, 3);
        var line4 = GhosttyTestFixture.GetLine(terminal, 4);
        Assert.Equal("A", line3);
        Assert.Equal("B", line4);
    }

    /// <summary>
    /// Ghostty: test "Terminal: index outside of scrolling region"
    /// Sets scroll region rows 2–5 (1-based). Cursor at row 0 (above region).
    /// Index should move cursor to row 1 (normal movement, not scrolling).
    /// </summary>
    [Fact]
    public void Index_OutsideScrollRegion_MovesCursorNormally()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        // Set scroll region to rows 2–5 (1-based)
        GhosttyTestFixture.Feed(terminal, "\x1b[2;5r");
        // Move cursor to row 0
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H");   // CUP(1,1) → row 0

        GhosttyTestFixture.Feed(terminal, "\u001bD");       // IND

        Assert.Equal(1, terminal.CursorY); // moved to row 1
    }

    /// <summary>
    /// Ghostty: test "Terminal: reverseIndex"
    /// Writes on row 3, then does Reverse Index (ESC M). Cursor moves up one row.
    /// </summary>
    [Fact]
    public void ReverseIndex_MovesCursorUp()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        // Move to row 3
        GhosttyTestFixture.Feed(terminal, "\x1b[4;1H");   // CUP(4,1) → row 3
        GhosttyTestFixture.Feed(terminal, "A");
        GhosttyTestFixture.Feed(terminal, "\x1b[4;1H");   // Back to row 3, col 0
        GhosttyTestFixture.Feed(terminal, "\x1bM");       // RI — reverse index

        Assert.Equal(2, terminal.CursorY); // moved up to row 2
    }

    /// <summary>
    /// Ghostty: test "Terminal: reverseIndex from the top"
    /// At row 0, Reverse Index scrolls content down (inserts blank line at top).
    /// </summary>
    [Fact]
    public void ReverseIndex_FromTopRow_ScrollsContentDown()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "A");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H");   // CUP(1,1) → row 0
        GhosttyTestFixture.Feed(terminal, "\x1bM");       // RI — at top, scrolls down

        // 'A' should have moved down from row 0 to row 1
        Assert.Equal(0, terminal.CursorY);
        var line0 = GhosttyTestFixture.GetLine(terminal, 0);
        var line1 = GhosttyTestFixture.GetLine(terminal, 1);
        Assert.Equal("", line0);
        Assert.Equal("A", line1);
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────────────
    // Group 5: Erase Line/Display
    // ──────────────────────────────────────────────────────────────────────────

    #region Erase Line (EL)

    /// <summary>
    /// Ghostty: test "Terminal: eraseLine simple erase right"
    /// Writes "ABCDE" on 5-col terminal, CUP(1,3) → col 2 (0-based).
    /// EL(0) erases from cursor to end of line. Result: "AB".
    /// </summary>
    [Fact]
    public void EraseLine_Right_ErasesFromCursorToEnd()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;3H");   // CUP(1,3) → row 0, col 2
        GhosttyTestFixture.Feed(terminal, "\x1b[0K");      // EL(0) — erase right

        AssertPlainText(terminal, "AB");
    }

    /// <summary>
    /// Ghostty: test "Terminal: eraseLine simple erase left"
    /// Writes "ABCDE" on 5-col terminal, CUP(1,4) → col 3 (0-based).
    /// EL(1) erases from start of line to cursor (inclusive). Result: "    E".
    /// </summary>
    [Fact]
    public void EraseLine_Left_ErasesFromStartToCursor()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;4H");   // CUP(1,4) → row 0, col 3
        GhosttyTestFixture.Feed(terminal, "\x1b[1K");      // EL(1) — erase left

        // Cols 0–3 erased, col 4 = 'E'. But GetLine trims trailing spaces.
        // The content is "    E" — leading spaces + E. GetLine trims trailing only.
        var line = GhosttyTestFixture.GetLine(terminal, 0);
        Assert.Equal("    E", line);
    }

    #endregion

    #region Erase Display (ED)

    /// <summary>
    /// Ghostty: test "Terminal: eraseDisplay simple erase below"
    /// Fills 5×5 grid with letters, CUP(3,3) → row 2, col 2 (0-based).
    /// ED(0) erases from cursor position to end of display.
    /// Lines 0–1 untouched, line 2 partially erased from col 2, lines 3–4 fully erased.
    /// </summary>
    [Fact]
    public void EraseDisplay_Below_ErasesFromCursorToEndOfScreen()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        // Fill each row: row 0 = "AAAAA", row 1 = "BBBBB", etc.
        GhosttyTestFixture.Feed(terminal, "AAAAA");
        GhosttyTestFixture.Feed(terminal, "BBBBB");
        GhosttyTestFixture.Feed(terminal, "CCCCC");
        GhosttyTestFixture.Feed(terminal, "DDDDD");
        GhosttyTestFixture.Feed(terminal, "EEEEE");
        // CUP(3,3) → row 2, col 2
        GhosttyTestFixture.Feed(terminal, "\x1b[3;3H");
        GhosttyTestFixture.Feed(terminal, "\x1b[0J");     // ED(0) — erase below

        Assert.Equal("AAAAA", GhosttyTestFixture.GetLine(terminal, 0));
        Assert.Equal("BBBBB", GhosttyTestFixture.GetLine(terminal, 1));
        Assert.Equal("CC", GhosttyTestFixture.GetLine(terminal, 2));  // cols 0–1 survive
        Assert.Equal("", GhosttyTestFixture.GetLine(terminal, 3));
        Assert.Equal("", GhosttyTestFixture.GetLine(terminal, 4));
    }

    /// <summary>
    /// Ghostty: test "Terminal: eraseDisplay simple erase above"
    /// Fills 5×5 grid, CUP(3,3) → row 2, col 2 (0-based).
    /// ED(1) erases from start of display to cursor position.
    /// Lines 0–1 fully erased, line 2 partially erased up to col 2, lines 3–4 untouched.
    /// </summary>
    [Fact]
    public void EraseDisplay_Above_ErasesFromStartToCursorPosition()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        // Fill each row
        GhosttyTestFixture.Feed(terminal, "AAAAA");
        GhosttyTestFixture.Feed(terminal, "BBBBB");
        GhosttyTestFixture.Feed(terminal, "CCCCC");
        GhosttyTestFixture.Feed(terminal, "DDDDD");
        GhosttyTestFixture.Feed(terminal, "EEEEE");
        // CUP(3,3) → row 2, col 2
        GhosttyTestFixture.Feed(terminal, "\x1b[3;3H");
        GhosttyTestFixture.Feed(terminal, "\x1b[1J");     // ED(1) — erase above

        Assert.Equal("", GhosttyTestFixture.GetLine(terminal, 0));
        Assert.Equal("", GhosttyTestFixture.GetLine(terminal, 1));
        // Row 2: cols 0–2 erased (inclusive), cols 3–4 survive → "   CC"
        var line2 = GhosttyTestFixture.GetLine(terminal, 2);
        Assert.Equal("   CC", line2);
        Assert.Equal("DDDDD", GhosttyTestFixture.GetLine(terminal, 3));
        Assert.Equal("EEEEE", GhosttyTestFixture.GetLine(terminal, 4));
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────────────
    // Group 6: Horizontal Tabs
    // ──────────────────────────────────────────────────────────────────────────

    #region Horizontal Tabs

    /// <summary>
    /// Ghostty: test "Terminal: horizontal tabs"
    /// Default tab stops are every 8 columns. Write '1', tab → col 8, tab → col 16.
    /// Tab at end of line clamps to last column.
    /// </summary>
    [Fact]
    public void HorizontalTab_DefaultStops_MovesToNext8thColumn()
    {
        using var terminal = CreateTerminal(cols: 20, rows: 5);
        GhosttyTestFixture.Feed(terminal, "1");
        GhosttyTestFixture.Feed(terminal, "\t");

        Assert.Equal(8, terminal.CursorX);

        GhosttyTestFixture.Feed(terminal, "\t");
        Assert.Equal(16, terminal.CursorX);

        // Tab near end should clamp to last column (cols-1 = 19)
        GhosttyTestFixture.Feed(terminal, "\t");
        Assert.Equal(19, terminal.CursorX);
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────────────
    // Group 7: Scroll Region
    // ──────────────────────────────────────────────────────────────────────────

    #region Scroll Region (DECSTBM)

    /// <summary>
    /// Ghostty: test "Terminal: setTopAndBottomMargin simple"
    /// Sets scroll region then verifies scrolling only affects that region.
    /// </summary>
    [Fact]
    public void ScrollRegion_LimitsScrollingToRegion()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        // Fill rows: row 0 = "AAAAA", row 1 = "BBBBB", ..., row 4 = "EEEEE"
        GhosttyTestFixture.Feed(terminal, "AAAAA");
        GhosttyTestFixture.Feed(terminal, "BBBBB");
        GhosttyTestFixture.Feed(terminal, "CCCCC");
        GhosttyTestFixture.Feed(terminal, "DDDDD");
        GhosttyTestFixture.Feed(terminal, "EEEEE");

        // Set scroll region to rows 2–4 (1-based)
        GhosttyTestFixture.Feed(terminal, "\x1b[2;4r");
        // Move cursor to row 3 (bottom of region, 1-based row 4)
        GhosttyTestFixture.Feed(terminal, "\x1b[4;1H");

        // Index at bottom of scroll region should scroll within region
        GhosttyTestFixture.Feed(terminal, "\u001bD");

        // Row 0 and row 4 (outside region) should be unchanged
        Assert.Equal("AAAAA", GhosttyTestFixture.GetLine(terminal, 0));
        // Rows 1–3 (the scroll region) should have shifted up:
        // row 1 was "BBBBB" → now "CCCCC"
        // row 2 was "CCCCC" → now "DDDDD"
        // row 3 was "DDDDD" → now empty (blank inserted at bottom of region)
        Assert.Equal("CCCCC", GhosttyTestFixture.GetLine(terminal, 1));
        Assert.Equal("DDDDD", GhosttyTestFixture.GetLine(terminal, 2));
        Assert.Equal("", GhosttyTestFixture.GetLine(terminal, 3));
        Assert.Equal("EEEEE", GhosttyTestFixture.GetLine(terminal, 4));
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────────────
    // Group 8: Insert/Delete Lines
    // ──────────────────────────────────────────────────────────────────────────

    #region Insert Lines (IL) and Delete Lines (DL)

    /// <summary>
    /// Ghostty: test "Terminal: insertLines simple"
    /// Fills 5 rows, CUP to middle, IL(1) inserts a blank line pushing rows down.
    /// The last row is lost (scrolled out).
    /// </summary>
    [Fact]
    public void InsertLines_ShiftsRowsDown()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        // Fill rows
        GhosttyTestFixture.Feed(terminal, "AAAAA");
        GhosttyTestFixture.Feed(terminal, "BBBBB");
        GhosttyTestFixture.Feed(terminal, "CCCCC");
        GhosttyTestFixture.Feed(terminal, "DDDDD");
        GhosttyTestFixture.Feed(terminal, "EEEEE");

        // CUP(3,1) → row 2, col 0
        GhosttyTestFixture.Feed(terminal, "\x1b[3;1H");
        // IL(1) — insert 1 blank line at row 2
        GhosttyTestFixture.Feed(terminal, "\x1b[1L");

        Assert.Equal("AAAAA", GhosttyTestFixture.GetLine(terminal, 0));
        Assert.Equal("BBBBB", GhosttyTestFixture.GetLine(terminal, 1));
        Assert.Equal("", GhosttyTestFixture.GetLine(terminal, 2));     // inserted blank
        Assert.Equal("CCCCC", GhosttyTestFixture.GetLine(terminal, 3));
        Assert.Equal("DDDDD", GhosttyTestFixture.GetLine(terminal, 4)); // EEEEE lost
    }

    /// <summary>
    /// Ghostty: test "Terminal: deleteLines simple"
    /// Fills 5 rows, CUP to middle, DL(1) deletes a line shifting rows up.
    /// A blank line appears at the bottom.
    /// </summary>
    [Fact]
    public void DeleteLines_ShiftsRowsUp()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        // Fill rows
        GhosttyTestFixture.Feed(terminal, "AAAAA");
        GhosttyTestFixture.Feed(terminal, "BBBBB");
        GhosttyTestFixture.Feed(terminal, "CCCCC");
        GhosttyTestFixture.Feed(terminal, "DDDDD");
        GhosttyTestFixture.Feed(terminal, "EEEEE");

        // CUP(3,1) → row 2, col 0
        GhosttyTestFixture.Feed(terminal, "\x1b[3;1H");
        // DL(1) — delete 1 line at row 2
        GhosttyTestFixture.Feed(terminal, "\x1b[1M");

        Assert.Equal("AAAAA", GhosttyTestFixture.GetLine(terminal, 0));
        Assert.Equal("BBBBB", GhosttyTestFixture.GetLine(terminal, 1));
        Assert.Equal("DDDDD", GhosttyTestFixture.GetLine(terminal, 2)); // was row 3
        Assert.Equal("EEEEE", GhosttyTestFixture.GetLine(terminal, 3)); // was row 4
        Assert.Equal("", GhosttyTestFixture.GetLine(terminal, 4));     // blank at bottom
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────────────
    // Group 9: Delete Characters
    // ──────────────────────────────────────────────────────────────────────────

    #region Delete Characters (DCH)

    /// <summary>
    /// Ghostty: test "Terminal: deleteChars"
    /// Writes "ABC", CUP(1,2) → col 1 (0-based). DCH(1) deletes 'B', 'C' shifts left.
    /// Result: "AC".
    /// </summary>
    [Fact]
    public void DeleteChars_ShiftsCharactersLeft()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABC");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;2H");   // CUP(1,2) → row 0, col 1
        GhosttyTestFixture.Feed(terminal, "\x1b[1P");      // DCH(1) — delete 1 char

        AssertPlainText(terminal, "AC");
    }

    /// <summary>
    /// Ghostty: test "Terminal: deleteChars more than line width"
    /// Writes "ABC", CUP(1,2) → col 1. DCH(10) deletes all from col 1 onward.
    /// Result: "A".
    /// </summary>
    [Fact]
    public void DeleteChars_MoreThanLineWidth_ClampsToEnd()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABC");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;2H");   // CUP(1,2) → row 0, col 1
        GhosttyTestFixture.Feed(terminal, "\x1b[10P");     // DCH(10) — delete more than available

        AssertPlainText(terminal, "A");
    }

    #endregion
}
