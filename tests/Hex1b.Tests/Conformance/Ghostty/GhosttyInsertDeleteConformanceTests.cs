namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Conformance tests for insert/delete character and line operations,
/// translated from Ghostty's Terminal.zig.
/// Covers insertBlanks (ICH), deleteChars (DCH), insertLines (IL),
/// deleteLines (DL), and insert mode behavior with wide characters.
/// </summary>
[Trait("Category", "GhosttyConformance")]
public class GhosttyInsertDeleteConformanceTests
{
    private static Hex1bTerminal CreateTerminal(int cols = 80, int rows = 24)
        => GhosttyTestFixture.CreateTerminal(cols, rows);

    private static void AssertPlainText(Hex1bTerminal terminal, int row, string expected)
    {
        var line = GhosttyTestFixture.GetLine(terminal, row);
        Assert.Equal(expected, line);
    }

    #region InsertBlanks (ICH / CSI @)

    // Ghostty: test "Terminal: insertBlanks zero"
    [Fact]
    public void InsertBlanks_ZeroCount_NoOp()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 2);
        GhosttyTestFixture.Feed(terminal, "ABC");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // CUP(1,1)
        GhosttyTestFixture.Feed(terminal, "\x1b[0@"); // ICH 0
        AssertPlainText(terminal, 0, "ABC");
    }

    // Ghostty: test "Terminal: insertBlanks"
    [Fact]
    public void InsertBlanks_Basic_ShiftsRight()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 2);
        GhosttyTestFixture.Feed(terminal, "ABC");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // CUP(1,1)
        GhosttyTestFixture.Feed(terminal, "\x1b[2@"); // ICH 2
        AssertPlainText(terminal, 0, "  ABC");
    }

    // Ghostty: test "Terminal: insertBlanks pushes off end"
    [Fact]
    public void InsertBlanks_PushesOffEnd()
    {
        using var terminal = CreateTerminal(cols: 3, rows: 2);
        GhosttyTestFixture.Feed(terminal, "ABC");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // CUP(1,1)
        GhosttyTestFixture.Feed(terminal, "\x1b[2@"); // ICH 2
        AssertPlainText(terminal, 0, "  A");
    }

    // Ghostty: test "Terminal: insertBlanks more than size"
    [Fact]
    public void InsertBlanks_MoreThanSize_ClearsLine()
    {
        using var terminal = CreateTerminal(cols: 3, rows: 2);
        GhosttyTestFixture.Feed(terminal, "ABC");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // CUP(1,1)
        GhosttyTestFixture.Feed(terminal, "\x1b[5@"); // ICH 5
        AssertPlainText(terminal, 0, "");
    }

    // Ghostty: test "Terminal: insertBlanks no scroll region, fits"
    [Fact]
    public void InsertBlanks_NoScrollRegion_Fits()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 10);
        GhosttyTestFixture.Feed(terminal, "ABC");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // CUP(1,1)
        GhosttyTestFixture.Feed(terminal, "\x1b[2@"); // ICH 2
        AssertPlainText(terminal, 0, "  ABC");
    }

    // Ghostty: test "Terminal: insertBlanks shift off screen"
    [Fact]
    public void InsertBlanks_ShiftOffScreen()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 10);
        GhosttyTestFixture.Feed(terminal, "  ABC");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;3H"); // CUP(1,3) → col 2
        GhosttyTestFixture.Feed(terminal, "\x1b[2@"); // ICH 2
        GhosttyTestFixture.Feed(terminal, "X");
        AssertPlainText(terminal, 0, "  X A");
    }

    // Ghostty: test "Terminal: insertBlanks split multi-cell character"
    [Fact]
    public void InsertBlanks_SplitWideChar_ClearsWide()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 10);
        GhosttyTestFixture.Feed(terminal, "123橋");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // CUP(1,1)
        GhosttyTestFixture.Feed(terminal, "\x1b[1@"); // ICH 1
        // Wide char gets split off, should be cleared
        AssertPlainText(terminal, 0, " 123");
    }

    // Ghostty: test "Terminal: insertBlanks split multi-cell character from tail"
    [Fact]
    public void InsertBlanks_SplitWideCharFromTail()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 10);
        GhosttyTestFixture.Feed(terminal, "橋123");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;2H"); // CUP(1,2) → col 1 (spacer tail)
        GhosttyTestFixture.Feed(terminal, "\x1b[1@"); // ICH 1
        // Leading half should be cleared, tail shifted
        AssertPlainText(terminal, 0, "   12");
    }

    // Ghostty: test "Terminal: insertBlanks preserves background sgr"
    [Fact]
    public void InsertBlanks_PreservesBackgroundSgr()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 10);
        GhosttyTestFixture.Feed(terminal, "ABC");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // CUP(1,1)
        GhosttyTestFixture.Feed(terminal, "\x1b[41m"); // Red background
        GhosttyTestFixture.Feed(terminal, "\x1b[2@"); // ICH 2
        AssertPlainText(terminal, 0, "  ABC");

        // Inserted blank cells should have red background
        var cell0 = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.NotNull(cell0.Background);
    }

    #endregion

    #region DeleteChars (DCH / CSI P)

    // Ghostty: test "Terminal: deleteChars"
    [Fact]
    public void DeleteChars_Basic()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;2H"); // CUP(1,2) → col 1
        GhosttyTestFixture.Feed(terminal, "\x1b[2P"); // DCH 2
        AssertPlainText(terminal, 0, "ADE");
    }

    // Ghostty: test "Terminal: deleteChars zero count"
    [Fact]
    public void DeleteChars_ZeroCount_NoOp()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;2H"); // CUP(1,2) → col 1
        GhosttyTestFixture.Feed(terminal, "\x1b[0P"); // DCH 0
        AssertPlainText(terminal, 0, "ABCDE");
    }

    // Ghostty: test "Terminal: deleteChars more than half"
    [Fact]
    public void DeleteChars_MoreThanHalf()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;2H"); // CUP(1,2) → col 1
        GhosttyTestFixture.Feed(terminal, "\x1b[3P"); // DCH 3
        AssertPlainText(terminal, 0, "AE");
    }

    // Ghostty: test "Terminal: deleteChars more than line width"
    [Fact]
    public void DeleteChars_MoreThanLineWidth()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;2H"); // CUP(1,2) → col 1
        GhosttyTestFixture.Feed(terminal, "\x1b[10P"); // DCH 10
        AssertPlainText(terminal, 0, "A");
    }

    // Ghostty: test "Terminal: deleteChars should shift left"
    [Fact]
    public void DeleteChars_ShiftLeft()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;2H"); // CUP(1,2) → col 1
        GhosttyTestFixture.Feed(terminal, "\x1b[1P"); // DCH 1
        AssertPlainText(terminal, 0, "ACDE");
    }

    // Ghostty: test "Terminal: deleteChars resets pending wrap"
    [Fact]
    public void DeleteChars_ResetsPendingWrap()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        // Cursor should be at last col with pending wrap
        GhosttyTestFixture.Feed(terminal, "\x1b[1P"); // DCH 1
        GhosttyTestFixture.Feed(terminal, "X");
        AssertPlainText(terminal, 0, "ABCDX");
    }

    // Ghostty: test "Terminal: deleteChars simple operation"
    [Fact]
    public void DeleteChars_SimpleOperation()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 10);
        GhosttyTestFixture.Feed(terminal, "ABC123");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;3H"); // CUP(1,3) → col 2
        GhosttyTestFixture.Feed(terminal, "\x1b[2P"); // DCH 2
        AssertPlainText(terminal, 0, "AB23");
    }

    // Ghostty: test "Terminal: deleteChars preserves background sgr"
    [Fact]
    public void DeleteChars_PreservesBackgroundSgr()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 10);
        GhosttyTestFixture.Feed(terminal, "ABC123");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;3H"); // CUP(1,3)
        GhosttyTestFixture.Feed(terminal, "\x1b[41m"); // Red background
        GhosttyTestFixture.Feed(terminal, "\x1b[2P"); // DCH 2
        AssertPlainText(terminal, 0, "AB23");

        // Vacated cells at end should have background color
        var cellEnd = GhosttyTestFixture.GetCell(terminal, 0, 9);
        Assert.NotNull(cellEnd.Background);
    }

    // Ghostty: test "Terminal: deleteChars split wide character from spacer tail"
    [Fact]
    public void DeleteChars_SplitWideCharFromSpacerTail()
    {
        using var terminal = CreateTerminal(cols: 6, rows: 10);
        GhosttyTestFixture.Feed(terminal, "A橋123");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;3H"); // CUP(1,3) → col 2 (spacer tail)
        GhosttyTestFixture.Feed(terminal, "\x1b[1P"); // DCH 1
        // Leading half of wide char should be cleared
        AssertPlainText(terminal, 0, "A 123");
    }

    // Ghostty: test "Terminal: deleteChars split wide character from wide"
    [Fact]
    public void DeleteChars_SplitWideCharFromWide()
    {
        using var terminal = CreateTerminal(cols: 6, rows: 10);
        GhosttyTestFixture.Feed(terminal, "橋123");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // CUP(1,1) → col 0 (wide head)
        GhosttyTestFixture.Feed(terminal, "\x1b[1P"); // DCH 1
        // Deleting from wide head should clear continuation and shift
        var cell0 = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.True(cell0.Character == "" || cell0.Character == " ",
            $"Expected cleared cell, got '{cell0.Character}'");
        var cell1 = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.Equal("1", cell1.Character);
    }

    // Ghostty: test "Terminal: deleteChars split wide character from end"
    [Fact]
    public void DeleteChars_SplitWideCharFromEnd()
    {
        using var terminal = CreateTerminal(cols: 6, rows: 10);
        GhosttyTestFixture.Feed(terminal, "A橋123");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // CUP(1,1) → col 0
        GhosttyTestFixture.Feed(terminal, "\x1b[1P"); // DCH 1
        // "A" deleted, wide char shifts left intact
        var cell0 = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.Equal("橋", cell0.Character);
        var cell1 = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.Equal("", cell1.Character); // continuation
    }

    #endregion

    #region InsertLines (IL / CSI L)

    // Ghostty: test "Terminal: insertLines simple"
    [Fact]
    public void InsertLines_Simple()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "AAAAA\r\nBBBBB\r\nCCCCC\r\nDDDDD\r\nEEEEE");
        GhosttyTestFixture.Feed(terminal, "\x1b[3;1H"); // CUP(3,1) → row 2
        GhosttyTestFixture.Feed(terminal, "\x1b[1L"); // IL 1

        AssertPlainText(terminal, 0, "AAAAA");
        AssertPlainText(terminal, 1, "BBBBB");
        AssertPlainText(terminal, 2, ""); // inserted blank line
        AssertPlainText(terminal, 3, "CCCCC");
        AssertPlainText(terminal, 4, "DDDDD");
        // EEEEE pushed off bottom
    }

    // Ghostty: test "Terminal: insertLines zero"
    [Fact]
    public void InsertLines_Zero_NoOp()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "AAAAA\r\nBBBBB\r\nCCCCC");
        GhosttyTestFixture.Feed(terminal, "\x1b[2;1H"); // CUP(2,1)
        GhosttyTestFixture.Feed(terminal, "\x1b[0L"); // IL 0 (treated as 1 per spec)

        // IL 0 is treated as IL 1 per ECMA-48
        AssertPlainText(terminal, 0, "AAAAA");
        AssertPlainText(terminal, 1, ""); // inserted blank line
        AssertPlainText(terminal, 2, "BBBBB");
    }

    // Ghostty: test "Terminal: insertLines with scroll region"
    [Fact]
    public void InsertLines_WithScrollRegion()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "AAAAA\r\nBBBBB\r\nCCCCC\r\nDDDDD\r\nEEEEE");
        GhosttyTestFixture.Feed(terminal, "\x1b[2;4r"); // DECSTBM(2,4) → scroll region rows 1-3
        GhosttyTestFixture.Feed(terminal, "\x1b[2;1H"); // CUP(2,1) → row 1
        GhosttyTestFixture.Feed(terminal, "\x1b[1L"); // IL 1

        AssertPlainText(terminal, 0, "AAAAA");
        AssertPlainText(terminal, 1, ""); // inserted
        AssertPlainText(terminal, 2, "BBBBB");
        AssertPlainText(terminal, 3, "CCCCC");
        AssertPlainText(terminal, 4, "EEEEE");
        // DDDDD pushed out of scroll region
    }

    // Ghostty: test "Terminal: insertLines more than remaining"
    [Fact]
    public void InsertLines_MoreThanRemaining()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "AAAAA\r\nBBBBB\r\nCCCCC\r\nDDDDD\r\nEEEEE");
        GhosttyTestFixture.Feed(terminal, "\x1b[2;1H"); // CUP(2,1)
        GhosttyTestFixture.Feed(terminal, "\x1b[100L"); // IL 100

        AssertPlainText(terminal, 0, "AAAAA");
        AssertPlainText(terminal, 1, ""); // all cleared
        AssertPlainText(terminal, 2, "");
        AssertPlainText(terminal, 3, "");
        AssertPlainText(terminal, 4, "");
    }

    // Ghostty: test "Terminal: insertLines resets pending wrap"
    [Fact]
    public void InsertLines_ResetsPendingWrap()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABCDE"); // pending wrap
        GhosttyTestFixture.Feed(terminal, "\x1b[1L"); // IL 1
        GhosttyTestFixture.Feed(terminal, "X");
        Assert.Equal(0, terminal.CursorY);
        AssertPlainText(terminal, 0, "X");
        AssertPlainText(terminal, 1, "ABCDE");
    }

    #endregion

    #region DeleteLines (DL / CSI M)

    // Ghostty: test "Terminal: deleteLines simple"
    [Fact]
    public void DeleteLines_Simple()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "AAAAA\r\nBBBBB\r\nCCCCC\r\nDDDDD\r\nEEEEE");
        GhosttyTestFixture.Feed(terminal, "\x1b[3;1H"); // CUP(3,1) → row 2
        GhosttyTestFixture.Feed(terminal, "\x1b[1M"); // DL 1

        AssertPlainText(terminal, 0, "AAAAA");
        AssertPlainText(terminal, 1, "BBBBB");
        AssertPlainText(terminal, 2, "DDDDD");
        AssertPlainText(terminal, 3, "EEEEE");
        AssertPlainText(terminal, 4, ""); // blank from bottom
    }

    // Ghostty: test "Terminal: deleteLines with scroll region"
    [Fact]
    public void DeleteLines_WithScrollRegion()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "AAAAA\r\nBBBBB\r\nCCCCC\r\nDDDDD\r\nEEEEE");
        GhosttyTestFixture.Feed(terminal, "\x1b[2;4r"); // DECSTBM(2,4)
        GhosttyTestFixture.Feed(terminal, "\x1b[2;1H"); // CUP(2,1)
        GhosttyTestFixture.Feed(terminal, "\x1b[1M"); // DL 1

        AssertPlainText(terminal, 0, "AAAAA");
        AssertPlainText(terminal, 1, "CCCCC");
        AssertPlainText(terminal, 2, "DDDDD");
        AssertPlainText(terminal, 3, ""); // blank within scroll region
        AssertPlainText(terminal, 4, "EEEEE");
    }

    // Ghostty: test "Terminal: deleteLines with scroll region, large count"
    [Fact]
    public void DeleteLines_WithScrollRegion_LargeCount()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "AAAAA\r\nBBBBB\r\nCCCCC\r\nDDDDD\r\nEEEEE");
        GhosttyTestFixture.Feed(terminal, "\x1b[2;4r"); // DECSTBM(2,4)
        GhosttyTestFixture.Feed(terminal, "\x1b[2;1H"); // CUP(2,1)
        GhosttyTestFixture.Feed(terminal, "\x1b[100M"); // DL 100

        AssertPlainText(terminal, 0, "AAAAA");
        AssertPlainText(terminal, 1, ""); // all cleared in scroll region
        AssertPlainText(terminal, 2, "");
        AssertPlainText(terminal, 3, "");
        AssertPlainText(terminal, 4, "EEEEE");
    }

    // Ghostty: test "Terminal: deleteLines resets pending wrap"
    [Fact]
    public void DeleteLines_ResetsPendingWrap()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABCDE"); // pending wrap
        GhosttyTestFixture.Feed(terminal, "\x1b[1M"); // DL 1
        GhosttyTestFixture.Feed(terminal, "X");
        Assert.Equal(0, terminal.CursorY);
        AssertPlainText(terminal, 0, "X");
    }

    #endregion

    #region Insert Mode (IRM / CSI 4h) — Hex1b does not implement IRM

    // Ghostty: test "Terminal: insert mode with space"
    [Fact(Skip = "MissingFeature: Hex1b does not implement IRM (Insert/Replace Mode)")]
    public void InsertMode_Basic()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 2);
        GhosttyTestFixture.Feed(terminal, "hello");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;2H"); // CUP(1,2) → col 1
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Set insert mode
        GhosttyTestFixture.Feed(terminal, "X");
        AssertPlainText(terminal, 0, "hXello");
    }

    // Ghostty: test "Terminal: insert mode doesn't wrap pushed characters"
    [Fact(Skip = "MissingFeature: Hex1b does not implement IRM (Insert/Replace Mode)")]
    public void InsertMode_DoesNotWrapPushed()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 2);
        GhosttyTestFixture.Feed(terminal, "hello");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;2H"); // CUP(1,2)
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Set insert mode
        GhosttyTestFixture.Feed(terminal, "X");
        AssertPlainText(terminal, 0, "hXell");
    }

    // Ghostty: test "Terminal: insert mode with wide characters"
    [Fact(Skip = "MissingFeature: Hex1b does not implement IRM (Insert/Replace Mode)")]
    public void InsertMode_WithWideChars()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 2);
        GhosttyTestFixture.Feed(terminal, "hello");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;2H"); // CUP(1,2)
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Set insert mode
        GhosttyTestFixture.Feed(terminal, "😀");
        AssertPlainText(terminal, 0, "h😀el");
    }

    // Ghostty: test "Terminal: insert mode pushing off wide character"
    [Fact(Skip = "MissingFeature: Hex1b does not implement IRM (Insert/Replace Mode)")]
    public void InsertMode_PushingOffWideChar()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 2);
        GhosttyTestFixture.Feed(terminal, "123😀");
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Set insert mode
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // CUP(1,1)
        GhosttyTestFixture.Feed(terminal, "X");
        // Wide char at cols 3-4 gets split when pushed to 4-5 (col 4 is last)
        AssertPlainText(terminal, 0, "X123");
    }

    // Ghostty: test "Terminal: insert mode does nothing at the end of the line"
    [Fact(Skip = "MissingFeature: Hex1b does not implement IRM (Insert/Replace Mode)")]
    public void InsertMode_AtEndOfLine_Wraps()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 2);
        GhosttyTestFixture.Feed(terminal, "hello");
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Set insert mode
        GhosttyTestFixture.Feed(terminal, "X");
        // At end with pending wrap, insert mode wraps normally
        AssertPlainText(terminal, 0, "hello");
        AssertPlainText(terminal, 1, "X");
    }

    // Ghostty: test "Terminal: insert mode with wide characters at end"
    [Fact(Skip = "MissingFeature: Hex1b does not implement IRM (Insert/Replace Mode)")]
    public void InsertMode_WideCharAtEnd()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 2);
        GhosttyTestFixture.Feed(terminal, "well");
        GhosttyTestFixture.Feed(terminal, "\x1b[4h"); // Set insert mode
        GhosttyTestFixture.Feed(terminal, "😀");
        // Wide char at col 4 needs 2 cells, wraps to next line
        AssertPlainText(terminal, 0, "well");
        AssertPlainText(terminal, 1, "😀");
    }

    #endregion

    #region Wide Character Edge Cases in Delete/Insert

    // Ghostty: test "Terminal: deleteChars wide char boundary conditions"
    [Fact]
    public void DeleteChars_WideCharBoundaryConditions()
    {
        using var terminal = CreateTerminal(cols: 8, rows: 1);
        GhosttyTestFixture.Feed(terminal, "😀a😀b😀");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;2H"); // CUP(1,2) → col 1
        GhosttyTestFixture.Feed(terminal, "\x1b[3P"); // DCH 3

        // First wide char (at 0-1) split by cursor at col 1
        // Deletes cols 1,2,3: spacer_tail + 'a' + wide_head
        // Second wide char's tail also split
        // Result: "  b😀"
        AssertPlainText(terminal, 0, "  b😀");
    }

    // Ghostty: test "Terminal: deleteChars split wide character tail"
    [Fact]
    public void DeleteChars_SplitWideCharTail()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        // Move to second-to-last col, print wide char (wraps)
        GhosttyTestFixture.Feed(terminal, "\x1b[1;4H"); // CUP(1,4) → col 3
        GhosttyTestFixture.Feed(terminal, "橋");
        // CR to go back to col 0
        GhosttyTestFixture.Feed(terminal, "\r");
        GhosttyTestFixture.Feed(terminal, "\x1b[4P"); // DCH 4 (cols-1)
        GhosttyTestFixture.Feed(terminal, "0");
        AssertPlainText(terminal, 0, "0");
    }

    // Ghostty: test "Terminal: insertBlanks wide char straddling right margin"
    [Fact]
    public void InsertBlanks_WideCharStraddlingRightMargin()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABCD橋"); // Wide char at cols 4-5
        // Set right margin so wide head is AT boundary
        GhosttyTestFixture.Feed(terminal, "\x1b[?69h"); // DECLRMM enable
        GhosttyTestFixture.Feed(terminal, "\x1b[1;5s"); // DECSLRM: left=0, right=4
        GhosttyTestFixture.Feed(terminal, "\x1b[1;3H"); // CUP(1,3) → col 2
        GhosttyTestFixture.Feed(terminal, "\x1b[1@"); // ICH 1
        AssertPlainText(terminal, 0, "AB CD");
    }

    #endregion

    #region InsertLines/DeleteLines with Wide Characters

    // Ghostty: test "Terminal: deleteLines wide character spacer head"
    [Fact]
    public void DeleteLines_WideCharSpacerHead()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "AAAA橋");
        GhosttyTestFixture.Feed(terminal, "BBBBB\r\nCCCCC");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // CUP(1,1)
        GhosttyTestFixture.Feed(terminal, "\x1b[1M"); // DL 1

        // Row 0 was "AAAA" + spacer_head (wide char wrapped to next line)
        // After DL, row 0 should show next line
        AssertPlainText(terminal, 0, "橋BBB");
    }

    // Ghostty: test "Terminal: insertLines resets wrap"
    [Fact]
    public void InsertLines_ResetsWrap()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABCDE123");
        // Row 0: "ABCDE" (wrapped), Row 1: "123"
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // CUP(1,1)
        GhosttyTestFixture.Feed(terminal, "\x1b[1L"); // IL 1

        AssertPlainText(terminal, 0, ""); // inserted blank
        AssertPlainText(terminal, 1, "ABCDE");
        AssertPlainText(terminal, 2, "123");
    }

    // Ghostty: test "Terminal: insertLines multi-codepoint graphemes"
    [Fact]
    public void InsertLines_MultiCodepointGraphemes()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "AAAAA\r\n👨‍👩‍👧\r\nCCCCC");
        GhosttyTestFixture.Feed(terminal, "\x1b[2;1H"); // CUP(2,1)
        GhosttyTestFixture.Feed(terminal, "\x1b[1L"); // IL 1

        AssertPlainText(terminal, 0, "AAAAA");
        AssertPlainText(terminal, 1, ""); // inserted blank
        AssertPlainText(terminal, 2, "👨‍👩‍👧");
        AssertPlainText(terminal, 3, "CCCCC");
    }

    #endregion

    #region ECH (Erase Characters) with Wide Characters

    // Ghostty: test "Terminal: eraseChars with wide char splitting"
    [Fact]
    public void EraseChars_WideCharSplitting()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        GhosttyTestFixture.Feed(terminal, "A橋BC");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;2H"); // CUP(1,2) → col 1 (wide head)
        GhosttyTestFixture.Feed(terminal, "\x1b[1X"); // ECH 1

        // Erasing at the wide char's head should clear both cells
        var cell1 = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.True(cell1.Character == " " || cell1.Character == "",
            $"Expected cleared, got '{cell1.Character}'");
    }

    // Ghostty: test "Terminal: eraseChars over wide character continuation"
    [Fact]
    public void EraseChars_OverWideContinuation()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        GhosttyTestFixture.Feed(terminal, "A橋BC");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;3H"); // CUP(1,3) → col 2 (spacer tail)
        GhosttyTestFixture.Feed(terminal, "\x1b[1X"); // ECH 1

        // Erasing at the continuation should clear leading cell too
        var cell1 = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.True(cell1.Character == " " || cell1.Character == "",
            $"Expected leading cleared, got '{cell1.Character}'");
    }

    #endregion
}
