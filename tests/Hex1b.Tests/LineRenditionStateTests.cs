using System.Text;
using System.Text.Json;
using Hex1b.Automation;
using Hex1b.Layout;
using Hex1b.Reflow;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class LineRenditionStateTests
{
    [TestMethod]
    [DataRow("character", false)]
    [DataRow("word", false)]
    [DataRow("line", false)]
    [DataRow("character", true)]
    [DataRow("word", true)]
    [DataRow("line", true)]
    public async Task Selection_EnlargedDeferredWrap_CopiesOneLogicalLine(string mode, bool history)
    {
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless().WithDimensions(10, 3).WithScrollback(10).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b#3ABCDEFG"));
        var view = new Hwt1ViewState();
        if (history)
        {
            terminal.ApplyTokens(AnsiTokenizer.Tokenize("\r\nTAIL\r\nLAST"));
            Send(terminal, view, new { type = "viewport", requestId = 1, delta = int.MinValue });
        }
        var initial = Capture(terminal, view);
        Send(terminal, view, new { type = "selection", action = "start", mode, requestId = 2,
            generation = initial.Generation, rowId = initial.RowIds[0], column = 0 });
        if (mode == "character")
            Send(terminal, view, new { type = "selection", action = "extend", requestId = 3,
                generation = initial.Generation, rowId = initial.RowIds[1], column = 1 });
        var selection = Capture(terminal, view).Selection;
        Assert.AreEqual("valid", selection.Status);
        Assert.AreEqual("ABCDEFG", selection.Text);
        Assert.AreEqual(5, selection.Ranges[0].EndColumn);
        Send(terminal, view, new { type = "copy", requestId = 4 });
        Assert.AreEqual("ABCDEFG", Capture(terminal, view).Copy!.Text);
    }

    [TestMethod]
    public async Task Selection_IndependentHalves_CopiesBothRowsWithHardBreak()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless().WithDimensions(10, 3).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b#3ABCDE\r\n\x1b#4ABCDE"));
        var view = new Hwt1ViewState();
        var initial = Capture(terminal, view);
        Send(terminal, view, new { type = "selection", action = "start", mode = "character", requestId = 1,
            generation = initial.Generation, rowId = initial.RowIds[0], column = 0 });
        Send(terminal, view, new { type = "selection", action = "extend", requestId = 2,
            generation = initial.Generation, rowId = initial.RowIds[1], column = 9 });
        Assert.AreEqual("ABCDE\nABCDE", Capture(terminal, view).Selection.Text);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Markers_EnlargedPendingWrap_FollowsDestinationBeforeSourceErasure(bool observe)
    {
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless().WithDimensions(10, 3).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b#6ABCDE\x1b]133;C\a"));
        var view = new Hwt1ViewState();
        if (observe)
            Assert.AreEqual(5, TestSeq.Single(Capture(terminal, view).Markers).Column);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("FG\x1b[H\x1b[2K"));
        var marker = TestSeq.Single(Capture(terminal, view).Markers);
        Assert.AreEqual(1, marker.Row);
        Assert.AreEqual(0, marker.Column);
    }

    [TestMethod]
    public async Task RenditionChange_CroppedText_ExpiresSelectionAndMarkerAndRejectsHiddenPosition()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless().WithDimensions(10, 3).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("ABCDEFGHIJ"));
        var view = new Hwt1ViewState();
        var initial = Capture(terminal, view);
        Send(terminal, view, new { type = "selection", action = "start", mode = "character", requestId = 1,
            generation = initial.Generation, rowId = initial.RowIds[0], column = 6 });
        Send(terminal, view, new { type = "marker", action = "add", requestId = 2,
            id = $"custom:{Guid.NewGuid():D}", generation = initial.Generation, rowId = initial.RowIds[0], column = 6 });
        Assert.AreEqual(1, Capture(terminal, view).Markers.Length);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\r\x1b#6"));
        var cropped = Capture(terminal, view);
        Assert.AreEqual("invalidated", cropped.Selection.Status);
        Assert.IsEmpty(cropped.Markers);
        Send(terminal, view, new { type = "marker", action = "add", requestId = 3,
            id = $"custom:{Guid.NewGuid():D}", generation = cropped.Generation, rowId = cropped.RowIds[0], column = 6 });
        Assert.IsEmpty(Capture(terminal, view).Markers);
    }

    [TestMethod]
    public async Task SnapshotRegion_HorizontalAndNestedSlices_PreserveLogicalGeometryAndAnsi()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless().WithDimensions(10, 3).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b#3ABCDE\r\n\x1b#4VWXYZ"));
        using var snapshot = terminal.CreateSnapshot();
        var region = snapshot.GetRegion(new Rect(1, 0, 3, 2));
        Assert.AreEqual(3, region.GetLogicalWidth(0));
        Assert.AreEqual(LineRendition.DoubleHeightTop, region.GetLineRendition(0));
        Assert.AreEqual(LineRendition.DoubleHeightBottom, region.GetLineRendition(1));
        var nested = region.GetRegion(new Rect(1, 0, 1, 1));
        Assert.AreEqual(1, nested.GetLogicalWidth(0));
        Assert.AreEqual("C", nested.GetCell(0, 0).Character);
        Assert.AreEqual(LineRendition.DoubleHeightTop, nested.GetLineRendition(0));
        Assert.AreEqual(0, snapshot.GetRegion(new Rect(-2, -1, 3, 1)).GetLogicalWidth(0));
        Assert.AreEqual(0, snapshot.GetRegion(new Rect(7, 0, 3, 1)).GetLogicalWidth(0));
        await using var replay = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless().WithDimensions(10, 3).Build();
        replay.ApplyTokens(AnsiTokenizer.Tokenize(region.ToAnsi()));
        using var replayed = replay.CreateSnapshot();
        Assert.AreEqual("BCD", string.Concat(Enumerable.Range(0, 3).Select(x => replayed.GetCell(x, 0).Character)));
        Assert.AreEqual("WXY", string.Concat(Enumerable.Range(0, 3).Select(x => replayed.GetCell(x, 1).Character)));
        Assert.AreEqual(LineRendition.DoubleHeightTop, replayed.GetLineRendition(0));
        Assert.AreEqual(LineRendition.DoubleHeightBottom, replayed.GetLineRendition(1));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public async Task Reflow_InvalidRenditionResult_DoesNotInvalidateCoordinatesOrLoseAnchors(int failure)
    {
        var provider = new TestReflowProvider { Failure = failure };
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless().WithDimensions(10, 3)
            .WithReflow(provider).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b#6AB\x1b]133;C\aCD"));
        var view = new Hwt1ViewState();
        var before = Capture(terminal, view);
        Assert.Throws<InvalidOperationException>(() => terminal.Resize(12, 4));
        var after = Capture(terminal, view);
        Assert.AreEqual(before.Generation, after.Generation);
        TestSeq.AreEqual(before.Markers, after.Markers);
        using (var snapshot = terminal.CreateSnapshot())
        {
            Assert.AreEqual(10, snapshot.Width);
            Assert.AreEqual(LineRendition.DoubleWidth, snapshot.GetLineRendition(0));
            Assert.AreEqual("C", snapshot.GetCell(2, 0).Character);
        }
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\r\x1b[2K"));
        Assert.IsEmpty(Capture(terminal, view).Markers);
        provider.Failure = -1;
        terminal.Resize(12, 4);
        using var resized = terminal.CreateSnapshot();
        Assert.AreEqual(LineRendition.DoubleWidth, resized.GetLineRendition(0));
        Assert.AreEqual(LineRendition.DoubleWidth, provider.InputMode);
    }

    [TestMethod]
    [DataRow(0, false)]
    [DataRow(0, true)]
    [DataRow(1, false)]
    [DataRow(1, true)]
    [DataRow(2, false)]
    [DataRow(2, true)]
    public async Task Reflow_EnlargedTextAnchors_RetainsBoundaryButExpiresClippedWideGlyph(int strategy, bool boundary)
    {
        var builder = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless().WithDimensions(12, 3);
        if (strategy != 0)
            builder.WithReflow(strategy == 1 ? NoReflowStrategy.Instance : GhosttyReflowStrategy.Instance);
        await using var terminal = builder.Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(boundary
            ? "\x1b#6ABCDEF\x1b]133;C\a"
            : "\x1b#6ABCD\x1b]133;C\a\u754c"));
        terminal.Resize(boundary ? 13 : 10, 3);
        var markers = Capture(terminal, new Hwt1ViewState()).Markers;
        Assert.AreEqual(boundary ? 1 : 0, markers.Length);
        if (boundary)
            Assert.AreEqual(6, markers[0].Column);
    }

    [TestMethod]
    public async Task Reflow_LegacyProvider_NormalTextAcceptsOmittedMetadata()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless().WithDimensions(10, 3)
            .WithReflow(new TestReflowProvider { Failure = 0 }).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("ABC"));
        terminal.Resize(12, 4);
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(12, snapshot.Width);
        Assert.AreEqual("A", snapshot.GetCell(0, 0).Character);
        Assert.AreEqual(LineRendition.SingleWidth, snapshot.GetLineRendition(0));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task TabSet_CustomStop_UsesLogicalColumnAndSerializes(bool enlarged)
    {
        const string sequence = "\x1bH";
        var token = TestSeq.Single(AnsiTokenizer.Tokenize(sequence));
        TestSeq.IsType<TabSetToken>(token);
        Assert.AreEqual(sequence, AnsiTokenSerializer.Serialize(token));
        Assert.AreEqual(sequence, Encoding.UTF8.GetString(AnsiTokenUtf8Serializer.Serialize(token).Span));
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless().WithDimensions(20, 3).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            (enlarged ? "\x1b#6" : "") + "\x1b[3g\x1b[5G\x1bH\r\tX"));
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("X", snapshot.GetCell(4, 0).Character);
        Assert.AreEqual(5, snapshot.CursorX);
    }

    [TestMethod]
    public async Task Snapshot_HistoricalWidth_CropsWideGlyphWithoutReleasingHistoryOwnership()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless().WithDimensions(12, 3).WithScrollback(10).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b#6ABCD\x1b]8;;https://example.test/a\a\u754c\x1b]8;;\a\r\nROW1\r\nROW2\r\nROW3"));
        var link = terminal.GetScrollbackRows(1)[0].Cells[4].TrackedHyperlink!;
        Assert.AreEqual(2, link.RefCount);
        terminal.Resize(10, 3);
        using (var current = terminal.CreateSnapshot(1))
        {
            Assert.AreEqual(5, current.GetLogicalWidth(0));
            Assert.AreEqual("ABCD", current.GetLineTrimmed(0));
            Assert.AreEqual(TerminalCell.Empty.Character, current.GetCell(4, 0).Character);
            Assert.AreEqual(2, link.RefCount);
        }
        using (var original = terminal.CreateSnapshot(1, ScrollbackWidth.Original, new TerminalCell("?", null, null)))
        {
            Assert.AreEqual(12, original.Width);
            Assert.AreEqual(6, original.GetLogicalWidth(0));
            Assert.AreEqual("\u754c", original.GetCell(4, 0).Character);
            Assert.AreEqual(10, original.GetLogicalWidth(1));
            Assert.AreEqual("?", original.GetCell(10, 1).Character);
            Assert.AreEqual(4, link.RefCount);
        }
        Assert.AreEqual(2, link.RefCount);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3J"));
        Assert.AreEqual(0, link.RefCount);
    }

    [TestMethod]
    public async Task RectangleSelection_MixedWidths_DoesNotSelectHiddenCells()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless().WithDimensions(10, 3).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("ABCDEFGHIJ\r\n\x1b#3KLMNO\r\nPQRSTUVWXY"));
        var view = new Hwt1ViewState();
        var initial = Capture(terminal, view);
        Send(terminal, view, new { type = "selection", action = "start", mode = "rectangle", requestId = 1,
            generation = initial.Generation, rowId = initial.RowIds[0], column = 7 });
        Send(terminal, view, new { type = "selection", action = "extend", requestId = 2,
            generation = initial.Generation, rowId = initial.RowIds[2], column = 9 });
        var selection = Capture(terminal, view).Selection;
        Assert.AreEqual("HIJ\n\nWXY", selection.Text);
        Assert.AreEqual(2, selection.Ranges.Count);
    }

    [TestMethod]
    [DataRow("\x1b#3ABCDE", "\x1b#4", "FG")]
    [DataRow("\x1b#6ABCD", "\x1b#5", "\u754c")]
    [DataRow("\x1b#5ABCDEFGHIJ", "\x1b#3", "K")]
    public async Task AnsiReplay_MixedRenditionWrap_PreservesTextAndBreak(string first, string second, string overflow)
    {
        await using var source = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless().WithDimensions(10, 3).Build();
        source.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[2;1H" + second + "\x1b[H" + first + overflow));
        using var snapshot = source.CreateSnapshot();
        await using var replay = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless().WithDimensions(10, 3).Build();
        replay.ApplyTokens(AnsiTokenizer.Tokenize(snapshot.ToAnsi(new TerminalAnsiOptions
        {
            IncludeClearScreen = true, IncludeTrailingNewline = false, IncludeCursorPosition = true
        }, includeHyperlinks: true, preserveSoftWrap: true)));
        using var actual = replay.CreateSnapshot();
        Assert.AreEqual(snapshot.CursorX, actual.CursorX);
        Assert.AreEqual(snapshot.CursorY, actual.CursorY);
        Assert.IsTrue(snapshot.IsLineSoftWrapped(0));
        Assert.IsTrue(actual.IsLineSoftWrapped(0));
        for (var row = 0; row < 3; row++)
        {
            Assert.AreEqual(snapshot.GetLineRendition(row), actual.GetLineRendition(row));
            Assert.AreEqual(snapshot.GetLine(row), actual.GetLine(row));
        }
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public async Task AnsiRegion_ClippedWideGlyph_DoesNotEscapeLogicalBounds(int x)
    {
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless().WithDimensions(10, 3).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b#6\u754cABC"));
        using var snapshot = terminal.CreateSnapshot();
        var region = snapshot.GetRegion(new Rect(x, 0, 1, 1));
        await using var replay = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless().WithDimensions(10, 3).Build();
        replay.ApplyTokens(AnsiTokenizer.Tokenize(region.ToAnsi()));
        using var actual = replay.CreateSnapshot();
        Assert.AreEqual("", actual.GetLineTrimmed(0));
        Assert.AreEqual(LineRendition.DoubleWidth, actual.GetLineRendition(0));
    }

    private sealed class TestReflowProvider : ITerminalReflowProvider
    {
        public int Failure { get; set; }
        public LineRendition InputMode { get; private set; }
        public bool ShouldClearSoftWrapOnAbsolutePosition => false;
        public ReflowResult Reflow(ReflowContext context)
        {
            InputMode = context.LineRenditions[0];
            var result = NoReflowStrategy.Instance.Reflow(context);
            return Failure switch
            {
                0 => result with { LineRenditions = [] },
                1 => result with { LineRenditions = [LineRendition.SingleWidth] },
                2 => result with { LineRenditions = Enumerable.Repeat((LineRendition)255, context.NewHeight).ToArray() },
                _ => result
            };
        }
    }

    private static Hwt1History Capture(Hex1bTerminal terminal, Hwt1ViewState view)
    {
        Assert.IsTrue(terminal.TryCaptureBrowserSnapshot(view, out var snapshot, out var history, out _, out _));
        snapshot.Dispose();
        return history;
    }

    private static void Send(Hex1bTerminal terminal, Hwt1ViewState view, object message)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(message));
        terminal.HandleBrowserHistoryMessage(view, document.RootElement);
    }
}
