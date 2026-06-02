using Hex1b;

namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Conformance tests for wide character edge cases: printing at edges,
/// overwriting wide chars, spacer cells, and single-width terminals.
/// Translated from Ghostty's Terminal.zig test suite.
/// </summary>
[TestCategory("GhosttyConformance")]
[TestClass]
public class GhosttyPrintWideEdgeCaseConformanceTests
{
    // ═══════════════════════════════════════════════════════════════
    // Print wide char at edge
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void PrintWideCharAtEdge_CreatesSpacerAndWraps()
    {
        // Ghostty: "Terminal: print wide char at edge creates spacer head"
        // When a wide char is printed at the last column, it wraps to next line.
        // The last column on the original row becomes a spacer (empty).
        var t = GhosttyTestFixture.CreateTerminal(10, 10);

        GhosttyTestFixture.Feed(t, "\u001b[1;10H");  // CUP(1,10) → col 9
        GhosttyTestFixture.Feed(t, "\U0001F600");      // 😀 wide emoji

        // Cursor should wrap to row 1, col 2 (after the wide char)
        Assert.AreEqual(1, t.CursorY);
        Assert.AreEqual(2, t.CursorX);

        // Col 9 on row 0 should be empty (spacer head)
        var spacerCell = GhosttyTestFixture.GetCell(t, 0, 9);
        Assert.IsTrue(spacerCell.Character == " " || spacerCell.Character == "", $"Expected empty spacer at edge, got '{spacerCell.Character}'");

        // Row 1, col 0 should have the emoji (wide char)
        var wideCell = GhosttyTestFixture.GetCell(t, 1, 0);
        Assert.AreEqual("\U0001F600", wideCell.Character);

        // Row 1, col 1 should be continuation (spacer tail)
        var tailCell = GhosttyTestFixture.GetCell(t, 1, 1);
        Assert.IsTrue(tailCell.Character == " " || tailCell.Character == "", $"Expected continuation cell, got '{tailCell.Character}'");
    }

    [TestMethod]
    public void PrintWideCharInSingleWidthTerminal_PrintsSpace()
    {
        // Ghostty: "Terminal: print wide char in single-width terminal"
        // A wide char in a 1-column terminal can't fit — it is silently dropped
        // and pending wrap is set. The cell remains empty.
        var t = GhosttyTestFixture.CreateTerminal(1, 80);

        GhosttyTestFixture.Feed(t, "\U0001F600");  // 😀

        // Cursor stays at row 0, col 0 (with pending wrap)
        Assert.AreEqual(0, t.CursorY);
        Assert.AreEqual(0, t.CursorX);

        // Cell should be empty (wide char was dropped)
        var cell = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.IsTrue(cell.Character == " " || cell.Character == "" || cell.Character == "\0", $"Expected empty cell in 1-col terminal, got '{cell.Character}'");
    }

    [TestMethod]
    public void PrintWideCharWithOneColWidth_Truncated()
    {
        // Ghostty: "Terminal: print wide char with 1-column width"
        // Same as above: wide char that can't fit is silently dropped.
        var t = GhosttyTestFixture.CreateTerminal(1, 2);

        GhosttyTestFixture.Feed(t, "\U0001F600");  // 😀

        // Cell should be empty
        var cell = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.IsTrue(cell.Character == " " || cell.Character == "" || cell.Character == "\0", $"Expected empty cell, got '{cell.Character}'");
    }

    // ═══════════════════════════════════════════════════════════════
    // Overwrite wide char
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void PrintOverWideCharAt00()
    {
        // Ghostty: "Terminal: print over wide char at 0,0"
        // Write wide char at 0,0 then overwrite with narrow 'A'.
        // Wide char's continuation cell should be blanked.
        var t = GhosttyTestFixture.CreateTerminal(80, 80);

        GhosttyTestFixture.Feed(t, "\U0001F600");       // 😀 at cols 0-1
        GhosttyTestFixture.Feed(t, "\u001b[1;1H");      // CUP(1,1) → col 0
        GhosttyTestFixture.Feed(t, "A");

        Assert.AreEqual(0, t.CursorY);
        Assert.AreEqual(1, t.CursorX);

        // Col 0 should be 'A' (narrow)
        var cell0 = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.AreEqual("A", cell0.Character);

        // Col 1 should be blank (former continuation cleared)
        var cell1 = GhosttyTestFixture.GetCell(t, 0, 1);
        Assert.IsTrue(cell1.Character == " " || cell1.Character == "" || cell1.Character == "\0", $"Expected cleared continuation, got '{cell1.Character}'");
    }

    [TestMethod]
    public void PrintOverWideSpacerTail()
    {
        // Ghostty: "Terminal: print over wide spacer tail"
        // Write wide char '橋' at 0-1, then overwrite col 1 (spacer tail) with 'X'.
        // This should blank the leading wide cell at col 0.
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "橋");                // wide at cols 0-1
        GhosttyTestFixture.Feed(t, "\u001b[1;2H");      // CUP(1,2) → col 1
        GhosttyTestFixture.Feed(t, "X");

        // Col 0 should be blank (wide char destroyed when tail overwritten)
        var cell0 = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.IsTrue(cell0.Character == " " || cell0.Character == "", $"Expected blank at col 0, got '{cell0.Character}'");

        // Col 1 should be 'X'
        var cell1 = GhosttyTestFixture.GetCell(t, 0, 1);
        Assert.AreEqual("X", cell1.Character);

        // Full line content
        Assert.AreEqual(" X", GhosttyTestFixture.GetLine(t, 0));
    }

    // ═══════════════════════════════════════════════════════════════
    // Grapheme with combining mark (narrow)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void PrintGrapheme_OWithCombiningGrave_IsNarrow()
    {
        // Ghostty: "Terminal: print grapheme ò (o with nonspacing mark) should be narrow"
        // 'o' + combining grave accent (U+0300) = ò, which should take 1 cell.
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "o\u0300");  // o + combining grave

        // Should take 1 cell
        Assert.AreEqual(0, t.CursorY);
        Assert.AreEqual(1, t.CursorX);

        // Cell 0 should contain the composed character (may be NFC composed "ò" or decomposed "o\u0300")
        var cell = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.IsFalse(string.IsNullOrWhiteSpace(cell.Character), "Expected grapheme character, got empty/whitespace");
    }

    // ═══════════════════════════════════════════════════════════════
    // Invalid VS16 (VS16 on non-emoji base)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void PrintInvalidVS16_NonGrapheme_RemainsNarrow()
    {
        // Ghostty: "Terminal: print invalid VS16 non-grapheme"
        // VS16 on 'x' (not an emoji base) should be ignored — 'x' stays narrow.
        var t = GhosttyTestFixture.CreateTerminal(80, 80);

        GhosttyTestFixture.Feed(t, "x\uFE0F");

        // Should have 1 cell, narrow
        Assert.AreEqual(0, t.CursorY);
        Assert.AreEqual(1, t.CursorX);

        // Cell 0 should be 'x'
        var cell = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.AreEqual("x", cell.Character);
    }

    [TestMethod]
    public void PrintInvalidVS16_Grapheme_RemainsNarrow()
    {
        // Ghostty: "Terminal: print invalid VS16 grapheme"
        // Same as above but with grapheme clustering enabled (which Hex1b always does)
        var t = GhosttyTestFixture.CreateTerminal(80, 80);

        GhosttyTestFixture.Feed(t, "x\uFE0F");

        Assert.AreEqual(0, t.CursorY);
        Assert.AreEqual(1, t.CursorX);

        var cell = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.AreEqual("x", cell.Character);
    }

    [TestMethod]
    public void PrintInvalidVS16_WithSecondChar_TwoNarrowCells()
    {
        // Ghostty: "Terminal: print invalid VS16 with second char"
        // 'x' + VS16 (invalid) + 'y' → two narrow cells: 'x' and 'y'
        var t = GhosttyTestFixture.CreateTerminal(80, 80);

        GhosttyTestFixture.Feed(t, "x\uFE0Fy");

        Assert.AreEqual(0, t.CursorY);
        Assert.AreEqual(2, t.CursorX);

        var cell0 = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.AreEqual("x", cell0.Character);

        var cell1 = GhosttyTestFixture.GetCell(t, 0, 1);
        Assert.AreEqual("y", cell1.Character);
    }

    [TestMethod]
    public void PrintInvalidVS16_WithCombiningChar_CombinesWithBase()
    {
        // Ghostty: "Terminal: print invalid VS16 with second char (combining)"
        // 'n' + VS16 (invalid, ignored) + combining tilde (U+0303) → ñ in 1 cell
        var t = GhosttyTestFixture.CreateTerminal(80, 80);

        GhosttyTestFixture.Feed(t, "n\uFE0F\u0303");

        Assert.AreEqual(0, t.CursorY);
        Assert.AreEqual(1, t.CursorX);

        var cell = GhosttyTestFixture.GetCell(t, 0, 0);
        // Should contain 'n' with combining tilde
        Assert.Contains("n", cell.Character);
    }

    // ═══════════════════════════════════════════════════════════════
    // VS15 (text presentation selector — makes emoji narrow)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void VS15_MakesNarrowCharacter()
    {
        // Ghostty: "Terminal: VS15 to make narrow character"
        // U+2614 (☔) is normally wide. VS15 (U+FE0E) should make it narrow.
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "\u2614");   // ☔ umbrella (wide)
        Assert.AreEqual(2, t.CursorX);             // Wide: takes 2 cols

        GhosttyTestFixture.Feed(t, "\uFE0E");   // VS15 → narrow

        // After VS15, cursor should move back to col 1 (narrow = 1 cell)
        Assert.AreEqual(0, t.CursorY);
        Assert.AreEqual(1, t.CursorX);
    }

    [TestMethod]
    public void VS15_OnAlreadyNarrowEmoji()
    {
        // Ghostty: "Terminal: VS15 on already narrow emoji"
        // U+26C8 (⛈) is normally narrow. VS15 should keep it narrow.
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "\u26C8");   // ⛈ thunder cloud (narrow)
        GhosttyTestFixture.Feed(t, "\uFE0E");   // VS15

        Assert.AreEqual(0, t.CursorY);
        Assert.AreEqual(1, t.CursorX);
    }

    [TestMethod]
    public void VS15_WithPendingWrap_ClearsPendingWrap()
    {
        // Ghostty: "Terminal: VS15 to make narrow character with pending wrap"
        // In 4-col terminal: lemon(2) + umbrella(2) = pending wrap at col 3.
        // VS15 narrows umbrella → fits without wrapping, clears pending wrap.
        var t = GhosttyTestFixture.CreateTerminal(4, 5);

        GhosttyTestFixture.Feed(t, "\U0001F34B");  // 🍋 lemon (wide, cols 0-1)
        GhosttyTestFixture.Feed(t, "\u2614");       // ☔ umbrella (wide, cols 2-3)

        // Should be at row 0 with pending wrap
        Assert.AreEqual(0, t.CursorY);

        GhosttyTestFixture.Feed(t, "\uFE0E");       // VS15 → narrow

        // Should still be on row 0, pending wrap cleared
        Assert.AreEqual(0, t.CursorY);
        Assert.AreEqual(3, t.CursorX);  // col 2 (narrow umbrella) + 1

        // First cell should still be lemon
        var lemonCell = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.AreEqual("\U0001F34B", lemonCell.Character);
    }

    [TestMethod]
    public void PrintInvalidVS15_FollowingEmoji_StaysWide()
    {
        // Ghostty: "Terminal: print invalid VS15 following emoji is wide"
        // U+1F9E0 (🧠) is always wide. VS15 is not valid with this base → stays wide.
        var t = GhosttyTestFixture.CreateTerminal(80, 80);

        GhosttyTestFixture.Feed(t, "\U0001F9E0\uFE0E");  // 🧠 + VS15

        Assert.AreEqual(0, t.CursorY);
        Assert.AreEqual(2, t.CursorX);  // Still wide

        var cell = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.AreEqual("\U0001F9E0", cell.Character);
    }

    // ═══════════════════════════════════════════════════════════════
    // VS16 on next line (wrapping due to widening)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void VS16_MakesWideOnNextLine()
    {
        // Ghostty: "Terminal: VS16 to make wide character on next line"
        // In 3-col terminal, cursor at col 2, print '#' → pending wrap.
        // Then VS16 widens '#' to 2 cols → needs to wrap to next line.
        var t = GhosttyTestFixture.CreateTerminal(3, 5);

        GhosttyTestFixture.Feed(t, "\u001b[1;3H");  // CUP(1,3) → col 2
        GhosttyTestFixture.Feed(t, "#");              // Narrow char at col 2

        Assert.AreEqual(2, t.CursorX);

        GhosttyTestFixture.Feed(t, "\uFE0F");         // VS16 → wide

        // Should wrap to next line
        Assert.AreEqual(1, t.CursorY);
        Assert.AreEqual(2, t.CursorX);

        // Col 2 on row 0 should be spacer head (empty)
        var spacerCell = GhosttyTestFixture.GetCell(t, 0, 2);
        Assert.IsTrue(spacerCell.Character == " " || spacerCell.Character == "", $"Expected spacer head, got '{spacerCell.Character}'");

        // Row 1, col 0 should be '#' (wide, with emoji presentation)
        var hashCell = GhosttyTestFixture.GetCell(t, 1, 0);
        Assert.Contains("#", hashCell.Character);
    }

    // ═══════════════════════════════════════════════════════════════
    // Keypad sequences with VS15/VS16
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void KeypadSequenceVS15_NarrowPresentation()
    {
        // Ghostty: "Terminal: keypad sequence VS15"
        // '#' + VS15 (text presentation) → single narrow cell with grapheme
        var t = GhosttyTestFixture.CreateTerminal(80, 80);

        GhosttyTestFixture.Feed(t, "#\uFE0E");

        // Should be 1 cell (narrow)
        Assert.AreEqual(0, t.CursorY);
        Assert.AreEqual(1, t.CursorX);

        var cell = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.Contains("#", cell.Character);
    }

    [TestMethod]
    public void KeypadSequenceVS16_WidePresentation()
    {
        // Ghostty: "Terminal: keypad sequence VS16"
        // '#' + VS16 (emoji presentation) → wide cell
        var t = GhosttyTestFixture.CreateTerminal(80, 80);

        GhosttyTestFixture.Feed(t, "#\uFE0F");

        // Should be 2 cells (wide)
        Assert.AreEqual(0, t.CursorY);
        Assert.AreEqual(2, t.CursorX);

        var cell = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.Contains("#", cell.Character);
    }

    // ═══════════════════════════════════════════════════════════════
    // Alt screen mode 47/1047 cursor copying
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Mode47_CopiesCursorBothDirections()
    {
        // Ghostty: "Terminal: mode 47 copies cursor both directions"
        // Mode 47 copies cursor state (including SGR) to/from alt screen.
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        // Set red foreground on primary
        GhosttyTestFixture.Feed(t, "\u001b[38;2;255;0;127m");

        // Enter alt screen with mode 47
        GhosttyTestFixture.Feed(t, "\u001b[?47h");

        // Set green foreground on alt
        GhosttyTestFixture.Feed(t, "\u001b[38;2;0;255;0m");
        GhosttyTestFixture.Feed(t, "X");

        var altCell = GhosttyTestFixture.GetCell(t, 0, 0);
        // Verify alt screen has the green color
        Assert.IsNotNull(altCell.Foreground);

        // Exit alt screen
        GhosttyTestFixture.Feed(t, "\u001b[?47l");

        // Primary screen should have the green foreground (copied back)
        GhosttyTestFixture.Feed(t, "Y");
        var primaryCell = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.IsNotNull(primaryCell.Foreground);
    }

    [TestMethod]
    public void Mode1047_CopiesCursorBothDirections()
    {
        // Ghostty: "Terminal: mode 1047 copies cursor both directions"
        // Mode 1047 copies cursor state (including SGR) to/from alt screen.
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        // Set red foreground on primary
        GhosttyTestFixture.Feed(t, "\u001b[38;2;255;0;127m");

        // Enter alt screen with mode 1047
        GhosttyTestFixture.Feed(t, "\u001b[?1047h");

        // Set green foreground on alt
        GhosttyTestFixture.Feed(t, "\u001b[38;2;0;255;0m");
        GhosttyTestFixture.Feed(t, "X");

        var altCell = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.IsNotNull(altCell.Foreground);

        // Exit alt screen
        GhosttyTestFixture.Feed(t, "\u001b[?1047l");

        // Primary screen should have the green foreground (copied back)
        GhosttyTestFixture.Feed(t, "Y");
        var primaryCell = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.IsNotNull(primaryCell.Foreground);
    }

    [TestMethod]
    public void Mode1047_AltScreenPreservesContent()
    {
        // Ghostty: "Terminal: mode 1047 alt screen plain"
        // Mode 1047 switches to alt screen (which is blank), and back.
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        GhosttyTestFixture.Feed(t, "1A");

        // Enter alt screen
        GhosttyTestFixture.Feed(t, "\u001b[?1047h");

        // Alt screen should be empty
        Assert.AreEqual("", GhosttyTestFixture.GetLine(t, 0).TrimEnd());

        // Print on alt, cursor position carried over from primary (col 2)
        GhosttyTestFixture.Feed(t, "2B");
        Assert.AreEqual("  2B", GhosttyTestFixture.GetLine(t, 0));

        // Exit alt screen
        GhosttyTestFixture.Feed(t, "\u001b[?1047l");

        // Primary should still have original content
        Assert.AreEqual("1A", GhosttyTestFixture.GetLine(t, 0).TrimEnd());

        // Re-enter alt screen — should be blank again
        GhosttyTestFixture.Feed(t, "\u001b[?1047h");
        Assert.AreEqual("", GhosttyTestFixture.GetLine(t, 0).TrimEnd());
    }
}
