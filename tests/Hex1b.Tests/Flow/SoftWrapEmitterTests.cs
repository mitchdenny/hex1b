using Hex1b.Flow;
using Hex1b.Surfaces;
using Hex1b.Theming;

namespace Hex1b.Tests.Flow;

/// <summary>
/// Unit tests for <see cref="SoftWrapEmitter"/>. These verify the byte-level
/// shape of the output so the host terminal can soft-wrap and scroll
/// tombstoned content naturally on resize.
/// </summary>
public class SoftWrapEmitterTests
{
    [Fact]
    public void Format_ZeroHeightSurface_RejectedByCtor()
    {
        // Surface requires a non-zero height; SoftWrapEmitter is therefore
        // never invoked on an empty surface in practice. Document the
        // boundary so we don't accidentally start passing zero in the runner.
        Assert.Throws<ArgumentOutOfRangeException>(() => new Surface(10, 0));
    }

    [Fact]
    public void Format_AllRowsBlank_EmitsClearLineAndNewlinePerRow()
    {
        var surface = new Surface(20, 3);

        var output = SoftWrapEmitter.Format(surface);

        // Preamble + (ESC[K + \n) per row + epilogue.
        Assert.Equal(
            "\x1b[?25l\x1b[?7h" +
            "\x1b[K\n" +
            "\x1b[K\n" +
            "\x1b[K\n" +
            "\x1b[?25h",
            output);
    }

    [Fact]
    public void Format_PlainTextRow_EmitsCharactersThenClearAndNewline()
    {
        var surface = new Surface(20, 1);
        surface.WriteText(0, 0, "hello");

        var output = SoftWrapEmitter.Format(surface);

        Assert.Contains("hello\x1b[K\n", output);
        Assert.StartsWith("\x1b[?25l\x1b[?7h", output);
        Assert.EndsWith("\x1b[?25h", output);
    }

    [Fact]
    public void Format_PlainTextRow_DoesNotEmitTrailingBlanksAsCharacters()
    {
        var surface = new Surface(20, 1);
        surface.WriteText(0, 0, "hi");

        var output = SoftWrapEmitter.Format(surface);

        // The row must end with the two characters and the clear-line directly
        // after them; trailing 18 columns must not be emitted as literal
        // spaces (they are wiped by ESC[K).
        Assert.Contains("hi\x1b[K\n", output);
        Assert.DoesNotContain("hi  ", output);
    }

    [Fact]
    public void Format_RowWithLeadingBlanks_EmitsSpacesUpToContent()
    {
        // Leading blanks must be preserved as actual spaces because we can't
        // use cursor-positioning and still call the output a "logical line".
        var surface = new Surface(20, 1);
        surface.WriteText(3, 0, "x");

        var output = SoftWrapEmitter.Format(surface);

        Assert.Contains("   x\x1b[K\n", output);
    }

    [Fact]
    public void Format_RowWithForegroundColor_EmitsSgrResetThenColor()
    {
        var surface = new Surface(10, 1);
        surface.WriteText(0, 0, "X", foreground: Hex1bColor.FromStandard(1, 128, 0, 0));

        var output = SoftWrapEmitter.Format(surface);

        // First SGR on row always resets, then sets the colour: ESC[0;31m.
        Assert.Contains("\x1b[0;31mX", output);
        // Reset SGR before the line clear so cleared cells don't inherit colour.
        Assert.Contains("X\x1b[0m\x1b[K\n", output);
    }

    [Fact]
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

    [Fact]
    public void Format_BackgroundColor_ResetBeforeClearLineSoCellsDontInherit()
    {
        var surface = new Surface(5, 1);
        var blue = Hex1bColor.FromStandard(4, 0, 0, 128);
        surface.WriteText(0, 0, "ZZZZZ", background: blue);

        var output = SoftWrapEmitter.Format(surface);

        // Without the reset, ESC[K would paint the blue background to the
        // edge of the terminal — not what we want for a logical-line emit.
        Assert.Contains("\x1b[0m\x1b[K\n", output);
    }

    [Fact]
    public void Format_WideCharacter_EmitsGraphemeOnceAndSkipsContinuation()
    {
        var surface = new Surface(10, 1);
        // "漢" is a 2-cell grapheme. WriteText writes the primary cell at x=0
        // and a continuation cell at x=1.
        surface.WriteText(0, 0, "漢A");

        var output = SoftWrapEmitter.Format(surface);

        // The wide char should appear exactly once and immediately precede
        // 'A'; the continuation cell at x=1 must not produce any glyph.
        Assert.Contains("漢A\x1b[K\n", output);
    }

    [Fact]
    public void Format_StartsWithCursorHide_EndsWithCursorShow()
    {
        var surface = new Surface(10, 2);
        surface.WriteText(0, 0, "row1");
        surface.WriteText(0, 1, "row2");

        var output = SoftWrapEmitter.Format(surface);

        Assert.StartsWith("\x1b[?25l", output);
        Assert.EndsWith("\x1b[?25h", output);
    }

    [Fact]
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
        Assert.True(awmIdx < firstA);
    }

    [Fact]
    public void Format_MultiRow_EmitsExpectedNumberOfLineFeeds()
    {
        var surface = new Surface(8, 4);
        surface.WriteText(0, 0, "r0");
        surface.WriteText(0, 1, "r1");
        surface.WriteText(0, 2, "r2");
        surface.WriteText(0, 3, "r3");

        var output = SoftWrapEmitter.Format(surface);

        // One \n per row, exactly.
        var lfCount = output.Count(c => c == '\n');
        Assert.Equal(4, lfCount);
    }

    [Fact]
    public void Format_BoldAttribute_EmittedAsSgr1()
    {
        var surface = new Surface(5, 1);
        surface.WriteText(0, 0, "B", attributes: CellAttributes.Bold);

        var output = SoftWrapEmitter.Format(surface);

        Assert.Contains("\x1b[0;1mB", output);
    }

    [Fact]
    public void Format_RowWithOnlyBlanks_BetweenContentRows_StillCleared()
    {
        var surface = new Surface(8, 3);
        surface.WriteText(0, 0, "top");
        // row 1 is left blank
        surface.WriteText(0, 2, "bottom");

        var output = SoftWrapEmitter.Format(surface);

        // Each row including the empty middle row terminates with ESC[K\n.
        Assert.Contains("top\x1b[K\n", output);
        Assert.Contains("bottom\x1b[K\n", output);
        // Three line feeds total.
        Assert.Equal(3, output.Count(c => c == '\n'));
    }
}
