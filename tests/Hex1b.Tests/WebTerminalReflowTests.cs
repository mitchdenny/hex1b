using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Hex1b.Automation;
using Hex1b.Reflow;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class WebTerminalReflowTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Resize_ProducerAndBrowser_PreserveLogicalLinesAndHistory(bool direct)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var muxer = new Hmp1PresentationAdapter(80, 10).WithReflow(GhosttyReflowStrategy.Instance);
        await using var standalone = new Hwt1PresentationAdapter(80, 10).WithReflow(GhosttyReflowStrategy.Instance);
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(direct ? standalone : muxer).WithDimensions(80, 10).WithScrollback(1000).Build();
        await using var shared = direct ? null : await muxer.CreateBrowserViewAsync("primary");
        var view = shared ?? standalone;
        await FrameAsync(view);
        await MessageAsync(view, new { type = "requestPrimary", columns = 80, rows = 10 });
        var lines = Enumerable.Range(0, 30).Select(i =>
            $"{i:D2}:" + new string((char)('A' + i % 26), i % 2 == 0 ? 63 : 107) + "-END").ToArray();
        workload.Write(string.Join("\r\n", lines) + "\r\n$ ");
        using var ready = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.GetLineTrimmed(9) == "$", TimeSpan.FromSeconds(5), "all output consumed")
            .Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);
        var expected = lines.Append("$").ToArray();

        foreach (var width in new[] { 20, 37, 80, 23, 100, 80 })
        {
            await MessageAsync(view, new { type = "resize", columns = width, rows = 10 });
            var frame = await FrameAsync(view);
            Assert.AreEqual(width, frame.GetProperty("columns").GetInt32());
            Assert.AreEqual(width, terminal.Width);
            using var snapshot = terminal.CreateSnapshot(1000);
            TestSeq.AreEqual(expected, LogicalLines(snapshot));
        }
    }

    [TestMethod]
    public async Task Resize_SecondaryCannotResizeOrChangeProducerReflow()
    {
        await using var muxer = new Hmp1PresentationAdapter(80, 10).WithReflow(GhosttyReflowStrategy.Instance);
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(muxer).WithDimensions(80, 10).WithScrollback(100).Build();
        await using var primary = await muxer.CreateBrowserViewAsync("primary");
        await using var secondary = await muxer.CreateBrowserViewAsync("secondary");
        secondary.WithReflow(NoReflowStrategy.Instance);
        await MessageAsync(primary, new { type = "requestPrimary", columns = 80, rows = 10 });
        var text = new string('x', 65) + "-END";
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(text + "\r\n$ "));
        await FrameAsync(primary);
        await FrameAsync(secondary);

        await MessageAsync(secondary, new { type = "resize", columns = 20, rows = 10 });
        Assert.AreEqual(80, terminal.Width);
        await MessageAsync(primary, new { type = "resize", columns = 20, rows = 10 });
        Assert.AreEqual(20, (await FrameAsync(primary)).GetProperty("columns").GetInt32());
        Assert.AreEqual(20, (await FrameAsync(secondary)).GetProperty("columns").GetInt32());
        using var snapshot = terminal.CreateSnapshot(100);
        TestSeq.AreEqual(new[] { text, "$" }, LogicalLines(snapshot));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Resize_UnconfiguredAdapters_KeepCropDefault(bool direct)
    {
        IHex1bTerminalPresentationAdapter adapter = direct
            ? new Hwt1PresentationAdapter(80, 10) : new Hmp1PresentationAdapter(80, 10);
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(adapter).WithDimensions(80, 10).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("ABCDEFGHIJKLMNOPQRSTUVWXYZ-END\r\n$ "));
        terminal.Resize(20, 10);
        terminal.Resize(80, 10);
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("ABCDEFGHIJKLMNOPQRST", snapshot.GetLineTrimmed(0));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WithReflow_Builder_ConfiguresWebAdapters(bool direct)
    {
        IHex1bTerminalPresentationAdapter adapter = direct
            ? new Hwt1PresentationAdapter(80, 10) : new Hmp1PresentationAdapter(80, 10);
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(adapter).WithDimensions(80, 10).WithReflow(GhosttyReflowStrategy.Instance).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("ABCDEFGHIJKLMNOPQRSTUVWXYZ-END\r\n$ "));
        terminal.Resize(20, 10);
        terminal.Resize(80, 10);
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("ABCDEFGHIJKLMNOPQRSTUVWXYZ-END", snapshot.GetLineTrimmed(0));
    }

    [TestMethod]
    [DataRow(20)]
    [DataRow(80)]
    public void Resize_WideCombiningStyledText_PreservesCellsAndCursor(int initialWidth)
    {
        var adapter = new Hmp1PresentationAdapter(initialWidth, 10).WithReflow(GhosttyReflowStrategy.Instance);
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(adapter).WithDimensions(initialWidth, 10).WithScrollback(100).Build();
        var text = new string('a', 19) + "\u754ce\u0301" + new string('b', 18) + "\u754c-END";
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;31m" + text + "\x1b[0m"));
        terminal.Resize(80, 10);
        using var before = terminal.CreateSnapshot();
        Assert.AreEqual(text, before.GetLineTrimmed(0));
        foreach (var width in new[] { 20, 21, 37, 80, 20, 80 })
        {
            terminal.Resize(width, 10);
            using var resized = terminal.CreateSnapshot();
            TestSeq.AreEqual(new[] { text }, LogicalLines(resized));
        }
        using var after = terminal.CreateSnapshot();
        Assert.AreEqual(text, after.GetLineTrimmed(0));
        Assert.AreEqual(before.CursorX, after.CursorX);
        Assert.AreEqual(before.CursorY, after.CursorY);
        for (var x = 0; x < before.Width; x++)
        {
            Assert.AreEqual(before.GetCell(x, 0).Character, after.GetCell(x, 0).Character);
            Assert.AreEqual(before.GetCell(x, 0).Foreground, after.GetCell(x, 0).Foreground);
            Assert.AreEqual(before.GetCell(x, 0).Attributes, after.GetCell(x, 0).Attributes);
        }
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("!"));
        using var edited = terminal.CreateSnapshot();
        Assert.AreEqual(text + "!", edited.GetLineTrimmed(0));
    }

    [TestMethod]
    public void Resize_SavedCursor_FollowsEditablePrompt()
    {
        var adapter = new Hmp1PresentationAdapter(80, 10).WithReflow(GhosttyReflowStrategy.Instance);
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(adapter).WithDimensions(80, 10).Build();
        var prefix = "$ " + new string('x', 39);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(prefix + "\x1b" + "7tail"));
        terminal.Resize(20, 10);
        terminal.Resize(80, 10);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b" + "8EDIT"));
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(prefix + "EDIT", snapshot.GetLineTrimmed(0));
    }

    [TestMethod]
    [DataRow(20, 40)]
    [DataRow(40, 20)]
    [DataRow(20, 20)]
    public void Resize_PendingWrap_AppendsWithoutOverwriting(int oldWidth, int newWidth)
    {
        var adapter = new Hmp1PresentationAdapter(oldWidth, 10).WithReflow(GhosttyReflowStrategy.Instance);
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(adapter).WithDimensions(oldWidth, 10).Build();
        var text = new string('a', oldWidth);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(text));
        terminal.Resize(newWidth, 10);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("!"));
        using var snapshot = terminal.CreateSnapshot();
        TestSeq.AreEqual(new[] { text + "!" }, LogicalLines(snapshot));
    }

    [TestMethod]
    public void Resize_CursorAfterBlankCells_PreservesInsertionOffset()
    {
        var adapter = new Hmp1PresentationAdapter(80, 10).WithReflow(GhosttyReflowStrategy.Instance);
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(adapter).WithDimensions(80, 10).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("text\x1b[1;32H\x1b" + "7"));
        terminal.Resize(20, 10);
        Assert.AreEqual(11, terminal.CursorX);
        Assert.AreEqual(1, terminal.CursorY);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b" + "8!"));
        terminal.Resize(80, 10);
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("text" + new string(' ', 27) + "!", snapshot.GetLineTrimmed(0));
    }

    [TestMethod]
    public void Resize_WhileAlternateActive_ReflowsMainOnReturnWithoutReflowingAlternate()
    {
        var adapter = new Hmp1PresentationAdapter(80, 10).WithReflow(GhosttyReflowStrategy.Instance);
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(adapter).WithDimensions(80, 10).WithScrollback(100).Build();
        var lines = Enumerable.Range(0, 15).Select(i => $"{i:D2}:" + new string('x', 63) + "-END").ToArray();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(string.Join("\r\n", lines) + "\r\n$ \x1b[?1049h\x1b[H" +
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ"));
        var historyCount = terminal.ScrollbackCount;
        terminal.Resize(20, 10);
        using (var alternate = terminal.CreateSnapshot())
        {
            Assert.IsTrue(alternate.InAlternateScreen);
            Assert.AreEqual("ABCDEFGHIJKLMNOPQRST", alternate.GetLineTrimmed(0));
            Assert.AreEqual("", alternate.GetLineTrimmed(1));
        }
        Assert.AreEqual(historyCount, terminal.ScrollbackCount);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049l"));
        terminal.Resize(80, 10);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("edited"));
        using var restored = terminal.CreateSnapshot(100);
        TestSeq.AreEqual(lines.Append("$ edited").ToArray(), LogicalLines(restored));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Resize_GraphicsAndHyperlinks_KeepProducerOwnershipAndReflowAnchors(bool alternate)
    {
        await using var muxer = new Hmp1PresentationAdapter(80, 10).WithReflow(GhosttyReflowStrategy.Instance);
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(muxer).WithDimensions(80, 10).WithScrollback(100).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(new string('a', 30) +
            KgpTestHelper.BuildCommand("a=T,f=32,s=1,v=1,i=529,C=1,q=2", [255, 0, 0, 255]) +
            "\x1b]8;;https://example.test/reflow\x1b\\linked\x1b]8;;\x1b\\\r\n$ "));
        await using var view = await muxer.CreateBrowserViewAsync();
        await FrameAsync(view);
        if (alternate)
            terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049h"));
        await MessageAsync(view, new { type = "requestPrimary", columns = 20, rows = 10 });
        if (alternate)
        {
            await FrameAsync(view);
            terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049l"));
        }
        var narrow = await FrameAsync(view);
        Assert.AreEqual(1, narrow.GetProperty("placements").GetArrayLength());
        using (var snapshot = terminal.CreateSnapshot())
        {
            var placement = TestSeq.Single(snapshot.KgpPlacements);
            Assert.AreEqual(1, placement.Row);
            Assert.AreEqual(10, placement.Column);
            Assert.AreEqual("https://example.test/reflow", snapshot.GetCell(10, 1).HyperlinkData?.Uri);
        }
        await MessageAsync(view, new { type = "resize", columns = 80, rows = 10 });
        await FrameAsync(view);
        using var restored = terminal.CreateSnapshot();
        var original = TestSeq.Single(restored.KgpPlacements);
        Assert.AreEqual(0, original.Row);
        Assert.AreEqual(30, original.Column);
        Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);
        Assert.AreEqual("https://example.test/reflow", restored.GetCell(30, 0).HyperlinkData?.Uri);
    }

    [TestMethod]
    public async Task Resize_LogicalWideSelection_CopiesExactTextThenInvalidates()
    {
        await using var muxer = new Hmp1PresentationAdapter(80, 10).WithReflow(GhosttyReflowStrategy.Instance);
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(muxer).WithDimensions(80, 10).WithScrollback(100).Build();
        var text = new string('a', 19) + "\u754ce\u0301-END";
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(text + "\r\n$ "));
        await using var view = await muxer.CreateBrowserViewAsync();
        await MessageAsync(view, new { type = "requestPrimary", columns = 20, rows = 10 });
        var history = (await FrameAsync(view)).GetProperty("history");
        var select = new
        {
            type = "selection", action = "start", mode = "line", requestId = 1, column = 0,
            generation = history.GetProperty("generation").GetString(),
            rowId = history.GetProperty("rowIds")[0].GetString()
        };
        await MessageAsync(view, select);
        var selection = (await FrameAsync(view)).GetProperty("history").GetProperty("selection");
        Assert.AreEqual("valid", selection.GetProperty("status").GetString());
        Assert.AreEqual(text, selection.GetProperty("text").GetString());
        await MessageAsync(view, new { type = "resize", columns = 80, rows = 10 });
        var resized = (await FrameAsync(view)).GetProperty("history");
        Assert.AreEqual("invalidated", resized.GetProperty("selection").GetProperty("status").GetString());
        Assert.AreNotEqual(history.GetProperty("generation").GetString(), resized.GetProperty("generation").GetString());
        await MessageAsync(view, select with { requestId = 2 });
        Assert.AreEqual("invalidated", (await FrameAsync(view)).GetProperty("history")
            .GetProperty("selection").GetProperty("status").GetString());
    }

    [TestMethod]
    public void Resize_RetentionLimit_DiscardsOnlyOldestRows()
    {
        var adapter = new Hmp1PresentationAdapter(80, 10).WithReflow(GhosttyReflowStrategy.Instance);
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(adapter).WithDimensions(80, 10).WithScrollback(3).Build();
        var text = string.Concat(Enumerable.Range(0, 600).Select(i => (char)('A' + i % 26)));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(text));
        terminal.Resize(20, 10);
        Assert.AreEqual(3, terminal.ScrollbackCount);
        using var narrow = terminal.CreateSnapshot(3);
        TestSeq.AreEqual(new[] { text[^260..] }, LogicalLines(narrow));
        terminal.Resize(80, 10);
        using var wide = terminal.CreateSnapshot(3);
        TestSeq.AreEqual(new[] { text[^260..] }, LogicalLines(wide));
    }

    [TestMethod]
    public void Resize_SavedPendingWrap_RestoresInsertionPointAfterGrow()
    {
        var adapter = new Hmp1PresentationAdapter(20, 10).WithReflow(GhosttyReflowStrategy.Instance);
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(adapter).WithDimensions(20, 10).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(new string('a', 20) + "\x1b" + "7\r\nprompt"));
        terminal.Resize(80, 10);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b" + "8!"));
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(new string('a', 20) + "!", snapshot.GetLineTrimmed(0));
        Assert.AreEqual("prompt", snapshot.GetLineTrimmed(1));
    }

    [TestMethod]
    public void Resize_OneColumnNativeGrid_DoesNotLoopOnUnplaceableWideGlyph()
    {
        var adapter = new Hmp1PresentationAdapter(20, 10).WithReflow(GhosttyReflowStrategy.Instance);
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(adapter).WithDimensions(20, 10).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("A\u754cB"));
        terminal.Resize(1, 10);
        using var snapshot = terminal.CreateSnapshot();
        TestSeq.AreEqual(new[] { "AB" }, LogicalLines(snapshot));
    }

    private static string[] LogicalLines(Hex1bTerminalSnapshot snapshot)
    {
        var lines = new List<string>();
        var line = new StringBuilder();
        for (var y = 0; y < snapshot.Height; y++)
        {
            for (var x = 0; x < snapshot.Width; x++)
            {
                var cell = snapshot.GetCell(x, y);
                if (!cell.IsWideWrapPadding)
                    line.Append(cell.Character);
            }
            if (!snapshot.GetCell(snapshot.Width - 1, y).IsSoftWrap)
            {
                lines.Add(line.ToString().TrimEnd());
                line.Clear();
            }
        }
        if (line.Length > 0)
            lines.Add(line.ToString().TrimEnd());
        while (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);
        return lines.ToArray();
    }

    private static Task MessageAsync(Hwt1PresentationAdapter view, object message)
        => view.HandleMessageAsync(JsonSerializer.SerializeToUtf8Bytes(message), TestContext.Current.CancellationToken);

    private static async Task<JsonElement> FrameAsync(Hwt1PresentationAdapter view)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var bytes = await view.ReadFrameAsync(timeout.Token);
        using var document = JsonDocument.Parse(bytes.Slice(8, BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[4..])));
        var frame = document.RootElement.Clone();
        await MessageAsync(view, new { type = "ack", revision = frame.GetProperty("revision").GetUInt32() });
        return frame;
    }
}
