using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Hex1b.Tokens;
using Microsoft.Extensions.Time.Testing;

namespace Hex1b.Tests;

[TestClass]
public class Hwt1HistoryTests
{
    [TestMethod]
    public async Task Hyperlinks_HistoricalView_UsesProducerViewportAndClearsOnReturnToLive()
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithPresentation(muxer)
            .WithDimensions(20, 10).WithScrollback(100).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b]8;;https://example.com/history\x1b\\old link\x1b]8;;\x1b\\\r\n" + Lines(0, 30)));
        await using var view = await muxer.CreateBrowserViewAsync();
        var live = await FrameAsync(view);
        Assert.AreEqual(0, live.GetProperty("hyperlinks").GetArrayLength());

        await MessageAsync(view, new { type = "viewport", requestId = 1, delta = -100 });
        var historical = await FrameAsync(view);
        var links = historical.GetProperty("hyperlinks");
        Assert.AreEqual(1, links.GetArrayLength());
        Assert.AreEqual("https://example.com/history", links[0].GetProperty("uri").GetString());
        Assert.AreEqual(0, links[0].GetProperty("row").GetInt32());
        Assert.AreEqual(0, links[0].GetProperty("startColumn").GetInt32());
        Assert.AreEqual(8, links[0].GetProperty("endColumn").GetInt32());

        await MessageAsync(view, new { type = "viewport", requestId = 2, live = true });
        var returned = await FrameAsync(view);
        Assert.AreEqual(0, returned.GetProperty("hyperlinks").GetArrayLength());
    }

    [TestMethod]
    [DataRow("character", false)]
    [DataRow("character", true)]
    [DataRow("word", false)]
    [DataRow("word", true)]
    [DataRow("line", false)]
    [DataRow("line", true)]
    public void Selection_TextBudget_RejectsOversizedSelectionsWithoutPartialCopy(string mode, bool oversized)
    {
        var text = new string('a', Hwt1ViewState.MaxSelectionTextLength -
            (mode == "line" ? 19 : 0) + (oversized ? 1 : 0));
        var buffer = new TerminalTextBuffer(1, false, 20, 10, 0, row => row + 1,
            (row, column) => row == 0 && column == 0 ? new TerminalCell(text, null, null) : TerminalCell.Empty,
            _ => 20);
        var view = new Hwt1ViewState();
        using var start = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            type = "selection", action = "start", mode, requestId = 1, generation = "1", rowId = "1", column = 0
        }));
        view.Handle(start.RootElement, buffer);
        using var copy = JsonDocument.Parse("""{"type":"copy","requestId":2}""");
        view.Handle(copy.RootElement, buffer);
        var history = view.Capture(buffer);
        Assert.AreEqual(oversized ? "invalidated" : "valid", history.Selection.Status);
        Assert.AreEqual(history.Selection.Status, history.Copy!.Status);
        if (oversized)
        {
            Assert.IsNull(history.Selection.Text);
            Assert.IsNull(history.Copy.Text);
            Assert.IsEmpty(history.Selection.Ranges);
        }
        else
        {
            Assert.AreEqual(text, history.Selection.Text);
            Assert.AreEqual(text, history.Copy.Text);
        }
    }

    [TestMethod]
    [DataRow("word", false)]
    [DataRow("word", true)]
    [DataRow("line", false)]
    [DataRow("line", true)]
    public void Selection_LongSoftWrappedExpansion_StopsAtBudgetBeforeTraversingRetainedRange(
        string mode, bool backward)
    {
        const int width = 20;
        const int historyRows = Hwt1ViewState.MaxSelectionTextLength * 8 / width;
        const int maximumCellReads = Hwt1ViewState.MaxSelectionTextLength * 8;
        var reads = 0;
        var cell = new TerminalCell("a", null, null);
        var wrappedCell = cell with { Attributes = CellAttributes.SoftWrap };
        var buffer = new TerminalTextBuffer(1, false, width, 10, historyRows, row => row + 1,
            (_, column) =>
            {
                if (++reads > maximumCellReads)
                    throw new InvalidOperationException("Selection expansion exceeded its bounded cell-read allowance.");
                return column == width - 1 ? wrappedCell : cell;
            }, _ => width);
        var view = new Hwt1ViewState();
        var selectedRowId = backward ? buffer.TotalRows : 1;
        using var start = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            type = "selection", action = "start", mode, requestId = 1,
            generation = "1", rowId = selectedRowId.ToString(), column = 0
        }));
        view.Handle(start.RootElement, buffer);
        using var copy = JsonDocument.Parse("""{"type":"copy","requestId":2}""");
        view.Handle(copy.RootElement, buffer);
        var history = view.Capture(buffer);
        Assert.AreEqual("invalidated", history.Selection.Status);
        Assert.IsNull(history.Selection.Text);
        Assert.IsEmpty(history.Selection.Ranges);
        Assert.AreEqual("invalidated", history.Copy!.Status);
        Assert.IsNull(history.Copy.Text);
        Assert.IsTrue(reads <= maximumCellReads, "Oversized ranges must stop before scanning all retained cells.");
    }

    [TestMethod]
    [DataRow("word", true)]
    [DataRow("line", false)]
    public void Selection_SoftWrappedRangeExactlyAtBudget_PreservesCompleteText(string mode, bool wide)
    {
        const int width = 32;
        var totalRows = Hwt1ViewState.MaxSelectionTextLength / (wide ? width / 2 : width);
        var buffer = new TerminalTextBuffer(1, false, width, 10, totalRows - 10, row => row + 1,
            (row, column) => new TerminalCell(
                wide ? column % 2 == 0 ? "\u754c" : "" : "a", null, null,
                row < totalRows - 1 && column == width - 1 ? CellAttributes.SoftWrap : CellAttributes.None,
                Sequence: 1),
            _ => width);
        var view = new Hwt1ViewState();
        using var start = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            type = "selection", action = "start", mode, requestId = 1,
            generation = "1", rowId = "1", column = 0
        }));
        view.Handle(start.RootElement, buffer);
        var selection = view.Capture(buffer).Selection;
        Assert.AreEqual("valid", selection.Status);
        Assert.AreEqual(Hwt1ViewState.MaxSelectionTextLength, selection.Text!.Length);
        Assert.AreEqual(wide ? '\u754c' : 'a', selection.Text[0]);
        Assert.AreEqual(wide ? '\u754c' : 'a', selection.Text[^1]);
        Assert.IsFalse(selection.Text.Contains('\n'));
    }

    [TestMethod]
    public async Task CreateBrowserViewAsync_LateAttachment_UsesProducerHistoryAndIndependentViewports()
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter()).WithPresentation(muxer)
            .WithDimensions(20, 10).WithScrollback(100).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(Lines(0, 30)));
        await using var first = await muxer.CreateBrowserViewAsync("first");
        await using var second = await muxer.CreateBrowserViewAsync("second");
        var initial = await FrameAsync(first);
        Assert.AreEqual(21, initial.GetProperty("history").GetProperty("liveTop").GetInt32());
        Assert.AreEqual(2, muxer.ClientCount);

        await MessageAsync(first, new { type = "viewport", requestId = 1, delta = -100 });
        var historical = await FrameAsync(first);
        var history = historical.GetProperty("history");
        Assert.AreEqual(0, history.GetProperty("top").GetInt32());
        Assert.IsFalse(history.GetProperty("following").GetBoolean());
        await SelectAsync(first, history, 0, 0, "line", 2);
        var selected = (await FrameAsync(first)).GetProperty("history").GetProperty("selection");
        Assert.AreEqual("row00", selected.GetProperty("text").GetString());
        var other = (await FrameAsync(second)).GetProperty("history");
        Assert.IsTrue(other.GetProperty("following").GetBoolean());
        Assert.AreEqual("none", other.GetProperty("selection").GetProperty("status").GetString());
        Assert.IsNull(muxer.PrimaryPeerId);
    }

    [TestMethod]
    public async Task Selection_OffscreenEndpoint_ExtractsUnloadedRowsAndPreservesSoftWraps()
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter()).WithPresentation(muxer)
            .WithDimensions(20, 10).WithScrollback(100).Build();
        var wrapped = new string('a', 45);
        producer.ApplyTokens(AnsiTokenizer.Tokenize(wrapped + "\r\n" + Lines(0, 20)));
        await using var view = await muxer.CreateBrowserViewAsync();
        await MessageAsync(view, new { type = "viewport", requestId = 1, delta = -100 });
        var history = (await FrameAsync(view)).GetProperty("history");
        await SelectAsync(view, history, 0, 0, "character", 2);
        await MessageAsync(view, new { type = "viewport", requestId = 3, live = true });
        var live = (await FrameAsync(view)).GetProperty("history");
        await SelectAsync(view, live, 9, 0, "character", 4, "extend");
        var selected = (await FrameAsync(view)).GetProperty("history").GetProperty("selection");
        var text = selected.GetProperty("text").GetString()!;
        Assert.StartsWith(wrapped + "\nrow00\n", text);
        Assert.Contains("row19", text);
        Assert.IsGreaterThan(10, text.Split('\n').Length);
        Assert.AreEqual(10, selected.GetProperty("ranges").GetArrayLength());
    }

    [TestMethod]
    public async Task Output_LiveRowsEnterHistory_KeepsViewportAndSelectionAnchored()
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter()).WithPresentation(muxer)
            .WithDimensions(20, 10).WithScrollback(100).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(Lines(0, 12)));
        await using var view = await muxer.CreateBrowserViewAsync();
        var live = (await FrameAsync(view)).GetProperty("history");
        var selectedId = live.GetProperty("rowIds")[0].GetString();
        await SelectAsync(view, live, 0, 0, "line", 1);
        await MessageAsync(view, new { type = "viewport", requestId = 2, delta = -1 });
        var before = (await FrameAsync(view)).GetProperty("history");
        var topId = before.GetProperty("rowIds")[0].GetString();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(Lines(12, 15)));
        var after = (await FrameAsync(view)).GetProperty("history");
        Assert.AreEqual(topId, after.GetProperty("rowIds")[0].GetString());
        Assert.AreEqual(selectedId, after.GetProperty("rowIds")[1].GetString());
        Assert.AreEqual("valid", after.GetProperty("selection").GetProperty("status").GetString());
        Assert.AreEqual("row03", after.GetProperty("selection").GetProperty("text").GetString());
    }

    [TestMethod]
    public async Task Output_EvictsSelectedRows_InvalidatesCopyExplicitly()
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter()).WithPresentation(muxer)
            .WithDimensions(20, 10).WithScrollback(3).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(Lines(0, 12)));
        await using var view = await muxer.CreateBrowserViewAsync();
        await MessageAsync(view, new { type = "viewport", requestId = 1, delta = -100 });
        var history = (await FrameAsync(view)).GetProperty("history");
        await SelectAsync(view, history, 0, 0, "line", 2);
        await MessageAsync(view, new { type = "copy", requestId = 3 });
        Assert.AreEqual("row00", (await FrameAsync(view)).GetProperty("history")
            .GetProperty("copy").GetProperty("text").GetString());
        producer.ApplyTokens(AnsiTokenizer.Tokenize(Lines(12, 5)));
        var after = (await FrameAsync(view)).GetProperty("history");
        Assert.AreEqual("invalidated", after.GetProperty("selection").GetProperty("status").GetString());
        Assert.AreEqual("invalidated", after.GetProperty("copy").GetProperty("status").GetString());
        Assert.AreEqual(JsonValueKind.Null, after.GetProperty("copy").GetProperty("text").ValueKind);
        await MessageAsync(view, new { type = "copy", requestId = 4 });
        Assert.AreEqual("invalidated", (await FrameAsync(view)).GetProperty("history")
            .GetProperty("copy").GetProperty("status").GetString());
    }

    [TestMethod]
    [DataRow("character", 2, 2, "\u754c", 1, 3)]
    [DataRow("character", 3, 3, "e\u0301", 3, 4)]
    [DataRow("rectangle", 2, 3, "\u754ce\u0301", 1, 4)]
    [DataRow("word", 3, 3, "A\u754ce\u0301", 0, 4)]
    public async Task Selection_WideAndCombiningCells_SelectsWholeGrapheme(
        string mode, int start, int end, string expected, int left, int right)
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter()).WithPresentation(muxer)
            .WithDimensions(20, 10).WithScrollback(20).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize("A\u754ce\u0301 text"));
        await using var view = await muxer.CreateBrowserViewAsync();
        var history = (await FrameAsync(view)).GetProperty("history");
        await SelectAsync(view, history, 0, start, mode, 1);
        await SelectAsync(view, history, 0, end, mode, 2, "extend");
        var selection = (await FrameAsync(view)).GetProperty("history").GetProperty("selection");
        Assert.AreEqual(expected, selection.GetProperty("text").GetString());
        Assert.AreEqual(left, selection.GetProperty("ranges")[0].GetProperty("startColumn").GetInt32());
        Assert.AreEqual(right, selection.GetProperty("ranges")[0].GetProperty("endColumn").GetInt32());
    }

    [TestMethod]
    public async Task Selection_LineMode_ExpandsAcrossSoftWrapsInBothDirections()
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter()).WithPresentation(muxer)
            .WithDimensions(20, 10).WithScrollback(20).Build();
        var logicalLine = new string('x', 45);
        producer.ApplyTokens(AnsiTokenizer.Tokenize(logicalLine + "\r\nnext"));
        await using var view = await muxer.CreateBrowserViewAsync();
        var history = (await FrameAsync(view)).GetProperty("history");
        await SelectAsync(view, history, 1, 4, "line", 1);
        var selection = (await FrameAsync(view)).GetProperty("history").GetProperty("selection");
        Assert.AreEqual(logicalLine, selection.GetProperty("text").GetString());
        Assert.AreEqual(3, selection.GetProperty("ranges").GetArrayLength());
        await SelectAsync(view, history, 3, 2, "line", 2, "extend");
        Assert.AreEqual(logicalLine + "\nnext", (await FrameAsync(view)).GetProperty("history")
            .GetProperty("selection").GetProperty("text").GetString());
    }

    [TestMethod]
    public async Task Selection_RectangleAndScrollExtension_PreservesColumnsAndPhysicalNewlines()
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter()).WithPresentation(muxer)
            .WithDimensions(20, 10).WithScrollback(100).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(string.Concat(Enumerable.Repeat(new string('a', 40) + "\r\n", 12))));
        await using var view = await muxer.CreateBrowserViewAsync();
        await MessageAsync(view, new { type = "viewport", requestId = 1, delta = -100 });
        var history = (await FrameAsync(view)).GetProperty("history");
        await SelectAsync(view, history, 0, 1, "rectangle", 2);
        await MessageAsync(view, new { type = "viewport", requestId = 3, delta = 2, extend = new { row = 0, column = 3 } });
        var extended = (await FrameAsync(view)).GetProperty("history");
        var selection = extended.GetProperty("selection");
        Assert.AreEqual("aaa\naaa\naaa", selection.GetProperty("text").GetString());
        Assert.AreEqual("rectangle", selection.GetProperty("mode").GetString());
        Assert.AreEqual(3L, selection.GetProperty("requestId").GetInt64());
        var text = selection.GetProperty("text").GetString();
        await MessageAsync(view, new { type = "viewport", requestId = 4, delta = 2 });
        Assert.AreEqual(text, (await FrameAsync(view)).GetProperty("history")
            .GetProperty("selection").GetProperty("text").GetString());
    }

    [TestMethod]
    [DataRow("\x1b[?1049h", "alternate")]
    [DataRow("\x1b[3J", "main")]
    [DataRow("\x1b[2J", "main")]
    [DataRow("\x1b[2;8r\x1b[8;1H\n", "main")]
    [DataRow("\x1b[2;1H\x1b[L", "main")]
    [DataRow("\x1b[2;1H\x1b[M", "main")]
    [DataRow("\x1b[T", "main")]
    [DataRow("\u001bc", "main")]
    public async Task StructuralMutation_ExistingSelection_ExplicitlyInvalidates(string mutation, string buffer)
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter()).WithPresentation(muxer)
            .WithDimensions(20, 10).WithScrollback(100).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(Lines(0, 15)));
        await using var view = await muxer.CreateBrowserViewAsync();
        var history = (await FrameAsync(view)).GetProperty("history");
        await SelectAsync(view, history, 2, 0, "line", 1);
        producer.ApplyTokens(AnsiTokenizer.Tokenize(mutation));
        var after = (await FrameAsync(view)).GetProperty("history");
        Assert.AreEqual("invalidated", after.GetProperty("selection").GetProperty("status").GetString());
        Assert.AreEqual(buffer, after.GetProperty("buffer").GetString());
        if (buffer == "alternate")
        {
            Assert.AreEqual(0, after.GetProperty("liveTop").GetInt32());
            producer.ApplyTokens(AnsiTokenizer.Tokenize(Lines(40, 20) + "\x1b[?1049l"));
            var main = (await FrameAsync(view)).GetProperty("history");
            Assert.AreEqual("main", main.GetProperty("buffer").GetString());
            Assert.AreEqual(6, main.GetProperty("liveTop").GetInt32());
        }
    }

    [TestMethod]
    public async Task Redraw_HistoricalViewportAndSelection_RemainPinnedWithoutSyntheticHistory()
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(muxer).WithDimensions(20, 10).WithScrollback(100).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(Lines(0, 30)));
        await using var view = await muxer.CreateBrowserViewAsync();
        await MessageAsync(view, new { type = "viewport", requestId = 1, delta = -100 });
        var history = (await FrameAsync(view)).GetProperty("history");
        await SelectAsync(view, history, 2, 0, "line", 2);
        producer.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[2J\x1b[Hredraw\x1b[K"));
        var after = (await FrameAsync(view)).GetProperty("history");
        Assert.AreEqual(history.GetProperty("rowIds")[0].GetString(), after.GetProperty("rowIds")[0].GetString());
        Assert.AreEqual(21, after.GetProperty("liveTop").GetInt32());
        Assert.AreEqual("valid", after.GetProperty("selection").GetProperty("status").GetString());
        Assert.AreEqual("row02", after.GetProperty("selection").GetProperty("text").GetString());
    }

    [TestMethod]
    public async Task Erase_InteriorSelectedRow_InvalidatesEntireSelection()
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(muxer).WithDimensions(20, 10).WithScrollback(100).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(Lines(0, 5)));
        await using var view = await muxer.CreateBrowserViewAsync();
        var history = (await FrameAsync(view)).GetProperty("history");
        await SelectAsync(view, history, 0, 0, "character", 1);
        await SelectAsync(view, history, 4, 4, "character", 2, "extend");
        producer.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3;1H\x1b[2K"));
        var selection = (await FrameAsync(view)).GetProperty("history").GetProperty("selection");
        Assert.AreEqual("invalidated", selection.GetProperty("status").GetString());
        Assert.AreEqual(JsonValueKind.Null, selection.GetProperty("text").ValueKind);
    }

    [TestMethod]
    [DataRow("hello", "\x1b[1;10H\x1b[K", 0)]
    [DataRow("hello    tail", "\x1b[1;10H\x1b[K", 0)]
    [DataRow("hello    tail", "\x1b[1;10H\x1b[3X", 0)]
    [DataRow("hello    tail", "\x1b[1;10H\x1b[P", 0)]
    [DataRow("hello    tail", "\x1b[1;10H\x1b[@", 0)]
    [DataRow("hello    tail", "\x1b[1;10;1;20$z", 0)]
    [DataRow("hello\r\nother", "\x1b[2;1;2;20$z", 0)]
    [DataRow("hello", "\x1b[3;5;4;10$z", 0)]
    [DataRow("hello    tail\r\nother", "\x1b[1;10H\x1b[J", 0)]
    [DataRow("tail hello", "\x1b[1;1H\x1b[1J", 5)]
    public async Task CellMutation_OutsideSelectionOrNoOp_PreservesRowIdentityAndSelectedText(
        string initial, string mutation, int column)
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(muxer).WithDimensions(20, 10).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(initial));
        await using var view = await muxer.CreateBrowserViewAsync();
        var before = (await FrameAsync(view)).GetProperty("history");
        await SelectAsync(view, before, 0, column, "word", 1);
        producer.ApplyTokens(AnsiTokenizer.Tokenize(mutation));
        await MessageAsync(view, new { type = "copy", requestId = 2 });
        var after = (await FrameAsync(view)).GetProperty("history");
        for (var row = 0; row < 10; row++)
            Assert.AreEqual(before.GetProperty("rowIds")[row].GetString(), after.GetProperty("rowIds")[row].GetString());
        Assert.AreEqual("valid", after.GetProperty("selection").GetProperty("status").GetString());
        Assert.AreEqual("hello", after.GetProperty("selection").GetProperty("text").GetString());
        Assert.AreEqual("hello", after.GetProperty("copy").GetProperty("text").GetString());
    }

    [TestMethod]
    [DataRow("\x1b[1;3H\x1b[K")]
    [DataRow("\x1b[1;3H\x1b[X")]
    [DataRow("\x1b[1;3H\x1b[P")]
    [DataRow("\x1b[1;3H\x1b[@")]
    [DataRow("\x1b[1;3;1;3$z")]
    [DataRow("\x1b[1;3H\x1b[J")]
    [DataRow("\x1b[1;3H\x1b[1J")]
    [DataRow("\x1b[2J")]
    public async Task CellMutation_ChangesSelectedText_InvalidatesWithoutReplacingRowIdentity(string mutation)
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(muxer).WithDimensions(20, 10).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize("hello world"));
        await using var view = await muxer.CreateBrowserViewAsync();
        var before = (await FrameAsync(view)).GetProperty("history");
        await SelectAsync(view, before, 0, 0, "word", 1);
        producer.ApplyTokens(AnsiTokenizer.Tokenize(mutation));
        await MessageAsync(view, new { type = "copy", requestId = 2 });
        var after = (await FrameAsync(view)).GetProperty("history");
        Assert.AreEqual(before.GetProperty("rowIds")[0].GetString(), after.GetProperty("rowIds")[0].GetString());
        Assert.AreEqual("invalidated", after.GetProperty("selection").GetProperty("status").GetString());
        Assert.AreEqual(JsonValueKind.Null, after.GetProperty("selection").GetProperty("text").ValueKind);
        Assert.AreEqual("invalidated", after.GetProperty("copy").GetProperty("status").GetString());
        Assert.AreEqual(JsonValueKind.Null, after.GetProperty("copy").GetProperty("text").ValueKind);
    }

    [TestMethod]
    [DataRow("\x1b[1;6r\x1b[6;1H\n")]
    [DataRow("\x1b[1;6r\x1b[T")]
    [DataRow("\x1b[1;6r\x1b[3;1H\x1b[L")]
    [DataRow("\x1b[1;6r\x1b[3;1H\x1b[M")]
    public async Task PartialRowMutation_UnchangedRowsBelowRegion_KeepIdentityAndSelection(string mutation)
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(muxer).WithDimensions(20, 10).WithScrollback(100).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(Lines(0, 20)));
        await using var view = await muxer.CreateBrowserViewAsync();
        var before = (await FrameAsync(view)).GetProperty("history");
        await SelectAsync(view, before, 8, 1, "line", 1);
        producer.ApplyTokens(AnsiTokenizer.Tokenize(mutation));
        var after = (await FrameAsync(view)).GetProperty("history");
        for (var row = 6; row < 10; row++)
            Assert.AreEqual(before.GetProperty("rowIds")[row].GetString(), after.GetProperty("rowIds")[row].GetString());
        Assert.AreEqual("valid", after.GetProperty("selection").GetProperty("status").GetString());
        Assert.AreEqual("row19", after.GetProperty("selection").GetProperty("text").GetString());
        Assert.AreEqual(8, after.GetProperty("selection").GetProperty("ranges")[0].GetProperty("row").GetInt32());
    }

    [TestMethod]
    [DataRow("hello", "jello", "copy")]
    [DataRow("hello", "jello", "extend")]
    [DataRow("e\u0301", "e\u0300", "copy")]
    [DataRow("\u754c", "\u8a9e", "copy")]
    public async Task Output_RewritesSelectedLiveText_InvalidatesBeforeCopyOrExtension(
        string initial, string replacement, string action)
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(muxer).WithDimensions(20, 10).WithScrollback(100).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(initial));
        await using var view = await muxer.CreateBrowserViewAsync();
        var before = (await FrameAsync(view)).GetProperty("history");
        await SelectAsync(view, before, 0, 0, "word", 1);
        Assert.AreEqual(initial, (await FrameAsync(view)).GetProperty("history")
            .GetProperty("selection").GetProperty("text").GetString());
        producer.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;1H" + replacement));
        if (action == "copy")
            await MessageAsync(view, new { type = "copy", requestId = 2 });
        else
            await SelectAsync(view, before, 0, 1, "word", 2, "extend");
        var after = (await FrameAsync(view)).GetProperty("history");
        Assert.AreEqual(before.GetProperty("generation").GetString(), after.GetProperty("generation").GetString());
        Assert.AreEqual(before.GetProperty("rowIds")[0].GetString(), after.GetProperty("rowIds")[0].GetString());
        Assert.AreEqual("invalidated", after.GetProperty("selection").GetProperty("status").GetString());
        Assert.AreEqual(JsonValueKind.Null, after.GetProperty("selection").GetProperty("text").ValueKind);
        Assert.AreEqual(0, after.GetProperty("selection").GetProperty("ranges").GetArrayLength());
        await MessageAsync(view, new { type = "copy", requestId = 3 });
        var copy = (await FrameAsync(view)).GetProperty("history").GetProperty("copy");
        Assert.AreEqual("invalidated", copy.GetProperty("status").GetString());
        Assert.AreEqual(JsonValueKind.Null, copy.GetProperty("text").ValueKind);
    }

    [TestMethod]
    public async Task Output_WritesOutsideSelectedSpan_PreservesIntendedText()
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(muxer).WithDimensions(20, 10).WithScrollback(100).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize("hello world"));
        await using var view = await muxer.CreateBrowserViewAsync();
        var before = (await FrameAsync(view)).GetProperty("history");
        await SelectAsync(view, before, 0, 0, "word", 1);
        producer.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;10HX"));
        await MessageAsync(view, new { type = "copy", requestId = 2 });
        var after = (await FrameAsync(view)).GetProperty("history");
        Assert.AreEqual("valid", after.GetProperty("selection").GetProperty("status").GetString());
        Assert.AreEqual("hello", after.GetProperty("selection").GetProperty("text").GetString());
        Assert.AreEqual("hello", after.GetProperty("copy").GetProperty("text").GetString());
    }

    [TestMethod]
    public async Task Selection_WordDragAcrossWrapAndReverseExtension_PreservesWholeWordAnchor()
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(muxer).WithDimensions(20, 10).WithScrollback(100).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize("first " + new string('b', 24) + " last"));
        await using var view = await muxer.CreateBrowserViewAsync();
        var history = (await FrameAsync(view)).GetProperty("history");
        await SelectAsync(view, history, 1, 2, "word", 1);
        Assert.AreEqual(new string('b', 24), (await FrameAsync(view)).GetProperty("history")
            .GetProperty("selection").GetProperty("text").GetString());
        await SelectAsync(view, history, 0, 2, "word", 2, "extend");
        var reverse = (await FrameAsync(view)).GetProperty("history").GetProperty("selection");
        Assert.AreEqual("first " + new string('b', 24), reverse.GetProperty("text").GetString());
        await SelectAsync(view, history, 1, 12, "word", 3, "extend");
        Assert.AreEqual(new string('b', 24) + " last", (await FrameAsync(view)).GetProperty("history")
            .GetProperty("selection").GetProperty("text").GetString());
    }

    [TestMethod]
    [DataRow("""{"type":"key","key":"c","ctrl":true}""", "\u0003")]
    [DataRow("""{"type":"paste","text":"abc"}""", "\u001b[200~abc\u001b[201~")]
    public async Task Input_SecondaryWithSelection_ClearsSelectionAndReturnsLive(string message, string expected)
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var producer = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            Width = 20, Height = 10, ScrollbackCapacity = 20,
            PresentationAdapter = muxer, WorkloadAdapter = workload, RunCallback = _ => Task.FromResult(0)
        });
        producer.ApplyTokens(AnsiTokenizer.Tokenize(Lines(0, 15) + "\x1b[?2004h"));
        await using var view = await muxer.CreateBrowserViewAsync();
        await MessageAsync(view, new { type = "viewport", requestId = 1, delta = -3 });
        var history = (await FrameAsync(view)).GetProperty("history");
        await SelectAsync(view, history, 0, 1, "line", 2);
        await view.HandleMessageAsync(Encoding.UTF8.GetBytes(message));
        Assert.AreEqual(expected, Encoding.UTF8.GetString((await muxer.ReadInputAsync()).Span));
        var after = (await FrameAsync(view)).GetProperty("history");
        Assert.IsTrue(after.GetProperty("following").GetBoolean());
        Assert.AreEqual("none", after.GetProperty("selection").GetProperty("status").GetString());
        Assert.IsNull(muxer.PrimaryPeerId);
    }

    [TestMethod]
    public async Task PeerAuthority_LocalInspectionNeverTakesPrimary_ClosingPrimaryLeavesNone()
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var producer = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            Width = 20, Height = 10, ScrollbackCapacity = 20,
            PresentationAdapter = muxer, WorkloadAdapter = workload, RunCallback = _ => Task.FromResult(0)
        });
        producer.ApplyTokens(AnsiTokenizer.Tokenize(Lines(0, 15)));
        await using var first = await muxer.CreateBrowserViewAsync("one");
        await using var second = await muxer.CreateBrowserViewAsync("two");
        await FrameAsync(first);
        var initial = await FrameAsync(second);
        await MessageAsync(second, new { type = "viewport", requestId = 1, delta = -2 });
        await MessageAsync(second, new { type = "resize", columns = 40, rows = 12 });
        Assert.IsNull(muxer.PrimaryPeerId);
        Assert.AreEqual(20, producer.Width);
        await MessageAsync(first, new { type = "requestPrimary", columns = 20, rows = 10 });
        var role = await FrameAsync(second);
        Assert.IsFalse(role.GetProperty("peer").GetProperty("isPrimary").GetBoolean());
        Assert.IsNotNull(role.GetProperty("peer").GetProperty("primaryId").GetString());
        await MessageAsync(second, new { type = "input", text = "secondary-input" });
        Assert.AreEqual("secondary-input", Encoding.UTF8.GetString(
            (await muxer.ReadInputAsync(TestContext.Current.CancellationToken)).Span));
        var typed = (await FrameAsync(second)).GetProperty("history");
        Assert.IsTrue(typed.GetProperty("following").GetBoolean());
        Assert.AreEqual("none", typed.GetProperty("selection").GetProperty("status").GetString());
        await first.DisposeAsync();
        var disconnected = await FrameAsync(second);
        Assert.IsNull(disconnected.GetProperty("peer").GetProperty("primaryId").GetString());
        Assert.AreEqual(1, muxer.ClientCount);
        Assert.AreEqual(initial.GetProperty("peer").GetProperty("id").GetString(),
            disconnected.GetProperty("peer").GetProperty("id").GetString());
        await MessageAsync(second, new { type = "requestPrimary", columns = 30, rows = 12 });
        Assert.AreEqual(30, producer.Width);
        Assert.AreEqual(12, producer.Height);
    }

    [TestMethod]
    [DataRow("resize")]
    [DataRow("requestPrimary")]
    public async Task PeerAuthority_CancelledCallerWhileStateGateHeld_DoesNotApplyRequest(string type)
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(muxer).WithDimensions(20, 10).Build();
        await using var view = await muxer.CreateBrowserViewAsync();
        if (type == "resize")
            await MessageAsync(view, new { type = "requestPrimary", columns = 20, rows = 10 });
        using var cancellation = new CancellationTokenSource();
        await producer.Hmp1OutputStateLock.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            var pending = view.HandleMessageAsync(
                JsonSerializer.SerializeToUtf8Bytes(new { type, columns = 40, rows = 12 }), cancellation.Token);
            Assert.IsFalse(pending.IsCompleted);
            await cancellation.CancelAsync();
            await Assert.ThrowsAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(20, producer.Width);
            Assert.AreEqual(10, producer.Height);
            await view.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual(0, muxer.ClientCount);
            Assert.IsNull(muxer.PrimaryPeerId);
        }
        finally
        {
            producer.Hmp1OutputStateLock.Release();
        }
        await using var later = await muxer.CreateBrowserViewAsync();
        var frame = await FrameAsync(later);
        Assert.AreEqual(20, frame.GetProperty("columns").GetInt32());
        Assert.AreEqual(10, frame.GetProperty("rows").GetInt32());
        Assert.IsNull(frame.GetProperty("peer").GetProperty("primaryId").GetString());
    }

    [TestMethod]
    [DataRow("resize")]
    [DataRow("requestPrimary")]
    public async Task PeerAuthority_CancelledCallerWhileObserverBlocked_ReleasesRequestAndPeer(string type)
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(muxer).WithDimensions(20, 10).Build();
        await using var view = await muxer.CreateBrowserViewAsync();
        if (type == "resize")
            await MessageAsync(view, new { type = "requestPrimary", columns = 20, rows = 10 });
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        muxer.OnResized = async (_, token) =>
        {
            entered.TrySetResult(token);
            await release.Task;
            finished.TrySetResult();
        };
        using var cancellation = new CancellationTokenSource();
        try
        {
            var pending = view.HandleMessageAsync(
                JsonSerializer.SerializeToUtf8Bytes(new { type, columns = 40, rows = 12 }), cancellation.Token);
            var callbackToken = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await cancellation.CancelAsync();
            await Assert.ThrowsAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.IsTrue(callbackToken.IsCancellationRequested);
            Assert.AreEqual(40, producer.Width, "Cancellation after commit does not roll back the authority transaction.");
            await view.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual(0, muxer.ClientCount);
            Assert.IsNull(muxer.PrimaryPeerId);
        }
        finally
        {
            release.TrySetResult();
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestMethod]
    public async Task Resize_StaleAnchorAndSelectionRequests_DoNotSelectDifferentText()
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter()).WithPresentation(muxer)
            .WithDimensions(20, 10).WithScrollback(100).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize("before"));
        await using var view = await muxer.CreateBrowserViewAsync();
        var history = (await FrameAsync(view)).GetProperty("history");
        await SelectAsync(view, history, 0, 0, "word", 1);
        producer.Resize(30, 12);
        var resized = (await FrameAsync(view)).GetProperty("history");
        Assert.AreEqual("invalidated", resized.GetProperty("selection").GetProperty("status").GetString());
        await SelectAsync(view, history, 0, 0, "word", 2);
        Assert.AreEqual("invalidated", (await FrameAsync(view)).GetProperty("history")
            .GetProperty("selection").GetProperty("status").GetString());
        await SelectAsync(view, resized, 0, 0, "word", 4);
        await MessageAsync(view, new { type = "selection", requestId = 3, action = "clear" });
        var selected = (await FrameAsync(view)).GetProperty("history").GetProperty("selection");
        Assert.AreEqual("valid", selected.GetProperty("status").GetString());
        Assert.AreEqual(4L, selected.GetProperty("requestId").GetInt64());
    }

    [TestMethod]
    [DataRow(10)]
    [DataRow(30)]
    public async Task Resize_NewHistoricalLineSelection_UsesOriginalRowWidthsAndSoftWraps(int width)
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(muxer).WithDimensions(20, 10).WithScrollback(100).Build();
        var line = new string('x', 45);
        producer.ApplyTokens(AnsiTokenizer.Tokenize(line + "\r\n" + Lines(0, 20)));
        producer.Resize(width, 10);
        await using var view = await muxer.CreateBrowserViewAsync();
        await MessageAsync(view, new { type = "viewport", requestId = 1, delta = -100 });
        var history = (await FrameAsync(view)).GetProperty("history");
        await SelectAsync(view, history, 1, 1, "line", 2);
        var selection = (await FrameAsync(view)).GetProperty("history").GetProperty("selection");
        Assert.AreEqual(line, selection.GetProperty("text").GetString());
        Assert.IsTrue(selection.GetProperty("ranges").EnumerateArray()
            .All(range => range.GetProperty("endColumn").GetInt32() <= width));
    }

    [TestMethod]
    public async Task Copy_StaleSelectionIdentity_RejectsWithoutClearingNewSelection()
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(muxer).WithDimensions(20, 10).WithScrollback(100).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize("one two"));
        await using var view = await muxer.CreateBrowserViewAsync();
        var history = (await FrameAsync(view)).GetProperty("history");
        await SelectAsync(view, history, 0, 0, "word", 1);
        await SelectAsync(view, history, 0, 4, "word", 2);
        await MessageAsync(view, new { type = "copy", requestId = 3, selectionRequestId = 1,
            generation = history.GetProperty("generation").GetString() });
        var after = (await FrameAsync(view)).GetProperty("history");
        Assert.AreEqual("invalidated", after.GetProperty("copy").GetProperty("status").GetString());
        Assert.AreEqual(JsonValueKind.Null, after.GetProperty("copy").GetProperty("text").ValueKind);
        Assert.AreEqual("valid", after.GetProperty("selection").GetProperty("status").GetString());
        Assert.AreEqual("two", after.GetProperty("selection").GetProperty("text").GetString());
        await MessageAsync(view, new { type = "copy", requestId = 4, selectionRequestId = 2,
            generation = history.GetProperty("generation").GetString() });
        var copied = (await FrameAsync(view)).GetProperty("history");
        Assert.AreEqual("valid", copied.GetProperty("copy").GetProperty("status").GetString());
        Assert.AreEqual("two", copied.GetProperty("copy").GetProperty("text").GetString());
        Assert.AreEqual("two", copied.GetProperty("selection").GetProperty("text").GetString());
    }

    [TestMethod]
    public async Task Graphics_HistoricalViewOmitsImages_ReturningLivePreservesAnimationAndSixel()
    {
        var time = new FakeTimeProvider();
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter()).WithPresentation(muxer)
            .WithDimensions(20, 10).WithScrollback(100).WithTimeProvider(time).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(Lines(0, 15) +
            "\x1b[1;1H\x1bPq#1;2;100;0;0#1~\x1b\\" +
            KgpTestHelper.BuildCommand("a=T,f=32,s=1,v=1,i=7,p=11,C=1,q=2", [1, 0, 0, 255]) +
            KgpTestHelper.BuildCommand("a=f,f=32,s=1,v=1,i=7,X=1,z=100,q=2", [2, 0, 0, 255]) +
            KgpTestHelper.BuildCommand("a=a,i=7,r=1,z=100,c=1,s=3,v=1,q=2")));
        await using var view = await muxer.CreateBrowserViewAsync();
        var initial = await FrameAsync(view);
        Assert.AreEqual(2, initial.GetProperty("placements").GetArrayLength());
        time.Advance(TimeSpan.FromMilliseconds(100));
        var animated = await FrameAsync(view);
        Assert.IsGreaterThan(0, animated.GetProperty("images").GetArrayLength());
        await MessageAsync(view, new { type = "viewport", requestId = 1, delta = -1 });
        var historical = await FrameAsync(view);
        Assert.AreEqual(0, historical.GetProperty("placements").GetArrayLength());
        Assert.IsFalse(historical.GetProperty("cursor").GetProperty("visible").GetBoolean());
        await MessageAsync(view, new { type = "viewport", requestId = 2, live = true });
        var live = await FrameAsync(view);
        Assert.AreEqual(2, live.GetProperty("placements").GetArrayLength());
        Assert.AreEqual(10, live.GetProperty("cellWidth").GetInt32());
        Assert.AreEqual(20, live.GetProperty("cellHeight").GetInt32());
        Assert.AreEqual(initial.GetProperty("placements")[0].GetProperty("width").GetDouble(),
            live.GetProperty("placements")[0].GetProperty("width").GetDouble());
    }

    private static string Lines(int first, int count)
        => string.Concat(Enumerable.Range(first, count).Select(index => $"row{index:00}\r\n"));

    private static Task MessageAsync(Hwt1PresentationAdapter view, object command)
        => view.HandleMessageAsync(JsonSerializer.SerializeToUtf8Bytes(command), TestContext.Current.CancellationToken);

    private static Task SelectAsync(Hwt1PresentationAdapter view, JsonElement history,
        int row, int column, string mode, int requestId, string action = "start")
        => MessageAsync(view, new
        {
            type = "selection", requestId, action, mode, column,
            generation = history.GetProperty("generation").GetString(),
            rowId = history.GetProperty("rowIds")[row].GetString()
        });

    private static async Task<JsonElement> FrameAsync(Hwt1PresentationAdapter view)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var frame = await view.ReadFrameAsync(timeout.Token);
        var length = BinaryPrimitives.ReadInt32LittleEndian(frame.Span[4..]);
        using var document = JsonDocument.Parse(frame.Slice(8, length));
        var metadata = document.RootElement.Clone();
        await MessageAsync(view, new { type = "ack", revision = metadata.GetProperty("revision").GetUInt32() });
        return metadata;
    }
}
