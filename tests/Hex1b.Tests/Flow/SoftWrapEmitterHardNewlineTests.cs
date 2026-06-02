using Hex1b.Flow;
using Hex1b.Surfaces;

namespace Hex1b.Tests.Flow;

/// <summary>
/// Pins the CR + LF hard-newline contract that <see cref="SoftWrapEmitter"/>
/// must honour. The flow resize handler relies on every inter-row boundary
/// in an emitted tombstone being terminated with <c>"\r\n"</c> (not a bare
/// <c>"\n"</c>): hard-newline-terminated paragraphs are the *only* way
/// xterm, Windows Terminal, VTE, iTerm2 and friends guarantee a row will
/// not be reflowed across a resize. If someone accidentally changes the
/// terminator to a bare LF, paragraph boundaries become soft-wrap state on
/// the host's grid and the row-origin recompute described in
/// <c>FlowResizeRowOriginFeasibilityTests</c> stops matching reality.
/// </summary>
[TestClass]
public class SoftWrapEmitterHardNewlineTests
{
    [TestMethod]
    public void Format_MultiRowSurface_TerminatesInteriorRowsWithCarriageReturnPlusLineFeed()
    {
        var surface = new Surface(20, 4);
        surface.WriteText(0, 0, "row-0");
        surface.WriteText(0, 1, "row-1");
        surface.WriteText(0, 2, "row-2");
        surface.WriteText(0, 3, "row-3");

        var output = SoftWrapEmitter.Format(surface);

        // Every interior row must be terminated with CR + LF.
        Assert.Contains("row-0\x1b[K\r\n", output);
        Assert.Contains("row-1\x1b[K\r\n", output);
        Assert.Contains("row-2\x1b[K\r\n", output);

        // The final row must NOT carry a trailing newline (deliberate — see
        // SoftWrapEmitter remarks).
        Assert.EndsWith("row-3\x1b[K\x1b[?25h", output);
    }

    [TestMethod]
    public void Format_NoBareLineFeedsAtInteriorRowBoundaries()
    {
        // Walk the output looking for any inter-row boundary terminated by
        // a bare LF (LF without an immediately preceding CR). Such a
        // terminator would survive the existing happy-path assertions
        // (which only check the *presence* of CR+LF) but break the
        // hard-newline contract on hosts that don't auto-translate.
        var surface = new Surface(12, 5);
        for (var row = 0; row < 5; row++)
        {
            surface.WriteText(0, row, $"r{row}");
        }

        var output = SoftWrapEmitter.Format(surface);

        for (var i = 0; i < output.Length; i++)
        {
            if (output[i] != '\n') continue;
            Assert.IsTrue(i > 0 && output[i - 1] == '\r', $"Bare LF found at index {i}; every LF must be preceded by CR. Output: {output.Replace("\x1b", "ESC")}");
        }
    }

    [TestMethod]
    public void Format_SingleRowSurface_HasNoLineTerminatorAtAll()
    {
        // Single-row case: the only row is also the last row, so no CR or
        // LF should appear in the output. This pins the "no trailing
        // newline on the final row" half of the contract.
        var surface = new Surface(10, 1);
        surface.WriteText(0, 0, "only");

        var output = SoftWrapEmitter.Format(surface);

        Assert.DoesNotContain("\r", output);
        Assert.DoesNotContain("\n", output);
    }
}
