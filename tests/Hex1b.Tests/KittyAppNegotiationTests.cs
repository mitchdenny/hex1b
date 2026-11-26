using System.Text;
using System.Threading.Channels;
using Hex1b.Tokens;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class KittyAppNegotiationTests
{
    [TestMethod]
    public async Task RunAsync_SupportWithoutHostReply_DoesNotConfirmEmulatedKeyboardStack()
    {
        await using var presentation = new KeyboardHost(supported: true) { ReportedFlags = null };
        Hex1bApp? app = null;
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(_ => { }, instance =>
            {
                app = instance;
                return context => context.Text("Unconfirmed source");
            })
            .WithPresentation(presentation).WithDimensions(30, 8).Build();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var run = terminal.RunAsync(cancellation.Token);
        try
        {
            using var ready = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Unconfirmed source") && terminal.InputModes.KittyKeyboardFlags == 31,
                    TimeSpan.FromSeconds(5), "unconfirmed source ready")
                .Build().ApplyAsync(terminal);
            Assert.IsFalse(((IHex1bTerminalPresentationAdapter)presentation).AnswersProtocolQueriesDirectly);
            Assert.IsNull(app!.InputSourceReportsKeyReleases,
                "The terminal's emulated 31 must not stand in for the physical source's effective flags.");
            app.RequestStop();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            cancellation.Cancel();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestMethod]
    public async Task PresentationInput_LegacyKeysInFlight_AreDeliveredBeforeEffectiveFlagBoundary()
    {
        await using var presentation = new KeyboardHost(supported: true);
        using var workload = new Hex1bAppWorkloadAdapter(presentation);
        bool? reportsReleases = null;
        workload.SetInputSourceReportingHandler(value => reportsReleases = value);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var filter = new PassThroughFilter { InputGate = gate.Task };
        var options = new Hex1bTerminalOptions { PresentationAdapter = presentation, WorkloadAdapter = workload };
        options.PresentationFilters.Add(filter);
        await using var terminal = new Hex1bTerminal(options);
        try
        {
            presentation.EnqueueInput("a\r\t\x7f"u8.ToArray());
            await filter.InputSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));
            workload.EnterTuiMode();
            await presentation.Pushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsNull(reportsReleases);
            gate.TrySetResult();
            foreach (var key in new[] { Hex1bKey.A, Hex1bKey.Enter, Hex1bKey.Tab, Hex1bKey.Backspace })
            {
                Assert.AreEqual(key, TestSeq.IsType<Hex1bKeyEvent>(
                    await workload.InputEvents.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5))).Key);
                Assert.IsNull(reportsReleases, "Input read before the query must retain its old source epoch.");
            }
            presentation.EnqueueInput("z"u8.ToArray());
            Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(
                await workload.InputEvents.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5))).Text);
            Assert.AreEqual(true, reportsReleases);
        }
        finally
        {
            gate.TrySetResult();
        }
    }

    // The Kitty reply has no request ID. A report already read before the push/query was
    // handed to the presentation (here held by a filter until after EnterTuiMode) cannot
    // answer it, so held legacy keys must stay legacy rather than gaining live ownership.
    [TestMethod]
    public async Task PresentationInput_ReportReadBeforeQueryDelivery_CannotConfirmLaterEpoch()
    {
        await using var presentation = new KeyboardHost(supported: true) { ReportedFlags = null };
        using var workload = new Hex1bAppWorkloadAdapter(presentation);
        bool? reportsReleases = null;
        workload.SetInputSourceReportingHandler(value => reportsReleases = value);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var filter = new PassThroughFilter
        {
            InputGate = gate.Task,
            GateOn = t => t is UnrecognizedSequenceToken { Sequence: "\x1b[?31u" }
        };
        var options = new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            EscapeSequenceTimeout = TimeSpan.Zero
        };
        options.PresentationFilters.Add(filter);
        await using var terminal = new Hex1bTerminal(options);
        try
        {
            presentation.EnqueueInput("\x1b[?31ua"u8.ToArray());
            await filter.InputSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));
            workload.EnterTuiMode();
            await presentation.Pushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            gate.TrySetResult();
            Assert.AreEqual(Hex1bKey.A, TestSeq.IsType<Hex1bKeyEvent>(
                await workload.InputEvents.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5))).Key);
            presentation.EnqueueInput("\x1b[1;5Rz"u8.ToArray());
            var f3 = TestSeq.IsType<Hex1bKeyEvent>(
                await workload.InputEvents.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(Hex1bKey.F3, f3.Key);
            Assert.AreEqual(Hex1bModifiers.Control, f3.Modifiers);
            Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(
                await workload.InputEvents.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5))).Text);
            Assert.IsNull(workload.ConfirmedEnhancedInput);
            Assert.IsNull(reportsReleases);

            // A report read after delivery is accepted (and is indistinguishable from a stale one).
            presentation.EnqueueInput("\x1b[?31u\x1b[1;5Rz"u8.ToArray());
            Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(
                await workload.InputEvents.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5))).Text);
            Assert.AreEqual(true, workload.ConfirmedEnhancedInput);
            Assert.AreEqual(true, reportsReleases);
        }
        finally
        {
            gate.TrySetResult();
        }
    }

    [TestMethod]
    public async Task RunAsync_PopWriteFailure_StillRestoresRawModeAndSurfacesError()
    {
        await using var presentation = new KeyboardHost(supported: true) { FailPop = true };
        using var workload = new Hex1bAppWorkloadAdapter(presentation);
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            RunCallback = async ct =>
            {
                workload.EnterTuiMode();
                await presentation.Pushed.Task.WaitAsync(ct);
                return 0;
            }
        });
        var error = await Assert.ThrowsExactlyAsync<IOException>(
            () => terminal.RunAsync().WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.AreEqual("pop write rejected", error.Message);
        Assert.IsTrue(presentation.Trace.IndexOf("pop-failed") < presentation.Trace.IndexOf("raw-exit"));
        await terminal.DisposeAsync();
        Assert.AreEqual(1, presentation.Trace.Count(t => t == "pop-failed"), "The pop is attempted once.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Dispose_PopWriteFailure_StillCompletesCleanupAndSurfacesError(bool synchronous)
    {
        await using var presentation = new KeyboardHost(supported: true) { FailPop = true };
        var workload = new Hex1bAppWorkloadAdapter(presentation);
        var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload
        });
        workload.EnterTuiMode();
        await presentation.Pushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var error = synchronous
            ? Assert.ThrowsExactly<IOException>(terminal.Dispose)
            : await Assert.ThrowsExactlyAsync<IOException>(async () => await terminal.DisposeAsync());
        Assert.AreEqual("pop write rejected", error.Message);
        var failed = presentation.Trace.IndexOf("pop-failed");
        Assert.IsTrue(failed >= 0 && failed < presentation.Trace.IndexOf("alt-exit"),
            "Exit sequences are still written after the failed pop.");
        Assert.IsTrue(presentation.Trace.IndexOf("alt-exit") < presentation.Trace.IndexOf("raw-exit"));
        Assert.IsTrue(workload.IsDisposed);
        await terminal.DisposeAsync();
        Assert.AreEqual(1, presentation.Trace.Count(t => t == "pop-failed"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Dispose_PopAndEveryLaterWriteFail_StillRestoresRawModeAndDisposesResources(bool synchronous)
    {
        await using var presentation = new KeyboardHost(supported: true) { FailPop = true, FailAllAfterPop = true };
        var workload = new Hex1bAppWorkloadAdapter(presentation);
        var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload
        });
        workload.EnterTuiMode();
        await presentation.Pushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var error = synchronous
            ? Assert.ThrowsExactly<AggregateException>(terminal.Dispose)
            : await Assert.ThrowsExactlyAsync<AggregateException>(async () => await terminal.DisposeAsync());
        var messages = error.InnerExceptions.Select(e => TestSeq.IsType<IOException>(e).Message).ToArray();
        Assert.AreEqual("pop write rejected", messages[0]);
        Assert.Contains("broken pipe", messages, "The exit-sequence write failure is preserved.");
        Assert.Contains("write-failed", presentation.Trace);
        Assert.Contains("flush-failed", presentation.Trace);
        Assert.IsTrue(presentation.Trace.IndexOf("write-failed") < presentation.Trace.IndexOf("raw-exit"),
            "Raw mode is restored after the exit write fails.");
        Assert.AreEqual(1, presentation.DisposeCount);
        Assert.IsTrue(workload.IsDisposed);
        if (synchronous)
            terminal.Dispose();
        else
            await terminal.DisposeAsync();
        Assert.AreEqual(1, presentation.Trace.Count(t => t == "raw-exit"), "A second dispose is a no-op.");
        Assert.AreEqual(1, presentation.DisposeCount);
    }

    [TestMethod]
    [DataRow(Hex1bKey.A, 97, "a")]
    [DataRow(Hex1bKey.Enter, 13, "\r")]
    [DataRow(Hex1bKey.Tab, 9, "\t")]
    [DataRow(Hex1bKey.Backspace, 127, "")]
    public async Task RunAsync_ConfirmedPushProtectsFirstHeldKey_AndPopDemotesIt(Hex1bKey key, int codepoint, string text)
    {
        await using var childWorkload = new ChildWorkload();
        await using var child = Hex1bTerminal.CreateBuilder().WithWorkload(childWorkload)
            .WithDimensions(24, 4).WithTerminalWidget(out var handle).Build();
        using var childCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var childRun = child.RunAsync(childCancellation.Token);
        child.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[>11u"));
        await using var presentation = new KeyboardHost(supported: true) { AdvertiseSupport = false };
        Hex1bApp? app = null;
        Hex1bAppWorkloadAdapter? appInput = null;
        await using var host = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                options => appInput = TestSeq.IsType<Hex1bAppWorkloadAdapter>(options.WorkloadAdapter),
                instance =>
                {
                    app = instance;
                    return _ => new VStackWidget([new TextBlockWidget("Source ready"), new TerminalWidget(handle)]);
                })
            .WithPresentation(presentation).WithDimensions(30, 8).Build();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var run = host.RunAsync(cancellation.Token);
        try
        {
            using var ready = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Source ready"), TimeSpan.FromSeconds(5), "source app ready")
                .Build().ApplyAsync(host);
            Assert.IsNull(app!.InputSourceReportsKeyReleases);
            foreach (var code in Enumerable.Range(0x4e00, TerminalWidgetHandle.MaxForwardedKeys - 1))
            {
                appInput!.TryWriteInputEvent(Press(code));
                Assert.AreEqual($"\x1b[{code}u", await childWorkload.ReadAsync());
            }
            appInput!.ExitTuiMode();
            presentation.AdvertiseSupport = true;
            appInput.EnterTuiMode();
            using var negotiated = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(_ => app.InputSourceReportsKeyReleases == true, TimeSpan.FromSeconds(5), "accepted release reporting")
                .Build().ApplyAsync(host);
            var held = new Hex1bKeyEvent(key, text, Hex1bModifiers.None) { UnicodeKeyCode = codepoint };
            appInput.TryWriteInputEvent(held);
            Assert.AreEqual($"\x1b[{codepoint}u", await childWorkload.ReadAsync());
            foreach (var code in Enumerable.Range(0x6000, TerminalWidgetHandle.MaxForwardedKeys + 20))
            {
                appInput.TryWriteInputEvent(Press(code));
                Assert.AreEqual($"\x1b[{code}u", await childWorkload.ReadAsync());
                appInput.TryWriteInputEvent(Press(code) with { Text = "", EventType = Hex1bKeyEventType.Release });
                Assert.AreEqual($"\x1b[{code};1:3u", await childWorkload.ReadAsync());
            }
            Assert.IsTrue(handle.OwnsForwardedKey((key, null)), "First held key survives stale overflow before its release.");
            appInput.ExitTuiMode();
            using var popped = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(_ => app.InputSourceReportsKeyReleases == false, TimeSpan.FromSeconds(5), "popped release reporting")
                .Build().ApplyAsync(host);
            foreach (var code in Enumerable.Range(0x7000, TerminalWidgetHandle.MaxForwardedKeys))
            {
                appInput.TryWriteInputEvent(Press(code));
                Assert.AreEqual($"\x1b[{code}u", await childWorkload.ReadAsync());
            }
            Assert.IsFalse(handle.OwnsForwardedKey((key, null)), "Pop demotes the prior live press to bounded bookkeeping.");
            Assert.IsFalse(childWorkload.Input.Reader.TryRead(out _), "Demotion never synthesizes a release.");
            app.RequestStop();
            await run.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            cancellation.Cancel();
            childCancellation.Cancel();
            await run.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => childRun.WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    private static Hex1bKeyEvent Press(int code)
        => Hex1bKeyEvent.FromText(char.ConvertFromUtf32(code)) with { UnicodeKeyCode = code };

    [TestMethod]
    [DataRow(null, null)]
    [DataRow(0, false)]
    [DataRow(1, false)]
    [DataRow(2, false)]
    [DataRow(4, false)]
    [DataRow(8, false)]
    [DataRow(16, false)]
    [DataRow(10, true)]
    [DataRow(31, true)]
    [DataRow(32, false)]
    [DataRow(64, false)]
    [DataRow(96, false)]
    [DataRow(33, false)]
    public async Task PresentationInput_EffectiveHostFlags_NotWriteAcceptance_EstablishReleaseGuarantee(int? flags, bool? expected)
    {
        await using var presentation = new KeyboardHost(supported: true) { ReportedFlags = flags };
        using var workload = new Hex1bAppWorkloadAdapter(presentation);
        bool? reportsReleases = null;
        workload.SetInputSourceReportingHandler(value => reportsReleases = value);
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload
        });
        workload.EnterTuiMode();
        await presentation.Pushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var processed = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => terminal.InputModes.KittyKeyboardFlags == 31, TimeSpan.FromSeconds(5), "keyboard query processed")
            .Build().ApplyAsync(terminal);
        presentation.EnqueueInput("\x1b[1;5Rz"u8.ToArray());
        if (flags is not { } reportedFlags || (reportedFlags & 31) == 0)
            Assert.AreEqual(Hex1bKey.F3, TestSeq.IsType<Hex1bKeyEvent>(
                await workload.InputEvents.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5))).Key);
        Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(
            await workload.InputEvents.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5))).Text);
        Assert.AreEqual(expected, reportsReleases);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EnterExitTuiMode_CapabilityControlsBalancedPushPop(bool supported)
    {
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities { SupportsKittyKeyboard = supported });
        workload.ExitTuiMode();
        Assert.IsFalse(workload.TryReadOutput(out _));
        workload.EnterTuiMode();
        workload.EnterTuiMode();
        Assert.IsTrue(workload.TryReadOutput(out var enter));
        Assert.IsFalse(workload.TryReadOutput(out _));
        var text = Encoding.UTF8.GetString(enter.Span);
        Assert.AreEqual(supported, text.Contains("\x1b[>31u", StringComparison.Ordinal));
        if (supported)
            Assert.IsTrue(text.IndexOf("\x1b[?1049h", StringComparison.Ordinal) < text.IndexOf("\x1b[>31u", StringComparison.Ordinal));
        workload.ExitTuiMode();
        workload.ExitTuiMode();
        Assert.IsTrue(workload.TryReadOutput(out var exit));
        text = Encoding.UTF8.GetString(exit.Span);
        Assert.AreEqual(supported, text.Contains("\x1b[<u", StringComparison.Ordinal));
        if (supported)
            Assert.IsTrue(text.IndexOf("\x1b[<u", StringComparison.Ordinal) < text.IndexOf("\x1b[?1049l", StringComparison.Ordinal));
        Assert.IsFalse(workload.TryReadOutput(out _));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunAsync_NormalExit_DrainsPopBeforeRawModeExit(bool filtered)
    {
        await using var presentation = new KeyboardHost(supported: true);
        using var workload = new Hex1bAppWorkloadAdapter(presentation);
        var options = new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            RunCallback = _ =>
            {
                workload.EnterTuiMode();
                workload.ExitTuiMode();
                workload.Dispose();
                return Task.FromResult(0);
            }
        };
        if (filtered)
            options.PresentationFilters.Add(new PassThroughFilter());
        await using var terminal = new Hex1bTerminal(options);
        Assert.AreEqual(0, await terminal.RunAsync().WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(1, presentation.Pushes);
        Assert.AreEqual(1, presentation.Pops);
        Assert.AreEqual(8, presentation.FlagsAfterPop);
        Assert.AreEqual(4, presentation.Host.InputModes.KittyKeyboardFlags);
        Assert.IsTrue(presentation.Trace.IndexOf("pop") < presentation.Trace.IndexOf("alt-exit"));
        Assert.IsTrue(presentation.Trace.IndexOf("alt-exit") < presentation.Trace.IndexOf("raw-exit"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Dispose_AfterSuccessfulPush_RestoresPreexistingStacks(bool synchronous)
    {
        await using var presentation = new KeyboardHost(supported: true);
        using var workload = new Hex1bAppWorkloadAdapter(presentation);
        var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload
        });
        workload.EnterTuiMode();
        await presentation.Pushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (synchronous)
            terminal.Dispose();
        else
            await terminal.DisposeAsync();
        Assert.AreEqual(1, presentation.Pushes);
        Assert.AreEqual(1, presentation.Pops);
        Assert.AreEqual(8, presentation.FlagsAfterPop);
        Assert.AreEqual(4, presentation.Host.InputModes.KittyKeyboardFlags);
        Assert.IsTrue(presentation.Trace.IndexOf("pop") < presentation.Trace.IndexOf("alt-exit"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunAsync_CancellationOrFailure_AfterPushRestoresOwnership(bool fail)
    {
        await using var presentation = new KeyboardHost(supported: true);
        using var workload = new Hex1bAppWorkloadAdapter(presentation);
        using var cancellation = new CancellationTokenSource();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            RunCallback = async ct =>
            {
                workload.EnterTuiMode();
                await presentation.Pushed.Task.WaitAsync(ct);
                if (fail)
                    throw new IOException("workload failed");
                await Task.Delay(Timeout.Infinite, ct);
                return 0;
            }
        });
        var run = terminal.RunAsync(cancellation.Token);
        await presentation.Pushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (fail)
            await Assert.ThrowsExactlyAsync<IOException>(async () => await run.WaitAsync(TimeSpan.FromSeconds(5)));
        else
        {
            cancellation.Cancel();
            await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () => await run.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        await terminal.DisposeAsync();
        Assert.AreEqual(1, presentation.Pops);
        Assert.AreEqual(8, presentation.FlagsAfterPop);
        Assert.IsTrue(presentation.Trace.IndexOf("pop") < presentation.Trace.IndexOf("raw-exit"));
    }

    [TestMethod]
    public async Task RunAsync_PushWriteFails_DoesNotPopUnownedHostStack()
    {
        await using var presentation = new KeyboardHost(supported: true) { FailPush = true };
        using var workload = new Hex1bAppWorkloadAdapter(presentation);
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            RunCallback = async ct =>
            {
                workload.EnterTuiMode();
                await Task.Delay(Timeout.Infinite, ct);
                return 0;
            }
        });
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await terminal.RunAsync().WaitAsync(TimeSpan.FromSeconds(5)));
        await terminal.DisposeAsync();
        Assert.AreEqual(0, presentation.Pushes);
        Assert.AreEqual(0, presentation.Pops);
        Assert.AreEqual(4, presentation.Host.InputModes.KittyKeyboardFlags);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunAsync_DelayedCapabilityStartup_AwaitsBeforeAppNegotiates(bool supported)
    {
        await using var presentation = new KeyboardHost(supported) { DelayStartup = true };
        using var workload = new Hex1bAppWorkloadAdapter(presentation);
        var called = false;
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            RunCallback = _ =>
            {
                called = true;
                workload.EnterTuiMode();
                workload.ExitTuiMode();
                workload.Dispose();
                return Task.FromResult(0);
            }
        });
        var run = terminal.RunAsync();
        await presentation.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsFalse(called);
        presentation.Ready.TrySetResult();
        Assert.AreEqual(0, await run.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(supported ? 1 : 0, presentation.Pushes);
        Assert.AreEqual(supported ? 1 : 0, presentation.Pops);
        Assert.AreEqual(1, presentation.RawEntries);
    }

    [TestMethod]
    public async Task Start_DelayedRawMode_DoesNotPresentAppOutputBeforeReady()
    {
        await using var presentation = new KeyboardHost(supported: true) { DelayStartup = true };
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities { SupportsKittyKeyboard = true });
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload
        });
        await presentation.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        workload.EnterTuiMode();
        Assert.AreEqual(0, presentation.Pushes);
        presentation.Ready.TrySetResult();
        await presentation.Pushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await terminal.DisposeAsync();
        Assert.AreEqual(1, presentation.RawEntries);
        Assert.AreEqual(1, presentation.Pops);
    }

    private sealed class KeyboardHost(bool supported) : IHex1bTerminalPresentationAdapter
    {
        public Hex1bTerminal Host { get; } = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().Build();
        public TaskCompletionSource Pushed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool DelayStartup { get; init; }
        public bool FailPush { get; init; }
        public bool FailPop { get; init; }
        public bool FailAllAfterPop { get; init; }
        private bool _broken;
        public int DisposeCount { get; private set; }
        public bool AdvertiseSupport { get; set; } = true;
        public int? ReportedFlags { get; init; } = 31;
        private readonly Channel<ReadOnlyMemory<byte>> _input = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        public void EnqueueInput(byte[] data) => Assert.IsTrue(_input.Writer.TryWrite(data));
        private bool _ready;
        private bool _initialized;
        public int Pushes { get; private set; }
        public int Pops { get; private set; }
        public int FlagsAfterPop { get; private set; }
        public int RawEntries { get; private set; }
        public List<string> Trace { get; } = [];
        public int Width => 80;
        public int Height => 24;
        public TerminalCapabilities Capabilities => new()
        {
            SupportsKittyKeyboard = supported && AdvertiseSupport && (!DelayStartup || _ready)
        };
        public event Action<int, int>? Resized { add { } remove { } }
        public event Action? Disconnected { add { } remove { } }
        public async ValueTask EnterRawModeAsync(CancellationToken ct = default)
        {
            RawEntries++;
            Started.TrySetResult();
            if (DelayStartup)
                await Ready.Task.WaitAsync(ct);
            _ready = true;
        }
        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            if (!_initialized)
            {
                Host.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[=4u\x1b[?1049h\x1b[=8u\x1b[?1049l"));
                _initialized = true;
            }
            if (_broken)
            {
                Trace.Add("write-failed");
                throw new IOException("broken pipe");
            }
            var text = Encoding.UTF8.GetString(data.Span);
            if (FailPush && text.Contains("\x1b[>31u", StringComparison.Ordinal))
                throw new IOException("push write rejected");
            if (FailPop && text.Contains("\x1b[<u", StringComparison.Ordinal))
            {
                Trace.Add("pop-failed");
                _broken = FailAllAfterPop;
                throw new IOException("pop write rejected");
            }
            foreach (var token in AnsiTokenizer.Tokenize(text))
            {
                Host.ApplyTokens([token]);
                if (token is UnrecognizedSequenceToken { Sequence: "\x1b[>31u" })
                {
                    Pushes++;
                    Trace.Add("push");
                }
                if (token is UnrecognizedSequenceToken { Sequence: "\x1b[<u" })
                {
                    Pops++;
                    FlagsAfterPop = Host.InputModes.KittyKeyboardFlags;
                    Trace.Add("pop");
                }
                if (token is PrivateModeToken { Mode: 1049, Enable: false })
                    Trace.Add("alt-exit");
            }
            if (Pushes > 0)
                Pushed.TrySetResult();
            if (text.Contains("\x1b[?u", StringComparison.Ordinal) && ReportedFlags is int flags)
                _input.Writer.TryWrite(Encoding.ASCII.GetBytes($"\x1b[?{flags}u"));
            return ValueTask.CompletedTask;
        }
        public ValueTask ExitRawModeAsync(CancellationToken ct = default)
        {
            Trace.Add("raw-exit");
            return ValueTask.CompletedTask;
        }
        public ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
            => _input.Reader.ReadAsync(ct);
        public ValueTask FlushAsync(CancellationToken ct = default)
        {
            if (_broken)
            {
                Trace.Add("flush-failed");
                throw new IOException("broken pipe");
            }
            return ValueTask.CompletedTask;
        }
        public (int Row, int Column) GetCursorPosition() => (0, 0);
        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return Host.DisposeAsync();
        }
    }

    private sealed class PassThroughFilter : IHex1bTerminalPresentationFilter
    {
        public Task? InputGate { get; init; }
        public Func<AnsiToken, bool> GateOn { get; init; } = t => t is TextToken { Text: "a" };
        public TaskCompletionSource InputSeen { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask<IReadOnlyList<AnsiToken>> OnOutputAsync(
            IReadOnlyList<AppliedToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.FromResult<IReadOnlyList<AnsiToken>>(tokens.Select(t => t.Token).ToArray());
        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public async ValueTask OnInputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
        {
            if (InputGate is not null && tokens.Any(GateOn))
            {
                InputSeen.TrySetResult();
                await InputGate.WaitAsync(ct);
            }
        }
        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
    }

    private sealed class ChildWorkload : IHex1bTerminalWorkloadAdapter
    {
        public Channel<string> Input { get; } = Channel.CreateUnbounded<string>();
        private readonly Channel<ReadOnlyMemory<byte>> _output = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        public async Task<string> ReadAsync()
            => await Input.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            => Input.Writer.WriteAsync(Encoding.UTF8.GetString(data.Span), ct);
        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
            => _output.Reader.ReadAsync(ct);
        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default) => ValueTask.CompletedTask;
        public event Action? Disconnected { add { } remove { } }
        public ValueTask DisposeAsync()
        {
            Input.Writer.TryComplete();
            _output.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
