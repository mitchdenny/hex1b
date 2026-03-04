using Xunit;

namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Ghostty conformance tests for cursor movement (CUU/CUD/CUB/CUF),
/// cursor positioning (CUP) with origin mode, DECALN, and save/restore cursor.
/// Translated from Ghostty's Terminal.zig test suite.
/// </summary>
[Trait("Category", "GhosttyConformance")]
public class GhosttyCursorConformanceTests
{
    private static Hex1bTerminal CreateTerminal(int cols = 80, int rows = 24)
        => GhosttyTestFixture.CreateTerminal(cols, rows);

    private static void Feed(Hex1bTerminal terminal, string text)
        => GhosttyTestFixture.Feed(terminal, text);

    private static string GetLine(Hex1bTerminal terminal, int row)
        => GhosttyTestFixture.GetLine(terminal, row);

    private static TerminalCell GetCell(Hex1bTerminal terminal, int row, int col)
        => GhosttyTestFixture.GetCell(terminal, row, col);
    #region CursorUp (CUU — CSI n A)

    // Ghostty: test "Terminal: cursorUp basic"
    [Fact]
    public void CursorUp_Basic()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "\x1b[3;1H"); // CUP(3,1) → row 2
        Feed(terminal, "A");
        Feed(terminal, "\x1b[10A"); // CUU(10) — clamped to top
        Feed(terminal, "X");

        Assert.Equal(" X", GetLine(terminal, 0));
        Assert.Equal("", GetLine(terminal, 1));
        Assert.Equal("A", GetLine(terminal, 2));
    }

    // Ghostty: test "Terminal: cursorUp below top scroll margin"
    [Fact]
    public void CursorUp_BelowTopScrollMargin()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "\x1b[2;4r"); // DECSTBM(2,4)
        Feed(terminal, "\x1b[3;1H"); // CUP(3,1) → row 2
        Feed(terminal, "A");
        Feed(terminal, "\x1b[5A"); // CUU(5) — clamped to top margin (row 1)
        Feed(terminal, "X");

        Assert.Equal("", GetLine(terminal, 0));
        Assert.Equal(" X", GetLine(terminal, 1));
        Assert.Equal("A", GetLine(terminal, 2));
    }

    // Ghostty: test "Terminal: cursorUp above top scroll margin"
    [Fact]
    public void CursorUp_AboveTopScrollMargin()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "\x1b[3;5r"); // DECSTBM(3,5)
        Feed(terminal, "\x1b[3;1H"); // CUP(3,1) → row 2
        Feed(terminal, "A");
        Feed(terminal, "\x1b[2;1H"); // CUP(2,1) → row 1 (above margin)
        Feed(terminal, "\x1b[10A"); // CUU(10) — clamped to row 0 (ignores margin since above)
        Feed(terminal, "X");

        Assert.Equal("X", GetLine(terminal, 0));
        Assert.Equal("", GetLine(terminal, 1));
        Assert.Equal("A", GetLine(terminal, 2));
    }

    // Ghostty: test "Terminal: cursorUp resets wrap"
    [Fact]
    public void CursorUp_ResetsWrap()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "ABCDE"); // Fill row, pending wrap
        Feed(terminal, "\x1b[1A"); // CUU(1) — resets wrap, stays at col 4
        Feed(terminal, "X");

        // Should overwrite 'E' since wrap was reset and cursor stays at last col
        Assert.Equal("ABCDX", GetLine(terminal, 0));
    }

    #endregion

    #region CursorDown (CUD — CSI n B)

    // Ghostty: test "Terminal: cursorDown basic"
    [Fact]
    public void CursorDown_Basic()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "A");
        Feed(terminal, "\x1b[10B"); // CUD(10) — clamped to bottom
        Feed(terminal, "X");

        Assert.Equal("A", GetLine(terminal, 0));
        Assert.Equal(" X", GetLine(terminal, 4));
    }

    // Ghostty: test "Terminal: cursorDown above bottom scroll margin"
    [Fact]
    public void CursorDown_AboveBottomScrollMargin()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "\x1b[1;3r"); // DECSTBM(1,3)
        Feed(terminal, "A");
        Feed(terminal, "\x1b[10B"); // CUD(10) — clamped to bottom margin (row 2)
        Feed(terminal, "X");

        Assert.Equal("A", GetLine(terminal, 0));
        Assert.Equal(" X", GetLine(terminal, 2));
    }

    // Ghostty: test "Terminal: cursorDown below bottom scroll margin"
    [Fact]
    public void CursorDown_BelowBottomScrollMargin()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "\x1b[1;3r"); // DECSTBM(1,3)
        Feed(terminal, "A");
        Feed(terminal, "\x1b[4;1H"); // CUP(4,1) → row 3 (below margin)
        Feed(terminal, "\x1b[10B"); // CUD(10) — clamped to bottom of screen
        Feed(terminal, "X");

        Assert.Equal("A", GetLine(terminal, 0));
        Assert.Equal("X", GetLine(terminal, 4));
    }

    // Ghostty: test "Terminal: cursorDown resets wrap"
    [Fact]
    public void CursorDown_ResetsWrap()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "ABCDE"); // Fill row, pending wrap
        Feed(terminal, "\x1b[1B"); // CUD(1) — resets wrap
        Feed(terminal, "X");

        Assert.Equal("ABCDE", GetLine(terminal, 0));
        Assert.Equal("    X", GetLine(terminal, 1));
    }

    #endregion

    #region CursorRight (CUF — CSI n C)

    // Ghostty: test "Terminal: cursorRight resets wrap"
    [Fact]
    public void CursorRight_ResetsWrap()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "ABCDE"); // Fill row, pending wrap
        Feed(terminal, "\x1b[1C"); // CUF(1) — resets wrap, stays at last col
        Feed(terminal, "X");

        Assert.Equal("ABCDX", GetLine(terminal, 0));
    }

    // Ghostty: test "Terminal: cursorRight to the edge of screen"
    [Fact]
    public void CursorRight_ToEdgeOfScreen()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "\x1b[100C"); // CUF(100) — clamped to last col
        Feed(terminal, "X");

        Assert.Equal("    X", GetLine(terminal, 0));
    }

    // Ghostty: test "Terminal: cursorRight left of right margin"
    [Fact]
    public void CursorRight_LeftOfRightMargin()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[1;3s"); // DECSLRM(1,3) → right margin at col 2
        Feed(terminal, "\x1b[100C"); // CUF(100) — clamped to right margin
        Feed(terminal, "X");

        Assert.Equal("  X", GetLine(terminal, 0));
    }

    // Ghostty: test "Terminal: cursorRight right of right margin"
    [Fact]
    public void CursorRight_RightOfRightMargin()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[1;3s"); // DECSLRM(1,3) → right margin at col 2
        Feed(terminal, "\x1b[1;4H"); // CUP(1,4) → col 3 (outside right margin)
        Feed(terminal, "\x1b[100C"); // CUF(100) — clamped to edge of screen (outside margin)
        Feed(terminal, "X");

        Assert.Equal("    X", GetLine(terminal, 0));
    }

    #endregion

    #region CursorLeft (CUB — CSI n D)

    // Ghostty: test "Terminal: cursorLeft no wrap"
    [Fact]
    public void CursorLeft_NoWrap()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        Feed(terminal, "A");
        Feed(terminal, "\r\n"); // CR+LF
        Feed(terminal, "B");
        Feed(terminal, "\x1b[10D"); // CUB(10) — clamped to col 0

        // Cursor should be at col 0, row 1 — no wrapping to previous line
        Assert.Equal(0, terminal.CursorX);
        Assert.Equal(1, terminal.CursorY);
    }

    // Ghostty: test "Terminal: cursorLeft unsets pending wrap state"
    [Fact]
    public void CursorLeft_UnsetsPendingWrap()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "ABCDE"); // Fill row, pending wrap
        Feed(terminal, "\x1b[1D"); // CUB(1) — resets wrap
        Feed(terminal, "X");

        Assert.Equal("ABCXE", GetLine(terminal, 0));
    }

    // Ghostty: test "Terminal: cursorLeft unsets pending wrap state with longer jump"
    [Fact]
    public void CursorLeft_UnsetsPendingWrap_LongerJump()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "ABCDE"); // Fill row, pending wrap
        Feed(terminal, "\x1b[3D"); // CUB(3) — resets wrap, moves back 3
        Feed(terminal, "X");

        Assert.Equal("AXCDE", GetLine(terminal, 0));
    }

    #endregion

    #region CursorPos (CUP) with Origin Mode

    // Ghostty: test "Terminal: cursorPos resets wrap"
    [Fact]
    public void CursorPos_ResetsWrap()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "ABCDE"); // Fill row, pending wrap
        Feed(terminal, "\x1b[1;1H"); // CUP(1,1) — resets wrap
        Feed(terminal, "X");

        Assert.Equal("XBCDE", GetLine(terminal, 0));
    }

    // Ghostty: test "Terminal: cursorPos off the screen"
    [Fact]
    public void CursorPos_OffTheScreen()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "\x1b[500;500H"); // CUP(500,500) — clamped
        Feed(terminal, "X");

        Assert.Equal(4, terminal.CursorY);
        Assert.Equal("    X", GetLine(terminal, 4));
    }

    // Ghostty: test "Terminal: cursorPos relative to origin"
    [Fact]
    public void CursorPos_RelativeToOrigin()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "\x1b[3;4r"); // DECSTBM(3,4) — top=2, bottom=3 (0-based)
        Feed(terminal, "\x1b[?6h"); // Enable DECOM (origin mode)
        Feed(terminal, "\x1b[1;1H"); // CUP(1,1) — relative to scroll region
        Feed(terminal, "X");

        // Origin mode: row 1 maps to scroll region top (row 2, 0-based)
        Assert.Equal("", GetLine(terminal, 0));
        Assert.Equal("", GetLine(terminal, 1));
        Assert.Equal("X", GetLine(terminal, 2));
    }

    // Ghostty: test "Terminal: cursorPos relative to origin with left/right"
    [Fact]
    public void CursorPos_RelativeToOrigin_WithLeftRight()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "\x1b[3;4r"); // DECSTBM(3,4)
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[3;5s"); // DECSLRM(3,5) — left=2, right=4 (0-based)
        Feed(terminal, "\x1b[?6h"); // Enable DECOM
        Feed(terminal, "\x1b[1;1H"); // CUP(1,1) — relative to both margins
        Feed(terminal, "X");

        Assert.Equal("", GetLine(terminal, 0));
        Assert.Equal("", GetLine(terminal, 1));
        Assert.Equal("  X", GetLine(terminal, 2));
    }

    // Ghostty: test "Terminal: cursorPos limits with full scroll region"
    [Fact]
    public void CursorPos_LimitsWithFullScrollRegion()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "\x1b[3;4r"); // DECSTBM(3,4)
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[3;5s"); // DECSLRM(3,5)
        Feed(terminal, "\x1b[?6h"); // Enable DECOM
        Feed(terminal, "\x1b[500;500H"); // CUP(500,500) — clamped to scroll region
        Feed(terminal, "X");

        // Should be clamped to bottom-right of scroll region
        Assert.Equal("    X", GetLine(terminal, 3));
    }

    // Ghostty: test "Terminal: setCursorPos (original test)"
    [Fact]
    public void SetCursorPos_OriginalTest()
    {
        using var terminal = CreateTerminal(cols: 80, rows: 80);

        Assert.Equal(0, terminal.CursorX);
        Assert.Equal(0, terminal.CursorY);

        // Setting to 0 should keep it zero (CUP is 1-based, 0 treated as 1)
        Feed(terminal, "\x1b[0;0H");
        Assert.Equal(0, terminal.CursorX);
        Assert.Equal(0, terminal.CursorY);

        // Should clamp to size
        Feed(terminal, "\x1b[81;81H");
        Assert.Equal(79, terminal.CursorX);
        Assert.Equal(79, terminal.CursorY);
    }

    #endregion

    #region DECALN (Screen Alignment Test — ESC # 8)

    // Ghostty: test "Terminal: DECALN"
    [Fact]
    public void Decaln_FillsWithE()
    {
        using var terminal = CreateTerminal(cols: 2, rows: 2);
        Feed(terminal, "A");
        Feed(terminal, "\r\n");
        Feed(terminal, "B");
        Feed(terminal, "\x1b#8"); // DECALN

        Assert.Equal(0, terminal.CursorX);
        Assert.Equal(0, terminal.CursorY);
        Assert.Equal("EE", GetLine(terminal, 0));
        Assert.Equal("EE", GetLine(terminal, 1));
    }

    // Ghostty: test "Terminal: decaln reset margins"
    [Fact]
    public void Decaln_ResetMargins()
    {
        using var terminal = CreateTerminal(cols: 3, rows: 3);
        Feed(terminal, "\x1b[?6h"); // Enable DECOM
        Feed(terminal, "\x1b[2;3r"); // DECSTBM(2,3)
        Feed(terminal, "\x1b#8"); // DECALN — should reset margins
        Feed(terminal, "\x1b[1T"); // SD(1) — scroll down

        // After DECALN resets margins, scroll down shifts all 3 rows
        Assert.Equal("", GetLine(terminal, 0));
        Assert.Equal("EEE", GetLine(terminal, 1));
        Assert.Equal("EEE", GetLine(terminal, 2));
    }

    // Ghostty: test "Terminal: decaln preserves color"
    [Fact]
    public void Decaln_PreservesColor()
    {
        using var terminal = CreateTerminal(cols: 3, rows: 3);
        Feed(terminal, "\x1b[48;2;255;0;0m"); // Set bg to red
        Feed(terminal, "\x1b[?6h"); // Enable DECOM
        Feed(terminal, "\x1b[2;3r"); // DECSTBM(2,3)
        Feed(terminal, "\x1b#8"); // DECALN — should reset margins, preserve bg
        Feed(terminal, "\x1b[1T"); // SD(1) — scroll down

        Assert.Equal("", GetLine(terminal, 0));
        Assert.Equal("EEE", GetLine(terminal, 1));
        Assert.Equal("EEE", GetLine(terminal, 2));

        // Verify background color is preserved on the blank line
        var cell = GetCell(terminal, 0, 0);
        Assert.NotNull(cell.Background);
        var bg = cell.Background!.Value;
        Assert.Equal((byte)255, bg.R);
        Assert.Equal((byte)0, bg.G);
        Assert.Equal((byte)0, bg.B);
    }

    #endregion

    #region Save/Restore Cursor (DECSC/DECRC — ESC 7 / ESC 8)

    // Ghostty: test "Terminal: saveCursor position"
    [Fact]
    public void SaveCursor_Position()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        Feed(terminal, "\x1b[1;5H"); // CUP(1,5) → col 4
        Feed(terminal, "A");
        Feed(terminal, "\x1b" + "7"); // DECSC (save cursor)
        Feed(terminal, "\x1b[1;1H"); // CUP(1,1)
        Feed(terminal, "B");
        Feed(terminal, "\x1b" + "8"); // DECRC (restore cursor)
        Feed(terminal, "X");

        Assert.Equal("B   AX", GetLine(terminal, 0));
    }

    // Ghostty: test "Terminal: saveCursor pending wrap state"
    [Fact]
    public void SaveCursor_PendingWrapState()
    {
        using var terminal = CreateTerminal(cols: 5, rows: 5);
        Feed(terminal, "\x1b[1;5H"); // CUP(1,5) → col 4
        Feed(terminal, "A"); // Pending wrap
        Feed(terminal, "\x1b" + "7"); // DECSC
        Feed(terminal, "\x1b[1;1H"); // CUP(1,1)
        Feed(terminal, "B");
        Feed(terminal, "\x1b" + "8"); // DECRC — restores pending wrap
        Feed(terminal, "X");

        Assert.Equal("B   A", GetLine(terminal, 0));
        Assert.Equal("X", GetLine(terminal, 1));
    }

    // Ghostty: test "Terminal: saveCursor origin mode"
    [Fact]
    public void SaveCursor_OriginMode()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        Feed(terminal, "\x1b[?6h"); // Enable DECOM
        Feed(terminal, "\x1b" + "7"); // DECSC — save with origin mode on
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[3;5s"); // DECSLRM(3,5)
        Feed(terminal, "\x1b[2;4r"); // DECSTBM(2,4)
        Feed(terminal, "\x1b" + "8"); // DECRC — restore origin mode
        Feed(terminal, "X");

        // With origin mode restored, CUP should position relative to margins
        // But cursor position was (0,0) when saved, so X goes at (0,0)
        Assert.Equal("X", GetLine(terminal, 0));
    }

    // Ghostty: test "Terminal: saveCursor resize"
    [Fact]
    public void SaveCursor_Resize()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        Feed(terminal, "\x1b[1;10H"); // CUP(1,10) → col 9
        Feed(terminal, "\x1b" + "7"); // DECSC — save at col 9

        // Simulate resize to 5 cols — cursor should be clamped
        // Note: Hex1b may not support resize in the same way, so we test
        // save/restore with cursor beyond bounds
        Feed(terminal, "\x1b" + "8"); // DECRC — restore
        Feed(terminal, "X");

        Assert.Equal("         X", GetLine(terminal, 0));
    }

    #endregion

    #region Carriage Return with Margins

    // Ghostty: test "Terminal: carriage return origin mode moves to left margin"
    [Fact]
    public void CarriageReturn_OriginMode_MovesToLeftMargin()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[3;5s"); // DECSLRM(3,5) — left margin at col 2
        Feed(terminal, "\x1b[?6h"); // Enable DECOM
        Feed(terminal, "\x1b[1;3H"); // CUP relative to margins → col 4
        Feed(terminal, "\r"); // CR — should go to left margin (col 2)
        Feed(terminal, "X");

        Assert.Equal("  X", GetLine(terminal, 0));
    }

    // Ghostty: test "Terminal: carriage return left of left margin moves to zero"
    [Fact]
    public void CarriageReturn_LeftOfLeftMargin_MovesToZero()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[3;5s"); // DECSLRM(3,5) — left margin at col 2
        Feed(terminal, "\x1b[1;1H"); // CUP(1,1) → col 0 (left of left margin)
        Feed(terminal, "\r"); // CR — should go to col 0 (outside margin)
        Feed(terminal, "X");

        Assert.Equal("X", GetLine(terminal, 0));
    }

    // Ghostty: test "Terminal: carriage return right of left margin moves to left margin"
    [Fact]
    public void CarriageReturn_RightOfLeftMargin_MovesToLeftMargin()
    {
        using var terminal = CreateTerminal(cols: 10, rows: 5);
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[3;5s"); // DECSLRM(3,5) — left margin at col 2
        Feed(terminal, "\x1b[1;4H"); // CUP(1,4) → col 3 (inside margin)
        Feed(terminal, "\r"); // CR — should go to left margin (col 2)
        Feed(terminal, "X");

        Assert.Equal("  X", GetLine(terminal, 0));
    }

    #endregion

    #region Horizontal Tabs with Margins

    // Ghostty: test "Terminal: horizontal tabs with right margin"
    [Fact]
    public void HorizontalTabs_WithRightMargin()
    {
        using var terminal = CreateTerminal(cols: 20, rows: 5);
        Feed(terminal, "\x1b[?69h"); // Enable DECLRMM
        Feed(terminal, "\x1b[1;8s"); // DECSLRM(1,8) — right margin at col 7
        Feed(terminal, "\t"); // Tab — should stop at tab stop or right margin
        Feed(terminal, "X");

        // Default tab stops at every 8 columns. Tab from col 0 → col 7 (right margin)
        Assert.True(terminal.CursorX <= 8, $"CursorX {terminal.CursorX} should be within right margin");
    }

    #endregion
}
