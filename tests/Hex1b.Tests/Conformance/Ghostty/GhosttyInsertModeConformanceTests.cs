namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Extensive tests for Insert/Replace Mode (IRM, ECMA-48 mode 4).
/// CSI 4 h enables insert mode, CSI 4 l disables (back to replace mode).
/// In insert mode, printing a character shifts existing content right.
/// </summary>
[Trait("Category", "GhosttyConformance")]
public class GhosttyInsertModeConformanceTests
{
    private static Hex1bTerminal CreateTerminal(int cols = 80, int rows = 24)
        => GhosttyTestFixture.CreateTerminal(cols, rows);

    private static void AssertPlainText(Hex1bTerminal terminal, int row, string expected)
    {
        var line = GhosttyTestFixture.GetLine(terminal, row);
        Assert.Equal(expected, line);
    }

    #region Basic Insert Mode Toggle

    [Fact]
    public void InsertMode_DefaultOff_OverwritesCharacters()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 2);
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;3H"); // CUP(1,3) → col 2
        GhosttyTestFixture.Feed(terminal, "X");
        // Default replace mode: X overwrites C
        AssertPlainText(terminal, 0, "ABXDE");
    }

    [Fact]
    public void InsertMode_Enable_InsertsCharacters()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 2);
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;3H"); // CUP(1,3) → col 2
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Enable IRM
        GhosttyTestFixture.Feed(terminal, "X");
        // Insert mode: X inserted at col 2, CDE shifted right
        AssertPlainText(terminal, 0, "ABXCDE");
    }

    [Fact]
    public void InsertMode_DisableWithCSI4l_ReturnsToReplace()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 2);
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Enable IRM
        GhosttyTestFixture.Feed(terminal, "\x1b[4l"); // Disable IRM
        GhosttyTestFixture.Feed(terminal, "\x1b[1;3H"); // CUP(1,3) → col 2
        GhosttyTestFixture.Feed(terminal, "X");
        // Back to replace mode: X overwrites C
        AssertPlainText(terminal, 0, "ABXDE");
    }

    [Fact]
    public void InsertMode_MultipleCharsInserted()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 2);
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;3H"); // CUP(1,3) → col 2
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Enable IRM
        GhosttyTestFixture.Feed(terminal, "XYZ");
        // Each char inserts: AB → ABXYZCDE
        AssertPlainText(terminal, 0, "ABXYZCDE");
    }

    #endregion

    #region Edge Cases — Line Boundary

    [Fact]
    public void InsertMode_PushesCharsOffEnd()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 2);
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // CUP(1,1) → col 0
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Enable IRM
        GhosttyTestFixture.Feed(terminal, "X");
        // X inserted at col 0, E pushed off right edge
        AssertPlainText(terminal, 0, "XABCD");
    }

    [Fact]
    public void InsertMode_PushedCharsDoNotWrapToNextLine()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 2);
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // CUP(1,1) → col 0
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Enable IRM
        GhosttyTestFixture.Feed(terminal, "XY");
        // XY inserted, DE pushed off edge (not wrapped)
        AssertPlainText(terminal, 0, "XYABC");
        AssertPlainText(terminal, 1, "");
    }

    [Fact]
    public void InsertMode_AtEndOfLine_PendingWrap()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 3);
        GhosttyTestFixture.Feed(terminal, "ABCDE"); // Fills line, pending wrap
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Enable IRM
        GhosttyTestFixture.Feed(terminal, "X");
        // Pending wrap resolves: X goes to next line (insert mode at start of new line is just normal print)
        AssertPlainText(terminal, 0, "ABCDE");
        AssertPlainText(terminal, 1, "X");
    }

    [Fact]
    public void InsertMode_FillEntireLine()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 2);
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Enable IRM
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        // Insert mode on empty line, each char shifts previous ones
        AssertPlainText(terminal, 0, "ABCDE");
    }

    [Fact]
    public void InsertMode_InsertAtCol0_EmptyLine()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 2);
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Enable IRM
        GhosttyTestFixture.Feed(terminal, "ABC");
        AssertPlainText(terminal, 0, "ABC");
        Assert.Equal(3, terminal.CursorX);
    }

    #endregion

    #region Wide Characters

    [Fact]
    public void InsertMode_WideCharInserted()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 2);
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;3H"); // CUP(1,3) → col 2
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Enable IRM
        GhosttyTestFixture.Feed(terminal, "😀");
        // Wide char (2 cells) inserted at col 2, shifts CDE right by 2
        AssertPlainText(terminal, 0, "AB😀CDE");
    }

    [Fact]
    public void InsertMode_WideCharPushedOff()
    {
        // Wide char at cols 3-4 gets split when pushed to 4-5 (col 4 is last)
        using var terminal = CreateTerminal(cols: 5, rows: 2);
        GhosttyTestFixture.Feed(terminal, "AB😀");
        // AB at 0-1, 😀 at 2-3, cursor at 4
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // CUP(1,1) → col 0
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Enable IRM
        GhosttyTestFixture.Feed(terminal, "X");
        // X at 0, AB shifts to 1-2, 😀 would go to 3-4 which fits in 5 cols
        AssertPlainText(terminal, 0, "XAB😀");
    }

    [Fact]
    public void InsertMode_WideCharSplitAtEdge()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 2);
        GhosttyTestFixture.Feed(terminal, "AB😀X");
        // AB at 0-1, 😀 at 2-3, X at 4
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // CUP(1,1) → col 0
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Enable IRM
        GhosttyTestFixture.Feed(terminal, "Z");
        // Z at 0, AB shifts 1-2, 😀 shifts 3-4, X pushed off
        AssertPlainText(terminal, 0, "ZAB😀");
    }

    [Fact]
    public void InsertMode_WideCharDoesntFitAtEnd()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 2);
        GhosttyTestFixture.Feed(terminal, "ABCD");
        // Cursor at col 4
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Enable IRM
        GhosttyTestFixture.Feed(terminal, "😀");
        // Wide char at col 4 can't fit (needs 2 cells), wraps to next line
        AssertPlainText(terminal, 0, "ABCD");
        AssertPlainText(terminal, 1, "😀");
    }

    #endregion

    #region SGR Attributes Preservation

    [Fact]
    public void InsertMode_PreservesAttributesOnShiftedChars()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 2);
        GhosttyTestFixture.Feed(terminal, "\x1b[31m"); // Red foreground
        GhosttyTestFixture.Feed(terminal, "ABC");
        GhosttyTestFixture.Feed(terminal, "\x1b[0m"); // Reset
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // CUP(1,1) → col 0
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Enable IRM
        GhosttyTestFixture.Feed(terminal, "X");
        // X should not have red, shifted A should still be red
        AssertPlainText(terminal, 0, "XABC");

        // Shifted 'A' (now at col 1) should still have red foreground
        var cellA = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.Equal("A", cellA.Character);
        Assert.NotNull(cellA.Foreground);
    }

    [Fact]
    public void InsertMode_InsertedBlankHasCurrentBg()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 2);
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;3H"); // CUP(1,3) → col 2
        GhosttyTestFixture.Feed(terminal, "\x1b[41m"); // Red background
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Enable IRM
        GhosttyTestFixture.Feed(terminal, "X");
        // X at col 2 should have red background
        var cellX = GhosttyTestFixture.GetCell(terminal, 0, 2);
        Assert.Equal("X", cellX.Character);
        Assert.NotNull(cellX.Background);
    }

    #endregion

    #region Interaction with Other Operations

    [Fact]
    public void InsertMode_CursorMoveDoesNotInsert()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 2);
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Enable IRM
        // CUP should not insert — only printing characters do
        GhosttyTestFixture.Feed(terminal, "\x1b[1;3H"); // CUP(1,3) → col 2
        AssertPlainText(terminal, 0, "ABCDE");
    }

    [Fact]
    public void InsertMode_PersistsAcrossLines()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 3);
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Enable IRM
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        GhosttyTestFixture.Feed(terminal, "\r\n"); // Move to next line
        GhosttyTestFixture.Feed(terminal, "FG");
        GhosttyTestFixture.Feed(terminal, "\x1b[2;1H"); // CUP(2,1) → col 0
        GhosttyTestFixture.Feed(terminal, "X");
        // Insert mode persists: X inserted before FG
        AssertPlainText(terminal, 1, "XFG");
    }

    [Fact]
    public void InsertMode_WithBackspace()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 2);
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;4H"); // CUP(1,4) → col 3
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Enable IRM
        GhosttyTestFixture.Feed(terminal, "X");
        // X inserted at col 3, DE shifts right
        AssertPlainText(terminal, 0, "ABCXDE");
        // Backspace moves cursor but doesn't insert
        GhosttyTestFixture.Feed(terminal, "\b");
        Assert.Equal(3, terminal.CursorX);
    }

    [Fact]
    public void InsertMode_WithTab()
    {
        using var terminal = CreateTerminal(cols: 20, rows: 2);
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // CUP(1,1)
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Enable IRM
        // Tab should not insert — it's a control character
        GhosttyTestFixture.Feed(terminal, "\t");
        // Cursor moves to next tab stop but doesn't shift anything
        Assert.Equal(8, terminal.CursorX);
        AssertPlainText(terminal, 0, "ABCDE");
    }

    #endregion

    #region Tokenizer Roundtrip

    [Fact]
    public void StandardModeToken_ParsedCorrectly()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 2);
        // Verify CSI 4 h is not treated as unrecognized
        GhosttyTestFixture.Feed(terminal, "AB");
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Enable IRM
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // Back to start
        GhosttyTestFixture.Feed(terminal, "X");
        // If token was parsed correctly, X should insert before A
        AssertPlainText(terminal, 0, "XAB");
    }

    [Fact]
    public void StandardModeToken_DisableParsedCorrectly()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 2);
        GhosttyTestFixture.Feed(terminal, "AB");
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Enable IRM
        GhosttyTestFixture.Feed(terminal, "\x1b[4l"); // Disable IRM
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // Back to start
        GhosttyTestFixture.Feed(terminal, "X");
        // Replace mode: X overwrites A
        AssertPlainText(terminal, 0, "XB");
    }

    #endregion

    #region Stress / Boundary Tests

    [Fact]
    public void InsertMode_SingleColumnTerminal()
    {
        using var terminal = CreateTerminal(cols: 1, rows: 3);
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Enable IRM
        GhosttyTestFixture.Feed(terminal, "A");
        AssertPlainText(terminal, 0, "A");
        // Insert B — A pushed off the 1-col line
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // Back to start
        GhosttyTestFixture.Feed(terminal, "B");
        AssertPlainText(terminal, 0, "B");
    }

    [Fact]
    public void InsertMode_InsertManyCharsRapidly()
    {
        using var terminal = CreateTerminal(cols: 20, rows: 2);
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // CUP(1,1)
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Enable IRM
        GhosttyTestFixture.Feed(terminal, "1234567890");
        // 10 chars inserted at col 0, ABCDE shifted right
        AssertPlainText(terminal, 0, "1234567890ABCDE");
    }

    [Fact]
    public void InsertMode_ExactlyFillsLine()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 2);
        GhosttyTestFixture.Feed(terminal, "AB");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // CUP(1,1)
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Enable IRM
        GhosttyTestFixture.Feed(terminal, "XYZ");
        // XYZ inserted, AB shifted right: XYZAB (exactly 5 cols)
        AssertPlainText(terminal, 0, "XYZAB");
    }

    [Fact]
    public void InsertMode_OverflowExactly()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 2);
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // CUP(1,1)
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Enable IRM
        GhosttyTestFixture.Feed(terminal, "XY");
        // XY at 0-1, ABC shifted to 2-4, DE lost
        AssertPlainText(terminal, 0, "XYABC");
    }

    #endregion
}
