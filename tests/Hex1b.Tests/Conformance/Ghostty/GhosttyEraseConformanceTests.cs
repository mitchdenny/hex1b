using Hex1b.Theming;

namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Conformance tests for eraseDisplay (ED) and eraseLine (EL) operations,
/// translated from Ghostty's Terminal.zig.
/// </summary>
[TestCategory("GhosttyConformance")]
[TestClass]
public class GhosttyEraseConformanceTests
{
    private static Hex1bTerminal CreateTerminal(int cols = 5, int rows = 5)
        => GhosttyTestFixture.CreateTerminal(cols, rows);

    private static void AssertPlainText(Hex1bTerminal terminal, string expected)
    {
        var expectedLines = expected.Split('\n');
        for (int i = 0; i < expectedLines.Length; i++)
        {
            var actualLine = GhosttyTestFixture.GetLine(terminal, i);
            Assert.AreEqual(expectedLines[i], actualLine);
        }
        for (int i = expectedLines.Length; i < terminal.Height; i++)
        {
            var line = GhosttyTestFixture.GetLine(terminal, i);
            Assert.AreEqual("", line);
        }
    }

    // ── EraseDisplay (ED) ────────────────────────────────────────────────

    // Ghostty: test "eraseDisplay: simple erase below"
    [TestMethod]
    public void EraseDisplay_EraseBelow_ErasesFromCursorDown()
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal, "ABC\r\nDEF\r\nGHI");
        // CUP(2,2) → 0-based (1,1)
        GhosttyTestFixture.Feed(terminal, "\u001b[2;2H");
        // ED 0 — erase below (from cursor to end of screen)
        GhosttyTestFixture.Feed(terminal, "\u001b[J");

        AssertPlainText(terminal, "ABC\nD");
    }

    // Ghostty: test "eraseDisplay: simple erase above"
    [TestMethod]
    public void EraseDisplay_EraseAbove_ErasesFromCursorUp()
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal, "ABC\r\nDEF\r\nGHI");
        // CUP(2,2) → 0-based (1,1)
        GhosttyTestFixture.Feed(terminal, "\u001b[2;2H");
        // ED 1 — erase above (from beginning of screen to cursor, inclusive)
        GhosttyTestFixture.Feed(terminal, "\u001b[1J");

        AssertPlainText(terminal, "\n  F\nGHI");
    }

    // Ghostty: test "eraseDisplay: erase complete"
    [TestMethod]
    public void EraseDisplay_EraseComplete_ClearsAllContent()
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal, "ABC\r\nDEF\r\nGHI");
        // CUP(2,2) → 0-based (1,1)
        GhosttyTestFixture.Feed(terminal, "\u001b[2;2H");
        // ED 2 — erase complete
        GhosttyTestFixture.Feed(terminal, "\u001b[2J");

        // All lines should be empty
        for (int i = 0; i < terminal.Height; i++)
        {
            Assert.AreEqual("", GhosttyTestFixture.GetLine(terminal, i));
        }
        // Cursor should NOT move
        Assert.AreEqual(1, terminal.CursorX);
        Assert.AreEqual(1, terminal.CursorY);
    }

    // Ghostty: test "eraseDisplay: erase below preserves cursor"
    [TestMethod]
    public void EraseDisplay_EraseBelow_PreservesCursorPosition()
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal, "ABC\r\nDEF");
        // CUP(2,3) → 0-based (1,2)
        GhosttyTestFixture.Feed(terminal, "\u001b[2;3H");
        // ED 0
        GhosttyTestFixture.Feed(terminal, "\u001b[J");

        Assert.AreEqual(2, terminal.CursorX);
        Assert.AreEqual(1, terminal.CursorY);
        AssertPlainText(terminal, "ABC\nDE");
    }

    // ── EraseLine (EL) ──────────────────────────────────────────────────

    // Ghostty: test "eraseLine: simple erase right"
    [TestMethod]
    public void EraseLine_EraseRight_ErasesFromCursorToEnd()
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        // CUP(1,3) → 0-based col 2
        GhosttyTestFixture.Feed(terminal, "\u001b[1;3H");
        // EL 0 — erase right
        GhosttyTestFixture.Feed(terminal, "\u001b[K");

        AssertPlainText(terminal, "AB");
    }

    // Ghostty: test "eraseLine: erase right preserves cursor"
    [TestMethod]
    public void EraseLine_EraseRight_PreservesCursorPosition()
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        // CUP(1,3) → 0-based col 2
        GhosttyTestFixture.Feed(terminal, "\u001b[1;3H");
        // EL 0
        GhosttyTestFixture.Feed(terminal, "\u001b[K");

        Assert.AreEqual(2, terminal.CursorX);
        Assert.AreEqual(0, terminal.CursorY);
    }

    // Ghostty: test "eraseLine: simple erase left"
    [TestMethod]
    public void EraseLine_EraseLeft_ErasesFromCursorToStart()
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        // CUP(1,4) → 0-based col 3
        GhosttyTestFixture.Feed(terminal, "\u001b[1;4H");
        // EL 1 — erase left (cols 0-3 inclusive)
        GhosttyTestFixture.Feed(terminal, "\u001b[1K");

        AssertPlainText(terminal, "    E");
    }

    // Ghostty: test "eraseLine: erase complete"
    [TestMethod]
    public void EraseLine_EraseComplete_ClearsEntireLine()
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        // CUP(1,3) → 0-based col 2
        GhosttyTestFixture.Feed(terminal, "\u001b[1;3H");
        // EL 2 — erase complete line
        GhosttyTestFixture.Feed(terminal, "\u001b[2K");

        AssertPlainText(terminal, "");
        Assert.AreEqual(2, terminal.CursorX);
        Assert.AreEqual(0, terminal.CursorY);
    }

    // Ghostty: test "eraseLine: erase right preserves background SGR"
    // BUG: Hex1b's EL doesn't apply current background SGR to erased cells
    [TestMethod]
    public void EraseLine_EraseRight_PreservesBackgroundSgr()
    {
        using var terminal = CreateTerminal(10, 10);
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        // CUP(1,3) → 0-based col 2
        GhosttyTestFixture.Feed(terminal, "\u001b[1;3H");
        // Set background red via 24-bit SGR, then EL right
        GhosttyTestFixture.Feed(terminal, "\u001b[48;2;255;0;0m");
        GhosttyTestFixture.Feed(terminal, "\u001b[K");

        // Cols 0-1 survive ("AB"), cols 2+ erased with red background
        Assert.AreEqual("AB", GhosttyTestFixture.GetLine(terminal, 0));

        // Erased cell (0,2) should have red background
        var cell = GhosttyTestFixture.GetCell(terminal, 0, 2);
        Assert.IsNotNull(cell.Background);
        Assert.AreEqual(Hex1bColor.FromRgb(255, 0, 0), cell.Background);
    }

    // Ghostty: test "eraseLine: erase right with wide character"
    // BUG: When EL starts at a wide char spacer cell, the leading half should also be cleared
    [TestMethod]
    public void EraseLine_EraseRight_WithWideCharacter()
    {
        using var terminal = CreateTerminal();
        // A(col0) 橋(col1-2) B(col3) C(col4)
        GhosttyTestFixture.Feed(terminal, "A橋BC");
        // CUP(1,3) → 0-based col 2, which is the spacer of wide char '橋'
        GhosttyTestFixture.Feed(terminal, "\u001b[1;3H");
        // EL right — splits the wide char, both halves cleared
        GhosttyTestFixture.Feed(terminal, "\u001b[K");

        AssertPlainText(terminal, "A");
    }

    // Ghostty: test "eraseLine: resets pending wrap"
    [TestMethod]
    public void EraseLine_EraseRight_ResetsPendingWrap()
    {
        using var terminal = CreateTerminal();
        // Fill line completely — cursor at col 4 with pending wrap
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        // EL right — erases at cursor, resets pending wrap
        GhosttyTestFixture.Feed(terminal, "\u001b[K");
        // Write 'X' — should go at cursor position, not next line
        GhosttyTestFixture.Feed(terminal, "X");

        AssertPlainText(terminal, "ABCDX");
    }

    // Ghostty: test "eraseLine: resets wrap marker"
    [TestMethod]
    public void EraseLine_EraseRight_ResetsWrapMarker()
    {
        using var terminal = CreateTerminal();
        // Write "ABCDE123" — wraps: row 0="ABCDE" (wrapped), row 1="123"
        GhosttyTestFixture.Feed(terminal, "ABCDE123");
        // CUP(1,1) → back to row 0 col 0
        GhosttyTestFixture.Feed(terminal, "\u001b[1;1H");
        // EL right — clears row 0 and its wrap marker
        GhosttyTestFixture.Feed(terminal, "\u001b[K");
        // Write 'X'
        GhosttyTestFixture.Feed(terminal, "X");

        AssertPlainText(terminal, "X\n123");
    }

    // ── Edge Cases ──────────────────────────────────────────────────────

    // Ghostty: test "eraseDisplay: scrollback (ED 3)"
    [TestMethod]
    public void EraseDisplay_ClearScrollback_NotImplemented()
    {
        using var terminal = CreateTerminal();
        GhosttyTestFixture.Feed(terminal, "\u001b[3J");
    }

    // Ghostty: test "eraseLine: erase left with wide character"
    [TestMethod]
    public void EraseLine_EraseLeft_WithWideCharacter()
    {
        using var terminal = CreateTerminal();
        // A(col0) B(col1) 橋(col2-3) C(col4)
        GhosttyTestFixture.Feed(terminal, "AB橋C");
        // CUP(1,4) → 0-based col 3, right half of '橋'
        GhosttyTestFixture.Feed(terminal, "\u001b[1;4H");
        // EL left — erases cols 0-3 inclusive, splitting wide char
        GhosttyTestFixture.Feed(terminal, "\u001b[1K");

        AssertPlainText(terminal, "    C");
    }

    // Ghostty: test "eraseDisplay: erase below resets pending wrap"
    [TestMethod]
    public void EraseDisplay_EraseBelow_ResetsPendingWrap()
    {
        using var terminal = CreateTerminal();
        // Fill line completely — pending wrap
        GhosttyTestFixture.Feed(terminal, "ABCDE");
        // ED 0 — erase below, should reset pending wrap
        GhosttyTestFixture.Feed(terminal, "\u001b[J");
        // Write 'X' — should go at cursor position, not next line
        GhosttyTestFixture.Feed(terminal, "X");

        AssertPlainText(terminal, "ABCDX");
    }
}
