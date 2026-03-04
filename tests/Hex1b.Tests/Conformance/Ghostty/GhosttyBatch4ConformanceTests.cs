using Hex1b.Tokens;
using static Hex1b.Tests.Conformance.Ghostty.GhosttyTestFixture;

namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Batch 4: Full reset (RIS), print repeat (REP), VS15, overwrite, soft wrap, zero-width chars.
/// Translated from Ghostty Terminal.zig conformance tests.
/// </summary>
[Trait("Category", "GhosttyConformance")]
public class GhosttyBatch4ConformanceTests
{
    #region Full Reset (RIS - ESC c)

    // Ghostty: test "Terminal: fullReset with a non-empty pen"
    [Fact]
    public void FullReset_ClearsPen()
    {
        using var terminal = CreateTerminal(cols: 80, rows: 80);
        // Set foreground and background colors
        Feed(terminal, "\x1b[38;2;255;0;127m"); // FG: RGB(255,0,127)
        Feed(terminal, "\x1b[48;2;255;0;127m"); // BG: RGB(255,0,127)
        
        // Full reset
        Feed(terminal, "\u001bc");
        
        // Print after reset — should have default style (no colors)
        Feed(terminal, "A");
        var cell = GetCell(terminal, 0, 0);
        Assert.Equal("A", cell.Character);
        Assert.Null(cell.Foreground); // Default foreground
        Assert.Null(cell.Background); // Default background
    }

    // Ghostty: test "Terminal: fullReset with a non-empty saved cursor"
    [Fact]
    public void FullReset_ClearsSavedCursor()
    {
        using var terminal = CreateTerminal(cols: 80, rows: 80);
        // Set colors and save cursor
        Feed(terminal, "\x1b[38;2;255;0;127m"); // FG color
        Feed(terminal, "\x1b[48;2;255;0;127m"); // BG color
        Feed(terminal, "\x1b" + "7"); // DECSC (save cursor)
        
        // Full reset
        Feed(terminal, "\u001bc");
        
        // Restore cursor — should get default state, not saved colors
        Feed(terminal, "\x1b" + "8"); // DECRC (restore cursor)
        Feed(terminal, "A");
        var cell = GetCell(terminal, 0, 0);
        Assert.Equal("A", cell.Character);
        Assert.Null(cell.Foreground);
        Assert.Null(cell.Background);
    }

    // Ghostty: test "Terminal: fullReset origin mode"
    [Fact]
    public void FullReset_ClearsOriginMode()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 10);
        // Move cursor and enable origin mode
        Feed(terminal, "\x1b[3;5H"); // CUP(3,5)
        Feed(terminal, "\x1b[?6h"); // Enable origin mode
        
        // Full reset
        Feed(terminal, "\u001bc");
        
        // Origin mode should be reset and cursor at (0,0)
        Assert.Equal(0, terminal.CursorY);
        Assert.Equal(0, terminal.CursorX);
    }

    // Test that fullReset clears screen content
    [Fact]
    public void FullReset_ClearsScreen()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        Feed(terminal, "Hello");
        Assert.Equal("Hello", GetLine(terminal, 0));
        
        // Full reset
        Feed(terminal, "\u001bc");
        
        // Screen should be cleared
        Assert.Equal("", GetLine(terminal, 0));
        Assert.Equal(0, terminal.CursorX);
        Assert.Equal(0, terminal.CursorY);
    }

    // Test that fullReset clears scroll regions
    [Fact]
    public void FullReset_ClearsScrollRegion()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 10);
        Feed(terminal, "\x1b[3;7r"); // DECSTBM(3,7)
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[3;7s"); // DECSLRM(3,7)
        
        // Full reset
        Feed(terminal, "\u001bc");
        
        // Now fill screen with text — if regions were cleared, it should wrap at screen edges
        Feed(terminal, "1234567890");
        Assert.Equal("1234567890", GetLine(terminal, 0));
    }

    // Test that fullReset resets IRM
    [Fact]
    public void FullReset_ClearsInsertMode()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        Feed(terminal, "\x1b[4h"); // Enable IRM
        Feed(terminal, "ABCDE");
        
        // Full reset
        Feed(terminal, "\u001bc");
        
        Feed(terminal, "12345");
        Feed(terminal, "\x1b[1;1H"); // CUP(1,1) — go to start
        Feed(terminal, "X"); // Should overwrite, not insert
        Assert.Equal("X2345", GetLine(terminal, 0));
    }

    // Test that fullReset resets pending wrap
    [Fact]
    public void FullReset_ClearsPendingWrap()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "12345"); // Fill row, cursor at pending wrap
        
        // Full reset
        Feed(terminal, "\u001bc");
        
        Feed(terminal, "A");
        Assert.Equal(0, terminal.CursorY);
        Assert.Equal(1, terminal.CursorX);
        Assert.Equal("A", GetLine(terminal, 0));
    }

    #endregion

    #region Print Repeat (REP - CSI b)

    // Ghostty: test "Terminal: printRepeat simple"
    [Fact]
    public void PrintRepeat_Simple()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "A");
        Feed(terminal, "\x1b[b"); // REP — repeat last char 1 time (default)
        
        Assert.Equal("AA", GetLine(terminal, 0));
    }

    // Ghostty: test "Terminal: printRepeat wrap"
    [Fact]
    public void PrintRepeat_Wrap()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "    A"); // Fill row, cursor in pending wrap
        Feed(terminal, "\x1b[b"); // REP — repeat last char
        
        Assert.Equal("    A", GetLine(terminal, 0));
        Assert.Equal("A", GetLine(terminal, 1));
    }

    // Ghostty: test "Terminal: printRepeat no previous character"
    [Fact]
    public void PrintRepeat_NoPreviousCharacter()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "\x1b[b"); // REP with no previous char — should be no-op
        
        Assert.Equal("", GetLine(terminal, 0));
        Assert.Equal(0, terminal.CursorX);
    }

    // REP with explicit count
    [Fact]
    public void PrintRepeat_WithCount()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        Feed(terminal, "X");
        Feed(terminal, "\x1b[3b"); // REP 3 — repeat 3 times
        
        Assert.Equal("XXXX", GetLine(terminal, 0)); // Original + 3 repeats
    }

    #endregion

    #region VS15 (Text Presentation Selector)

    // Ghostty: test "Terminal: VS15 to make narrow character"
    // VS15 (U+FE0E) converts wide emoji to text presentation (narrow)
    // Requires retroactive cell modification when VS15 arrives after base char
    [Fact(Skip = "VS15 retroactive width change not yet implemented")]
    public void VS15_MakeNarrowCharacter()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        // U+2614 Umbrella with rain drops — default width 2
        Feed(terminal, "\u2614");
        Assert.Equal(2, terminal.CursorX); // Wide char takes 2 cells
        
        // VS15 makes it narrow
        Feed(terminal, "\uFE0E");
        Assert.Equal(1, terminal.CursorX); // Should now take 1 cell
    }

    // Ghostty: test "Terminal: VS15 on already narrow emoji"
    [Fact]
    public void VS15_AlreadyNarrowEmoji()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        // U+26C8 Thunder cloud and rain — already narrow (width 1)
        Feed(terminal, "\u26C8");
        Assert.Equal(1, terminal.CursorX);
        
        // VS15 on already narrow — should remain at 1
        Feed(terminal, "\uFE0E");
        Assert.Equal(1, terminal.CursorX);
    }

    // Ghostty: test "Terminal: VS15 to make narrow character with pending wrap"
    // Requires retroactive cell modification when VS15 arrives after base char
    [Fact(Skip = "VS15 retroactive width change not yet implemented")]
    public void VS15_WithPendingWrap()
    {
        using var terminal = CreateTerminal(cols: 4, rows: 5);
        // U+1F34B Lemon — wide (2 cells), fills cols 0-1
        Feed(terminal, "\U0001F34B");
        // U+2614 Umbrella — wide (2 cells), fills cols 2-3, pending wrap set
        Feed(terminal, "\u2614");
        Assert.Equal(0, terminal.CursorY); // Still on row 0 (pending wrap)
        Assert.Equal(3, terminal.CursorX); // At last column
        
        // VS15 on the umbrella — makes it narrow, should resolve pending wrap
        // and leave cursor at col 3 (not wrapped)
        Feed(terminal, "\uFE0E");
        Assert.Equal(0, terminal.CursorY);
    }

    #endregion

    #region Zero-Width Character

    // Ghostty: test "Terminal: zero-width character at start"
    [Fact]
    public void ZeroWidth_AtStart()
    {
        using var terminal = CreateTerminal(cols: 80, rows: 80);
        // ZWJ at start of empty screen — should be ignored
        Feed(terminal, "\u200D");
        Assert.Equal(0, terminal.CursorY);
        Assert.Equal(0, terminal.CursorX);
    }

    #endregion

    #region Soft Wrap

    // Ghostty: test "Terminal: soft wrap"
    [Fact]
    public void SoftWrap_Basic()
    {
        using var terminal = CreateTerminal(cols: 3, rows: 80);
        Feed(terminal, "hello");
        Assert.Equal(1, terminal.CursorY); // Wrapped to next line
        Assert.Equal(2, terminal.CursorX); // 'l','o' on row 1, cursor after 'o'
        Assert.Equal("hel", GetLine(terminal, 0));
        Assert.Equal("lo", GetLine(terminal, 1));
    }

    // Test long line wrapping doesn't crash (Ghostty issue #1400)
    [Fact]
    public void SoftWrap_VeryLongLine()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        // Print 1000 characters — should not crash
        Feed(terminal, new string('x', 1000));
        // Just verify we didn't crash and cursor is in valid position
        Assert.True(terminal.CursorX >= 0 && terminal.CursorX < 5);
        Assert.True(terminal.CursorY >= 0 && terminal.CursorY < 5);
    }

    #endregion

    #region Overwrite Wide Character

    // Ghostty: test "Terminal: overwrite multicodepoint grapheme clears grapheme data"
    [Fact]
    public void Overwrite_WideCharWithNarrow()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 10);
        // Print a wide character (takes 2 cells)
        Feed(terminal, "\U0001F600"); // 😀
        Assert.Equal(2, terminal.CursorX);
        
        // Move back and overwrite with narrow character
        Feed(terminal, "\x1b[1;1H"); // CUP(1,1)
        Feed(terminal, "X");
        
        Assert.Equal(1, terminal.CursorX);
        Assert.Equal("X", GetCell(terminal, 0, 0).Character);
        // The continuation cell should be cleared
        Assert.NotEqual("\U0001F600", GetCell(terminal, 0, 1).Character);
    }

    // Overwrite wide char at tail position
    [Fact]
    public void Overwrite_WideCharTail()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 10);
        Feed(terminal, "\U0001F600"); // 😀 at cols 0-1
        
        // Move to col 1 (the tail of the wide char) and overwrite
        Feed(terminal, "\x1b[1;2H"); // CUP(1,2) — col 1 (0-based)
        Feed(terminal, "X");
        
        // Both cells of the old wide char should be affected
        Assert.NotEqual("\U0001F600", GetCell(terminal, 0, 0).Character);
        Assert.Equal("X", GetCell(terminal, 0, 1).Character);
    }

    #endregion

    #region Disabled Wraparound

    // Ghostty: test "Terminal: disabled wraparound with wide char and one space"
    [Fact]
    public void DisabledWraparound_WideCharNoSpace()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "\x1b[?7l"); // Disable wraparound
        Feed(terminal, "AAAA"); // Fill 4 columns
        
        // Try to print a wide char with only 1 cell remaining — should not fit
        Feed(terminal, "\U0001F6A8"); // 🚨 — wide char
        Assert.Equal(0, terminal.CursorY); // Should not wrap
        Assert.Equal(4, terminal.CursorX); // At last column
        Assert.Equal("AAAA", GetLine(terminal, 0)); // Wide char not printed
    }

    // Ghostty: test "Terminal: disabled wraparound" (basic)
    [Fact]
    public void DisabledWraparound_Basic()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "\x1b[?7l"); // Disable wraparound
        Feed(terminal, "ABCDEFGH"); // More chars than columns
        
        Assert.Equal(0, terminal.CursorY); // Should not wrap
        Assert.Equal("ABCDH", GetLine(terminal, 0)); // Last char overwrites col 4
    }

    #endregion

    #region Fitzpatrick Skin Tone Modifiers

    // Ghostty: test "Terminal: Fitzpatrick skin tone next to non-base"
    // .NET grapheme clustering combines modifier with non-base; needs terminal-specific splitting
    [Fact(Skip = "Fitzpatrick skin tone with non-base needs terminal-specific grapheme splitting")]
    public void FitzpatrickSkinTone_NonBase()
    {
        using var terminal = CreateTerminal(cols: 80, rows: 80);
        // Print quote, skin tone modifier, quote
        // Skin tone should NOT combine with quote
        Feed(terminal, "\"\U0001F3FF\""); // " + 🏿 + "
        
        // " is 1 cell, 🏿 is 2 cells (wide), " is 1 cell = 4 cells total
        Assert.Equal(4, terminal.CursorX);
    }

    #endregion

    #region Bold Style

    // Ghostty: test "Terminal: bold style"
    [Fact]
    public void Bold_AppliesAttribute()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "\x1b[1m"); // Bold
        Feed(terminal, "A");
        
        var cell = GetCell(terminal, 0, 0);
        Assert.Equal("A", cell.Character);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Bold));
    }

    // Ghostty: test "Terminal: default style is empty"
    [Fact]
    public void DefaultStyle_IsEmpty()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "A");
        
        var cell = GetCell(terminal, 0, 0);
        Assert.Equal("A", cell.Character);
        Assert.Null(cell.Foreground);
        Assert.Null(cell.Background);
        Assert.Equal(CellAttributes.None, cell.Attributes);
    }

    #endregion

    #region Overwrite Hyperlink

    // Ghostty: test "Terminal: overwrite hyperlink"
    // When a hyperlink cell is overwritten, the hyperlink reference should be cleared
    [Fact]
    public void Overwrite_ClearsHyperlink()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        // Set up hyperlink and print
        Feed(terminal, "\x1b]8;;http://example.com\x1b\\");
        Feed(terminal, "HELLO");
        Feed(terminal, "\x1b]8;;\x1b\\"); // End hyperlink
        
        // Overwrite the first character
        Feed(terminal, "\x1b[1;1H"); // CUP(1,1)
        Feed(terminal, "X");
        
        var cell = GetCell(terminal, 0, 0);
        Assert.Equal("X", cell.Character);
        Assert.Null(cell.TrackedHyperlink); // Hyperlink should be cleared
        
        // But the next cell should still have the hyperlink
        var cell2 = GetCell(terminal, 0, 1);
        Assert.Equal("E", cell2.Character);
        Assert.NotNull(cell2.TrackedHyperlink);
    }

    #endregion
}
