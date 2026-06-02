using Hex1b.Tokens;
using static Hex1b.Tests.Conformance.Ghostty.GhosttyTestFixture;

namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Batch 5: Horizontal tabs, alt screen modes, basic input, disabled wraparound extras.
/// Translated from Ghostty Terminal.zig conformance tests.
/// </summary>
[TestCategory("GhosttyConformance")]
[TestClass]
public class GhosttyBatch5ConformanceTests
{
    #region Horizontal Tabs

    // Ghostty: test "Terminal: horizontal tabs"
    [TestMethod]
    public void HorizontalTab_Basic()
    {
        using var terminal = CreateTerminal(cols: 20, rows: 5);
        Feed(terminal, "1");
        Feed(terminal, "\t"); // Tab from col 1 → col 8
        Assert.AreEqual(8, terminal.CursorX);

        Feed(terminal, "\t"); // Tab from col 8 → col 16
        Assert.AreEqual(16, terminal.CursorX);

        Feed(terminal, "\t"); // Tab at end → col 19
        Assert.AreEqual(19, terminal.CursorX);
        Feed(terminal, "\t"); // Tab at end again → still col 19
        Assert.AreEqual(19, terminal.CursorX);
    }

    // Ghostty: test "Terminal: horizontal tabs starting on tabstop"
    [TestMethod]
    public void HorizontalTab_StartingOnTabstop()
    {
        using var terminal = CreateTerminal(cols: 20, rows: 5);
        // Move to col 8 (a tab stop), print X, then tab from same col
        Feed(terminal, "\x1b[1;9H"); // CUP(1,9) — col 8 (0-based)
        Feed(terminal, "X");
        Feed(terminal, "\x1b[1;9H"); // Back to col 8
        Feed(terminal, "\t"); // Tab from col 8 → col 16
        Feed(terminal, "A");

        Assert.AreEqual("        X       A", GetLine(terminal, 0));
    }

    // Ghostty: test "Terminal: horizontal tabs back"
    [TestMethod]
    public void HorizontalTabBack_Basic()
    {
        using var terminal = CreateTerminal(cols: 20, rows: 5);
        // Move to end of screen
        Feed(terminal, "\x1b[1;20H"); // CUP(1,20) — col 19
        
        Feed(terminal, "\x1b[Z"); // Back tab → col 16
        Assert.AreEqual(16, terminal.CursorX);

        Feed(terminal, "\x1b[Z"); // Back tab → col 8
        Assert.AreEqual(8, terminal.CursorX);

        Feed(terminal, "\x1b[Z"); // Back tab → col 0
        Assert.AreEqual(0, terminal.CursorX);
        Feed(terminal, "\x1b[Z"); // Back tab at col 0 → still col 0
        Assert.AreEqual(0, terminal.CursorX);
    }

    // Ghostty: test "Terminal: horizontal tabs back starting on tabstop"
    [TestMethod]
    public void HorizontalTabBack_StartingOnTabstop()
    {
        using var terminal = CreateTerminal(cols: 20, rows: 5);
        Feed(terminal, "\x1b[1;9H"); // CUP(1,9) — col 8
        Feed(terminal, "X");
        Feed(terminal, "\x1b[1;9H"); // Back to col 8
        Feed(terminal, "\x1b[Z"); // Back tab from col 8 → col 0
        Feed(terminal, "A");

        Assert.AreEqual("A       X", GetLine(terminal, 0));
    }

    #endregion

    #region Alt Screen Modes

    // Ghostty: test "Terminal: mode 1049 alt screen plain"
    [TestMethod]
    public void AltScreen_Mode1049_Basic()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        // Print on primary screen
        Feed(terminal, "1A");

        // Enter alt screen (mode 1049)
        Feed(terminal, "\x1b[?1049h");
        
        // Alt screen should be empty
        Assert.AreEqual("", GetLine(terminal, 0));

        // Print on alt screen — cursor position preserved from primary
        Feed(terminal, "2B");
        Assert.AreEqual("  2B", GetLine(terminal, 0));

        // Return to primary screen
        Feed(terminal, "\x1b[?1049l");
        
        // Primary screen should be preserved
        Assert.AreEqual("1A", GetLine(terminal, 0));

        // Write after restore — cursor should be restored to primary position
        Feed(terminal, "C");
        Assert.AreEqual("1AC", GetLine(terminal, 0));
    }

    // Test re-entering alt screen clears it
    [TestMethod]
    public void AltScreen_Mode1049_ReEnterClears()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "1A");

        // Enter alt screen, write, exit
        Feed(terminal, "\x1b[?1049h");
        Feed(terminal, "XY");
        Feed(terminal, "\x1b[?1049l");
        
        // Primary preserved
        Assert.AreEqual("1A", GetLine(terminal, 0));

        // Re-enter alt screen — should be cleared
        Feed(terminal, "\x1b[?1049h");
        Assert.AreEqual("", GetLine(terminal, 0));
    }

    #endregion

    #region Basic Input (print + scroll)

    // Ghostty: test "Terminal: input with no control characters"
    [TestMethod]
    public void Input_NoControlCharacters()
    {
        using var terminal = CreateTerminal(cols: 40, rows: 40);
        Feed(terminal, "hello");
        Assert.AreEqual(0, terminal.CursorY);
        Assert.AreEqual(5, terminal.CursorX);
        Assert.AreEqual("hello", GetLine(terminal, 0));
    }

    // Ghostty: test "Terminal: input with basic wraparound"
    [TestMethod]
    public void Input_BasicWraparound()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 40);
        Feed(terminal, "helloworldabc12");
        Assert.AreEqual(2, terminal.CursorY);
        Assert.AreEqual("hello", GetLine(terminal, 0));
        Assert.AreEqual("world", GetLine(terminal, 1));
        Assert.AreEqual("abc12", GetLine(terminal, 2));
    }

    // Ghostty: test "Terminal: input that forces scroll"
    [TestMethod]
    public void Input_ForcesScroll()
    {
        using var terminal = CreateTerminal(cols: 1, rows: 5);
        Feed(terminal, "abcdef"); // 6 chars in 1-col terminal, forces scroll
        Assert.AreEqual(4, terminal.CursorY);
        Assert.AreEqual(0, terminal.CursorX);
        // First char 'a' scrolled off, remaining: b,c,d,e,f
        Assert.AreEqual("b", GetLine(terminal, 0));
        Assert.AreEqual("c", GetLine(terminal, 1));
        Assert.AreEqual("d", GetLine(terminal, 2));
        Assert.AreEqual("e", GetLine(terminal, 3));
        Assert.AreEqual("f", GetLine(terminal, 4));
    }

    #endregion

    #region Disabled Wraparound - Additional Cases

    // Ghostty: test "Terminal: disabled wraparound with wide char and no space"
    // All 5 cols filled with 'A', then try to print wide char
    [TestMethod]
    public void DisabledWraparound_WideCharFilledRow()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "\x1b[?7l"); // Disable wraparound
        Feed(terminal, "AAAAA"); // Fill all 5 cols
        Feed(terminal, "\U0001F6A8"); // Try wide char — no space at all
        Assert.AreEqual(0, terminal.CursorY);
        Assert.AreEqual(4, terminal.CursorX);
        Assert.AreEqual("AAAAA", GetLine(terminal, 0)); // Wide char not printed
    }

    #endregion

    #region Overwrite with Print Repeat

    // Ghostty: test "Terminal: overwrite" (basic overwrite)
    [TestMethod]
    public void Overwrite_Basic()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        Feed(terminal, "ABCDE");
        Feed(terminal, "\x1b[1;1H"); // CUP(1,1)
        Feed(terminal, "XYZ");
        Assert.AreEqual("XYZDE", GetLine(terminal, 0));
    }

    // Print repeat with attributes preserved
    [TestMethod]
    public void PrintRepeat_PreservesAttributes()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        Feed(terminal, "\x1b[1m"); // Bold
        Feed(terminal, "A");
        Feed(terminal, "\x1b[2b"); // REP 2
        
        var cell0 = GetCell(terminal, 0, 0);
        var cell1 = GetCell(terminal, 0, 1);
        var cell2 = GetCell(terminal, 0, 2);
        Assert.AreEqual("A", cell0.Character);
        Assert.AreEqual("A", cell1.Character);
        Assert.AreEqual("A", cell2.Character);
        Assert.IsTrue(cell1.Attributes.HasFlag(CellAttributes.Bold));
        Assert.IsTrue(cell2.Attributes.HasFlag(CellAttributes.Bold));
    }

    #endregion

    #region Newline/Carriage Return Edge Cases

    // Ghostty: test "Terminal: linefeed and carriage return"
    // This test exists in misc already, but let's test the sequence behavior directly
    [TestMethod]
    public void NewlineAndCR_BasicSequence()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        Feed(terminal, "ABC");
        Feed(terminal, "\r\n"); // CR+LF
        Feed(terminal, "DEF");
        Assert.AreEqual("ABC", GetLine(terminal, 0));
        Assert.AreEqual("DEF", GetLine(terminal, 1));
    }

    // Test that LF alone doesn't perform CR (default behavior)
    [TestMethod]
    public void Linefeed_NoCR_ByDefault()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        Feed(terminal, "ABC");
        Feed(terminal, "\n"); // LF only — should NOT do CR
        Feed(terminal, "X");
        Assert.AreEqual("ABC", GetLine(terminal, 0));
        // X should be at the same column (col 3), not at col 0
        Assert.AreEqual("   X", GetLine(terminal, 1));
    }

    #endregion

    #region Set Top and Bottom Margin Edge Cases

    // Ghostty: test "Terminal: setTopAndBottomMargin simple"
    [TestMethod]
    public void DECSTBM_Simple()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 10);
        Feed(terminal, "\x1b[3;5r"); // DECSTBM(3,5) — rows 2-4 (0-based)
        
        // Fill the screen
        for (int i = 0; i < 10; i++)
        {
            Feed(terminal, $"{i}");
            if (i < 9) Feed(terminal, "\r\n");
        }

        // After scrolling within region, rows outside region should be preserved
        // Row 0 and 1 should still have '0' and '1'
        Assert.StartsWith("0", GetLine(terminal, 0));
        Assert.StartsWith("1", GetLine(terminal, 1));
    }

    #endregion

    #region Full Reset Additional

    // Test RIS resets LNM mode
    [TestMethod]
    public void FullReset_ClearsLnm()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        Feed(terminal, "\x1b[20h"); // Enable LNM
        
        // Full reset
        Feed(terminal, "\u001bc");
        
        // After reset, LF should NOT do CR (LNM off)
        Feed(terminal, "ABC");
        Feed(terminal, "\n");
        Feed(terminal, "X");
        Assert.AreEqual("ABC", GetLine(terminal, 0));
        Assert.AreEqual("   X", GetLine(terminal, 1)); // X at col 3, not col 0
    }

    // Test RIS resets DECLRMM
    [TestMethod]
    public void FullReset_ClearsDeclrmm()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[3;7s"); // DECSLRM(3,7)
        
        // Full reset
        Feed(terminal, "\u001bc");
        
        // After reset, text should fill the full width
        Feed(terminal, "1234567890");
        Assert.AreEqual("1234567890", GetLine(terminal, 0));
    }

    #endregion
}
