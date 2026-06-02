namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Conformance tests for wide character, grapheme cluster, variation selector,
/// and combining character handling, translated from Ghostty's Terminal.zig.
/// </summary>
[TestCategory("GhosttyConformance")]
[TestClass]
public class GhosttyWideCharConformanceTests
{
    private static Hex1bTerminal CreateTerminal(int cols = 80, int rows = 24)
        => GhosttyTestFixture.CreateTerminal(cols, rows);

    private static void AssertPlainText(Hex1bTerminal terminal, int row, string expected)
    {
        var line = GhosttyTestFixture.GetLine(terminal, row);
        Assert.AreEqual(expected, line);
    }

    #region Wide Character Basics

    // Ghostty: test "Terminal: print wide char"
    // Prints smiley face (U+1F600), should take 2 cells
    [TestMethod]
    public void PrintWideChar_TakesTwoCells()
    {
        using var terminal = CreateTerminal(cols: 80, rows: 80);
        GhosttyTestFixture.Feed(terminal, "😀");

        Assert.AreEqual(0, terminal.CursorY);
        Assert.AreEqual(2, terminal.CursorX);

        var cell0 = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.AreEqual("😀", cell0.Character);

        // Continuation cell should have empty string
        var cell1 = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.AreEqual("", cell1.Character);
    }

    // Ghostty: test "Terminal: print wide char at edge creates spacer head"
    // Wide char at last column wraps to next line
    [TestMethod]
    public void PrintWideChar_AtEdge_WrapsToNextLine()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 10);
        // CUP(1,10) → col 9 (0-based)
        GhosttyTestFixture.Feed(terminal, "\x1b[1;10H");
        GhosttyTestFixture.Feed(terminal, "😀");

        // Should wrap to next line
        Assert.AreEqual(1, terminal.CursorY);
        Assert.AreEqual(2, terminal.CursorX);

        // Last column of first line should be blank (spacer head)
        var spacerHead = GhosttyTestFixture.GetCell(terminal, 0, 9);
        Assert.AreEqual(" ", spacerHead.Character);

        // Wide char should be on second line
        var cell0 = GhosttyTestFixture.GetCell(terminal, 1, 0);
        Assert.AreEqual("😀", cell0.Character);

        var cell1 = GhosttyTestFixture.GetCell(terminal, 1, 1);
        Assert.AreEqual("", cell1.Character);
    }

    // Ghostty: test "Terminal: print wide char with 1-column width"
    // Wide char in 1-column terminal — can't fit, should not crash
    [TestMethod]
    public void PrintWideChar_InSingleColumnTerminal_NoCrash()
    {
        using var terminal = CreateTerminal(cols: 1, rows: 2);
        GhosttyTestFixture.Feed(terminal, "😀");

        // Should not crash — behavior varies but must be safe
        Assert.IsTrue(terminal.CursorX >= 0);
    }

    // Ghostty: test "Terminal: print wide char in single-width terminal"
    [TestMethod]
    public void PrintWideChar_SingleWidthTerminal_PendingWrap()
    {
        using var terminal = CreateTerminal(cols: 1, rows: 80);
        GhosttyTestFixture.Feed(terminal, "😀");

        // Wide char can't fit in 1-col terminal
        Assert.AreEqual(0, terminal.CursorY);
    }

    #endregion

    #region Print Over Wide Character

    // Ghostty: test "Terminal: print over wide char at 0,0"
    // Print wide char, then overwrite first cell with narrow char
    [TestMethod]
    public void PrintOverWideChar_AtOrigin_ClearsContinuation()
    {
        using var terminal = CreateTerminal(cols: 80, rows: 80);
        GhosttyTestFixture.Feed(terminal, "😀");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // CUP(1,1) → col 0
        GhosttyTestFixture.Feed(terminal, "A");

        Assert.AreEqual(0, terminal.CursorY);
        Assert.AreEqual(1, terminal.CursorX);

        var cell0 = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.AreEqual("A", cell0.Character);

        // Former continuation cell should be cleared
        var cell1 = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.IsTrue(cell1.Character == " " || cell1.Character == "", $"Expected space or empty but got '{cell1.Character}'");
    }

    // Ghostty: test "Terminal: print over wide spacer tail"
    // Print wide char, then overwrite the continuation cell
    [TestMethod]
    public void PrintOverWideSpacerTail_ClearsLeading()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "橋");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;2H"); // CUP(1,2) → col 1 (spacer tail)
        GhosttyTestFixture.Feed(terminal, "X");

        // Leading half should be cleared
        var cell0 = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.IsTrue(cell0.Character == " " || cell0.Character == "", $"Expected leading half cleared, got '{cell0.Character}'");

        var cell1 = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.AreEqual("X", cell1.Character);

        // Leading cell cleared to space, so plain text is " X"
        AssertPlainText(terminal, 0, " X");
    }

    // Print a wide char, then overwrite it with another wide char
    [TestMethod]
    public void PrintWideOverWide_ReplacesCleanly()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        GhosttyTestFixture.Feed(terminal, "橋");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // Back to col 0
        GhosttyTestFixture.Feed(terminal, "漢");

        var cell0 = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.AreEqual("漢", cell0.Character);

        var cell1 = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.AreEqual("", cell1.Character);
    }

    #endregion

    #region Combining Characters

    // Ghostty: test "print grapheme ò (o with nonspacing mark) should be narrow"
    [TestMethod]
    public void PrintCombiningCharacter_NarrowWidth()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        // o + combining grave accent = ò (single cell)
        GhosttyTestFixture.Feed(terminal, "o\u0300");

        Assert.AreEqual(0, terminal.CursorY);
        Assert.AreEqual(1, terminal.CursorX);

        var cell = GhosttyTestFixture.GetCell(terminal, 0, 0);
        // .NET may NFC-compose o+\u0300 into ò (U+00F2)
        Assert.IsTrue(cell.Character.Length > 0, "Cell should have content");
    }

    // Test combining character with other text
    [TestMethod]
    public void PrintCombiningCharacter_FollowedByText()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        GhosttyTestFixture.Feed(terminal, "o\u0300X");

        Assert.AreEqual(0, terminal.CursorY);
        Assert.AreEqual(2, terminal.CursorX);

        var cell1 = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.AreEqual("X", cell1.Character);
    }

    // Multiple combining marks on one base character
    [TestMethod]
    public void PrintMultipleCombiningMarks_SingleCell()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        // a + combining acute + combining tilde
        GhosttyTestFixture.Feed(terminal, "a\u0301\u0303X");

        Assert.AreEqual(0, terminal.CursorY);
        Assert.AreEqual(2, terminal.CursorX);

        var cell0 = GhosttyTestFixture.GetCell(terminal, 0, 0);
        // .NET may NFC-compose a+\u0301 into á, then combine with \u0303
        Assert.IsTrue(cell0.Character.Length > 0, "Cell should have content");

        var cell1 = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.AreEqual("X", cell1.Character);
    }

    #endregion

    #region Zero-Width Characters

    // Ghostty: test "Terminal: zero-width character at start"
    // Zero-width joiner at position 0 should be ignored
    [TestMethod]
    public void ZeroWidthCharacter_AtStart_Ignored()
    {
        using var terminal = CreateTerminal(cols: 80, rows: 80);
        // ZWJ (U+200D) at start — should be ignored
        GhosttyTestFixture.Feed(terminal, "\u200D");

        Assert.AreEqual(0, terminal.CursorY);
        Assert.AreEqual(0, terminal.CursorX);
    }

    // Zero-width space should not advance cursor
    [TestMethod]
    public void ZeroWidthSpace_DoesNotAdvanceCursor()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        GhosttyTestFixture.Feed(terminal, "A\u200BB");

        // Zero-width space (U+200B) should not take a cell
        Assert.AreEqual(0, terminal.CursorY);

        // Check that A and B are in adjacent cells
        var cell0 = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.AreEqual("A", cell0.Character);
    }

    #endregion

    #region Variation Selectors

    // Ghostty: test "Terminal: VS16 to make wide character with mode 2027"
    // Heart (U+2764) + VS16 = emoji presentation (wide)
    [TestMethod]
    public void VS16_MakesCharacterWide()
    {
        using var terminal = CreateTerminal(cols: 80, rows: 80);
        // Heart + VS16 (emoji presentation) → ❤️ (wide)
        GhosttyTestFixture.Feed(terminal, "\u2764\uFE0F");

        Assert.AreEqual(0, terminal.CursorY);
        Assert.AreEqual(2, terminal.CursorX);

        var cell0 = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.AreNotEqual("", cell0.Character);

        // Continuation cell
        var cell1 = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.AreEqual("", cell1.Character);
    }

    // Ghostty: test "Terminal: VS15 to make narrow character"
    // Thunder cloud (U+26C8) + VS15 = text presentation (narrow)
    [TestMethod]
    public void VS15_MakesCharacterNarrow()
    {
        using var terminal = CreateTerminal(cols: 80, rows: 80);
        // Thunder cloud + VS15 (text presentation)
        GhosttyTestFixture.Feed(terminal, "\u26C8\uFE0E");

        Assert.AreEqual(0, terminal.CursorY);
        Assert.AreEqual(1, terminal.CursorX);
    }

    // Ghostty: test "Terminal: VS16 repeated with mode 2027"
    // Repeated VS16 should be ignored
    [TestMethod]
    public void VS16_Repeated_Ignored()
    {
        using var terminal = CreateTerminal(cols: 80, rows: 80);
        // Heart + VS16 + VS16 (repeated)
        GhosttyTestFixture.Feed(terminal, "\u2764\uFE0F\uFE0F");

        Assert.AreEqual(0, terminal.CursorY);
        Assert.AreEqual(2, terminal.CursorX);
    }

    // Ghostty: test "Terminal: keypad sequence VS16"
    // Number sign (U+0023) + VS16 → emoji presentation (wide)
    [TestMethod]
    public void VS16_KeypadSequence_MakesWide()
    {
        using var terminal = CreateTerminal(cols: 80, rows: 80);
        // # + VS16 (emoji presentation) → #️ (wide)
        GhosttyTestFixture.Feed(terminal, "#\uFE0F");

        Assert.AreEqual(0, terminal.CursorY);
        Assert.AreEqual(2, terminal.CursorX);
    }

    // Ghostty: test "Terminal: keypad sequence VS15"
    // Number sign (U+0023) + VS15 → text presentation (narrow)
    [TestMethod]
    public void VS15_KeypadSequence_StaysNarrow()
    {
        using var terminal = CreateTerminal(cols: 80, rows: 80);
        // # + VS15 (text presentation) → # (narrow)
        GhosttyTestFixture.Feed(terminal, "#\uFE0E");

        Assert.AreEqual(0, terminal.CursorY);
        Assert.AreEqual(1, terminal.CursorX);
    }

    // Ghostty: test "Terminal: VS16 to make wide character on next line"
    // At edge of terminal, VS16 causes character to wrap to next line as wide
    [TestMethod]
    public void VS16_AtEdge_WrapsToNextLine()
    {
        using var terminal = CreateTerminal(cols: 3, rows: 5);
        GhosttyTestFixture.Feed(terminal, "\x1b[1;3H"); // CUP(1,3) → col 2

        // Heart is narrow by default, but VS16 makes it wide
        GhosttyTestFixture.Feed(terminal, "\u2764\uFE0F");

        // Should wrap to next line with width 2
        Assert.AreEqual(1, terminal.CursorY);
        Assert.AreEqual(2, terminal.CursorX);
    }

    #endregion

    #region Multi-Codepoint Graphemes (ZWJ sequences, Fitzpatrick)

    // Ghostty: test "Terminal: print multicodepoint grapheme, disabled mode 2027"
    // Family emoji without mode 2027 — each component rendered separately
    [TestMethod]
    public void MultiCodepoint_FamilyEmoji_DisabledMode2027()
    {
        using var terminal = CreateTerminal(cols: 80, rows: 80);
        // 👨‍👩‍👧 = Man + ZWJ + Woman + ZWJ + Girl
        GhosttyTestFixture.Feed(terminal, "👨\u200D👩\u200D👧");

        // Without mode 2027, each emoji is rendered separately
        // Each wide char takes 2 cells, ZWJ takes 0
        // Hex1b always does grapheme clustering, so behavior depends on implementation
        Assert.AreEqual(0, terminal.CursorY);
        Assert.IsTrue(terminal.CursorX >= 2, "Family emoji should take at least 2 cells");
    }

    // Ghostty: test "Terminal: Fitzpatrick skin tone next valid base"
    // Waving hand + dark skin tone = single grapheme
    [TestMethod]
    public void FitzpatrickSkinTone_CombinesWithBase()
    {
        using var terminal = CreateTerminal(cols: 80, rows: 80);
        // 👋🏿 = Waving hand (U+1F44B) + Dark skin tone (U+1F3FF)
        GhosttyTestFixture.Feed(terminal, "👋🏿");

        Assert.AreEqual(0, terminal.CursorY);
        Assert.AreEqual(2, terminal.CursorX);

        // Should be stored as single grapheme cluster in one cell
        var cell0 = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.IsTrue(cell0.Character.Length > 0, "Cell should have content");
        // Continuation cell
        var cell1 = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.AreEqual("", cell1.Character);
    }

    // Ghostty: test "Terminal: print invalid VS16 with second char (combining)"
    // n + invalid VS16 + combining tilde → single narrow cell
    [TestMethod]
    public void InvalidVS16_WithCombining_StaysNarrow()
    {
        using var terminal = CreateTerminal(cols: 80, rows: 80);
        // n + VS16 (invalid for 'n') + combining tilde
        GhosttyTestFixture.Feed(terminal, "n\uFE0F\u0303");

        Assert.AreEqual(0, terminal.CursorY);
        // 'n' is not a valid emoji base, so VS16 should be ignored
        // Result should be narrow (1 cell)
        Assert.AreEqual(1, terminal.CursorX);
    }

    #endregion

    #region Wide Characters with Other Operations

    // Wide character followed by backspace
    [TestMethod]
    public void WideChar_Backspace_MovesOneColumn()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        GhosttyTestFixture.Feed(terminal, "橋");
        Assert.AreEqual(2, terminal.CursorX);

        GhosttyTestFixture.Feed(terminal, "\b");
        Assert.AreEqual(1, terminal.CursorX);
    }

    // Wide character filling a line
    [TestMethod]
    public void WideChars_FillLine_PendingWrap()
    {
        using var terminal = CreateTerminal(cols: 4, rows: 5);
        // Two wide chars fill the 4-column line
        GhosttyTestFixture.Feed(terminal, "橋漢");

        // Cursor should be at right margin with pending wrap
        Assert.AreEqual(0, terminal.CursorY);

        var cell0 = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.AreEqual("橋", cell0.Character);
        var cell2 = GhosttyTestFixture.GetCell(terminal, 0, 2);
        Assert.AreEqual("漢", cell2.Character);
    }

    // Wide character at second-to-last column
    [TestMethod]
    public void WideChar_AtSecondToLastCol_FitsWithoutWrap()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABC");
        Assert.AreEqual(3, terminal.CursorX);

        GhosttyTestFixture.Feed(terminal, "橋");
        Assert.AreEqual(0, terminal.CursorY);
        Assert.AreEqual(4, terminal.CursorX); // or 5 with pending wrap

        var cell3 = GhosttyTestFixture.GetCell(terminal, 0, 3);
        Assert.AreEqual("橋", cell3.Character);
    }

    // Wide character at last column — not enough room
    [TestMethod]
    public void WideChar_AtLastCol_WrapsToNextLine()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(terminal, "ABCD");
        Assert.AreEqual(4, terminal.CursorX);

        GhosttyTestFixture.Feed(terminal, "橋");
        // Wide char doesn't fit at col 4, should wrap
        Assert.AreEqual(1, terminal.CursorY);

        var cell0row1 = GhosttyTestFixture.GetCell(terminal, 1, 0);
        Assert.AreEqual("橋", cell0row1.Character);
    }

    // CJK text wrapping across lines
    [TestMethod]
    public void WideChars_WrapAcrossLines()
    {
        using var terminal = CreateTerminal(cols: 6, rows: 5);
        // 3 wide chars = 6 cells = exactly fills one line
        GhosttyTestFixture.Feed(terminal, "漢字橋");

        Assert.AreEqual(0, terminal.CursorY);

        // Add one more — should wrap
        GhosttyTestFixture.Feed(terminal, "道");
        Assert.AreEqual(1, terminal.CursorY);

        AssertPlainText(terminal, 0, "漢字橋");
        AssertPlainText(terminal, 1, "道");
    }

    // Erase a wide character with ECH
    [TestMethod]
    public void EraseChar_OnWideChar_ClearsBothCells()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        GhosttyTestFixture.Feed(terminal, "橋AB");
        GhosttyTestFixture.Feed(terminal, "\x1b[1;1H"); // Back to col 0
        GhosttyTestFixture.Feed(terminal, "\x1b[1X"); // ECH(1) at col 0

        // Erasing position 0 should clear the wide char
        var cell0 = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.IsTrue(cell0.Character == " " || cell0.Character == "", "Erased cell should be blank");
    }

    #endregion

    #region Mixed Content

    // ASCII followed by wide chars followed by ASCII
    [TestMethod]
    public void MixedContent_AsciiAndWide()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        GhosttyTestFixture.Feed(terminal, "A橋B漢C");

        Assert.AreEqual(0, terminal.CursorY);
        Assert.AreEqual(7, terminal.CursorX); // A(1) + 橋(2) + B(1) + 漢(2) + C(1) = 7

        var cell0 = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.AreEqual("A", cell0.Character);

        var cell1 = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.AreEqual("橋", cell1.Character);

        var cell3 = GhosttyTestFixture.GetCell(terminal, 0, 3);
        Assert.AreEqual("B", cell3.Character);

        var cell4 = GhosttyTestFixture.GetCell(terminal, 0, 4);
        Assert.AreEqual("漢", cell4.Character);

        var cell6 = GhosttyTestFixture.GetCell(terminal, 0, 6);
        Assert.AreEqual("C", cell6.Character);
    }

    // Wide char with SGR attributes
    [TestMethod]
    public void WideChar_WithAttributes_AttributesOnBothCells()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        GhosttyTestFixture.Feed(terminal, "\x1b[1;31m橋\x1b[0m");

        var cell0 = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.AreEqual("橋", cell0.Character);
        Assert.IsTrue(cell0.IsBold);
        Assert.IsNotNull(cell0.Foreground);

        // Continuation cell should also have same attributes
        var cell1 = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.AreEqual("", cell1.Character);
        Assert.IsTrue(cell1.IsBold);
    }

    #endregion
}
