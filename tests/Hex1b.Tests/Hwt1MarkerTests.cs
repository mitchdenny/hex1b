using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Hex1b.Reflow;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class Hwt1MarkerTests
{
    [TestMethod]
    [DataRow(true, 8)]
    [DataRow(true, 20)]
    [DataRow(false, 8)]
    [DataRow(false, 20)]
    public async Task Markers_OneColumnReflow_ExpiresDiscardedGlyphsAndMapsSurvivingText(bool preserveCursorRow, int width)
    {
        await using var adapter = new Hwt1PresentationAdapter(width, 10).WithReflow(
            preserveCursorRow ? GhosttyReflowStrategy.Instance : AlacrittyReflowStrategy.Instance);
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(adapter).WithDimensions(width, 10).WithScrollback(100).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b]133;A\u0007界\x1b]133;C\u0007AB界\x1b]133;C\u0007CD\x1b]133;D;0\u0007\r\n"));
        var view = new Hwt1ViewState();
        var original = Capture(terminal, view);
        Assert.AreEqual(4, original.Markers.Length);
        AssertMarkerText(terminal, original.Markers[0], "界");
        AssertMarkerText(terminal, original.Markers[1], "A");
        AssertMarkerText(terminal, original.Markers[2], "C");
        Assert.AreEqual(8, original.Markers[3].Column);
        Send(terminal, view, new { type = "marker", action = "add", requestId = 1,
            id = $"custom:{Guid.NewGuid():D}", generation = original.Generation,
            rowId = original.RowIds[0], column = 1 });
        Send(terminal, view, new { type = "marker", action = "add", requestId = 2,
            id = $"custom:{Guid.NewGuid():D}", generation = original.Generation,
            rowId = original.RowIds[0], column = 2 });

        terminal.Resize(1, 10);
        var narrow = Capture(terminal, view);
        Assert.AreEqual(4, narrow.Markers.Length);
        AssertMarkerText(terminal, narrow.Markers[0], "A");
        AssertMarkerText(terminal, narrow.Markers[1], "C");
        Assert.AreEqual(3, narrow.Markers[2].Row);
        Assert.AreEqual(1, narrow.Markers[2].Column);
        AssertMarkerText(terminal, narrow.Markers[3], "A");
        Send(terminal, view, new { type = "marker", action = "jump", requestId = 3, id = original.Markers[0].Id });
        Assert.AreEqual("unknown-marker", Capture(terminal, view).MarkerResult!.Error);

        terminal.Resize(width, 10);
        var restored = Capture(terminal, view);
        Assert.AreEqual(4, restored.Markers.Length);
        AssertMarkerText(terminal, restored.Markers[0], "A");
        AssertMarkerText(terminal, restored.Markers[1], "C");
        Assert.AreEqual(0, restored.Markers[2].Row);
        Assert.AreEqual(4, restored.Markers[2].Column);
        AssertMarkerText(terminal, restored.Markers[3], "A");
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("ABCD", snapshot.GetLineTrimmed(0));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Markers_OneColumnReflowOfOnlyWideGlyph_PreservesEndBoundaryNotDiscardedGlyph(bool preserveCursorRow)
    {
        await using var adapter = new Hwt1PresentationAdapter(20, 10).WithReflow(
            preserveCursorRow ? GhosttyReflowStrategy.Instance : AlacrittyReflowStrategy.Instance);
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(adapter).WithDimensions(20, 10).WithScrollback(100).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b]133;A\u0007界\x1b]133;C\u0007\r\nNEXT"));
        var view = new Hwt1ViewState();
        terminal.Resize(1, 10);
        var narrow = Capture(terminal, view);
        Assert.AreEqual(1, narrow.Markers.Length);
        Assert.AreEqual(0, narrow.Markers[0].Row);
        Assert.AreEqual(0, narrow.Markers[0].Column);
        terminal.Resize(20, 10);
        var restored = Capture(terminal, view);
        Assert.AreEqual(1, restored.Markers.Length);
        Assert.AreEqual(0, restored.Markers[0].Row);
        Assert.AreEqual(0, restored.Markers[0].Column);
    }

    [TestMethod]
    public async Task Markers_SameRow_AreDistinctAndPreserveLegacyMarkAndDetails()
    {
        await using var adapter = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(adapter).WithDimensions(20, 10).WithScrollback(100).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;A\u0007abc\x1b]133;C;cmdline_url=secret\u0007"));
        var frame = await FrameAsync(adapter);
        var markers = frame.GetProperty("history").GetProperty("markers");
        Assert.AreEqual(2, markers.GetArrayLength());
        Assert.AreNotEqual(markers[0].GetProperty("id").GetString(), markers[1].GetProperty("id").GetString());
        Assert.AreEqual(0, markers[0].GetProperty("column").GetInt32());
        Assert.AreEqual(3, markers[1].GetProperty("column").GetInt32());
        Assert.IsFalse(markers[1].TryGetProperty("rawParameters", out _));
        Assert.AreEqual("executing", frame.GetProperty("commandMark").GetProperty("phase").GetString());
        var id = markers[1].GetProperty("id").GetString();
        await MessageAsync(adapter, new { type = "marker", action = "details", requestId = 1, id });
        var result = (await FrameAsync(adapter)).GetProperty("history").GetProperty("markerResult");
        Assert.IsTrue(result.GetProperty("success").GetBoolean());
        Assert.AreEqual("cmdline_url=secret", result.GetProperty("details").GetProperty("rawParameters").GetString());
    }

    [TestMethod]
    public async Task Markers_RepeatedReflow_TrackExactWideAndCombiningText()
    {
        await using var adapter = new Hwt1PresentationAdapter(20, 10).WithReflow(GhosttyReflowStrategy.Instance);
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(adapter).WithDimensions(20, 10).WithScrollback(1000).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "1234567890123456789\x1b]133;C\u0007界e\u0301-END\r\n" +
            string.Concat(Enumerable.Range(0, 20).Select(i => $"line{i}\r\n"))));
        var view = new Hwt1ViewState();
        var before = Capture(terminal, view);
        var command = TestSeq.Single(before.Markers);
        AssertMarkerText(terminal, command, "界");
        var id = $"custom:{Guid.NewGuid():D}";
        Send(terminal, view, new { type = "marker", action = "add", requestId = 1, id,
            generation = before.Generation, rowId = RowIdAt(terminal, command.Row!.Value), column = command.Column });
        Assert.IsTrue(Capture(terminal, view).MarkerResult!.Success);
        var combiningId = $"custom:{Guid.NewGuid():D}";
        Send(terminal, view, new { type = "marker", action = "add", requestId = 2, id = combiningId,
            generation = before.Generation, rowId = RowIdAt(terminal, command.Row.Value), column = 2 });
        var generation = before.Generation;

        foreach (var width in new[] { 7, 13, 31, 5, 20 })
        {
            terminal.Resize(width, 10);
            var history = Capture(terminal, view);
            Assert.AreNotEqual(generation, history.Generation);
            generation = history.Generation;
            foreach (var marker in history.Markers)
            {
                Assert.IsNotNull(marker.Row);
                AssertMarkerText(terminal, marker, marker.Id == combiningId ? "e\u0301" : "界");
            }
            Assert.AreEqual(command.Id, history.Markers[0].Id);
        }
    }

    [TestMethod]
    public async Task MarkerInventory_LargeRetention_PagesCoherentlyAndResyncRestartsReplacement()
    {
        await using var adapter = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            Width = 20, Height = 10, CommandMarkHistoryCapacity = 3000,
            WorkloadAdapter = new Hex1bAppWorkloadAdapter(), PresentationAdapter = adapter
        });
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(string.Concat(
            Enumerable.Repeat("\x1b]133;A\u0007", 2500))));
        var first = (await FrameAsync(adapter)).GetProperty("history");
        Assert.AreEqual(Hwt1PresentationAdapter.MarkerPageSize, first.GetProperty("markers").GetArrayLength());
        var page = first.GetProperty("markerPage");
        Assert.AreEqual(2500, page.GetProperty("total").GetInt32());
        Assert.AreEqual(0, page.GetProperty("offset").GetInt32());
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\r\nnew output"));
        var last = (await FrameAsync(adapter)).GetProperty("history");
        Assert.AreEqual(first.GetProperty("rowIds").GetRawText(), last.GetProperty("rowIds").GetRawText());
        Assert.AreEqual(page.GetProperty("revision").GetString(), last.GetProperty("markerPage").GetProperty("revision").GetString());
        Assert.AreEqual(452, last.GetProperty("markers").GetArrayLength());
        Assert.AreEqual(2500, first.GetProperty("markers").EnumerateArray()
            .Concat(last.GetProperty("markers").EnumerateArray()).Select(m => m.GetProperty("id").GetString()).Distinct().Count());
        await MessageAsync(adapter, new { type = "resync" });
        var restarted = (await FrameAsync(adapter)).GetProperty("history");
        Assert.AreEqual(0, restarted.GetProperty("markerPage").GetProperty("offset").GetInt32());
        await MessageAsync(adapter, new { type = "resync" });
        var resynced = await FrameAsync(adapter);
        Assert.IsTrue(resynced.GetProperty("full").GetBoolean());
        Assert.AreNotEqual(restarted.GetProperty("markerPage").GetProperty("revision").GetString(),
            resynced.GetProperty("history").GetProperty("markerPage").GetProperty("revision").GetString());
    }

    [TestMethod]
    [DataRow("crop")]
    [DataRow("noReflow")]
    [DataRow("custom")]
    public async Task Markers_CropAndUnknownProviders_NeverGuessLostText(string strategy)
    {
        await using var adapter = new Hwt1PresentationAdapter(20, 10);
        if (strategy != "crop")
            adapter.WithReflow(strategy == "noReflow" ? XtermReflowStrategy.Instance : new UnknownReflowProvider());
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(adapter).WithDimensions(20, 10).WithScrollback(100).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;A\u0007ABCDEF\x1b]133;C\u0007LOST"));
        var view = new Hwt1ViewState();
        terminal.Resize(5, 10);
        var history = Capture(terminal, view);
        if (strategy == "custom")
            Assert.AreEqual(0, history.Markers.Length);
        else
            AssertMarkerText(terminal, TestSeq.Single(history.Markers), "A");
    }

    [TestMethod]
    public async Task Markers_EmptyAndEndPositions_PreserveBoundaryAcrossShrinkGrow()
    {
        await using var adapter = new Hwt1PresentationAdapter(20, 10).WithReflow(GhosttyReflowStrategy.Instance);
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(adapter).WithDimensions(20, 10).WithScrollback(100).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(new string('a', 20) + "\x1b]133;C\u0007\r\n\x1b]133;A\u0007"));
        var view = new Hwt1ViewState();
        var original = Capture(terminal, view);
        Assert.AreEqual(20, original.Markers[0].Column);
        terminal.Resize(5, 10);
        var narrow = Capture(terminal, view);
        Assert.AreEqual(3, narrow.Markers[0].Row);
        Assert.AreEqual(5, narrow.Markers[0].Column);
        Assert.AreEqual(4, narrow.Markers[1].Row);
        Assert.AreEqual(0, narrow.Markers[1].Column);
        terminal.Resize(20, 10);
        var restored = Capture(terminal, view);
        Assert.AreEqual(0, restored.Markers[0].Row);
        Assert.AreEqual(20, restored.Markers[0].Column);
        Assert.AreEqual(1, restored.Markers[1].Row);
        Assert.AreEqual(0, restored.Markers[1].Column);
    }

    [TestMethod]
    public async Task Markers_ReflowRetentionBoundary_ExpiresOnlyDiscardedPositions()
    {
        await using var adapter = new Hwt1PresentationAdapter(80, 10).WithReflow(GhosttyReflowStrategy.Instance);
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(adapter).WithDimensions(80, 10).WithScrollback(3).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b]133;A\u0007" + new string('a', 400) + "\x1b]133;C\u0007TARGET" + new string('z', 194)));
        var view = new Hwt1ViewState();
        terminal.Resize(20, 10);
        var narrow = Capture(terminal, view);
        AssertMarkerText(terminal, TestSeq.Single(narrow.Markers), "T");
        terminal.Resize(80, 10);
        var wide = Capture(terminal, view);
        AssertMarkerText(terminal, TestSeq.Single(wide.Markers), "T");
    }

    [TestMethod]
    public async Task Markers_SelectionReflow_InvalidatesSelectionWithoutExpiringMarker()
    {
        await using var adapter = new Hwt1PresentationAdapter(20, 10).WithReflow(GhosttyReflowStrategy.Instance);
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(adapter).WithDimensions(20, 10).WithScrollback(100).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;A\u0007abcdef"));
        var view = new Hwt1ViewState();
        var original = Capture(terminal, view);
        Send(terminal, view, new { type = "selection", action = "start", mode = "character", requestId = 1,
            generation = original.Generation, rowId = original.RowIds[0], column = 0 });
        Assert.AreEqual("valid", Capture(terminal, view).Selection.Status);
        terminal.Resize(5, 10);
        var reflowed = Capture(terminal, view);
        Assert.AreEqual("invalidated", reflowed.Selection.Status);
        AssertMarkerText(terminal, reflowed.Markers[0], "a");
    }

    [TestMethod]
    public async Task CustomMarkers_TrailingBlankPosition_DoesNotChangeReflowedContent()
    {
        await using var adapter = new Hwt1PresentationAdapter(20, 10).WithReflow(GhosttyReflowStrategy.Instance);
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(adapter).WithDimensions(20, 10).WithScrollback(100).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("A\r\n\r\nB"));
        var view = new Hwt1ViewState();
        var original = Capture(terminal, view);
        Send(terminal, view, new { type = "marker", action = "add", requestId = 1,
            id = $"custom:{Guid.NewGuid():D}", generation = original.Generation,
            rowId = original.RowIds[1], column = 19 });
        terminal.Resize(5, 10);
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("A", snapshot.GetLineTrimmed(0));
        Assert.AreEqual("", snapshot.GetLineTrimmed(1));
        Assert.AreEqual("B", snapshot.GetLineTrimmed(2));
        Assert.AreEqual(0, Capture(terminal, view).Markers.Length);
        Assert.AreEqual(0, terminal.TextAnchorCount);
    }

    [TestMethod]
    public async Task Markers_ClearingScrollback_PreservesLiveTextPositions()
    {
        await using var adapter = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(adapter).WithDimensions(20, 10).WithScrollback(100).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            string.Concat(Enumerable.Repeat("old\r\n", 20)) + "\x1b]133;A\u0007LIVE"));
        var view = new Hwt1ViewState();
        var before = Capture(terminal, view);
        terminal.Scrollback!.Clear();
        var after = Capture(terminal, view);
        Assert.AreNotEqual(before.Generation, after.Generation);
        AssertMarkerText(terminal, after.Markers[0], "L");
    }

    [TestMethod]
    public async Task CommandRetention_EvictsAnchorsAndReturnsUnknownForEvictedIds()
    {
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            Width = 20, Height = 10, CommandMarkHistoryCapacity = 2,
            WorkloadAdapter = new Hex1bAppWorkloadAdapter(),
            PresentationAdapter = new Hwt1PresentationAdapter(20, 10)
        });
        var view = new Hwt1ViewState();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;A\u0007"));
        var firstId = TestSeq.Single(Capture(terminal, view).Markers).Id;
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;B\u0007\x1b]133;C\u0007"));
        Assert.AreEqual(2, terminal.TextAnchorCount);
        Assert.AreEqual(2, Capture(terminal, view).Markers.Length);
        Send(terminal, view, new { type = "marker", action = "jump", requestId = 1, id = firstId });
        Assert.AreEqual("unknown-marker", Capture(terminal, view).MarkerResult!.Error);
    }

    private sealed class UnknownReflowProvider : ITerminalReflowProvider
    {
        public bool ShouldClearSoftWrapOnAbsolutePosition => false;
        public ReflowResult Reflow(ReflowContext context) => GhosttyReflowStrategy.Instance.Reflow(context);
    }

    [TestMethod]
    public async Task CustomMarkers_DefaultLimit_Rejects1001stAndReleasesAllOnDispose()
    {
        await using var adapter = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(adapter).WithDimensions(20, 10).Build();
        var initial = (await FrameAsync(adapter)).GetProperty("history");
        string? firstId = null;
        for (var i = 0; i < 1001; i++)
        {
            var id = $"custom:{Guid.NewGuid():D}";
            firstId ??= id;
            await MessageAsync(adapter, new { type = "marker", action = "add", requestId = i + 1, id,
                generation = initial.GetProperty("generation").GetString(),
                rowId = initial.GetProperty("rowIds")[0].GetString(), column = 0 });
        }
        var history = (await FrameAsync(adapter)).GetProperty("history");
        Assert.AreEqual(1000, history.GetProperty("markers").GetArrayLength());
        Assert.AreEqual("marker-limit", history.GetProperty("markerResult").GetProperty("error").GetString());
        Assert.AreEqual(1000, terminal.TextAnchorCount);
        await MessageAsync(adapter, new { type = "marker", action = "remove", requestId = 1002, id = firstId });
        Assert.AreEqual(999, terminal.TextAnchorCount);
        await adapter.DisposeAsync();
        Assert.AreEqual(0, terminal.TextAnchorCount);
    }

    [TestMethod]
    public async Task CustomMarkers_ReadOnlyViews_AreIndependentAndJumpDoesNotAcknowledgeViewport()
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(muxer).WithDimensions(20, 10).WithScrollback(100).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(string.Concat(Enumerable.Range(0, 30).Select(i => $"line{i}\r\n"))));
        await using var first = await muxer.CreateBrowserViewAsync("first");
        await using var second = await muxer.CreateBrowserViewAsync("second");
        first.IsReadOnly = true;
        await FrameAsync(first);
        await MessageAsync(first, new { type = "viewport", requestId = 1, delta = -100 });
        var history = (await FrameAsync(first)).GetProperty("history");
        var id = $"custom:{Guid.NewGuid():D}";
        await MessageAsync(first, new { type = "marker", action = "add", requestId = 10, id,
            generation = history.GetProperty("generation").GetString(),
            rowId = history.GetProperty("rowIds")[0].GetString(), column = 0 });
        history = (await FrameAsync(first)).GetProperty("history");
        Assert.IsTrue(history.GetProperty("markerResult").GetProperty("success").GetBoolean());
        Assert.AreEqual(0, (await FrameAsync(second)).GetProperty("history").GetProperty("markers").GetArrayLength());
        await MessageAsync(first, new { type = "viewport", requestId = 2, live = true });
        await FrameAsync(first);
        await MessageAsync(first, new { type = "marker", action = "jump", requestId = 11, id });
        history = (await FrameAsync(first)).GetProperty("history");
        Assert.AreEqual(0, history.GetProperty("top").GetInt32());
        Assert.AreEqual(2, history.GetProperty("requestId").GetInt64());
        Assert.IsTrue(history.GetProperty("markerResult").GetProperty("success").GetBoolean());
        Assert.IsNull(muxer.PrimaryPeerId);
    }

    [TestMethod]
    [DataRow(true, 6)]
    [DataRow(true, 14)]
    [DataRow(false, 6)]
    [DataRow(false, 14)]
    public async Task Markers_AlternateBuffer_RestoresMainAfterResizeAndExpiresAlternate(bool reflow, int height)
    {
        await using var adapter = new Hwt1PresentationAdapter(20, 10);
        if (reflow)
            adapter.WithReflow(GhosttyReflowStrategy.Instance);
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(adapter).WithDimensions(20, 10).WithScrollback(100).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("abcdef\x1b]133;A\u0007MAIN\r\n"));
        var view = new Hwt1ViewState();
        var main = TestSeq.Single(Capture(terminal, view).Markers);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049h\x1b[H\x1b]133;C\u0007ALT"));
        var alternate = Capture(terminal, view);
        Assert.IsNull(alternate.Markers[0].Row);
        Assert.AreEqual("alternate", alternate.Markers[1].Buffer);
        AssertMarkerText(terminal, alternate.Markers[1], "A");
        Send(terminal, view, new { type = "marker", action = "jump", requestId = 1, id = main.Id });
        Assert.AreEqual("inactive-buffer", Capture(terminal, view).MarkerResult!.Error);
        terminal.Resize(7, height);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049l"));
        var restored = Capture(terminal, view);
        AssertMarkerText(terminal, restored.Markers[0], "M");
        Assert.AreEqual(1, restored.Markers.Length);
        Send(terminal, view, new { type = "marker", action = "jump", requestId = 2, id = main.Id });
        Assert.IsTrue(Capture(terminal, view).MarkerResult!.Success);
    }

    [TestMethod]
    [DataRow("\x1b[2J")]
    [DataRow("\u001bc")]
    [DataRow("\r\n1\r\n2\r\n3\r\n4\r\n5\r\n6\r\n7\r\n8\r\n9\r\n10\r\n11\r\n12\r\n13")]
    public async Task Markers_DestructiveClearResetOrEviction_ExpiresNavigation(string output)
    {
        await using var adapter = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(adapter).WithDimensions(20, 10).WithScrollback(2).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;A\u0007old"));
        var view = new Hwt1ViewState();
        var marker = TestSeq.Single(Capture(terminal, view).Markers);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(output));
        Send(terminal, view, new { type = "marker", action = "jump", requestId = 1, id = marker.Id });
        var expired = Capture(terminal, view);
        Assert.AreEqual(0, expired.Markers.Length);
        Assert.AreEqual(0, terminal.TextAnchorCount);
        Assert.AreEqual(0, terminal.CommandMarks.Count);
        Assert.AreEqual("unknown-marker", expired.MarkerResult!.Error);
        Send(terminal, view, new { type = "marker", action = "jump", requestId = 2, id = "command:9999" });
        Assert.AreEqual("unknown-marker", Capture(terminal, view).MarkerResult!.Error);
    }

    [TestMethod]
    public async Task AbsoluteViewport_OutputAndEviction_ResolvesOriginalIdentityWithoutAccumulatingDeltas()
    {
        await using var adapter = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(adapter).WithDimensions(20, 10).WithScrollback(30).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(string.Concat(Enumerable.Range(0, 35).Select(i => $"line{i}\r\n"))));
        var view = new Hwt1ViewState();
        var original = Capture(terminal, view);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(string.Concat(Enumerable.Range(35, 10).Select(i => $"line{i}\r\n"))));
        Send(terminal, view, new { type = "viewport", requestId = 1, top = original.Top - 10,
            generation = original.Generation, originRowId = original.RowIds[0], originTop = original.Top });
        var first = Capture(terminal, view);
        Send(terminal, view, new { type = "viewport", requestId = 2, top = original.Top - 12,
            generation = original.Generation, originRowId = original.RowIds[0], originTop = original.Top });
        Assert.AreEqual(first.Top - 2, Capture(terminal, view).Top);
        terminal.Resize(21, 10);
        Send(terminal, view, new { type = "viewport", requestId = 3, top = 0,
            generation = original.Generation, originRowId = original.RowIds[0], originTop = original.Top });
        Assert.AreEqual(3, Capture(terminal, view).RequestId);
        Assert.AreEqual("stale-position", Capture(terminal, view).ViewportError);
        Send(terminal, view, new { type = "viewport", requestId = 4, live = true });
        Assert.IsNull(Capture(terminal, view).ViewportError);
    }

    [TestMethod]
    [DataRow(8192, true)]
    [DataRow(8193, false)]
    public async Task MarkerDetails_RawParametersLimit_ReturnsCompleteDetailsOrExplicitError(int length, bool success)
    {
        await using var adapter = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(adapter).WithDimensions(20, 10).Build();
        var parameters = "key=" + new string('a', length - 4);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize($"\u001b]133;C;{parameters}\u0007"));
        var view = new Hwt1ViewState();
        var marker = TestSeq.Single(Capture(terminal, view).Markers);
        Send(terminal, view, new { type = "marker", action = "details", requestId = 1, id = marker.Id });
        var result = Capture(terminal, view).MarkerResult!;
        Assert.AreEqual(success, result.Success);
        if (success)
            Assert.AreEqual(parameters, result.Details!.RawParameters);
        else
        {
            Assert.AreEqual("details-too-large", result.Error);
            Assert.IsNull(result.Details);
        }
    }

    [TestMethod]
    public async Task CustomMarkers_ConfiguredLimitAndStalePositions_RejectWithoutLeaking()
    {
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            Width = 20, Height = 10, CustomMarkerLimit = 1,
            WorkloadAdapter = new Hex1bAppWorkloadAdapter(),
            PresentationAdapter = new Hwt1PresentationAdapter(20, 10)
        });
        var view = new Hwt1ViewState();
        var initial = Capture(terminal, view);
        var id = $"custom:{Guid.NewGuid():D}";
        Send(terminal, view, new { type = "marker", action = "add", requestId = 1, id,
            generation = "999", rowId = initial.RowIds[0], column = 0 });
        Assert.AreEqual("stale-position", Capture(terminal, view).MarkerResult!.Error);
        Assert.AreEqual(0, terminal.TextAnchorCount);
        Send(terminal, view, new { type = "marker", action = "add", requestId = 2, id,
            generation = initial.Generation, rowId = initial.RowIds[0], column = 0 });
        Assert.IsTrue(Capture(terminal, view).MarkerResult!.Success);
        Send(terminal, view, new { type = "marker", action = "add", requestId = 3,
            id = $"custom:{Guid.NewGuid():D}", generation = initial.Generation, rowId = initial.RowIds[0], column = 0 });
        Assert.AreEqual("marker-limit", Capture(terminal, view).MarkerResult!.Error);
        Assert.AreEqual(1, terminal.TextAnchorCount);
        terminal.ReleaseBrowserMarkers(view);
        Assert.AreEqual(0, terminal.TextAnchorCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Markers_EvictionWithoutBrowserObserver_ReclaimsAnchorsAndCommandMetadata(bool captureImpacts)
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless()
            .WithDimensions(8, 2).WithScrollback(2).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;C;key=retained-metadata\u0007old\r\none\r\ntwo"));
        Assert.AreEqual(1, terminal.TextAnchorCount);
        Assert.AreEqual("retained-metadata", TestSeq.Single(terminal.CommandMarks).Parameters["key"]);

        var output = AnsiTokenizer.Tokenize("\r\nthree\r\nfour");
        if (captureImpacts)
            terminal.ApplyTokensWithImpacts(output);
        else
            terminal.ApplyTokens(output);

        Assert.AreEqual(2, terminal.ScrollbackCount);
        Assert.AreEqual(0, terminal.TextAnchorCount);
        Assert.AreEqual(0, terminal.CommandMarks.Count);
        Assert.AreEqual(TerminalShellIntegrationPhase.Executing, terminal.ShellIntegration.Phase);
    }

    [TestMethod]
    [DataRow("\x1b[2J")]
    [DataRow("\u001bc")]
    [DataRow("\x1b[H\x1b[L")]
    [DataRow("\x1b[H\x1b[M")]
    [DataRow("\x1b[S")]
    [DataRow("\x1b[T")]
    [DataRow("\x1b[H\x1b[3X")]
    public async Task Markers_DestructiveMutationWithoutBrowserObserver_ReclaimsNeverResolvedMarks(string output)
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().WithDimensions(8, 2).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;A;key=value\u0007old"));
        Assert.AreEqual(1, terminal.TextAnchorCount);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize(output));

        Assert.AreEqual(0, terminal.TextAnchorCount);
        Assert.AreEqual(0, terminal.CommandMarks.Count);
    }

    [TestMethod]
    public async Task Markers_OffscreenEviction_ReleasesCustomQuotaAndCachedDetailsInEveryView()
    {
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            Width = 8, Height = 2, ScrollbackCapacity = 2, CustomMarkerLimit = 1,
            WorkloadAdapter = new Hex1bAppWorkloadAdapter(),
            PresentationAdapter = new HeadlessPresentationAdapter(8, 2)
        });
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;C;key=metadata\u0007old"));
        var first = new Hwt1ViewState();
        var second = new Hwt1ViewState();
        var initial = Capture(terminal, first);
        var commandId = TestSeq.Single(initial.Markers).Id;
        foreach (var view in new[] { first, second })
        {
            Send(terminal, view, new { type = "marker", action = "add", requestId = 1,
                id = $"custom:{Guid.NewGuid():D}", generation = initial.Generation,
                rowId = initial.RowIds[0], column = 0 });
            Send(terminal, view, new { type = "marker", action = "details", requestId = 2, id = commandId });
            Assert.AreEqual("key=metadata", view.MarkerResult!.Details!.RawParameters);
        }
        Assert.AreEqual(3, terminal.TextAnchorCount);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\r\none\r\ntwo"));
        Assert.AreEqual(3, terminal.TextAnchorCount);

        // Neither view requests another inventory while the backing row is evicted.
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\r\nthree\r\nfour"));

        Assert.AreEqual(0, terminal.TextAnchorCount);
        Assert.AreEqual(0, terminal.CommandMarks.Count);
        foreach (var view in new[] { first, second })
        {
            Assert.AreEqual(0, view.CustomMarkers.Count);
            Assert.IsNull(view.MarkerResult!.Details);
            Assert.AreEqual("unknown-marker", view.MarkerResult.Error);
            var current = Capture(terminal, view);
            Send(terminal, view, new { type = "marker", action = "add", requestId = 3,
                id = $"custom:{Guid.NewGuid():D}", generation = current.Generation,
                rowId = current.RowIds[0], column = 0 });
            Assert.IsTrue(view.MarkerResult.Success);
            Assert.AreEqual(1, view.CustomMarkers.Count);
        }
        Assert.AreEqual(2, terminal.TextAnchorCount);
    }

    [TestMethod]
    public async Task Markers_AlternateScreenAndHistoryClear_CollectsOnlyDiscardedMainContent()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless()
            .WithDimensions(8, 2).WithScrollback(2).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;A\u0007old"));
        var view = new Hwt1ViewState();
        var initial = Capture(terminal, view);
        var historyCommandId = TestSeq.Single(initial.Markers).Id;
        Send(terminal, view, new { type = "marker", action = "add", requestId = 1,
            id = $"custom:{Guid.NewGuid():D}", generation = initial.Generation,
            rowId = initial.RowIds[0], column = 0 });
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\r\none\r\n\x1b]133;C\u0007LIVE"));
        var main = Capture(terminal, view);
        var liveCommand = main.Markers.Single(marker => marker.Phase == "executing");
        var liveCustomId = $"custom:{Guid.NewGuid():D}";
        Send(terminal, view, new { type = "marker", action = "add", requestId = 2,
            id = liveCustomId, generation = main.Generation,
            rowId = main.RowIds[1], column = 0 });

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049h\x1b[2J\x1b]133;A\u0007ALT\x1b[2J"));
        Assert.AreEqual(4, terminal.TextAnchorCount);
        Send(terminal, view, new { type = "marker", action = "jump", requestId = 3, id = liveCommand.Id });
        Assert.AreEqual("inactive-buffer", view.MarkerResult!.Error);
        Assert.AreEqual(4, Capture(terminal, view).Markers.Length);

        terminal.Scrollback!.Clear();

        Assert.AreEqual(2, terminal.TextAnchorCount);
        Assert.AreEqual(1, terminal.CommandMarks.Count);
        Assert.AreEqual(liveCustomId, TestSeq.Single(view.CustomMarkers).Key);
        Send(terminal, view, new { type = "marker", action = "details", requestId = 4, id = historyCommandId });
        Assert.AreEqual("unknown-marker", view.MarkerResult!.Error);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049l"));
        var restored = Capture(terminal, view);
        Assert.AreEqual(2, restored.Markers.Length);
        foreach (var marker in restored.Markers)
            AssertMarkerText(terminal, marker, "L");
    }

    [TestMethod]
    public async Task Markers_ClearScrollbackWithoutBrowserObserver_PreservesScreenMetadata()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless()
            .WithDimensions(8, 2).WithScrollback(2).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b]133;A;key=old\u0007old\r\none\r\n\x1b]133;C;key=live\u0007LIVE"));
        Assert.AreEqual(2, terminal.CommandMarks.Count);

        terminal.Scrollback!.Clear();

        Assert.AreEqual(1, terminal.TextAnchorCount);
        Assert.AreEqual("live", TestSeq.Single(terminal.CommandMarks).Parameters["key"]);
    }

    [TestMethod]
    [DataRow("\x1b[2J")]
    [DataRow("\u001bc")]
    public async Task Markers_ClearOrReset_ReleasesCustomQuotaAndCachedCommandDetails(string output)
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().WithDimensions(8, 2).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;C;key=metadata\u0007old"));
        var view = new Hwt1ViewState();
        var initial = Capture(terminal, view);
        Send(terminal, view, new { type = "marker", action = "add", requestId = 1,
            id = $"custom:{Guid.NewGuid():D}", generation = initial.Generation,
            rowId = initial.RowIds[0], column = 0 });
        Send(terminal, view, new { type = "marker", action = "details", requestId = 2,
            id = TestSeq.Single(initial.Markers).Id });
        Assert.IsNotNull(view.MarkerResult!.Details);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize(output));

        Assert.AreEqual(0, terminal.TextAnchorCount);
        Assert.AreEqual(0, terminal.CommandMarks.Count);
        Assert.AreEqual(0, view.CustomMarkers.Count);
        Assert.IsNull(view.MarkerResult.Details);
    }

    [TestMethod]
    public async Task MarkerDetails_CommandCapacityEviction_ReleasesCachedMetadata()
    {
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            Width = 8, Height = 2, CommandMarkHistoryCapacity = 1,
            WorkloadAdapter = new Hex1bAppWorkloadAdapter(),
            PresentationAdapter = new HeadlessPresentationAdapter(8, 2)
        });
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;C;key=old\u0007"));
        var view = new Hwt1ViewState();
        var marker = TestSeq.Single(Capture(terminal, view).Markers);
        Send(terminal, view, new { type = "marker", action = "details", requestId = 1, id = marker.Id });
        Assert.IsNotNull(view.MarkerResult!.Details);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;C;key=new\u0007"));

        Assert.IsNull(view.MarkerResult.Details);
        Assert.AreEqual("unknown-marker", view.MarkerResult.Error);
        Assert.AreEqual("new", TestSeq.Single(terminal.CommandMarks).Parameters["key"]);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MarkerDetails_EvictedBeforeNextResponse_PreservesAcknowledgementAndEarlierSnapshot(
        bool captureEarlierResponse)
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless()
            .WithDimensions(8, 2).WithScrollback(2).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;C;key=metadata\u0007old"));
        var view = new Hwt1ViewState();
        var marker = TestSeq.Single(Capture(terminal, view).Markers);
        Send(terminal, view, new { type = "marker", action = "details", requestId = 42, id = marker.Id });
        Hwt1History? earlierResponse = captureEarlierResponse ? Capture(terminal, view) : null;

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\r\none\r\ntwo\r\nthree\r\nfour"));

        var response = Capture(terminal, view).MarkerResult;
        Assert.IsNotNull(response);
        Assert.AreEqual(42L, response.RequestId);
        Assert.AreEqual(marker.Id, response.MarkerId);
        Assert.IsFalse(response.Success);
        Assert.AreEqual("unknown-marker", response.Error);
        Assert.IsNull(response.Details);
        if (earlierResponse is not null)
        {
            var earlierResult = earlierResponse.MarkerResult;
            Assert.IsNotNull(earlierResult);
            Assert.AreEqual(42L, earlierResult.RequestId);
            Assert.AreEqual(marker.Id, earlierResult.MarkerId);
            Assert.IsTrue(earlierResult.Success);
            Assert.IsNull(earlierResult.Error);
            Assert.AreEqual("key=metadata", earlierResult.Details!.RawParameters);
        }
    }

    [TestMethod]
    [DataRow("\x1b[H\x1b[2P", 0)]
    [DataRow("\x1b[H\x1b[2@", 4)]
    [DataRow("\x1b[1;3H\x1b[1@", 3)]
    [DataRow("\x1b[H\x1b[4hXY\x1b[4l", 4)]
    [DataRow("\x1b[1;3H\x1b[4hX\x1b[4l", 3)]
    [DataRow("\x1b[H\x1b[0@", 2)]
    [DataRow("\x1b[H\x1b[0P", 1)]
    public async Task Markers_HorizontalEdit_FollowsRetainedCells(string edit, int column)
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().WithDimensions(8, 2).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("AB\x1b]133;C\u0007TARGET"));
        var view = new Hwt1ViewState();
        var before = Capture(terminal, view);
        var commandId = TestSeq.Single(before.Markers).Id;
        var customId = $"custom:{Guid.NewGuid():D}";
        Send(terminal, view, new { type = "marker", action = "add", requestId = 1, id = customId,
            generation = before.Generation, rowId = before.RowIds[0], column = 2 });

        terminal.ApplyTokens(AnsiTokenizer.Tokenize(edit));

        var after = Capture(terminal, view);
        Assert.AreEqual(2, after.Markers.Length);
        TestSeq.AreEqual(new[] { commandId, customId }, after.Markers.Select(marker => marker.Id));
        foreach (var marker in after.Markers)
        {
            Assert.AreEqual(column, marker.Column);
            AssertMarkerText(terminal, marker, "T");
        }
        Send(terminal, view, new { type = "marker", action = "jump", requestId = 2, id = customId });
        Assert.IsTrue(Capture(terminal, view).MarkerResult!.Success);
    }

    [TestMethod]
    [DataRow("\x1b[H\x1b[8P")]
    [DataRow("\x1b[H\x1b[8@")]
    [DataRow("\x1b[1;3H\x1b[P")]
    [DataRow("\x1b[H\x1b[4h123456\x1b[4l")]
    public async Task Markers_HorizontalEdit_CollectsDeletedCellsAndReclaimsQuota(string edit)
    {
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            Width = 8, Height = 2, CustomMarkerLimit = 1,
            WorkloadAdapter = new Hex1bAppWorkloadAdapter(),
            PresentationAdapter = new HeadlessPresentationAdapter(8, 2)
        });
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("AB\x1b]133;C;key=details\u0007TARGET"));
        var view = new Hwt1ViewState();
        var before = Capture(terminal, view);
        var commandId = TestSeq.Single(before.Markers).Id;
        var customId = $"custom:{Guid.NewGuid():D}";
        Send(terminal, view, new { type = "marker", action = "add", requestId = 1, id = customId,
            generation = before.Generation, rowId = before.RowIds[0], column = 2 });
        Send(terminal, view, new { type = "marker", action = "details", requestId = 2, id = commandId });
        Assert.IsNotNull(Capture(terminal, view).MarkerResult!.Details);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize(edit));

        Assert.AreEqual(0, terminal.CommandMarks.Count);
        Assert.AreEqual(0, terminal.TextAnchorCount);
        var after = Capture(terminal, view);
        Assert.AreEqual(0, after.Markers.Length);
        Assert.AreEqual("unknown-marker", after.MarkerResult!.Error);
        Assert.IsNull(after.MarkerResult.Details);
        Send(terminal, view, new { type = "marker", action = "jump", requestId = 3, id = customId });
        Assert.AreEqual("unknown-marker", Capture(terminal, view).MarkerResult!.Error);
        Send(terminal, view, new { type = "marker", action = "add", requestId = 4,
            id = $"custom:{Guid.NewGuid():D}", generation = after.Generation,
            rowId = after.RowIds[0], column = 0 });
        Assert.IsTrue(Capture(terminal, view).MarkerResult!.Success);
    }

    [TestMethod]
    [DataRow("\x1b[P", 2)]
    [DataRow("\x1b[@", 4)]
    public async Task Markers_HorizontalEdit_LeavesOtherRowsBuffersAndMarginsUntouched(string edit, int column)
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().WithDimensions(8, 3).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b]133;C\u0007ABC\x1b]133;C\u0007DEF\x1b]133;C\u0007GH" +
            "\r\n\x1b]133;C\u0007OTHER"));
        var view = new Hwt1ViewState();
        var before = Capture(terminal, view);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?69h\x1b[2;6s\x1b[1;2H" + edit));
        var after = Capture(terminal, view);
        Assert.AreEqual(4, after.Markers.Length);
        TestSeq.AreEqual(before.Markers.Select(marker => marker.Id), after.Markers.Select(marker => marker.Id));
        AssertMarkerText(terminal, after.Markers[0], "A");
        Assert.AreEqual(column, after.Markers[1].Column);
        AssertMarkerText(terminal, after.Markers[1], "D");
        AssertMarkerText(terminal, after.Markers[2], "G");
        AssertMarkerText(terminal, after.Markers[3], "O");

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?69l\x1b[?1049hAB\x1b]133;C\u0007TARGET\x1b[H\x1b[8P"));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049l"));
        TestSeq.AreEqual(after.Markers, Capture(terminal, view).Markers);
    }

    [TestMethod]
    [DataRow("\x1b[H\x1b[P", 1)]
    [DataRow("\x1b[H\x1b[@", 3)]
    public async Task Markers_HorizontalEdit_MovesWideGlyphAndContinuationBookmarkTogether(string edit, int column)
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().WithDimensions(8, 2).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("AB\x1b]133;C\u0007界XYZ"));
        var view = new Hwt1ViewState();
        var before = Capture(terminal, view);
        Send(terminal, view, new { type = "marker", action = "add", requestId = 1,
            id = $"custom:{Guid.NewGuid():D}", generation = before.Generation,
            rowId = before.RowIds[0], column = 3 });

        terminal.ApplyTokens(AnsiTokenizer.Tokenize(edit));

        var markers = Capture(terminal, view).Markers;
        Assert.AreEqual(2, markers.Length);
        foreach (var marker in markers)
        {
            Assert.AreEqual(column, marker.Column);
            AssertMarkerText(terminal, marker, "界");
        }
    }

    [TestMethod]
    [DataRow("\x1b[1;4H\x1b[P")]
    [DataRow("\x1b[1;4H\x1b[@")]
    [DataRow("\x1b[H\x1b[3P")]
    [DataRow("\x1b[H\x1b[5@")]
    [DataRow("\x1b[?69h\x1b[1;3s\x1b[H\x1b[P")]
    public async Task Markers_HorizontalEdit_ExpiresSplitWideGlyph(string edit)
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().WithDimensions(8, 2).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("AB\x1b]133;C\u0007界XYZ"));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize(edit));

        Assert.AreEqual(0, terminal.CommandMarks.Count);
        Assert.AreEqual(0, terminal.TextAnchorCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Markers_InsertModeBlankPosition_BindsToFirstPrintedCell(bool fullRow)
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().WithDimensions(8, 2).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[4h\x1b]133;C\u0007" + (fullRow ? "12345678" : "TEXT")));
        var marker = TestSeq.Single(Capture(terminal, new Hwt1ViewState()).Markers);
        Assert.AreEqual(0, marker.Column);
        AssertMarkerText(terminal, marker, fullRow ? "1" : "T");
    }

    [TestMethod]
    [DataRow(false, 1)]
    [DataRow(false, 2)]
    [DataRow(true, 1)]
    [DataRow(true, 2)]
    public async Task Markers_InsertModeWrap_BindsBoundaryToPrintedGlyph(bool wide, int height)
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().WithDimensions(4, height).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[2;1HOLD\x1b[H\x1b[4h" +
            (wide ? "ABC\x1b]133;C\u0007界" : "ABCD\x1b]133;C\u0007E")));
        var marker = TestSeq.Single(Capture(terminal, new Hwt1ViewState()).Markers);
        Assert.AreEqual(height - 1, marker.Row);
        Assert.AreEqual(0, marker.Column);
        AssertMarkerText(terminal, marker, wide ? "界" : "E");
    }

    [TestMethod]
    [DataRow("\x1b[2P")]
    [DataRow("\x1b[2@")]
    public async Task Markers_HorizontalEdit_PreservesPendingEndBoundary(string edit)
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().WithDimensions(8, 2).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("12345678\x1b]133;C\u0007\x1b[H" + edit));
        var marker = TestSeq.Single(Capture(terminal, new Hwt1ViewState()).Markers);
        Assert.AreEqual(8, marker.Column);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;8HXY"));
        marker = TestSeq.Single(Capture(terminal, new Hwt1ViewState()).Markers);
        Assert.AreEqual(1, marker.Row);
        Assert.AreEqual(0, marker.Column);
        AssertMarkerText(terminal, marker, "Y");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Markers_HorizontalEditThenReflow_PreservesShiftedGlyph(bool preserveCursorRow)
    {
        await using var adapter = new Hwt1PresentationAdapter(8, 6).WithReflow(
            preserveCursorRow ? GhosttyReflowStrategy.Instance : AlacrittyReflowStrategy.Instance);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithPresentation(adapter)
            .WithDimensions(8, 6).WithScrollback(20).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("AB\x1b]133;C\u0007界e\u0301Z\x1b[H\x1b[@"));
        var view = new Hwt1ViewState();
        var original = TestSeq.Single(Capture(terminal, view).Markers);
        Assert.AreEqual(3, original.Column);
        AssertMarkerText(terminal, original, "界");
        foreach (var width in new[] { 4, 8 })
        {
            terminal.Resize(width, 6);
            var marker = TestSeq.Single(Capture(terminal, view).Markers);
            Assert.AreEqual(original.Id, marker.Id);
            AssertMarkerText(terminal, marker, "界");
        }
    }

    [TestMethod]
    [DataRow("\x1b[P")]
    [DataRow("\x1b[@")]
    public async Task Markers_HorizontalEditBeyondRightMargin_LeavesTextAndMarkersUntouched(string edit)
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().WithDimensions(8, 2).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("ABCDEF\x1b]133;C\u0007界"));
        var view = new Hwt1ViewState();
        var before = Capture(terminal, view);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?69h\x1b[1;3s\x1b[1;8H" + edit));
        TestSeq.AreEqual(before.Markers, Capture(terminal, view).Markers);
        AssertMarkerText(terminal, TestSeq.Single(before.Markers), "界");
    }

    [TestMethod]
    public async Task Markers_AlignmentTest_ExpiresDiscardedScreenText()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().WithDimensions(8, 2).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("AB\x1b]133;C\u0007TARGET"));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b#8"));
        Assert.AreEqual(0, terminal.CommandMarks.Count);
    }

    [TestMethod]
    [DataRow("\x1b[99X")]
    [DataRow("\x1b[2K")]
    [DataRow("\x1b[K")]
    public async Task Markers_EraseWithinMargins_PreservesTextOutsideRightMargin(string erase)
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().WithDimensions(8, 2).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("ABCDE\x1b]133;C\u0007FGH"));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?69h\x1b[1;5s\x1b[H" + erase));

        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("F", snapshot.GetCell(5, 0).Character);
        var marker = TestSeq.Single(Capture(terminal, new Hwt1ViewState()).Markers);
        AssertMarkerText(terminal, marker, "F");
    }

    [TestMethod]
    [DataRow(false, 1)]
    [DataRow(true, 1)]
    [DataRow(false, 2)]
    [DataRow(true, 2)]
    public async Task Markers_UnobservedWrappedBoundary_SurvivesOriginalRowEviction(bool wide, int height)
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless()
            .WithDimensions(4, height).WithScrollback(2).Build();
        var wrapped = wide ? "ABC\x1b]133;C\u0007界FG" : "ABCD\x1b]133;C\u0007EFGH";
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(wrapped +
            string.Concat(Enumerable.Repeat("\r\nnext", height + 1))));

        Assert.AreEqual(1, terminal.CommandMarks.Count);
        var marker = TestSeq.Single(Capture(terminal, new Hwt1ViewState()).Markers);
        Assert.AreEqual(0, marker.Row);
        Assert.AreEqual(0, marker.Column);
        AssertMarkerText(terminal, marker, wide ? "界" : "E");
    }

    [TestMethod]
    public async Task CustomMarkers_UnobservedRepeatWrap_SurvivesOriginalRowErasure()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().WithDimensions(4, 2).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("ABCD"));
        var view = new Hwt1ViewState();
        var initial = Capture(terminal, view);
        Send(terminal, view, new { type = "marker", action = "add", requestId = 1,
            id = $"custom:{Guid.NewGuid():D}", generation = initial.Generation,
            rowId = initial.RowIds[0], column = 4 });

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[4b\x1b[H\x1b[2K"));

        var marker = TestSeq.Single(Capture(terminal, view).Markers);
        Assert.AreEqual(1, marker.Row);
        Assert.AreEqual(0, marker.Column);
        AssertMarkerText(terminal, marker, "D");
    }

    [TestMethod]
    public async Task Markers_ResolvedBoundaryBetweenHistoryRows_FollowsNewRowLifetime()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless()
            .WithDimensions(4, 2).WithScrollback(2).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "ABCD\x1b]133;C\u0007EFGH\r\none\r\ntwo"));
        Assert.AreEqual(2, terminal.ScrollbackCount);
        var view = new Hwt1ViewState();
        var boundary = TestSeq.Single(Capture(terminal, view).Markers);
        Assert.AreEqual(1, boundary.Row);
        Assert.AreEqual(0, boundary.Column);
        AssertMarkerText(terminal, boundary, "E");

        // Prune the original row, retaining the row the insertion boundary resolved to.
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\r\nnext"));

        Assert.AreEqual(1, terminal.TextAnchorCount);
        Assert.AreEqual(1, terminal.CommandMarks.Count);
        var retained = TestSeq.Single(Capture(terminal, view).Markers);
        Assert.AreEqual(boundary.Id, retained.Id);
        Assert.AreEqual(0, retained.Row);
        AssertMarkerText(terminal, retained, "E");

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\r\nlast"));

        Assert.AreEqual(0, terminal.TextAnchorCount);
        Assert.AreEqual(0, terminal.CommandMarks.Count);
        Assert.AreEqual(0, Capture(terminal, view).Markers.Length);
    }

    private static Hwt1History Capture(Hex1bTerminal terminal, Hwt1ViewState view)
    {
        Assert.IsTrue(terminal.TryCaptureBrowserSnapshot(view, out var snapshot, out var history, out _, out _));
        snapshot.Dispose();
        return history;
    }

    private static string RowIdAt(Hex1bTerminal terminal, int row)
    {
        var view = new Hwt1ViewState();
        Send(terminal, view, new { type = "viewport", requestId = 1, delta = int.MinValue });
        var history = Capture(terminal, view);
        Send(terminal, view, new { type = "viewport", requestId = 2, delta = row });
        history = Capture(terminal, view);
        return history.RowIds[row - history.Top];
    }

    private static void AssertMarkerText(Hex1bTerminal terminal, Hwt1Marker marker, string expected)
    {
        using var snapshot = terminal.CreateSnapshot(int.MaxValue);
        Assert.IsNotNull(marker.Row);
        Assert.AreEqual(expected, snapshot.GetCell(marker.Column, marker.Row.Value).Character);
    }

    private static void Send(Hex1bTerminal terminal, Hwt1ViewState view, object message)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(message));
        terminal.HandleBrowserHistoryMessage(view, document.RootElement);
    }

    private static Task MessageAsync(Hwt1PresentationAdapter adapter, object message)
        => adapter.HandleMessageAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message)));

    private static async Task<JsonElement> FrameAsync(Hwt1PresentationAdapter adapter)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var frame = await adapter.ReadFrameAsync(timeout.Token);
        var length = BinaryPrimitives.ReadInt32LittleEndian(frame.Span[4..]);
        using var json = JsonDocument.Parse(frame.Slice(8, length));
        var result = json.RootElement.Clone();
        await MessageAsync(adapter, new { type = "ack", revision = result.GetProperty("revision").GetUInt32() });
        return result;
    }
}
