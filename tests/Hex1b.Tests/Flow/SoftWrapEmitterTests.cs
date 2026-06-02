using Hex1b.Flow;
using Hex1b.Surfaces;
using Hex1b.Theming;

namespace Hex1b.Tests.Flow;

/// <summary>
/// Unit tests for <see cref="SoftWrapEmitter"/>. These verify the byte-level
/// shape of the output so the host terminal can soft-wrap and scroll
/// tombstoned content naturally on resize.
/// </summary>
[TestClass]
public class SoftWrapEmitterTests
{
    [TestMethod]
    public void Format_ZeroHeightSurface_RejectedByCtor()
    {
        // Surface requires a non-zero height; SoftWrapEmitter is therefore
        // never invoked on an empty surface in practice. Document the
        // boundary so we don't accidentally start passing zero in the runner.
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Surface(10, 0));
    }

    [TestMethod]
    public void Format_AllRowsBlank_EmitsClearLinePerRow_AndCrLfBetweenRows()
    {
        var surface = new Surface(20, 3);

        var output = SoftWrapEmitter.Format(surface);

        // Preamble + (ESC[K + CR + LF) for rows 0..n-2 + ESC[K for the last row + epilogue.
        // The last row deliberately omits the CR + LF so emitting at the bottom
        // of the viewport does not scroll the screen.
        Assert.AreEqual("\x1b[?25l\x1b[?7h" +
            "\x1b[K\r\n" +
            "\x1b[K\r\n" +
            "\x1b[K" +
            "\x1b[?25h", output);
    }

    [TestMethod]
    public void Format_PlainTextRow_EmitsCharactersThenClearLine()
    {
        var surface = new Surface(20, 1);
        surface.WriteText(0, 0, "hello");

        var output = SoftWrapEmitter.Format(surface);

        // Single-row surface: the only row is also the last row, so it ends
        // with ESC[K and no trailing CR + LF.
        Assert.Contains("hello\x1b[K", output);
        Assert.DoesNotContain("hello\x1b[K\r\n", output);
        Assert.DoesNotContain("hello\x1b[K\n", output);
        Assert.StartsWith("\x1b[?25l\x1b[?7h", output);
        Assert.EndsWith("\x1b[?25h", output);
    }

    [TestMethod]
    public void Format_PlainTextRow_DoesNotEmitTrailingBlanksAsCharacters()
    {
        var surface = new Surface(20, 1);
        surface.WriteText(0, 0, "hi");

        var output = SoftWrapEmitter.Format(surface);

        // The row must end with the two characters and the clear-line directly
        // after them; trailing 18 columns must not be emitted as literal
        // spaces (they are wiped by ESC[K).
        Assert.Contains("hi\x1b[K", output);
        Assert.DoesNotContain("hi  ", output);
    }

    [TestMethod]
    public void Format_RowWithLeadingBlanks_EmitsSpacesUpToContent()
    {
        // Leading blanks must be preserved as actual spaces because we can't
        // use cursor-positioning and still call the output a "logical line".
        var surface = new Surface(20, 1);
        surface.WriteText(3, 0, "x");

        var output = SoftWrapEmitter.Format(surface);

        Assert.Contains("   x\x1b[K", output);
    }

    [TestMethod]
    public void Format_RowWithForegroundColor_EmitsSgrResetThenColor()
    {
        var surface = new Surface(10, 1);
        surface.WriteText(0, 0, "X", foreground: Hex1bColor.FromStandard(1, 128, 0, 0));

        var output = SoftWrapEmitter.Format(surface);

        // First SGR on row always resets, then sets the colour: ESC[0;31m.
        Assert.Contains("\x1b[0;31mX", output);
        // Reset SGR before the line clear so cleared cells don't inherit colour.
        Assert.Contains("X\x1b[0m\x1b[K", output);
    }

    [TestMethod]
    public void Format_MultipleColorsInRow_GroupsConsecutiveCellsIntoRuns()
    {
        var surface = new Surface(10, 1);
        var red = Hex1bColor.FromStandard(1, 128, 0, 0);
        var green = Hex1bColor.FromStandard(2, 0, 128, 0);
        surface.WriteText(0, 0, "AB", foreground: red);
        surface.WriteText(2, 0, "CD", foreground: green);

        var output = SoftWrapEmitter.Format(surface);

        // Two SGR transitions, not four. "AB" share one run; "CD" share another.
        Assert.Contains("\x1b[0;31mAB\x1b[32mCD", output);
    }

    [TestMethod]
    public void Format_BackgroundColor_ResetBeforeClearLineSoCellsDontInherit()
    {
        var surface = new Surface(5, 1);
        var blue = Hex1bColor.FromStandard(4, 0, 0, 128);
        surface.WriteText(0, 0, "ZZZZZ", background: blue);

        var output = SoftWrapEmitter.Format(surface);

        // Without the reset, ESC[K would paint the blue background to the
        // edge of the terminal — not what we want for a logical-line emit.
        Assert.Contains("\x1b[0m\x1b[K", output);
    }

    [TestMethod]
    public void Format_WideCharacter_EmitsGraphemeOnceAndSkipsContinuation()
    {
        var surface = new Surface(10, 1);
        // "漢" is a 2-cell grapheme. WriteText writes the primary cell at x=0
        // and a continuation cell at x=1.
        surface.WriteText(0, 0, "漢A");

        var output = SoftWrapEmitter.Format(surface);

        // The wide char should appear exactly once and immediately precede
        // 'A'; the continuation cell at x=1 must not produce any glyph.
        Assert.Contains("漢A\x1b[K", output);
    }

    [TestMethod]
    public void Format_StartsWithCursorHide_EndsWithCursorShow()
    {
        var surface = new Surface(10, 2);
        surface.WriteText(0, 0, "row1");
        surface.WriteText(0, 1, "row2");

        var output = SoftWrapEmitter.Format(surface);

        Assert.StartsWith("\x1b[?25l", output);
        Assert.EndsWith("\x1b[?25h", output);
    }

    [TestMethod]
    public void Format_EmitsAutowrapEnableInPreamble()
    {
        var surface = new Surface(5, 1);
        surface.WriteText(0, 0, "a");

        var output = SoftWrapEmitter.Format(surface);

        // Defensive ESC[?7h so a host that previously turned wraparound off
        // still gets the soft-wrap behaviour we depend on.
        Assert.Contains("\x1b[?7h", output);
        // The hint comes before any row content.
        var awmIdx = output.IndexOf("\x1b[?7h", StringComparison.Ordinal);
        var firstA = output.IndexOf('a');
        Assert.IsTrue(awmIdx < firstA);
    }

    [TestMethod]
    public void Format_MultiRow_EmitsCrLfBetweenRowsButNotAfterLast()
    {
        var surface = new Surface(8, 4);
        surface.WriteText(0, 0, "r0");
        surface.WriteText(0, 1, "r1");
        surface.WriteText(0, 2, "r2");
        surface.WriteText(0, 3, "r3");

        var output = SoftWrapEmitter.Format(surface);

        // CR + LF between rows: 3 of them for a 4-row surface (rows 0, 1, 2 end
        // with CR + LF; row 3 — the last row — ends with just ESC[K).
        Assert.AreEqual(3, output.Count(c => c == '\n'));
        Assert.AreEqual(3, output.Count(c => c == '\r'));

        // Rows 0, 1, 2 each terminate with ESC[K + CR + LF.
        Assert.Contains("r0\x1b[K\r\n", output);
        Assert.Contains("r1\x1b[K\r\n", output);
        Assert.Contains("r2\x1b[K\r\n", output);

        // Row 3 (last) terminates with just ESC[K, immediately followed by
        // the cursor-show epilogue.
        Assert.Contains("r3\x1b[K\x1b[?25h", output);
        Assert.DoesNotContain("r3\x1b[K\r\n", output);
        Assert.DoesNotContain("r3\x1b[K\n", output);
    }

    [TestMethod]
    public void Format_MultiRow_EmitsCarriageReturnBeforeLineFeed_SoNextRowStartsAtColumnZero()
    {
        // Regression: in raw output mode a bare LF only moves the cursor down
        // one row without resetting the column, so a subsequent row's
        // characters would emit at the column where the previous row ended,
        // shifting every row except the first to the right and effectively
        // losing their leading content. The CR before LF is the fix.
        var surface = new Surface(8, 2);
        surface.WriteText(0, 0, "abc");
        surface.WriteText(0, 1, "xyz");

        var output = SoftWrapEmitter.Format(surface);

        // Critically: the line feed must be immediately preceded by a CR,
        // not by a bare ESC[K. This guarantees column reset between rows.
        Assert.Contains("abc\x1b[K\r\n", output);
        Assert.DoesNotContain("abc\x1b[K\n", output);
    }

    [TestMethod]
    public void Format_BoldAttribute_EmittedAsSgr1()
    {
        var surface = new Surface(5, 1);
        surface.WriteText(0, 0, "B", attributes: CellAttributes.Bold);

        var output = SoftWrapEmitter.Format(surface);

        Assert.Contains("\x1b[0;1mB", output);
    }

    [TestMethod]
    public void Format_RowWithOnlyBlanks_BetweenContentRows_StillCleared()
    {
        var surface = new Surface(8, 3);
        surface.WriteText(0, 0, "top");
        // row 1 is left blank
        surface.WriteText(0, 2, "bottom");

        var output = SoftWrapEmitter.Format(surface);

        // Row 0 (top) terminates with ESC[K + CR + LF.
        Assert.Contains("top\x1b[K\r\n", output);
        // Row 2 (bottom, last) terminates with just ESC[K.
        Assert.Contains("bottom\x1b[K", output);
        Assert.DoesNotContain("bottom\x1b[K\r\n", output);
        // Two line feeds total: one between rows 0/1, one between rows 1/2.
        Assert.AreEqual(2, output.Count(c => c == '\n'));
        Assert.AreEqual(2, output.Count(c => c == '\r'));
    }
}
