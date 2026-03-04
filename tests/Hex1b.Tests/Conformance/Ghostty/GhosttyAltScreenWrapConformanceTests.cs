using Xunit;

namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Conformance tests for alt screen modes, disabled wraparound with wide chars,
/// soft wrap, and other misc operations.
/// Translated from Ghostty's Terminal.zig.
/// </summary>
[Trait("Category", "GhosttyConformance")]
public class GhosttyAltScreenWrapConformanceTests
{
    private static Hex1bTerminal CreateTerminal(int cols = 80, int rows = 24)
        => GhosttyTestFixture.CreateTerminal(cols, rows);

    #region Alt Screen Modes (47, 1047, 1049)

    [Fact]
    [Trait("FailureReason", "MissingFeature")]
    public void Mode47_AltScreenPlain()
    {
        // Ghostty: "Terminal: mode 47 alt screen plain"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "1A");

        // Go to alt screen with mode 47
        GhosttyTestFixture.Feed(t, "\u001b[?47h");
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));

        // Print on alt screen — cursor position copied from primary
        GhosttyTestFixture.Feed(t, "2B");
        Assert.Equal("  2B", GhosttyTestFixture.GetLine(t, 0));

        // Go back to primary
        GhosttyTestFixture.Feed(t, "\u001b[?47l");
        Assert.Equal("1A", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    [Trait("FailureReason", "MissingFeature")]
    public void Mode1047_AltScreenPlain()
    {
        // Ghostty: "Terminal: mode 1047 alt screen plain"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "1A");

        GhosttyTestFixture.Feed(t, "\u001b[?1047h");
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));

        GhosttyTestFixture.Feed(t, "2B");
        Assert.Equal("  2B", GhosttyTestFixture.GetLine(t, 0));

        GhosttyTestFixture.Feed(t, "\u001b[?1047l");
        Assert.Equal("1A", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void Mode1049_AltScreenPlain()
    {
        // Ghostty: "Terminal: mode 1049 alt screen plain"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "1A");

        // Mode 1049 saves cursor then switches to alt screen
        GhosttyTestFixture.Feed(t, "\u001b[?1049h");
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));

        // Print on alt screen — cursor position is copied from primary
        GhosttyTestFixture.Feed(t, "2B");
        Assert.Contains("2B", GhosttyTestFixture.GetLine(t, 0));

        // Go back to primary — cursor is restored
        GhosttyTestFixture.Feed(t, "\u001b[?1049l");
        Assert.Equal("1A", GhosttyTestFixture.GetLine(t, 0));
    }

    #endregion

    #region Disabled Wraparound with Wide Characters

    [Fact]
    public void DisabledWraparound_WideCharAndNoSpace()
    {
        // Ghostty: "Terminal: disabled wraparound with wide char and no space"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "\u001b[?7l"); // Disable wraparound
        GhosttyTestFixture.Feed(t, "AAAAA");
        GhosttyTestFixture.Feed(t, "\U0001F6A8"); // Police car light (wide)

        Assert.Equal(0, t.CursorY);
        Assert.Equal(4, t.CursorX);
        Assert.Equal("AAAAA", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void DisabledWraparound_WideCharAndOneSpace()
    {
        // Ghostty: "Terminal: disabled wraparound with wide char and one space"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "\u001b[?7l"); // Disable wraparound
        GhosttyTestFixture.Feed(t, "AAAA");
        GhosttyTestFixture.Feed(t, "\U0001F6A8"); // Police car light (wide)

        Assert.Equal(0, t.CursorY);
        Assert.Equal(4, t.CursorX);
        Assert.Equal("AAAA", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void DisabledWraparound_WideGraphemeAndHalfSpace()
    {
        // Ghostty: "Terminal: disabled wraparound with wide grapheme and half space"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "\u001b[?2027h"); // Enable grapheme cluster mode
        GhosttyTestFixture.Feed(t, "\u001b[?7l");    // Disable wraparound
        GhosttyTestFixture.Feed(t, "AAAA");
        GhosttyTestFixture.Feed(t, "\u2764");  // Heart (narrow)
        GhosttyTestFixture.Feed(t, "\uFE0F");  // VS16 — should make wide but no space

        Assert.Equal(0, t.CursorY);
        Assert.Equal(4, t.CursorX);
        Assert.Contains("\u2764", GhosttyTestFixture.GetLine(t, 0));
    }

    #endregion

    #region Input and Wrap

    [Fact]
    public void Input_NoControlCharacters()
    {
        // Ghostty: "Terminal: input with no control characters"
        using var t = CreateTerminal(cols: 40, rows: 40);
        GhosttyTestFixture.Feed(t, "hello");
        Assert.Equal(0, t.CursorY);
        Assert.Equal(5, t.CursorX);
        Assert.Equal("hello", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void Input_BasicWraparound()
    {
        // Ghostty: "Terminal: input with basic wraparound"
        using var t = CreateTerminal(cols: 5, rows: 40);
        GhosttyTestFixture.Feed(t, "helloworldabc12");
        Assert.Equal(2, t.CursorY);
        Assert.Equal(4, t.CursorX);
        Assert.True(t.PendingWrap);
        Assert.Equal("hello", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("world", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("abc12", GhosttyTestFixture.GetLine(t, 2));
    }

    [Fact]
    public void SoftWrap()
    {
        // Ghostty: "Terminal: soft wrap"
        using var t = CreateTerminal(cols: 3, rows: 80);
        GhosttyTestFixture.Feed(t, "hello");
        Assert.Equal(1, t.CursorY);
        Assert.Equal(2, t.CursorX);
        Assert.Equal("hel", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("lo", GhosttyTestFixture.GetLine(t, 1));
    }

    #endregion

    #region DECALN Preserves Color

    [Fact]
    public void Decaln_PreservesColor()
    {
        // Ghostty: "Terminal: decaln preserves color"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "\u001b#8"); // DECALN
        Assert.Equal("EEEEE", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("EEEEE", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("EEEEE", GhosttyTestFixture.GetLine(t, 2));
        Assert.Equal("EEEEE", GhosttyTestFixture.GetLine(t, 3));
        Assert.Equal("EEEEE", GhosttyTestFixture.GetLine(t, 4));
    }

    #endregion

    #region DeleteChars Zero Count

    [Fact]
    [Trait("FailureReason", "Bug")]
    public void DeleteChars_ZeroCount()
    {
        // Ghostty: "Terminal: deleteChars zero count"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABCDE");
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // setCursorPos(1,1)
        GhosttyTestFixture.Feed(t, "\u001b[0P"); // deleteChars(0) — should delete 1

        Assert.Equal("BCDE", GhosttyTestFixture.GetLine(t, 0));
    }

    #endregion

    #region SaveCursor Resize

    [Fact]
    public void SaveCursor_Resize()
    {
        // Ghostty: "Terminal: saveCursor resize"
        // Save cursor at col 7, verify restore works
        using var t = CreateTerminal(cols: 10, rows: 5);
        GhosttyTestFixture.Feed(t, "\u001b[1;8H"); // setCursorPos(1,8) → col 7
        GhosttyTestFixture.Feed(t, "\u001b7"); // saveCursor
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // move home
        GhosttyTestFixture.Feed(t, "\u001b8"); // restoreCursor
        Assert.Equal(7, t.CursorX);
        Assert.Equal(0, t.CursorY);
    }

    #endregion

    #region EraseChars Wide Char Wrap Boundary

    [Fact]
    public void EraseChars_WideCharWrapBoundary()
    {
        // Ghostty: "Terminal: eraseChars wide char wrap boundary conditions"
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "ABCD");
        GhosttyTestFixture.Feed(t, "\u6A4B"); // 橋 (wide) — wraps to next line
        GhosttyTestFixture.Feed(t, "\u001b[1;5H"); // setCursorPos(1,5) → col 4
        GhosttyTestFixture.Feed(t, "\u001b[X"); // eraseChars(1)

        Assert.Equal("ABCD", GhosttyTestFixture.GetLine(t, 0));
        Assert.StartsWith("\u6A4B", GhosttyTestFixture.GetLine(t, 1));
    }

    #endregion
}
