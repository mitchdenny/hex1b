using Xunit;
using static Hex1b.Tests.Conformance.Ghostty.GhosttyTestFixture;

namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Conformance tests for deleteChars (DCH — CSI n P) and insertBlanks (ICH — CSI n @)
/// translated from Ghostty Terminal.zig.
/// </summary>
[Trait("Category", "GhosttyConformance")]
public class GhosttyDeleteCharsInsertBlanksConformanceTests
{
    #region deleteChars (DCH — CSI n P)

    [Fact]
    public void DeleteChars_Basic()
    {
        // Ghostty: "Terminal: deleteChars"
        using var t = CreateTerminal(cols: 5, rows: 5);
        Feed(t, "ABCDE");
        Feed(t, "\u001b[1;2H"); // setCursorPos(1, 2) → col 1
        Feed(t, "\u001b[2P");   // deleteChars(2)

        Assert.Equal("ADE", GetLine(t, 0));
    }

    [Fact]
    public void DeleteChars_MoreThanHalf()
    {
        // Ghostty: "Terminal: deleteChars more than half"
        using var t = CreateTerminal(cols: 5, rows: 5);
        Feed(t, "ABCDE");
        Feed(t, "\u001b[1;2H"); // setCursorPos(1, 2) → col 1
        Feed(t, "\u001b[3P");   // deleteChars(3)

        Assert.Equal("AE", GetLine(t, 0));
    }

    [Fact]
    public void DeleteChars_MoreThanLineWidth()
    {
        // Ghostty: "Terminal: deleteChars more than line width"
        using var t = CreateTerminal(cols: 5, rows: 5);
        Feed(t, "ABCDE");
        Feed(t, "\u001b[1;2H"); // setCursorPos(1, 2) → col 1
        Feed(t, "\u001b[10P");  // deleteChars(10)

        Assert.Equal("A", GetLine(t, 0));
    }

    [Fact]
    public void DeleteChars_ShouldShiftLeft()
    {
        // Ghostty: "Terminal: deleteChars should shift left"
        using var t = CreateTerminal(cols: 5, rows: 5);
        Feed(t, "ABCDE");
        Feed(t, "\u001b[1;2H"); // setCursorPos(1, 2) → col 1
        Feed(t, "\u001b[1P");   // deleteChars(1)

        Assert.Equal("ACDE", GetLine(t, 0));
    }

    [Fact]
    public void DeleteChars_ResetsPendingWrap()
    {
        // Ghostty: "Terminal: deleteChars resets pending wrap"
        using var t = CreateTerminal(cols: 5, rows: 5);
        Feed(t, "ABCDE");       // fills row, pending wrap
        Assert.True(t.PendingWrap);
        Feed(t, "\u001b[1P");   // deleteChars(1) — resets pending wrap
        Assert.False(t.PendingWrap);
        Feed(t, "X");

        Assert.Equal("ABCDX", GetLine(t, 0));
    }

    [Fact]
    public void DeleteChars_ResetsWrap()
    {
        // Ghostty: "Terminal: deleteChars resets wrap"
        using var t = CreateTerminal(cols: 5, rows: 5);
        Feed(t, "ABCDE123");   // wraps: row 0 = "ABCDE" (wrapped), row 1 = "123"
        Feed(t, "\u001b[1;1H"); // setCursorPos(1, 1) → row 0, col 0
        Feed(t, "\u001b[1P");   // deleteChars(1)
        Feed(t, "X");

        Assert.Equal("XCDE", GetLine(t, 0));
        Assert.Equal("123", GetLine(t, 1));
    }

    [Fact]
    public void DeleteChars_SimpleOperation()
    {
        // Ghostty: "Terminal: deleteChars simple operation"
        using var t = CreateTerminal(cols: 10, rows: 10);
        Feed(t, "ABC123");
        Feed(t, "\u001b[1;3H"); // setCursorPos(1, 3) → col 2
        Feed(t, "\u001b[2P");   // deleteChars(2)

        Assert.Equal("AB23", GetLine(t, 0));
    }

    [Fact]
    public void DeleteChars_PreservesBackgroundSgr()
    {
        // Ghostty: "Terminal: deleteChars preserves background sgr"
        using var t = CreateTerminal(cols: 10, rows: 10);
        Feed(t, "ABC123");
        Feed(t, "\u001b[1;3H");        // setCursorPos(1, 3) → col 2
        Feed(t, "\u001b[48;2;255;0;0m"); // direct_color_bg red
        Feed(t, "\u001b[2P");           // deleteChars(2)

        Assert.Equal("AB23", GetLine(t, 0));

        // Verify background color on cleared cells (cols 8 and 9)
        for (int x = 8; x < 10; x++)
        {
            var cell = GetCell(t, 0, x);
            Assert.NotNull(cell.Background);
        }
    }

    [Fact]
    public void DeleteChars_OutsideScrollRegion()
    {
        // Ghostty: "Terminal: deleteChars outside scroll region"
        // Cursor outside left/right scroll region → deleteChars is a no-op
        using var t = CreateTerminal(cols: 6, rows: 10);
        Feed(t, "ABC123");              // fills row 0, pending wrap
        Feed(t, "\u001b[?69h");         // Enable DECLRMM
        Feed(t, "\u001b[3;5s");         // DECSLRM(3,5) = left=2, right=4 (0-based)
        Feed(t, "\u001b[1;7H");         // CUP → col 5 (clamped, outside scroll region)
        Feed(t, "\u001b[2P");           // deleteChars(2) — should be no-op

        Assert.Equal("ABC123", GetLine(t, 0));
    }

    [Fact]
    public void DeleteChars_InsideScrollRegion()
    {
        // Ghostty: "Terminal: deleteChars inside scroll region"
        using var t = CreateTerminal(cols: 6, rows: 10);
        Feed(t, "ABC123");
        Feed(t, "\u001b[?69h");         // Enable DECLRMM
        Feed(t, "\u001b[3;5s");         // DECSLRM(3,5) = left=2, right=4 (0-based)
        Feed(t, "\u001b[1;4H");         // setCursorPos(1, 4) → col 3 (inside scroll region)
        Feed(t, "\u001b[1P");           // deleteChars(1)

        Assert.Equal("ABC2 3", GetLine(t, 0));
    }

    [Fact]
    public void DeleteChars_SplitWideCharFromSpacerTail()
    {
        // Ghostty: "Terminal: deleteChars split wide character from spacer tail"
        using var t = CreateTerminal(cols: 6, rows: 10);
        Feed(t, "A\u6A4B123");          // A橋123 — 橋 is wide (2 cells)
        Feed(t, "\u001b[1;3H");         // setCursorPos(1, 3) → col 2 (spacer tail of 橋)
        Feed(t, "\u001b[1P");           // deleteChars(1)

        Assert.Equal("A 123", GetLine(t, 0));
    }

    [Fact]
    public void DeleteChars_SplitWideCharFromWide()
    {
        // Ghostty: "Terminal: deleteChars split wide character from wide"
        using var t = CreateTerminal(cols: 6, rows: 10);
        Feed(t, "\u6A4B123");           // 橋123 — 橋 at cols 0-1
        Feed(t, "\u001b[1;1H");         // setCursorPos(1, 1) → col 0 (wide char head)
        Feed(t, "\u001b[1P");           // deleteChars(1)

        // Cell at (0,0) should be empty (codepoint 0), cell at (0,1) should be '1'
        var cell0 = GetCell(t, 0, 0);
        var cell1 = GetCell(t, 0, 1);
        Assert.Equal("1", cell1.Character);
    }

    [Fact]
    public void DeleteChars_SplitWideCharFromEnd()
    {
        // Ghostty: "Terminal: deleteChars split wide character from end"
        using var t = CreateTerminal(cols: 6, rows: 10);
        Feed(t, "A\u6A4B123");          // A橋123
        Feed(t, "\u001b[1;1H");         // setCursorPos(1, 1) → col 0
        Feed(t, "\u001b[1P");           // deleteChars(1)

        // 橋 should shift left to cols 0-1
        var cell0 = GetCell(t, 0, 0);
        Assert.Equal("\u6A4B", cell0.Character); // 橋
    }

    [Fact]
    public void DeleteChars_WithSpacerHeadAtEnd()
    {
        // Ghostty: "Terminal: deleteChars with a spacer head at the end"
        // 5-col terminal: "0123橋123" wraps with spacer head at col 4
        using var t = CreateTerminal(cols: 5, rows: 10);
        Feed(t, "0123\u6A4B123");       // 0123橋123 — wraps: row 0 = "0123_", row 1 = "橋123"
        Feed(t, "\u001b[1;1H");         // setCursorPos(1, 1) → row 0, col 0
        Feed(t, "\u001b[1P");           // deleteChars(1)

        // After delete: spacer head at col 4 shifts to col 3, becomes empty
        // Row 0 should be "123" (with trailing empty cells)
        Assert.Equal("123", GetLine(t, 0));
    }

    [Fact]
    public void DeleteChars_SplitWideCharTail()
    {
        // Ghostty: "Terminal: deleteChars split wide character tail"
        using var t = CreateTerminal(cols: 5, rows: 5);
        Feed(t, "\u001b[1;4H");         // setCursorPos(1, cols-1) → col 3 (0-based, 5-col terminal)
        Feed(t, "\u6A4B");              // 橋 — wide char at cols 3-4
        Feed(t, "\r");                  // carriageReturn → col 0
        Feed(t, $"\u001b[4P");          // deleteChars(cols - 1 = 4)
        Feed(t, "0");

        Assert.Equal("0", GetLine(t, 0));
    }

    [Fact]
    public void DeleteChars_WideCharBoundaryConditions()
    {
        // Ghostty: "Terminal: deleteChars wide char boundary conditions"
        // 8-col terminal: 😀a😀b😀 = 8 cells
        // DCH 3 at col 1 splits first two wide chars
        using var t = CreateTerminal(cols: 8, rows: 1);
        Feed(t, "\U0001F600a\U0001F600b\U0001F600"); // 😀a😀b😀

        Assert.Equal("\U0001F600a\U0001F600b\U0001F600", GetLine(t, 0));

        Feed(t, "\u001b[1;2H");         // setCursorPos(1, 2) → col 1
        Feed(t, "\u001b[3P");           // deleteChars(3)

        Assert.Equal("  b\U0001F600", GetLine(t, 0));
    }

    [Fact]
    public void DeleteChars_WideCharWrapBoundaryConditions()
    {
        // Ghostty: "Terminal: deleteChars wide char wrap boundary conditions"
        // 8 cols, 3 rows
        // ".......😀abcde😀......" wraps across 3 rows:
        //   row 0: .......H (H = spacer head, wrapped)
        //   row 1: 😀abcdeH (wrapped)
        //   row 2: 😀......
        using var t = CreateTerminal(cols: 8, rows: 3);
        Feed(t, ".......\U0001F600abcde\U0001F600......");

        Assert.Equal(".......", GetLine(t, 0));
        Assert.Equal("\U0001F600abcde", GetLine(t, 1));
        Assert.Equal("\U0001F600......", GetLine(t, 2));

        Feed(t, "\u001b[2;2H");         // setCursorPos(2, 2) → row 1, col 1
        Feed(t, "\u001b[3P");           // deleteChars(3)

        Assert.Equal(".......", GetLine(t, 0));
        Assert.Equal(" cde", GetLine(t, 1));
        Assert.Equal("\U0001F600......", GetLine(t, 2));
    }

    [Fact]
    [Trait("FailureReason", "Bug")]
    public void DeleteChars_WideCharAcrossRightMargin()
    {
        // Ghostty: "Terminal: deleteChars wide char across right margin"
        // 8 cols, 3 rows. Scroll region cols 1-6 (0-based).
        // "123456橋" — 橋 at cols 6-7 straddles right margin.
        using var t = CreateTerminal(cols: 8, rows: 3);
        Feed(t, "123456\u6A4B");        // 123456橋

        Assert.Equal("123456\u6A4B", GetLine(t, 0));

        Feed(t, "\u001b[?69h");         // Enable DECLRMM
        Feed(t, "\u001b[2;7s");         // DECSLRM(2,7) = left=1, right=6 (0-based)
        Feed(t, "\u001b[1;2H");         // setCursorPos(1, 2) → col 1 (inside left margin)
        Feed(t, "\u001b[1P");           // deleteChars(1)

        Assert.Equal("13456", GetLine(t, 0));
    }

    #endregion

    #region insertBlanks (ICH — CSI n @)

    [Fact]
    public void InsertBlanks_Zero()
    {
        // Ghostty: "Terminal: insertBlanks zero"
        // Note: Ghostty calls insertBlanks(0) directly. Via CSI, param 0 defaults to 1.
        // Testing with explicit CSI 0 @ to match Ghostty's intent.
        using var t = CreateTerminal(cols: 5, rows: 2);
        Feed(t, "ABC");
        Feed(t, "\u001b[1;1H");         // setCursorPos(1, 1) → col 0
        Feed(t, "\u001b[0@");           // insertBlanks(0)

        // If Hex1b defaults param 0 to 1 (VT spec), content would shift.
        // Ghostty's internal function treats 0 as no-op.
        Assert.Equal("ABC", GetLine(t, 0));
    }

    [Fact]
    public void InsertBlanks_Basic()
    {
        // Ghostty: "Terminal: insertBlanks"
        using var t = CreateTerminal(cols: 5, rows: 2);
        Feed(t, "ABC");
        Feed(t, "\u001b[1;1H");         // setCursorPos(1, 1) → col 0
        Feed(t, "\u001b[2@");           // insertBlanks(2)

        Assert.Equal("  ABC", GetLine(t, 0));
    }

    [Fact]
    public void InsertBlanks_PushesOffEnd()
    {
        // Ghostty: "Terminal: insertBlanks pushes off end"
        using var t = CreateTerminal(cols: 3, rows: 2);
        Feed(t, "ABC");
        Feed(t, "\u001b[1;1H");         // setCursorPos(1, 1) → col 0
        Feed(t, "\u001b[2@");           // insertBlanks(2)

        Assert.Equal("  A", GetLine(t, 0));
    }

    [Fact]
    public void InsertBlanks_MoreThanSize()
    {
        // Ghostty: "Terminal: insertBlanks more than size"
        using var t = CreateTerminal(cols: 3, rows: 2);
        Feed(t, "ABC");
        Feed(t, "\u001b[1;1H");         // setCursorPos(1, 1) → col 0
        Feed(t, "\u001b[5@");           // insertBlanks(5)

        Assert.Equal("", GetLine(t, 0));
    }

    [Fact]
    public void InsertBlanks_NoScrollRegionFits()
    {
        // Ghostty: "Terminal: insertBlanks no scroll region, fits"
        using var t = CreateTerminal(cols: 10, rows: 10);
        Feed(t, "ABC");
        Feed(t, "\u001b[1;1H");         // setCursorPos(1, 1) → col 0
        Feed(t, "\u001b[2@");           // insertBlanks(2)

        Assert.Equal("  ABC", GetLine(t, 0));
    }

    [Fact]
    public void InsertBlanks_PreservesBackgroundSgr()
    {
        // Ghostty: "Terminal: insertBlanks preserves background sgr"
        using var t = CreateTerminal(cols: 10, rows: 10);
        Feed(t, "ABC");
        Feed(t, "\u001b[1;1H");         // setCursorPos(1, 1) → col 0
        Feed(t, "\u001b[48;2;255;0;0m"); // direct_color_bg red
        Feed(t, "\u001b[2@");           // insertBlanks(2)

        Assert.Equal("  ABC", GetLine(t, 0));

        // Verify inserted blank at col 0 has red background
        var cell = GetCell(t, 0, 0);
        Assert.NotNull(cell.Background);
    }

    [Fact]
    public void InsertBlanks_ShiftOffScreen()
    {
        // Ghostty: "Terminal: insertBlanks shift off screen"
        using var t = CreateTerminal(cols: 5, rows: 10);
        Feed(t, "  ABC");
        Feed(t, "\u001b[1;3H");         // setCursorPos(1, 3) → col 2
        Feed(t, "\u001b[2@");           // insertBlanks(2)
        Feed(t, "X");

        Assert.Equal("  X A", GetLine(t, 0));
    }

    [Fact]
    public void InsertBlanks_SplitMultiCellCharacter()
    {
        // Ghostty: "Terminal: insertBlanks split multi-cell character"
        using var t = CreateTerminal(cols: 5, rows: 10);
        Feed(t, "123\u6A4B");           // 123橋 — 橋 at cols 3-4
        Feed(t, "\u001b[1;1H");         // setCursorPos(1, 1) → col 0
        Feed(t, "\u001b[1@");           // insertBlanks(1)

        Assert.Equal(" 123", GetLine(t, 0));
    }

    [Fact]
    public void InsertBlanks_InsideLeftRightScrollRegion()
    {
        // Ghostty: "Terminal: insertBlanks inside left/right scroll region"
        using var t = CreateTerminal(cols: 10, rows: 10);
        Feed(t, "\u001b[?69h");         // Enable DECLRMM
        Feed(t, "\u001b[3;5s");         // DECSLRM(3,5) = left=2, right=4 (0-based)
        Feed(t, "\u001b[1;3H");         // setCursorPos(1, 3) → col 2 (at left margin)
        Feed(t, "ABC");                 // at cols 2, 3, 4 (inside scroll region)
        Feed(t, "\u001b[1;3H");         // setCursorPos(1, 3) → col 2
        Feed(t, "\u001b[2@");           // insertBlanks(2)
        Feed(t, "X");

        Assert.Equal("  X A", GetLine(t, 0));
    }

    [Fact]
    public void InsertBlanks_OutsideLeftRightScrollRegion()
    {
        // Ghostty: "Terminal: insertBlanks outside left/right scroll region"
        // Cursor outside left/right scroll region → insertBlanks resets pending wrap but no content change
        using var t = CreateTerminal(cols: 6, rows: 10);
        Feed(t, "\u001b[1;4H");         // setCursorPos(1, 4) → col 3
        Feed(t, "ABC");                 // at cols 3, 4, 5 → pending wrap
        Feed(t, "\u001b[?69h");         // Enable DECLRMM
        Feed(t, "\u001b[3;5s");         // DECSLRM(3,5) = left=2, right=4 (0-based)
        // DECSLRM homes cursor; re-position outside scroll region
        Feed(t, "\u001b[1;7H");         // CUP → col 5 (clamped, outside right margin)
        Feed(t, "\u001b[2@");           // insertBlanks(2) — cursor outside scroll region
        Feed(t, "X");

        Assert.Equal("   ABX", GetLine(t, 0));
    }

    [Fact]
    public void InsertBlanks_LeftRightScrollRegionLargeCount()
    {
        // Ghostty: "Terminal: insertBlanks left/right scroll region large count"
        using var t = CreateTerminal(cols: 10, rows: 10);
        Feed(t, "\u001b[?6h");          // Enable origin mode
        Feed(t, "\u001b[?69h");         // Enable DECLRMM
        Feed(t, "\u001b[3;5s");         // DECSLRM(3,5) = left=2, right=4 (0-based)
        Feed(t, "\u001b[1;1H");         // setCursorPos(1, 1) — origin mode → absolute col 2
        Feed(t, "\u001b[140@");         // insertBlanks(140)
        Feed(t, "X");

        Assert.Equal("  X", GetLine(t, 0));
    }

    [Fact]
    public void InsertBlanks_DeletingGraphemes()
    {
        // Ghostty: "Terminal: insertBlanks deleting graphemes"
        // Tests that inserting blanks correctly displaces grapheme clusters
        using var t = CreateTerminal(cols: 5, rows: 5);

        // Enable grapheme clustering
        Feed(t, "\u001b[?2027h");

        Feed(t, "ABC");
        // 👨‍👩‍👧 family emoji (grapheme cluster)
        Feed(t, "\U0001F468\u200D\U0001F469\u200D\U0001F467");

        Feed(t, "\u001b[1;1H");         // setCursorPos(1, 1) → col 0
        Feed(t, "\u001b[4@");           // insertBlanks(4)

        Assert.Equal("    A", GetLine(t, 0));
    }

    [Fact]
    public void InsertBlanks_ShiftGraphemes()
    {
        // Ghostty: "Terminal: insertBlanks shift graphemes"
        // Tests that grapheme clusters are correctly shifted
        using var t = CreateTerminal(cols: 5, rows: 5);

        // Enable grapheme clustering
        Feed(t, "\u001b[?2027h");

        Feed(t, "A");
        // 👨‍👩‍👧 family emoji (grapheme cluster)
        Feed(t, "\U0001F468\u200D\U0001F469\u200D\U0001F467");

        Feed(t, "\u001b[1;1H");         // setCursorPos(1, 1) → col 0
        Feed(t, "\u001b[1@");           // insertBlanks(1)

        Assert.Equal(" A\U0001F468\u200D\U0001F469\u200D\U0001F467", GetLine(t, 0));
    }

    [Fact]
    public void InsertBlanks_SplitMultiCellCharFromTail()
    {
        // Ghostty: "Terminal: insertBlanks split multi-cell character from tail"
        using var t = CreateTerminal(cols: 5, rows: 10);
        Feed(t, "\u6A4B123");           // 橋123 — 橋 at cols 0-1
        Feed(t, "\u001b[1;2H");         // setCursorPos(1, 2) → col 1 (tail of 橋)
        Feed(t, "\u001b[1@");           // insertBlanks(1)

        Assert.Equal("   12", GetLine(t, 0));
    }

    [Fact]
    public void InsertBlanks_WideCharStraddlingRightMargin()
    {
        // Ghostty: "Terminal: insertBlanks wide char straddling right margin"
        // 10 cols, 5 rows. Wide char at cols 4-5 straddles right margin at col 4.
        using var t = CreateTerminal(cols: 10, rows: 5);
        Feed(t, "ABCD\u6A4B");          // ABCD橋 — 橋 at cols 4-5
        Feed(t, "\u001b[?69h");         // Enable DECLRMM
        Feed(t, "\u001b[1;5s");         // DECSLRM(1,5) = left=0, right=4 (0-based)
        Feed(t, "\u001b[1;3H");         // setCursorPos(1, 3) → col 2
        Feed(t, "\u001b[1@");           // insertBlanks(1)

        Assert.Equal("AB CD", GetLine(t, 0));
    }

    [Fact]
    public void InsertBlanks_WideCharSpacerTailOrphanedBeyondRightMargin()
    {
        // Ghostty: "Terminal: insertBlanks wide char spacer_tail orphaned beyond right margin"
        // 10 cols, 5 rows. Fill with 5 wide chars (中 × 5), set margins, insert blanks.
        using var t = CreateTerminal(cols: 10, rows: 5);
        // Print 5 wide chars: 中中中中中 (fills all 10 cells)
        Feed(t, "\u4E2D\u4E2D\u4E2D\u4E2D\u4E2D");
        Feed(t, "\u001b[?69h");         // Enable DECLRMM
        Feed(t, "\u001b[1;9s");         // DECSLRM(1,9) = left=0, right=8 (0-based)
        Feed(t, "a");                   // at col 0 (cursor homed after DECSLRM)
        Feed(t, "\u001b[8@");           // insertBlanks(8) at col 1

        Assert.Equal("a", GetLine(t, 0));
    }

    #endregion
}
