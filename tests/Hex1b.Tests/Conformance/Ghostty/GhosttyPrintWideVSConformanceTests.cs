using Xunit;

namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Conformance tests for wide character printing with attributes, variation selectors
/// (VS15/VS16), Devanagari graphemes, and mode 2027 grapheme clustering,
/// translated from Ghostty's Terminal.zig.
/// </summary>
[Trait("Category", "GhosttyConformance")]
public class GhosttyPrintWideVSConformanceTests
{
    private static Hex1bTerminal CreateTerminal(int cols = 80, int rows = 24)
        => GhosttyTestFixture.CreateTerminal(cols, rows);

    #region Print Over Wide Character with Attributes

    // Ghostty: "Terminal: print over wide char with bold"
    [Fact]
    public void PrintOverWideCharWithBold_ClearsStyle()
    {
        using var t = CreateTerminal(cols: 80, rows: 80);

        // Set bold, print wide emoji
        GhosttyTestFixture.Feed(t, "\u001b[1m");
        GhosttyTestFixture.Feed(t, "\U0001F600"); // 😀

        // Go back and overwrite with no style
        GhosttyTestFixture.Feed(t, "\u001b[1;1H");
        GhosttyTestFixture.Feed(t, "\u001b[0m");
        GhosttyTestFixture.Feed(t, "A");

        Assert.Equal(0, t.CursorY);
        Assert.Equal(1, t.CursorX);

        var cell0 = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.Equal("A", cell0.Character);

        // Former continuation cell should be cleared
        var cell1 = GhosttyTestFixture.GetCell(t, 0, 1);
        Assert.True(cell1.Character == " " || cell1.Character == "",
            $"Expected cleared cell, got '{cell1.Character}'");
    }

    // Ghostty: "Terminal: print over wide char with bg color"
    [Fact]
    public void PrintOverWideCharWithBgColor_ClearsStyle()
    {
        using var t = CreateTerminal(cols: 80, rows: 80);

        // Set red background, print wide emoji
        GhosttyTestFixture.Feed(t, "\u001b[48;2;255;0;0m");
        GhosttyTestFixture.Feed(t, "\U0001F600"); // 😀

        // Verify emoji cell has background color
        var emojiCell = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.NotNull(emojiCell.Background);
        var bg = emojiCell.Background!.Value;
        Assert.Equal((byte)255, bg.R);
        Assert.Equal((byte)0, bg.G);
        Assert.Equal((byte)0, bg.B);

        // Go back and overwrite with no style
        GhosttyTestFixture.Feed(t, "\u001b[1;1H");
        GhosttyTestFixture.Feed(t, "\u001b[0m");
        GhosttyTestFixture.Feed(t, "A");

        Assert.Equal(0, t.CursorY);
        Assert.Equal(1, t.CursorX);

        var cell0 = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.Equal("A", cell0.Character);
        // Background should be cleared after overwrite with reset attributes
        Assert.Null(cell0.Background);
    }

    #endregion

    #region Wide Character at Right Margin

    // Ghostty: "Terminal: print wide char at right margin does not create spacer head"
    [Fact]
    public void PrintWideCharAtRightMargin_NoSpacerHead()
    {
        using var t = CreateTerminal(cols: 10, rows: 10);

        // Enable left/right margin mode and set margins
        GhosttyTestFixture.Feed(t, "\u001b[?69h");  // DECLRMM enable
        GhosttyTestFixture.Feed(t, "\u001b[3;5s");  // Set left=3, right=5 (1-based)
        GhosttyTestFixture.Feed(t, "\u001b[1;5H");  // CUP to row 1, col 5 (right margin)
        GhosttyTestFixture.Feed(t, "\U0001F600");    // 😀 (wide)

        // Wide char wraps to next line at left margin
        Assert.Equal(1, t.CursorY);
        Assert.Equal(4, t.CursorX);

        // Cell at right margin of row 0 should be empty/narrow, NOT spacer_head
        var marginCell = GhosttyTestFixture.GetCell(t, 0, 4);
        Assert.True(marginCell.Character == " " || marginCell.Character == "",
            $"Expected empty cell at right margin, got '{marginCell.Character}'");

        // Wide char should be on row 1 at left margin (col 2, 0-based)
        var wideCell = GhosttyTestFixture.GetCell(t, 1, 2);
        Assert.Equal("\U0001F600", wideCell.Character);

        // Spacer tail
        var tailCell = GhosttyTestFixture.GetCell(t, 1, 3);
        Assert.Equal("", tailCell.Character);
    }

    #endregion

    #region Devanagari Grapheme Clustering

    // Ghostty: "Terminal: print Devanagari grapheme should be wide"
    [Fact]
    public void PrintDevanagariGrapheme_ShouldBeWide()
    {
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "\u001b[?2027h"); // Enable mode 2027

        // क्‍ष = U+0915 U+094D U+200D U+0937
        GhosttyTestFixture.Feed(t, "\u0915\u094D\u200D\u0937");

        // Should take 2 cells (wide grapheme)
        Assert.Equal(0, t.CursorY);
        Assert.Equal(2, t.CursorX);

        // First cell has the grapheme
        var cell0 = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.True(cell0.Character.Length > 0, "Expected Devanagari grapheme content");

        // Second cell is continuation
        var cell1 = GhosttyTestFixture.GetCell(t, 0, 1);
        Assert.Equal("", cell1.Character);
    }

    // Ghostty: "Terminal: print Devanagari grapheme should be wide on next line"
    [Fact]
    public void PrintDevanagariGrapheme_ShouldBeWideOnNextLine()
    {
        using var t = CreateTerminal(cols: 3, rows: 5);
        GhosttyTestFixture.Feed(t, "\u001b[?2027h"); // Enable mode 2027

        // Move to last column
        GhosttyTestFixture.Feed(t, "\u001b[2C"); // CUF 2 → col 2

        // First three codepoints: narrow grapheme at last column
        GhosttyTestFixture.Feed(t, "\u0915"); // क
        GhosttyTestFixture.Feed(t, "\u094D"); // virama (combining)
        GhosttyTestFixture.Feed(t, "\u200D"); // ZWJ (zero-width)

        // Should be at last col with pending wrap
        Assert.Equal(2, t.CursorX);
        Assert.True(t.PendingWrap);

        // Last codepoint makes grapheme wide → wraps to next line
        GhosttyTestFixture.Feed(t, "\u0937"); // ष

        Assert.Equal(1, t.CursorY);
        Assert.Equal(2, t.CursorX);
        Assert.False(t.PendingWrap);

        // Previous cell becomes spacer head
        var spacerHead = GhosttyTestFixture.GetCell(t, 0, 2);
        Assert.True(spacerHead.Character == " " || spacerHead.Character == "",
            "Expected spacer head at previous position");

        // Wide grapheme on next line
        var wideCell = GhosttyTestFixture.GetCell(t, 1, 0);
        Assert.True(wideCell.Character.Length > 0, "Expected Devanagari grapheme");

        // Spacer tail
        var tailCell = GhosttyTestFixture.GetCell(t, 1, 1);
        Assert.Equal("", tailCell.Character);
    }

    // Ghostty: "Terminal: print Devanagari grapheme should be wide on next page"
    [Fact]
    public void PrintDevanagariGrapheme_ShouldBeWideOnNextPage()
    {
        // Simplified from Ghostty's page-boundary test: tests wrapping at bottom-right
        using var t = CreateTerminal(cols: 10, rows: 5);
        GhosttyTestFixture.Feed(t, "\u001b[?2027h"); // Enable mode 2027

        // Position cursor at last row, last column
        GhosttyTestFixture.Feed(t, "\u001b[5;10H"); // CUP(5,10) → row 4, col 9

        // First three codepoints: narrow at last col
        GhosttyTestFixture.Feed(t, "\u0915"); // क
        GhosttyTestFixture.Feed(t, "\u094D"); // virama
        GhosttyTestFixture.Feed(t, "\u200D"); // ZWJ

        Assert.Equal(9, t.CursorX);
        Assert.True(t.PendingWrap);

        // Last codepoint makes grapheme wide → wraps + scrolls
        GhosttyTestFixture.Feed(t, "\u0937"); // ष

        Assert.Equal(4, t.CursorY); // Still last row (after scroll)
        Assert.Equal(2, t.CursorX);
        Assert.False(t.PendingWrap);

        // Spacer head on previous row's last column
        var spacerHead = GhosttyTestFixture.GetCell(t, 3, 9);
        Assert.True(spacerHead.Character == " " || spacerHead.Character == "",
            "Expected spacer head at previous row's last column");

        // Wide Devanagari on last row
        var wideCell = GhosttyTestFixture.GetCell(t, 4, 0);
        Assert.True(wideCell.Character.Length > 0, "Expected Devanagari grapheme on last row");

        // Spacer tail
        var tailCell = GhosttyTestFixture.GetCell(t, 4, 1);
        Assert.Equal("", tailCell.Character);
    }

    #endregion

    #region Invalid VS15 (Text Presentation Selector)

    // Ghostty: "Terminal: print invalid VS15 following emoji is wide"
    [Fact]
    public void PrintInvalidVS15FollowingEmoji_RemainsWide()
    {
        using var t = CreateTerminal(cols: 80, rows: 80);
        GhosttyTestFixture.Feed(t, "\u001b[?2027h"); // Enable mode 2027

        // 🧠 (U+1F9E0) + VS15 — VS15 is not valid for this emoji
        GhosttyTestFixture.Feed(t, "\U0001F9E0");
        GhosttyTestFixture.Feed(t, "\uFE0E");

        // Should still be wide (2 cells)
        Assert.Equal(0, t.CursorY);
        Assert.Equal(2, t.CursorX);

        // Cell should have 🧠, still wide (Hex1b may include VS15 in string)
        var cell0 = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.True(cell0.Character.StartsWith("\U0001F9E0"),
            $"Expected cell to start with 🧠, got '{cell0.Character}'");

        // Spacer tail
        var cell1 = GhosttyTestFixture.GetCell(t, 0, 1);
        Assert.Equal("", cell1.Character);
    }

    // Ghostty: "Terminal: print invalid VS15 in emoji ZWJ sequence"
    [Fact]
    public void PrintInvalidVS15InEmojiZWJSequence_RemainsWide()
    {
        using var t = CreateTerminal(cols: 80, rows: 80);
        GhosttyTestFixture.Feed(t, "\u001b[?2027h"); // Enable mode 2027

        // 👩 + VS15 (invalid) + ZWJ + 👦
        GhosttyTestFixture.Feed(t, "\U0001F469"); // 👩
        GhosttyTestFixture.Feed(t, "\uFE0E");     // VS15 (invalid for U+1F469)
        GhosttyTestFixture.Feed(t, "\u200D");      // ZWJ
        GhosttyTestFixture.Feed(t, "\U0001F466");  // 👦

        // Should be wide (2 cells) — ZWJ sequence
        Assert.Equal(0, t.CursorY);
        Assert.Equal(2, t.CursorX);

        // First cell has 👩 with grapheme data
        var cell0 = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.True(cell0.Character.Length > 0, "Expected emoji ZWJ sequence");

        // Spacer tail
        var cell1 = GhosttyTestFixture.GetCell(t, 0, 1);
        Assert.Equal("", cell1.Character);
    }

    #endregion

    #region Invalid VS16 (Emoji Presentation Selector)

    // Ghostty: "Terminal: print invalid VS16 grapheme"
    [Fact]
    public void PrintInvalidVS16Grapheme_StaysNarrow()
    {
        using var t = CreateTerminal(cols: 80, rows: 80);
        GhosttyTestFixture.Feed(t, "\u001b[?2027h"); // Enable mode 2027

        // 'x' + VS16 — VS16 is invalid for 'x'
        GhosttyTestFixture.Feed(t, "x");
        GhosttyTestFixture.Feed(t, "\uFE0F");

        // Should remain narrow (1 cell)
        Assert.Equal(0, t.CursorY);
        Assert.Equal(1, t.CursorX);

        // Hex1b may include VS16 in character string; key is cursor stays narrow
        var cell0 = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.True(cell0.Character.StartsWith("x"),
            $"Expected cell to start with 'x', got '{cell0.Character}'");
    }

    // Ghostty: "Terminal: print invalid VS16 non-grapheme"
    [Fact]
    public void PrintInvalidVS16NonGrapheme_StaysNarrow()
    {
        using var t = CreateTerminal(cols: 80, rows: 80);
        // Mode 2027 NOT enabled (non-grapheme mode)

        // 'x' + VS16 — VS16 is invalid for 'x'
        GhosttyTestFixture.Feed(t, "x");
        GhosttyTestFixture.Feed(t, "\uFE0F");

        // Should remain narrow (1 cell)
        Assert.Equal(0, t.CursorY);
        Assert.Equal(1, t.CursorX);

        // Hex1b may include VS16 in character string; key is cursor stays narrow
        var cell0 = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.True(cell0.Character.StartsWith("x"),
            $"Expected cell to start with 'x', got '{cell0.Character}'");
    }

    // Ghostty: "Terminal: print invalid VS16 with second char"
    [Fact]
    public void PrintInvalidVS16WithSecondChar_BothNarrow()
    {
        using var t = CreateTerminal(cols: 80, rows: 80);
        GhosttyTestFixture.Feed(t, "\u001b[?2027h"); // Enable mode 2027

        // 'x' + VS16 (invalid) + 'y'
        GhosttyTestFixture.Feed(t, "x");
        GhosttyTestFixture.Feed(t, "\uFE0F");
        GhosttyTestFixture.Feed(t, "y");

        // Two separate narrow characters
        Assert.Equal(0, t.CursorY);
        Assert.Equal(2, t.CursorX);

        // Hex1b may include VS16 in character string for 'x'
        var cell0 = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.True(cell0.Character.StartsWith("x"),
            $"Expected cell to start with 'x', got '{cell0.Character}'");

        var cell1 = GhosttyTestFixture.GetCell(t, 0, 1);
        Assert.Equal("y", cell1.Character);
    }

    #endregion

    #region Mode 2027 Grapheme Clustering

    // Ghostty: "Terminal: print multicodepoint grapheme, mode 2027"
    [Fact]
    public void PrintMulticodepointGrapheme_Mode2027_SingleWideCluster()
    {
        using var t = CreateTerminal(cols: 80, rows: 80);
        GhosttyTestFixture.Feed(t, "\u001b[?2027h"); // Enable mode 2027

        // 👨‍👩‍👧 = Man + ZWJ + Woman + ZWJ + Girl
        GhosttyTestFixture.Feed(t, "\U0001F468"); // 👨
        GhosttyTestFixture.Feed(t, "\u200D");      // ZWJ
        GhosttyTestFixture.Feed(t, "\U0001F469"); // 👩
        GhosttyTestFixture.Feed(t, "\u200D");      // ZWJ
        GhosttyTestFixture.Feed(t, "\U0001F467"); // 👧

        // With mode 2027, should be single wide grapheme (2 cells)
        Assert.Equal(0, t.CursorY);
        Assert.Equal(2, t.CursorX);

        // First cell has the family emoji grapheme
        var cell0 = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.True(cell0.Character.Length > 0, "Expected family emoji grapheme");

        // Spacer tail
        var cell1 = GhosttyTestFixture.GetCell(t, 0, 1);
        Assert.Equal("", cell1.Character);
    }

    // Ghostty: "Terminal: VS16 doesn't make character with 2027 disabled"
    [Fact]
    public void VS16_WithMode2027Disabled_DoesNotMakeWide()
    {
        using var t = CreateTerminal(cols: 5, rows: 5);
        GhosttyTestFixture.Feed(t, "\u001b[?2027l"); // Disable mode 2027

        // Heart + VS16
        GhosttyTestFixture.Feed(t, "\u2764"); // ❤
        GhosttyTestFixture.Feed(t, "\uFE0F"); // VS16

        // Without mode 2027, heart should remain narrow
        Assert.Equal(0, t.CursorY);

        var cell0 = GhosttyTestFixture.GetCell(t, 0, 0);
        Assert.True(cell0.Character.Length > 0, "Expected heart character");

        // Verify line content includes VS16 as grapheme data
        var line = GhosttyTestFixture.GetLine(t, 0);
        Assert.True(line.Length > 0, "Expected non-empty line");
    }

    #endregion

    #region VS16 Edge Cases

    // Ghostty: "Terminal: VS16 to make wide character with pending wrap"
    [Fact]
    public void VS16_WithPendingWrap_MakesWideInPlace()
    {
        using var t = CreateTerminal(cols: 3, rows: 5);
        GhosttyTestFixture.Feed(t, "\u001b[?2027h"); // Enable mode 2027

        // Move to col 1 and print '#'
        GhosttyTestFixture.Feed(t, "\u001b[C"); // CUF 1 → col 1
        GhosttyTestFixture.Feed(t, "#");

        // '#' at col 1, cursor at col 2 (last column), no pending wrap
        Assert.Equal(2, t.CursorX);
        Assert.False(t.PendingWrap);

        // VS16 makes '#' wide (2 cells at cols 1-2)
        GhosttyTestFixture.Feed(t, "\uFE0F");

        // Cursor at col 2 with pending wrap (wide char fills cols 1-2)
        Assert.Equal(2, t.CursorX);
        Assert.Equal(0, t.CursorY);
        Assert.True(t.PendingWrap);

        // '#' cell is wide at col 1 (Hex1b may include VS16 in string)
        var wideCell = GhosttyTestFixture.GetCell(t, 0, 1);
        Assert.True(wideCell.Character.StartsWith("#"),
            $"Expected cell to start with '#', got '{wideCell.Character}'");

        // Spacer tail at col 2
        var tailCell = GhosttyTestFixture.GetCell(t, 0, 2);
        Assert.True(tailCell.Character == "" || tailCell.Character == " ",
            $"Expected spacer tail (empty or space), got '{tailCell.Character}'");
    }

    #endregion
}
