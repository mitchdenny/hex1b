using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using Hex1b.Tokens;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hex1b.Tests.Tape;

[TestClass]
public class TerminalCaptureTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task BeginCaptureAsync_AlreadyRunningIdleTerminal_SeedsThenStreamsAndDetaches(bool filtered)
    {
        var workload = new ControlledWorkload();
        var builder = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 6);
        using var idleRecorder = new AsciinemaRecorder();
        if (filtered)
            builder.AddWorkloadFilter(idleRecorder);
        await using var terminal = builder.Build();
        await workload.WriteAsync("before");
        var events = new ConcurrentQueue<TerminalCaptureEvent>();
        await using var capture = await terminal.BeginCaptureAsync((item, _) =>
        {
            events.Enqueue(item);
            return ValueTask.CompletedTask;
        }, Cancellation);

        Assert.IsTrue(capture.InitialState.Snapshot.ContainsText("before"));
        Assert.AreEqual(TimeSpan.Zero, events.Single().Elapsed);
        Assert.AreEqual(TerminalCaptureEventKind.State, events.Single().Kind);
        await workload.WriteAsync("after");
        using var final = await capture.DetachAsync(Cancellation);
        Assert.IsTrue(final.Snapshot.ContainsText("beforeafter"));
        Assert.AreEqual("after", events.Last().Output);
        await workload.WriteAsync("alive");
        Assert.AreEqual(2, events.Count);
        Assert.IsFalse(final.Snapshot.ContainsText("alive"));
        using var alive = terminal.CreateSnapshot();
        Assert.IsTrue(alive.ContainsText("beforeafteralive"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task BeginCaptureAsync_SplitInputAtAttachment_PublishesCompleteTokenOnce(bool utf8)
    {
        var workload = new ControlledWorkload();
        await using var terminal = CreateTerminal(workload);
        await workload.WriteAsync(utf8 ? [0xc3] : Encoding.UTF8.GetBytes("\x1b[3"));
        var events = new ConcurrentQueue<TerminalCaptureEvent>();
        await using var capture = await terminal.BeginCaptureAsync((item, _) =>
        {
            events.Enqueue(item);
            return ValueTask.CompletedTask;
        }, Cancellation);
        await workload.WriteAsync(utf8 ? [0xa9] : Encoding.UTF8.GetBytes("1mred"));
        using var final = await capture.DetachAsync(Cancellation);

        var item = events.Last();
        Assert.AreEqual(2, events.Count);
        Assert.AreEqual(utf8 ? "é" : "\x1b[31mred", item.Output);
        Assert.IsTrue(final.Snapshot.ContainsText(utf8 ? "é" : "red"));
    }

    [TestMethod]
    public async Task Capture_PooledPretokenizedOutput_OwnsDataBeforeListIsReturned()
    {
        var workload = new ControlledWorkload();
        await using var terminal = CreateTerminal(workload);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string? captured = null;
        await using var capture = await terminal.BeginCaptureAsync(async (item, ct) =>
        {
            if (item.Kind != TerminalCaptureEventKind.Output)
                return;
            entered.TrySetResult();
            await release.Task.WaitAsync(ct);
            captured = item.Output;
        }, Cancellation);

        await workload.WriteAsync(Encoding.UTF8.GetBytes("owned"), pretokenized: true);
        await entered.Task.WaitAsync(Cancellation);
        release.TrySetResult();
        using var barrier = await capture.BarrierAsync(Cancellation);
        Assert.AreEqual("owned", captured);
    }

    [TestMethod]
    public async Task SetVisibilityAsync_HiddenOutputAndResize_ResynchronizesBeforeFurtherOutput()
    {
        var workload = new ControlledWorkload();
        await using var terminal = CreateTerminal(workload);
        var events = new ConcurrentQueue<TerminalCaptureEvent>();
        await using var capture = await terminal.BeginCaptureAsync((item, _) =>
        {
            events.Enqueue(item);
            return ValueTask.CompletedTask;
        }, Cancellation);
        await workload.WriteAsync("visible");
        using var hidden = await capture.SetVisibilityAsync(false, Cancellation);
        await workload.WriteAsync("\rhidden!");
        terminal.Resize(30, 5);
        using var hiddenBarrier = await capture.BarrierAsync(Cancellation);
        Assert.AreEqual(2, events.Count);
        Assert.IsTrue(hiddenBarrier.Snapshot.ContainsText("hidden!"));

        using var shown = await capture.SetVisibilityAsync(true, Cancellation);
        await workload.WriteAsync("+");
        using var final = await capture.DetachAsync(Cancellation);
        var observed = events.ToArray();
        Assert.AreEqual(4, observed.Length);
        Assert.AreEqual(TerminalCaptureEventKind.State, observed[2].Kind);
        Assert.AreEqual(30, observed[2].Width);
        Assert.AreEqual(5, observed[2].Height);
        Assert.AreEqual("+", observed[3].Output);
        Assert.IsTrue(observed.Zip(observed.Skip(1)).All(pair =>
            pair.First.Sequence < pair.Second.Sequence && pair.First.Elapsed <= pair.Second.Elapsed));
        await AssertReplayMatchesAsync(observed, final.Snapshot);
    }

    [TestMethod]
    public async Task Capture_VisibleResize_PreservesOutputOrdering()
    {
        var workload = new ControlledWorkload();
        await using var terminal = CreateTerminal(workload);
        var events = new ConcurrentQueue<TerminalCaptureEvent>();
        await using var capture = await terminal.BeginCaptureAsync((item, _) =>
        {
            events.Enqueue(item);
            return ValueTask.CompletedTask;
        }, Cancellation);
        await workload.WriteAsync("first");
        terminal.Resize(40, 8);
        await workload.WriteAsync("second");
        using var final = await capture.DetachAsync(Cancellation);
        TestSeq.AreEqual(
            new[] { TerminalCaptureEventKind.State, TerminalCaptureEventKind.Output,
                TerminalCaptureEventKind.Resize, TerminalCaptureEventKind.Output },
            events.Select(item => item.Kind));
        Assert.AreEqual(40, events.ElementAt(2).Width);
        await AssertReplayMatchesAsync(events, final.Snapshot);
    }

    [TestMethod]
    [DataRow("12345678901234567890", "Z")]
    [DataRow("\x1b[31mred", "continued")]
    [DataRow("main\x1b[?1049h\x1b[2J\x1b[Halt", "\x1b[?1049l!")]
    [DataRow("\x1b[2;4r\x1b[?6h\x1b[2;3Hinside", "\r\nnext")]
    [DataRow("\x1b[2;3Hsaved\u001b7\x1b[1;1Hother", "\u001b8X")]
    [DataRow("\x1b[?69h\x1b[3;17s\x1b[?6htext", "\r\nnext")]
    [DataRow("\x1b[8mhidden\x1b[0m\x1b[4:3;58;2;5;6;7munder", "!")]
    [DataRow("\x1b]8;id=a;https://example.test\x1b\\link", "ed")]
    [DataRow("\x1b[3g\x1b[1;5H\x1bH\x1b[H", "\tX")]
    [DataRow("\x1b(0lqk", "x")]
    [DataRow("", "\x1b[31m\x1b[3b")]
    [DataRow("Z\r\x1b[K", "\x1b[3b")]
    [DataRow("R\x1b[32m\x1b[3;3H", "\x1b[3b")]
    [DataRow("👩\u200d", "💻")]
    [DataRow("12345678901234567890\u001b7\x1b[3;1Hx", "\u001b8Z")]
    [DataRow("p\x1b[1\"qQ\x1b[0\"qR", "\r\x1b[?2K")]
    [DataRow("p\x1b[1\"qQ\x1b[0\"qR", "\r\x1b[?2J")]
    [DataRow("p\x1bVQ\x1bWR", "\r\x1b[2K")]
    [DataRow("\x1b[?25l\x1b[5 q\x1b[?1h\x1b[?2004h\x1b[?1000h\x1b[?1006h", "text")]
    [DataRow("before", "\u001bDafter")]
    [DataRow("", "\x1b*0\x1bnlqk")]
    [DataRow("", "\x1b+0\x1bolqk")]
    public async Task BeginCaptureAsync_TextContinuationState_ReplaysFaithfully(string before, string after)
    {
        var workload = new ControlledWorkload();
        await using var terminal = CreateTerminal(workload);
        await workload.WriteAsync(before);
        var events = new ConcurrentQueue<TerminalCaptureEvent>();
        await using var capture = await terminal.BeginCaptureAsync((item, _) =>
        {
            events.Enqueue(item);
            return ValueTask.CompletedTask;
        }, Cancellation);
        await workload.WriteAsync(after);
        using var final = await capture.DetachAsync(Cancellation);
        await AssertReplayMatchesAsync(events, final.Snapshot);
    }

    [TestMethod]
    public async Task BeginCaptureAsync_StreamingProducer_InitialStateAndEventsCoverEveryWrite()
    {
        var workload = new ControlledWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(200, 5).Build();
        await workload.WriteAsync("x");
        var producer = Task.Run(async () =>
        {
            for (var index = 0; index < 100; index++)
                await workload.WriteAsync("x");
        }, Cancellation);
        var events = new ConcurrentQueue<TerminalCaptureEvent>();
        await using var capture = await terminal.BeginCaptureAsync((item, _) =>
        {
            events.Enqueue(item);
            return ValueTask.CompletedTask;
        }, Cancellation);
        await producer;
        using var final = await capture.DetachAsync(Cancellation);
        var initialCount = Enumerable.Range(0, 200)
            .Count(column => capture.InitialState.Snapshot.GetCell(column, 0).Character == "x");
        var streamedCount = events.Where(item => item.Kind == TerminalCaptureEventKind.Output)
            .Sum(item => item.Output.Length);
        Assert.AreEqual(101, initialCount + streamedCount);
        await AssertReplayMatchesAsync(events, final.Snapshot);
    }

    [TestMethod]
    public async Task Capture_ObserverFailure_DetachesWithoutStoppingTerminal()
    {
        var workload = new ControlledWorkload();
        await using var terminal = CreateTerminal(workload);
        var failure = new IOException("capture failed");
        await using var capture = await terminal.BeginCaptureAsync((item, _) =>
            item.Kind == TerminalCaptureEventKind.Output
                ? ValueTask.FromException(failure)
                : ValueTask.CompletedTask, Cancellation);
        await workload.WriteAsync("first");
        var observed = await Assert.ThrowsExactlyAsync<IOException>(() => capture.Completion.WaitAsync(Cancellation));
        Assert.AreSame(failure, observed);
        await workload.WriteAsync("second");
        using var snapshot = terminal.CreateSnapshot();
        Assert.IsTrue(snapshot.ContainsText("firstsecond"));
        await Assert.ThrowsExactlyAsync<IOException>(() => capture.BarrierAsync(Cancellation));
    }

    [TestMethod]
    public async Task BeginCaptureAsync_CancelledDuringInitialObserver_RemovesObserverAndKeepsTerminalAlive()
    {
        var workload = new ControlledWorkload();
        await using var terminal = CreateTerminal(workload);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var beginning = terminal.BeginCaptureAsync(async (_, ct) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
        }, cancellation.Token);
        await entered.Task.WaitAsync(Cancellation);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => beginning);
        await workload.WriteAsync("alive");
        await using var next = await terminal.BeginCaptureAsync((_, _) => ValueTask.CompletedTask, Cancellation);
        Assert.IsTrue(next.InitialState.Snapshot.ContainsText("alive"));
    }

    [TestMethod]
    public async Task Capture_TerminalDisposed_CompletesScopeWithExplicitFailure()
    {
        var workload = new ControlledWorkload();
        await using var terminal = CreateTerminal(workload);
        await using var capture = await terminal.BeginCaptureAsync((_, _) => ValueTask.CompletedTask, Cancellation);
        await terminal.DisposeAsync();
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => capture.Completion.WaitAsync(Cancellation));
    }

    [TestMethod]
    public async Task BeginCaptureAsync_ResidentSixel_RejectsUnfaithfulSeed()
    {
        var workload = new ControlledWorkload();
        await using var terminal = CreateTerminal(workload);
        await workload.WriteAsync("\x1bPq#1;2;100;0;0#1~\x1b\\");
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() =>
            terminal.BeginCaptureAsync((_, _) => ValueTask.CompletedTask, Cancellation));
        await workload.WriteAsync("\x1b[Hstill alive");
        using var snapshot = terminal.CreateSnapshot();
        Assert.IsTrue(snapshot.ContainsText("still alive"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task BeginCaptureAsync_ResidentKgpIncludingInactiveMainScreen_RejectsUnfaithfulSeed(bool alternate)
    {
        var workload = new ControlledWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithHeadless(new TerminalCapabilities { SupportsKgp = true }).WithDimensions(20, 6).Build();
        await workload.WriteAsync("\x1b_Ga=t,f=32,s=1,v=1,i=7,q=2;AQID/w==\x1b\\");
        if (alternate)
            await workload.WriteAsync("\x1b[?1049h");
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() =>
            terminal.BeginCaptureAsync((_, _) => ValueTask.CompletedTask, Cancellation));
    }

    [TestMethod]
    public async Task BeginCaptureAsync_SplitDcs_ObservesOriginalProtocolAfterCompletion()
    {
        var workload = new ControlledWorkload();
        await using var terminal = CreateTerminal(workload);
        const string prefix = "\x1bPq#1;2;100;0;0#1";
        const string suffix = "~\x1b\\";
        await workload.WriteAsync(prefix);
        var events = new ConcurrentQueue<TerminalCaptureEvent>();
        await using var capture = await terminal.BeginCaptureAsync((item, _) =>
        {
            events.Enqueue(item);
            return ValueTask.CompletedTask;
        }, Cancellation);
        await workload.WriteAsync(suffix);
        using var final = await capture.DetachAsync(Cancellation);
        Assert.AreEqual(prefix + suffix, events.Last().Output);
        Assert.AreEqual(2, events.Count);
    }

    [TestMethod]
    public async Task SetVisibilityAsync_GraphicsCreatedWhileHidden_FailsWithoutDetachingOtherScopes()
    {
        var workload = new ControlledWorkload();
        await using var terminal = CreateTerminal(workload);
        var firstEvents = new ConcurrentQueue<TerminalCaptureEvent>();
        await using var first = await terminal.BeginCaptureAsync((item, _) =>
        {
            firstEvents.Enqueue(item);
            return ValueTask.CompletedTask;
        }, Cancellation);
        await using var second = await terminal.BeginCaptureAsync((_, _) => ValueTask.CompletedTask, Cancellation);
        using var hidden = await first.SetVisibilityAsync(false, Cancellation);
        await workload.WriteAsync("\x1bPq#1;2;100;0;0#1~\x1b\\");
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => first.SetVisibilityAsync(true, Cancellation));
        using var secondFinal = await second.DetachAsync(Cancellation);
        using var firstFinal = await first.DetachAsync(Cancellation);
        Assert.AreEqual(1, firstEvents.Count);
    }

    [TestMethod]
    public async Task SetVisibilityAsync_AlternateScreenResizedWhileHidden_RetainsCroppedMainScreen()
    {
        var workload = new ControlledWorkload();
        await using var terminal = CreateTerminal(workload);
        await workload.WriteAsync("12345678901234567890\x1b[?1049h\x1b[Halt");
        var events = new ConcurrentQueue<TerminalCaptureEvent>();
        await using var capture = await terminal.BeginCaptureAsync((item, _) =>
        {
            events.Enqueue(item);
            return ValueTask.CompletedTask;
        }, Cancellation);
        using var hidden = await capture.SetVisibilityAsync(false, Cancellation);
        await terminal.ResizeForAutomationAsync(10, 4, Cancellation);
        using var visible = await capture.SetVisibilityAsync(true, Cancellation);
        await workload.WriteAsync("\x1b[?1049l");
        using var final = await capture.DetachAsync(Cancellation);
        await AssertReplayMatchesAsync(events, final.Snapshot);
    }

    private static Hex1bTerminal CreateTerminal(ControlledWorkload workload) =>
        Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 6).Build();

    [TestMethod]
    public async Task BarrierAsync_WithScrollback_OwnsIndependentViewportAndBufferStartSnapshots()
    {
        var workload = new ControlledWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithHeadless().WithDimensions(20, 6).WithScrollback(20).Build();
        await workload.WriteAsync("one\r\ntwo\r\nthree\r\nfour\r\nfive\r\nsix\r\nseven");
        await using var capture = await terminal.BeginCaptureAsync((_, _) => ValueTask.CompletedTask, Cancellation);
        using var boundary = await capture.BarrierAsync(Cancellation);
        Assert.AreEqual("two", boundary.Snapshot.GetLine(0).TrimEnd());
        Assert.AreEqual("one", boundary.BufferStartSnapshot.GetLine(0).TrimEnd());
        await workload.WriteAsync("\x1b[3J\x1b[Hchanged");
        Assert.AreEqual("one", boundary.BufferStartSnapshot.GetLine(0).TrimEnd());
        boundary.BufferStartSnapshot.Dispose();
        await workload.WriteAsync("\r\neight");
        Assert.AreEqual("two", boundary.Snapshot.GetLine(0).TrimEnd());
    }

    [TestMethod]
    public async Task AutomationTimeProvider_WithoutCapture_UsesConfiguredClock()
    {
        var clock = new FakeTimeProvider();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new ControlledWorkload())
            .WithHeadless().WithTimeProvider(clock).Build();
        Assert.AreSame(clock, terminal.AutomationTimeProvider);
        Assert.IsTrue(terminal.SupportsAutomationResize);
    }

    [TestMethod]
    public async Task Capture_FakeTimeProvider_UsesAttachmentOriginAndDeterministicVisibilityTimes()
    {
        var clock = new FakeTimeProvider();
        var workload = new ControlledWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithHeadless().WithDimensions(20, 6).WithTimeProvider(clock).Build();
        clock.Advance(TimeSpan.FromHours(8));
        var events = new ConcurrentQueue<TerminalCaptureEvent>();
        await using var capture = await terminal.BeginCaptureAsync((item, _) =>
        {
            events.Enqueue(item);
            return ValueTask.CompletedTask;
        }, Cancellation);
        Assert.AreEqual(TimeSpan.Zero, capture.InitialState.Elapsed);
        clock.Advance(TimeSpan.FromSeconds(2));
        await workload.WriteAsync("first");
        using var hidden = await capture.SetVisibilityAsync(false, Cancellation);
        Assert.AreEqual(TimeSpan.FromSeconds(2), hidden.Elapsed);
        clock.Advance(TimeSpan.FromSeconds(10));
        await workload.WriteAsync("secret");
        using var visible = await capture.SetVisibilityAsync(true, Cancellation);
        Assert.AreEqual(TimeSpan.FromSeconds(12), visible.Elapsed);
        clock.Advance(TimeSpan.FromSeconds(3));
        await workload.WriteAsync("last");
        using var final = await capture.DetachAsync(Cancellation);
        Assert.AreEqual(TimeSpan.FromSeconds(15), final.Elapsed);
        TestSeq.AreEqual(new[] { 0d, 2d, 12d, 15d }, events.Select(item => item.Elapsed.TotalSeconds));
        Assert.AreEqual(TimeSpan.FromSeconds(5), final.Elapsed - (visible.Elapsed - hidden.Elapsed));
    }

    [TestMethod]
    public async Task ResizeForAutomationAsync_PendingWorkloadAcknowledgement_AwaitsWithoutBlockingOutput()
    {
        var workload = new ControlledWorkload();
        await using var terminal = CreateTerminal(workload);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var acknowledged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        workload.ResizeHandler = async (width, height, ct) =>
        {
            Assert.AreEqual(30, width);
            Assert.AreEqual(5, height);
            entered.TrySetResult();
            await acknowledged.Task.WaitAsync(ct);
        };

        var resize = terminal.ResizeForAutomationAsync(30, 5, Cancellation);
        await entered.Task.WaitAsync(Cancellation);
        Assert.IsFalse(resize.IsCompleted);
        await workload.WriteAsync("responsive");
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(30, snapshot.Width);
        Assert.IsTrue(snapshot.ContainsText("responsive"));
        acknowledged.TrySetResult();
        await resize;
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ResizeForAutomationAsync_NotificationFailure_PropagatesOriginalError(bool filterFailure)
    {
        var workload = new ControlledWorkload();
        var error = new IOException("resize failed");
        var builder = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 6);
        if (filterFailure)
            builder.AddWorkloadFilter(new FailingResizeFilter(error));
        await using var terminal = builder.Build();
        if (!filterFailure)
            workload.ResizeHandler = (_, _, _) => ValueTask.FromException(error);

        var observed = await Assert.ThrowsExactlyAsync<IOException>(() =>
            terminal.ResizeForAutomationAsync(30, 5, Cancellation));
        Assert.AreSame(error, observed);
    }

    [TestMethod]
    public async Task ResizeForAutomationAsync_CancelledBeforeResize_DoesNotChangeGeometry()
    {
        var workload = new ControlledWorkload();
        await using var terminal = CreateTerminal(workload);
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            terminal.ResizeForAutomationAsync(30, 5, new CancellationToken(canceled: true)));
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(20, snapshot.Width);
        Assert.AreEqual(6, snapshot.Height);
    }

    [TestMethod]
    public async Task ResizeForAutomationAsync_Hmp1Workload_RejectsUnconfirmedGeometry()
    {
        var remote = new Hmp1WorkloadAdapter(new Hmp1ClientOptions
        {
            StreamFactory = _ => throw new InvalidOperationException("The test must not connect.")
        });
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(remote)
            .WithHeadless().WithDimensions(20, 6).Build();
        Assert.IsFalse(terminal.SupportsAutomationResize);
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() =>
            terminal.ResizeForAutomationAsync(30, 5, Cancellation));
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(20, snapshot.Width);
        Assert.AreEqual(6, snapshot.Height);
    }

    private static async Task AssertReplayMatchesAsync(
        IEnumerable<TerminalCaptureEvent> events, Hex1bTerminalSnapshot expected)
    {
        var observations = events.ToArray();
        await using var replay = Hex1bTerminal.CreateBuilder().WithWorkload(new ControlledWorkload())
            .WithHeadless().WithDimensions(observations[0].Width, observations[0].Height).Build();
        foreach (var item in observations)
        {
            if (item.Kind is TerminalCaptureEventKind.State or TerminalCaptureEventKind.Resize)
                replay.Resize(item.Width, item.Height);
            replay.ApplyTokens(AnsiTokenizer.Tokenize(item.Output));
        }
        using var actual = replay.CreateSnapshot();
        Assert.AreEqual(expected.Width, actual.Width);
        Assert.AreEqual(expected.Height, actual.Height);
        Assert.AreEqual(expected.CursorX, actual.CursorX, "cursor X");
        Assert.AreEqual(expected.CursorY, actual.CursorY, "cursor Y");
        Assert.AreEqual(expected.InAlternateScreen, actual.InAlternateScreen);
        Assert.AreEqual(expected.CursorVisible, actual.CursorVisible);
        Assert.AreEqual(expected.CursorShape, actual.CursorShape);
        Assert.AreEqual(expected.ApplicationCursorKeysEnabled, actual.ApplicationCursorKeysEnabled);
        Assert.AreEqual(expected.BracketedPasteEnabled, actual.BracketedPasteEnabled);
        Assert.AreEqual(expected.MouseProtocolNormalEnabled, actual.MouseProtocolNormalEnabled);
        Assert.AreEqual(expected.MouseEncodingSgrEnabled, actual.MouseEncodingSgrEnabled);
        for (var row = 0; row < expected.Height; row++)
        {
            for (var column = 0; column < expected.Width; column++)
            {
                var target = expected.GetCell(column, row);
                var result = actual.GetCell(column, row);
                Assert.AreEqual(target.Character, result.Character, $"character ({column},{row})");
                Assert.AreEqual(target.Foreground, result.Foreground, $"foreground ({column},{row})");
                Assert.AreEqual(target.Background, result.Background, $"background ({column},{row})");
                Assert.AreEqual(target.Attributes, result.Attributes, $"attributes ({column},{row})");
                Assert.AreEqual(target.UnderlineStyle, result.UnderlineStyle);
                Assert.AreEqual(target.UnderlineColor, result.UnderlineColor);
                Assert.AreEqual(target.HyperlinkData?.Uri, result.HyperlinkData?.Uri);
            }
        }
    }

    private sealed class ControlledWorkload : IHex1bTerminalTokenWorkloadAdapter
    {
        private readonly Channel<WorkloadOutputItem> _output = Channel.CreateUnbounded<WorkloadOutputItem>();

        public event Action? Disconnected { add { } remove { } }
        internal Func<int, int, CancellationToken, ValueTask>? ResizeHandler { get; set; }

        internal Task WriteAsync(string text) => WriteAsync(Encoding.UTF8.GetBytes(text));

        internal async Task WriteAsync(byte[] bytes, bool pretokenized = false)
        {
            var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var buffer = ArrayPool<byte>.Shared.Rent(bytes.Length);
            bytes.CopyTo(buffer, 0);
            var tokens = pretokenized ? AnsiTokenizer.Tokenize(Encoding.UTF8.GetString(bytes)).ToList() : [];
            await _output.Writer.WriteAsync(new WorkloadOutputItem(buffer.AsMemory(0, bytes.Length),
                pretokenized ? tokens : null)
            {
                PooledBuffer = buffer,
                PooledTokens = tokens,
                PooledTokensReturn = list =>
                {
                    list.Clear();
                    list.Add(new TextToken("returned"));
                    returned.TrySetResult();
                }
            }, Cancellation);
            await returned.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancellation);
        }

        public ValueTask<WorkloadOutputItem> ReadOutputItemAsync(CancellationToken ct = default) =>
            _output.Reader.ReadAsync(ct);

        public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default) =>
            (await ReadOutputItemAsync(ct)).Bytes;

        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            ValueTask.CompletedTask;

        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default) =>
            ResizeHandler?.Invoke(width, height, ct) ?? ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            _output.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FailingResizeFilter(Exception error) : IHex1bTerminalWorkloadFilter
    {
        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default) =>
            ValueTask.CompletedTask;
        public ValueTask OnOutputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default) =>
            ValueTask.CompletedTask;
        public ValueTask OnFrameCompleteAsync(TimeSpan elapsed, CancellationToken ct = default) =>
            ValueTask.CompletedTask;
        public ValueTask OnInputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default) =>
            ValueTask.CompletedTask;
        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default) =>
            ValueTask.FromException(error);
        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default) =>
            ValueTask.CompletedTask;
    }
}
