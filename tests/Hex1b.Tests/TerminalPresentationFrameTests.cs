using System.Reflection;
using Hex1b.Automation;
using Hex1b.Tokens;
using Hex1b.Widgets;
using Microsoft.Extensions.Time.Testing;

namespace Hex1b.Tests;

/// <summary>
/// Tests for presentation-only frames: content-only embedded terminal output re-renders the
/// existing node tree without rebuilding, reconciling, or laying out the host.
/// </summary>
[TestClass]
public class TerminalPresentationFrameTests
{
    private static AppliedToken Applied(AnsiToken token)
        => AppliedToken.WithNoCellImpacts(token, 0, 0, 0, 0);

    private static AppliedToken Text(int row, int col, string text)
    {
        var impacts = new List<CellImpact>();
        for (var i = 0; i < text.Length; i++)
            impacts.Add(new CellImpact(col + i, row, new TerminalCell(text[i].ToString(), null, null, CellAttributes.None, 0)));
        return new AppliedToken(new TextToken(text), impacts, col, row, col + text.Length, row);
    }

    private static async Task<List<bool>> CaptureClassificationAsync(TerminalWidgetHandle handle, params AppliedToken[] tokens)
    {
        var classified = new List<bool>();
        handle.TerminalInvalidated += classified.Add;
        await handle.WriteOutputWithImpactsAsync(tokens);
        handle.TerminalInvalidated -= classified.Add;
        return classified;
    }

    // ---- Scheduler arbitration ----

    [TestMethod]
    public void TryBeginFrame_OnlyPresentationRequested_IsPresentationOnly()
    {
        using var scheduler = new FrameScheduler(TimeSpan.FromMilliseconds(16), new FakeTimeProvider());
        scheduler.RequestPresentationFrame();
        scheduler.RequestPresentationFrame();

        Assert.IsTrue(scheduler.TryBeginFrame(bypassPacing: true, out var presentationOnly));
        Assert.IsTrue(presentationOnly);
        Assert.IsFalse(scheduler.TryBeginFrame(bypassPacing: true, out _), "Presentation requests must coalesce.");
    }

    [TestMethod]
    public void TryBeginFrame_FullAndPresentationRequested_FullDominates()
    {
        using var scheduler = new FrameScheduler(TimeSpan.FromMilliseconds(16), new FakeTimeProvider());
        scheduler.RequestPresentationFrame();
        scheduler.RequestFrame();
        scheduler.RequestPresentationFrame();

        Assert.IsTrue(scheduler.TryBeginFrame(bypassPacing: true, out var presentationOnly));
        Assert.IsFalse(presentationOnly);
    }

    [TestMethod]
    public void TryBeginFrame_FullRequestedDuringPresentationFrame_NextFrameIsFull()
    {
        using var scheduler = new FrameScheduler(TimeSpan.FromMilliseconds(16), new FakeTimeProvider());
        scheduler.RequestPresentationFrame();
        Assert.IsTrue(scheduler.TryBeginFrame(bypassPacing: true, out var first));
        Assert.IsTrue(first);

        scheduler.RequestFrame();
        scheduler.RequestPresentationFrame();
        Assert.IsTrue(scheduler.TryBeginFrame(bypassPacing: true, out var second));
        Assert.IsFalse(second);

        scheduler.RequestPresentationFrame();
        Assert.IsTrue(scheduler.TryBeginFrame(bypassPacing: true, out var third));
        Assert.IsTrue(third, "A consumed full request must not leak into later frames.");
    }

    // ---- Handle classification ----

    [TestMethod]
    public async Task WriteOutputWithImpacts_CellText_RaisesContentOnlyAndPublicEventOnce()
    {
        var handle = new TerminalWidgetHandle(40, 10);
        var publicCount = 0;
        handle.OutputReceived += () => publicCount++;

        var classified = await CaptureClassificationAsync(handle, Text(0, 0, "hello"));

        TestSeq.AreEqual(new[] { true }, classified);
        Assert.AreEqual(1, publicCount);
    }

    [TestMethod]
    [DataRow(1000)]
    [DataRow(1002)]
    [DataRow(1003)]
    [DataRow(1006)]
    [DataRow(1049)]
    public async Task WriteOutputWithImpacts_HostVisibleModeChange_RaisesFull(int mode)
    {
        var handle = new TerminalWidgetHandle(40, 10);

        var enable = await CaptureClassificationAsync(handle, Applied(new PrivateModeToken(mode, true)), Text(0, 0, "x"));
        var repeat = await CaptureClassificationAsync(handle, Applied(new PrivateModeToken(mode, true)), Text(0, 0, "y"));
        var disable = await CaptureClassificationAsync(handle, Applied(new PrivateModeToken(mode, false)), Text(0, 0, "z"));

        TestSeq.AreEqual(new[] { false }, enable);
        TestSeq.AreEqual(new[] { true }, repeat);
        TestSeq.AreEqual(new[] { false }, disable);
    }

    [TestMethod]
    public async Task WriteOutputWithImpacts_Ris_RaisesFull()
    {
        var handle = new TerminalWidgetHandle(40, 10);

        var classified = await CaptureClassificationAsync(handle, Applied(RisToken.Instance), Text(0, 0, "x"));

        TestSeq.AreEqual(new[] { false }, classified);
    }

    [TestMethod]
    public async Task WriteOutputWithImpacts_TitleChange_RaisesFullOnlyWhenChanged()
    {
        var handle = new TerminalWidgetHandle(40, 10);

        var changed = await CaptureClassificationAsync(handle, Applied(new OscToken("2", "", "Title A")), Text(0, 0, "x"));
        var same = await CaptureClassificationAsync(handle, Applied(new OscToken("2", "", "Title A")), Text(0, 0, "y"));

        Assert.AreEqual("Title A", handle.WindowTitle);
        TestSeq.AreEqual(new[] { false }, changed);
        TestSeq.AreEqual(new[] { true }, same);
    }

    [TestMethod]
    public async Task LifecycleResetFallbackAndCopyMode_RaiseFull()
    {
        var handle = new TerminalWidgetHandle(40, 10);
        var classified = new List<bool>();
        handle.TerminalInvalidated += classified.Add;
        var lifecycle = (ITerminalLifecycleAwarePresentationAdapter)handle;

        lifecycle.TerminalStarted();
        await handle.WriteOutputAsync(new byte[] { (byte)'a' });
        ((IHex1bTerminalPresentationAdapter)handle).InvalidatePresentation();
        handle.EnterCopyMode();
        handle.ExitCopyMode();
        lifecycle.TerminalCompleted(0);
        handle.Reset();

        Assert.IsTrue(classified.Count >= 7);
        Assert.IsFalse(classified.Contains(true), "Lifecycle and host-state notifications must not be content-only.");
    }

    // ---- App integration ----

    private sealed class AppHarness : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private Task? _runTask;
        private int _builds;

        public required Hex1bTerminal Terminal { get; init; }
        public required TerminalWidgetHandle Handle { get; init; }
        public required HeadlessPresentationAdapter Presentation { get; init; }
        public Hex1bApp App { get; set; } = null!;
        public int Builds => Volatile.Read(ref _builds);

        public static AppHarness Create(Func<AppHarness, TerminalWidgetHandle, Hex1bWidget> build, int width = 80, int height = 24)
        {
            var handle = new TerminalWidgetHandle(60, 16);
            AppHarness? harness = null;
            var presentation = new HeadlessPresentationAdapter(width, height);
            var terminal = Hex1bTerminal.CreateBuilder()
                .WithHex1bApp(_ => { }, app =>
                {
                    harness!.App = app;
                    return _ =>
                    {
                        Interlocked.Increment(ref harness._builds);
                        return build(harness, handle);
                    };
                })
                .WithPresentation(presentation)
                .Build();
            harness = new AppHarness { Terminal = terminal, Handle = handle, Presentation = presentation };
            return harness;
        }

        public async Task StartAsync(string readyText)
        {
            _runTask = Terminal.RunAsync(_cts.Token);
            await WaitForTextAsync(readyText);
        }

        public Task WriteAsync(int row, int col, string text)
            => Handle.WriteOutputWithImpactsAsync([Text(row, col, text)]).AsTask();

        public async Task WaitForTextAsync(string text)
            => await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText(text), TimeSpan.FromSeconds(10))
                .Build()
                .ApplyAsync(Terminal);

        public async Task WaitUntilAsync(Func<bool> condition, string message)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline)
                    Assert.Fail(message);
                await Task.Delay(10);
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            if (_runTask is not null)
            {
                try { await _runTask; } catch { }
            }
            await Terminal.DisposeAsync();
            _cts.Dispose();
        }
    }

    private static Hex1bWidget Chrome(TerminalWidgetHandle handle)
        => new VStackWidget([
            new TextBlockWidget("Host Header"),
            new BorderWidget(new TerminalWidget(handle)).Title("Inner Terminal"),
        ]).Fill();

    [TestMethod]
    public async Task SustainedContentOutput_UsesPresentationFramesWithoutRebuilding()
    {
        await using var harness = AppHarness.Create((_, handle) => Chrome(handle));
        await harness.StartAsync("Host Header");
        await harness.WriteAsync(0, 0, "warmup");
        await harness.WaitForTextAsync("warmup");

        var buildsBefore = harness.Builds;
        var fullBefore = harness.App.FullFrameCount;
        var presentationBefore = harness.App.PresentationFrameCount;

        const int batches = 60;
        for (var i = 0; i < batches; i++)
        {
            await harness.WriteAsync(i % 16, 0, $"line-{i:D3}");
            await Task.Delay(5);
        }
        await harness.WaitForTextAsync($"line-{batches - 1:D3}");

        var builds = harness.Builds - buildsBefore;
        var full = harness.App.FullFrameCount - fullBefore;
        var presentation = harness.App.PresentationFrameCount - presentationBefore;
        Console.WriteLine($"Sustained output: {batches} batches -> {presentation} presentation frames, {full} full frames, {builds} builds");

        Assert.IsTrue(presentation >= 3, $"Expected presentation frames, got {presentation}.");
        Assert.AreEqual(0, full, "Content-only output must not run full frames.");
        Assert.AreEqual(0, builds, "Content-only output must not rebuild the host.");
        var screen = harness.Terminal.GetScreenText();
        StringAssert.Contains(screen, "Host Header");
        StringAssert.Contains(screen, "Inner Terminal");
    }

    [TestMethod]
    public async Task BuilderReadsHandleState_OutputStillRebuilds()
    {
        await using var harness = AppHarness.Create((_, handle) => new VStackWidget([
            new TextBlockWidget($"Cursor at {handle.CursorX}"),
            new TerminalWidget(handle),
        ]));
        await harness.StartAsync("Cursor at 0");

        var presentationBefore = harness.App.PresentationFrameCount;
        await harness.WriteAsync(0, 0, "abc");

        await harness.WaitForTextAsync("Cursor at 3");
        Assert.AreEqual(presentationBefore, harness.App.PresentationFrameCount,
            "Output from a handle the builder reads must keep full rebuild semantics.");
    }

    [TestMethod]
    public async Task ProcessCompleted_FallbackRebuilds()
    {
        await using var harness = AppHarness.Create((_, handle) =>
            new TerminalWidget(handle).WhenNotRunning(args => new TextBlockWidget($"Exited {args.ExitCode}")));
        ((ITerminalLifecycleAwarePresentationAdapter)harness.Handle).TerminalStarted();
        await harness.StartAsync("");
        await harness.WriteAsync(0, 0, "running");
        await harness.WaitForTextAsync("running");

        var buildsBefore = harness.Builds;
        ((ITerminalLifecycleAwarePresentationAdapter)harness.Handle).TerminalCompleted(7);

        await harness.WaitForTextAsync("Exited 7");
        Assert.IsTrue(harness.Builds > buildsBefore);
    }

    [TestMethod]
    public async Task Resize_RunsFullFrameThenResumesPresentationFrames()
    {
        await using var harness = AppHarness.Create((_, handle) => Chrome(handle));
        await harness.StartAsync("Host Header");

        var buildsBefore = harness.Builds;
        harness.Presentation.TriggerResize(100, 30);
        await harness.WaitUntilAsync(() => harness.Builds > buildsBefore, "Resize must rebuild the host.");

        await harness.WriteAsync(1, 0, "after-resize");
        await harness.WaitForTextAsync("after-resize");
        var buildsAfterResize = harness.Builds;
        var presentationBefore = harness.App.PresentationFrameCount;

        await harness.WriteAsync(2, 0, "content-only");
        await harness.WaitForTextAsync("content-only");
        Assert.IsTrue(harness.App.PresentationFrameCount > presentationBefore);
        Assert.AreEqual(buildsAfterResize, harness.Builds);
    }

    [TestMethod]
    public async Task Invalidate_DuringContentOutput_StillRebuilds()
    {
        var label = "first";
        await using var harness = AppHarness.Create((_, handle) => new VStackWidget([
            new TextBlockWidget($"Label {label}"),
            new TerminalWidget(handle),
        ]));
        await harness.StartAsync("Label first");

        label = "second";
        await harness.WriteAsync(0, 0, "x");
        harness.App.Invalidate();
        await harness.WriteAsync(0, 1, "y");

        await harness.WaitForTextAsync("Label second");
    }

    [TestMethod]
    public async Task RedrawAfter_AnimationSurvivesPresentationFrames()
    {
        await using var harness = AppHarness.Create((_, handle) => new VStackWidget([
            new TextBlockWidget("Animated").RedrawAfter(100),
            new TerminalWidget(handle),
        ]));
        await harness.StartAsync("Animated");

        var buildsBefore = harness.Builds;
        var presentationBefore = harness.App.PresentationFrameCount;
        var stop = DateTime.UtcNow + TimeSpan.FromMilliseconds(600);
        var i = 0;
        while (DateTime.UtcNow < stop)
        {
            await harness.WriteAsync(0, 0, $"tick-{i++:D4}");
            await Task.Delay(5);
        }

        Assert.IsTrue(harness.App.PresentationFrameCount > presentationBefore, "Output should produce presentation frames.");
        Assert.IsTrue(harness.Builds - buildsBefore >= 2,
            $"Owned redraw must keep firing across presentation frames; builds={harness.Builds - buildsBefore}.");
    }

    [TestMethod]
    public async Task OutputDuringBuild_IsPresented()
    {
        var wroteDuringBuild = 0;
        await using var harness = AppHarness.Create((h, handle) =>
        {
            if (h.Builds == 3 && Interlocked.Exchange(ref wroteDuringBuild, 1) == 0)
                handle.WriteOutputWithImpactsAsync([Text(3, 0, "written-during-build")]).AsTask().GetAwaiter().GetResult();
            return Chrome(handle);
        });
        await harness.StartAsync("Host Header");
        while (harness.Builds < 3)
        {
            harness.App.Invalidate();
            await Task.Delay(20);
        }

        await harness.WaitForTextAsync("written-during-build");
        Assert.AreEqual(1, wroteDuringBuild);
    }

    [TestMethod]
    public async Task OverlayAboveTerminal_RemainsCompositedOnPresentationFrames()
    {
        await using var harness = AppHarness.Create((_, handle) => new ZStackWidget([
            new TerminalWidget(handle),
            new TextBlockWidget("OVERLAY"),
        ]));
        await harness.StartAsync("OVERLAY");
        var presentationBefore = harness.App.PresentationFrameCount;

        await harness.WriteAsync(0, 0, "UNDERNEATH-TEXT");
        await harness.WriteAsync(2, 0, "visible-row");
        await harness.WaitForTextAsync("visible-row");

        Assert.IsTrue(harness.App.PresentationFrameCount > presentationBefore);
        var firstLine = harness.Terminal.GetScreenText().Split('\n')[0];
        Assert.IsTrue(firstLine.StartsWith("OVERLAY", StringComparison.Ordinal), $"Overlay must stay on top: '{firstLine}'");
    }

    [TestMethod]
    public async Task RepeatedFullFrames_KeepSingleTerminalSubscription()
    {
        await using var harness = AppHarness.Create((_, handle) => Chrome(handle));
        await harness.StartAsync("Host Header");
        for (var i = 0; i < 5; i++)
        {
            var builds = harness.Builds;
            harness.App.Invalidate();
            await harness.WaitUntilAsync(() => harness.Builds > builds, "Invalidate must rebuild.");
        }

        var field = typeof(TerminalWidgetHandle).GetField(nameof(TerminalWidgetHandle.TerminalInvalidated),
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var subscribers = ((Delegate?)field.GetValue(harness.Handle))?.GetInvocationList().Length ?? 0;
        Assert.AreEqual(1, subscribers);
    }
}
