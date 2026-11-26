using System.Text;
using System.Threading.Channels;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Tokens;

namespace Hex1b.Tests;

public partial class PresentationRawInputTests
{
    [TestMethod]
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
    public async Task WriteInputAsync_EffectiveFlagsRequireReportEventsAndAllKeys(int flags, bool expected)
    {
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities { SupportsKittyKeyboard = true });
        bool? reportsReleases = null;
        workload.SetInputSourceReportingHandler(value => reportsReleases = value);
        workload.EnterTuiMode();
        Assert.IsNull(reportsReleases, "Support and a queued push are not confirmation of effective flags.");
        await workload.WriteInputAsync(Encoding.ASCII.GetBytes($"\x1b[?{flags}u\x1b[97u\x1b[13u\x1b[9u\x1b[127u"));
        foreach (var key in new[] { Hex1bKey.A, Hex1bKey.Enter, Hex1bKey.Tab, Hex1bKey.Backspace })
            Assert.AreEqual(key, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Key);
        Assert.AreEqual(expected, reportsReleases);
        workload.ExitTuiMode();
    }

    [TestMethod]
    public async Task WriteInputAsync_QueuedLegacyKeyKeepsPreNegotiationSourceState()
    {
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities { SupportsKittyKeyboard = true });
        bool? reportsReleases = null;
        workload.SetInputSourceReportingHandler(value => reportsReleases = value);
        await workload.WriteInputAsync("a\r\t\x7f"u8.ToArray());
        workload.EnterTuiMode();
        await workload.WriteInputAsync("\x1b[?31u\x1b[13u"u8.ToArray());
        foreach (var key in new[] { Hex1bKey.A, Hex1bKey.Enter, Hex1bKey.Tab, Hex1bKey.Backspace })
        {
            Assert.AreEqual(key, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Key);
            Assert.IsNull(reportsReleases, "An already-queued legacy press must not inherit the new epoch.");
        }
        Assert.AreEqual(Hex1bKey.Enter, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Key);
        Assert.AreEqual(true, reportsReleases);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task KittyIngress_ReleaseReachesCustomRoutingWithoutExecutingBindings(bool raw)
    {
        await using var presentation = new QueuedPresentation();
        using var workload = new Hex1bAppWorkloadAdapter { RawInputTimeProvider = new RawClock() };
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            EscapeSequenceTimeout = TimeSpan.Zero
        });
        var node = new IngressNode { IsFocused = true };
        var ring = new FocusRing();
        ring.Rebuild(node);
        var state = new InputRouterState();
        var bytes = "\x1b[13;1:1u\x1b[13;1:2u\x1b[13;1:3uz"u8.ToArray();
        if (raw)
            await workload.WriteInputAsync(bytes);
        else
            presentation.Enqueue(bytes);
        for (var i = 0; i < 4; i++)
            await InputRouter.RouteInputAsync(node, await ReadAsync(workload.InputEvents), ring, state);
        Assert.AreEqual(2, node.Actions);
        var release = TestSeq.IsType<Hex1bKeyEvent>(node.Received[0]);
        Assert.AreEqual(Hex1bKey.Enter, release.Key);
        Assert.AreEqual(Hex1bKeyEventType.Release, release.EventType);
        Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(node.Received[1]).Text);
        Assert.HasCount(2, node.Received);
    }

    [TestMethod]
    public void InputSequenceParser_UnterminatedCsi_RetainsLessThanFourKiB()
    {
        var parser = new InputSequenceParser();
        parser.Parse("\x1b[", enhanced: true);
        for (var i = 0; i < 8192; i++)
        {
            parser.Parse("1", enhanced: true);
            Assert.IsLessThan(4096, parser.PendingLength);
        }
        var tokens = parser.Parse("\x1b[97u", enhanced: true);
        Assert.AreEqual(Hex1bKey.A, TestSeq.IsType<Hex1bKeyEvent>(
            Hex1bTerminal.TokenToEvent(TestSeq.Single(tokens))).Key);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task KittyIngress_OneByteReads_PreserveEventsUtf8AndOrdering(bool raw)
    {
        await using var presentation = new QueuedPresentation();
        using var workload = new Hex1bAppWorkloadAdapter { RawInputTimeProvider = new RawClock() };
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            EscapeSequenceTimeout = TimeSpan.Zero
        });
        foreach (var value in "a\x1b[97:65;2:2;65u\x1b[97;1:3ué😀z"u8.ToArray())
        {
            if (raw)
                await workload.WriteInputAsync(new byte[] { value });
            else
                presentation.Enqueue([value]);
        }
        Assert.AreEqual("a", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
        var repeat = TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents));
        Assert.AreEqual(Hex1bKey.A, repeat.Key);
        Assert.AreEqual(Hex1bKeyEventType.Repeat, repeat.EventType);
        Assert.AreEqual(Hex1bModifiers.Shift, repeat.Modifiers);
        Assert.AreEqual(65, repeat.ShiftedKeyCode);
        Assert.AreEqual("A", repeat.Text);
        var release = TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents));
        Assert.AreEqual(Hex1bKey.A, release.Key);
        Assert.AreEqual(Hex1bKeyEventType.Release, release.EventType);
        Assert.AreEqual("é", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
        Assert.AreEqual("😀", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
        Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
        Assert.IsFalse(workload.InputEvents.TryRead(out _));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task KittyIngress_PasteIsOpaqueAcrossEveryBoundary(bool raw)
    {
        const string content = "é\x1b[97;1:3u\x1b[200~\x1b]unfinished\x1b_Gprotocol\r\n😀";
        var bytes = Encoding.UTF8.GetBytes("\x1b[200~" + content + "\x1b[201~z");
        for (var split = 1; split < bytes.Length; split++)
        {
            await using var presentation = new QueuedPresentation();
            using var workload = new Hex1bAppWorkloadAdapter { RawInputTimeProvider = new RawClock() };
            await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
            {
                PresentationAdapter = presentation, WorkloadAdapter = workload,
                EscapeSequenceTimeout = TimeSpan.Zero
            });
            var producer = Task.Run(async () =>
            {
                if (raw)
                {
                    await workload.WriteInputAsync(bytes.AsMemory(0, split));
                    await workload.WriteInputAsync(bytes.AsMemory(split));
                }
                else
                {
                    presentation.Enqueue(bytes[..split]);
                    presentation.Enqueue(bytes[split..]);
                }
            });
            var paste = TestSeq.IsType<Hex1bPasteEvent>(await ReadAsync(workload.InputEvents)).Paste;
            Assert.AreEqual(content, await paste.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(5)), $"split {split}");
            Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
            await producer.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(workload.InputEvents.TryRead(out _));
            await paste.DisposeAsync();
        }
    }

    [TestMethod]
    [DataRow(0, false)]
    [DataRow(32, false)]
    [DataRow(64, false)]
    [DataRow(96, false)]
    [DataRow(33, true)]
    [DataRow(1, true)]
    [DataRow(2, true)]
    [DataRow(4, true)]
    [DataRow(8, true)]
    [DataRow(16, true)]
    public async Task KittyIngress_OnlyKnownFlagsDisambiguateCursorReport(int flags, bool enhanced)
    {
        await using var presentation = new QueuedPresentation();
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            EscapeSequenceTimeout = TimeSpan.Zero
        });
        terminal.ApplyTokens(AnsiTokenizer.Tokenize($"\x1b[={flags}u"));
        Assert.AreEqual(flags, terminal.InputModes.KittyKeyboardFlags);
        presentation.Enqueue("\x1b[1;5R\x1b[13;5:2~z"u8.ToArray());
        if (!enhanced)
        {
            var f3 = TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents));
            Assert.AreEqual(Hex1bKey.F3, f3.Key);
            Assert.AreEqual(Hex1bModifiers.Control, f3.Modifiers);
        }
        var repeat = TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents));
        Assert.AreEqual(Hex1bKey.F3, repeat.Key);
        Assert.AreEqual(Hex1bKeyEventType.Repeat, repeat.EventType);
        Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
        Assert.IsFalse(workload.InputEvents.TryRead(out _));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task WriteInputAsync_NegotiationControlsCprNotLegacyF3(bool supported)
    {
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities { SupportsKittyKeyboard = supported });
        workload.EnterTuiMode();
        await workload.WriteInputAsync("\x1b[1;5R\x1b[13~z"u8.ToArray());
        if (!supported)
            Assert.AreEqual(Hex1bKey.F3, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Key);
        Assert.AreEqual(Hex1bKey.F3, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Key);
        Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
        Assert.IsFalse(workload.InputEvents.TryRead(out _));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task KittyIngress_FinishedPrivateMalformedAndOversizedCsi_AllowNextInput(bool raw)
    {
        await using var presentation = new QueuedPresentation();
        using var workload = new Hex1bAppWorkloadAdapter { RawInputTimeProvider = new RawClock() };
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            EscapeSequenceTimeout = TimeSpan.Zero
        });
        var chunks = new[]
        {
            "\x1b[?2026;2$y", "\x1b[?997;1n", "\x1b[97;1:9u",
            "\x1b[" + new string('1', 4094), "\x1b[97u", "z"
        };
        foreach (var chunk in chunks)
        {
            if (raw)
                await workload.WriteInputAsync(Encoding.UTF8.GetBytes(chunk));
            else
                presentation.Enqueue(Encoding.UTF8.GetBytes(chunk));
        }
        Assert.AreEqual(Hex1bKey.A, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Key);
        Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
        Assert.IsFalse(workload.InputEvents.TryRead(out _));
    }

    [TestMethod]
    public async Task WriteInputAsync_EscapeTimeoutAndStaleCallbacks_PreserveOrdering()
    {
        var clock = new RawClock();
        using var workload = new Hex1bAppWorkloadAdapter { RawInputTimeProvider = clock };
        await workload.WriteInputAsync("\x1b"u8.ToArray());
        await workload.WriteInputAsync("[97"u8.ToArray());
        clock.Fire(0);
        Assert.IsFalse(workload.InputEvents.TryRead(out _));
        await workload.WriteInputAsync("u"u8.ToArray());
        Assert.AreEqual(Hex1bKey.A, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Key);
        await workload.WriteInputAsync("\x1b"u8.ToArray());
        clock.FireLast();
        Assert.AreEqual(Hex1bKey.Escape, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Key);
        await workload.WriteInputAsync("\x1b["u8.ToArray());
        clock.FireLast();
        var alt = TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents));
        Assert.AreEqual("[", alt.Text);
        Assert.AreEqual(Hex1bModifiers.Alt, alt.Modifiers);
        await workload.WriteInputAsync("z"u8.ToArray());
        Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
        Assert.IsFalse(workload.InputEvents.TryRead(out _));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task WriteInputAsync_BlockedPaste_CancellationAndDisposalUnblockWriter(bool dispose)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await workload.WriteInputAsync("\x1b[200~"u8.ToArray());
        var paste = TestSeq.IsType<Hex1bPasteEvent>(await ReadAsync(workload.InputEvents)).Paste;
        for (var i = 0; i < 64; i++)
            await workload.WriteInputAsync("x"u8.ToArray());
        using var cancellation = new CancellationTokenSource();
        var blocked = workload.WriteInputAsync("x\x1b[201~"u8.ToArray(), cancellation.Token).AsTask();
        Assert.IsFalse(blocked.IsCompleted);
        if (dispose)
        {
            workload.Dispose();
            await blocked.WaitAsync(TimeSpan.FromSeconds(5));
        }
        else
        {
            cancellation.Cancel();
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                async () => await blocked.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        Assert.IsTrue(paste.IsCancelled);
        await paste.DisposeAsync();
    }

    private sealed class RawClock : TimeProvider
    {
        private readonly List<(TimerCallback Callback, object? State)> _callbacks = [];
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Assert.AreEqual(TimeSpan.FromMilliseconds(50), dueTime);
            Assert.AreEqual(Timeout.InfiniteTimeSpan, period);
            _callbacks.Add((callback, state));
            return new RawTimer();
        }
        public void Fire(int index) => _callbacks[index].Callback(_callbacks[index].State);
        public void FireLast() => Fire(_callbacks.Count - 1);
        private sealed class RawTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class IngressNode : Hex1bNode
    {
        public int Actions { get; private set; }
        public List<Hex1bEvent> Received { get; } = [];
        public override bool IsFocusable => true;
        public override bool IsFocused { get; set; }
        public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
            => bindings.Key(Hex1bKey.Enter).Action(() => Actions++);
        public override InputResult HandleInput(Hex1bEvent inputEvent)
        {
            Received.Add(inputEvent);
            return InputResult.Handled;
        }
        protected override Size MeasureCore(Constraints constraints) => new(1, 1);
        public override void Render(Hex1bRenderContext context) { }
    }
}
