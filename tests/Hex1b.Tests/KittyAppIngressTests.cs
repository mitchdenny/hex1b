using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks.Sources;
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
        DeliverQueuedOutput(workload);
        await workload.WriteInputAsync(Encoding.ASCII.GetBytes($"\x1b[?{flags}u\x1b[1;5R\x1b[97u\x1b[13u\x1b[9u\x1b[127u"));
        if ((flags & 31) == 0)
            Assert.AreEqual(Hex1bKey.F3, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Key);
        foreach (var key in new[] { Hex1bKey.A, Hex1bKey.Enter, Hex1bKey.Tab, Hex1bKey.Backspace })
            Assert.AreEqual(key, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Key);
        Assert.AreEqual(expected, reportsReleases);
        workload.ExitTuiMode();
        await workload.WriteInputAsync("\x1b[?31u\x1b[97u"u8.ToArray());
        Assert.AreEqual(Hex1bKey.A, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Key);
        Assert.AreEqual(false, reportsReleases, "A report after pop cannot restore release guarantees.");
    }

    [TestMethod]
    public async Task WriteInputAsync_QueuedLegacyKeyKeepsPreNegotiationSourceState()
    {
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities { SupportsKittyKeyboard = true });
        bool? reportsReleases = null;
        workload.SetInputSourceReportingHandler(value => reportsReleases = value);
        await workload.WriteInputAsync("a\r\t\x7f"u8.ToArray());
        workload.EnterTuiMode();
        DeliverQueuedOutput(workload);
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
        Assert.IsEmpty(parser.Parse("\x1b[", enhanced: true));
        for (var i = 0; i < 8192; i++)
        {
            Assert.IsEmpty(parser.Parse("1", enhanced: true), $"Leaked oversized CSI byte {i}.");
            Assert.IsLessThan(4096, parser.PendingLength);
        }
        Assert.IsEmpty(parser.Parse("123u", enhanced: true), "The remainder ends the oversized CSI.");
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

    // Raw-child/ParsedEvents workloads that are not app adapters drive this terminal's
    // mode directly, so the terminal-local stack remains authoritative for them.
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
    public async Task PresentationIngress_NonAppWorkload_TerminalLocalKnownFlagsDisambiguateCursorReport(int flags, bool enhanced)
    {
        await using var presentation = new QueuedPresentation();
        var workload = new RecordingEventWorkload();
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
            var f3 = TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.Events));
            Assert.AreEqual(Hex1bKey.F3, f3.Key);
            Assert.AreEqual(Hex1bModifiers.Control, f3.Modifiers);
        }
        var repeat = TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.Events));
        Assert.AreEqual(Hex1bKey.F3, repeat.Key);
        Assert.AreEqual(Hex1bKeyEventType.Repeat, repeat.EventType);
        Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.Events)).Text);
        Assert.IsFalse(workload.Events.TryRead(out _));
    }

    // An app adapter's own requested mode is not the host's effective mode, so the
    // terminal-local stack alone must not suppress legacy Ctrl+F3.
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task KittyIngress_AppAdapter_TerminalLocalFlagsDoNotConfirmHost(bool raw)
    {
        await using var presentation = new QueuedPresentation();
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities { SupportsKittyKeyboard = true })
        {
            RawInputTimeProvider = new RawClock()
        };
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            EscapeSequenceTimeout = TimeSpan.Zero
        });
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[=31u"));
        workload.EnterTuiMode();
        await WriteIngressAsync(raw, presentation, workload, "\x1b[1;5Rz");
        Assert.AreEqual(Hex1bKey.F3, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Key);
        Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
        Assert.IsFalse(workload.InputEvents.TryRead(out _));
    }

    // Kitty spec: the program must not assume the protocol is active until the terminal
    // answers CSI ? u. Until then (or with only unknown/reserved bits) CSI 1;5R stays the
    // legacy xterm Ctrl+F3 encoding; afterwards Kitty's F3 is CSI 13~ and the same bytes
    // can only be a cursor-position report.
    [TestMethod]
    [DataRow(false, 0, false)]
    [DataRow(false, 32, false)]
    [DataRow(false, 64, false)]
    [DataRow(false, 96, false)]
    [DataRow(false, 1, true)]
    [DataRow(false, 2, true)]
    [DataRow(false, 4, true)]
    [DataRow(false, 8, true)]
    [DataRow(false, 16, true)]
    [DataRow(false, 33, true)]
    [DataRow(false, 31, true)]
    [DataRow(true, 0, false)]
    [DataRow(true, 32, false)]
    [DataRow(true, 64, false)]
    [DataRow(true, 96, false)]
    [DataRow(true, 1, true)]
    [DataRow(true, 2, true)]
    [DataRow(true, 4, true)]
    [DataRow(true, 8, true)]
    [DataRow(true, 16, true)]
    [DataRow(true, 33, true)]
    [DataRow(true, 31, true)]
    public async Task KittyIngress_SupportedButUnconfirmedHost_KeepsLegacyF3UntilKnownReport(bool raw, int flags, bool enhanced)
    {
        await using var presentation = new QueuedPresentation();
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities { SupportsKittyKeyboard = true })
        {
            RawInputTimeProvider = new RawClock()
        };
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            EscapeSequenceTimeout = TimeSpan.Zero
        });
        workload.EnterTuiMode();

        // Push queued and written, host silent so far: an F3 already in flight is legacy.
        await WriteIngressAsync(raw, presentation, workload, "\x1b[1;5R");
        AssertCtrlF3(await ReadAsync(workload.InputEvents));
        Assert.IsNull(workload.ConfirmedEnhancedInput);

        // Same read: in-flight F3, then the confirming reply, then a later F3.
        await presentation.QueryDelivered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WriteIngressAsync(raw, presentation, workload, $"\x1b[1;5R\x1b[?{flags}u\x1b[1;5R\x1b[13~z");
        AssertCtrlF3(await ReadAsync(workload.InputEvents));
        if (!enhanced)
            AssertCtrlF3(await ReadAsync(workload.InputEvents));
        Assert.AreEqual(Hex1bKey.F3, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Key);
        Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
        Assert.IsFalse(workload.InputEvents.TryRead(out _));
        Assert.AreEqual(enhanced, workload.ConfirmedEnhancedInput);

        // After pop the confirmation is gone and legacy decoding returns.
        workload.ExitTuiMode();
        await WriteIngressAsync(raw, presentation, workload, "\x1b[1;5Rz");
        AssertCtrlF3(await ReadAsync(workload.InputEvents));
        Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task KittyIngress_SilentHostThatNeverReplies_StaysLegacy(bool raw)
    {
        await using var presentation = new QueuedPresentation();
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities { SupportsKittyKeyboard = true })
        {
            RawInputTimeProvider = new RawClock()
        };
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            EscapeSequenceTimeout = TimeSpan.Zero
        });
        workload.EnterTuiMode();
        for (var i = 0; i < 3; i++)
        {
            await WriteIngressAsync(raw, presentation, workload, "\x1b[1;5R");
            AssertCtrlF3(await ReadAsync(workload.InputEvents));
        }
        await WriteIngressAsync(raw, presentation, workload, "\x1b[13~z");
        Assert.AreEqual(Hex1bKey.F3, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Key);
        Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
        Assert.IsFalse(workload.InputEvents.TryRead(out _));
        Assert.IsNull(workload.ConfirmedEnhancedInput);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task WriteInputAsync_UnsupportedOrUnconfirmedNegotiation_KeepsLegacyF3(bool supported)
    {
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities { SupportsKittyKeyboard = supported });
        workload.EnterTuiMode();
        await workload.WriteInputAsync("\x1b[1;5R\x1b[13~z"u8.ToArray());
        AssertCtrlF3(await ReadAsync(workload.InputEvents));
        Assert.AreEqual(Hex1bKey.F3, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Key);
        Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
        Assert.IsFalse(workload.InputEvents.TryRead(out _));
    }

    private static void DeliverQueuedOutput(Hex1bAppWorkloadAdapter workload)
    {
        while (workload.TryReadOutput(out _)) { }
    }

    // Raw ingress can only order a reply against the query leaving the adapter's output queue.
    [TestMethod]
    public async Task WriteInputAsync_ReportBeforeQueryDequeued_CannotConfirmEpoch()
    {
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities { SupportsKittyKeyboard = true });
        bool? reportsReleases = null;
        workload.SetInputSourceReportingHandler(value => reportsReleases = value);
        workload.EnterTuiMode();

        await workload.WriteInputAsync("\x1b[?31u\x1b[1;5R"u8.ToArray());
        AssertCtrlF3(await ReadAsync(workload.InputEvents));
        Assert.IsNull(workload.ConfirmedEnhancedInput);

        // A report split across delivery keeps the stamp of its first (pre-delivery) byte.
        await workload.WriteInputAsync("\x1b[?3"u8.ToArray());
        DeliverQueuedOutput(workload);
        await workload.WriteInputAsync("1u\x1b[1;5R"u8.ToArray());
        AssertCtrlF3(await ReadAsync(workload.InputEvents));
        Assert.IsNull(workload.ConfirmedEnhancedInput);
        Assert.IsNull(reportsReleases);

        await workload.WriteInputAsync("\x1b[?31u\x1b[1;5R\x1b[13~"u8.ToArray());
        Assert.AreEqual(Hex1bKey.F3, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Key);
        Assert.IsFalse(workload.InputEvents.TryRead(out _));
        Assert.AreEqual(true, workload.ConfirmedEnhancedInput);

        // A re-entered epoch whose query is still queued cannot reuse a reply.
        workload.ExitTuiMode();
        workload.EnterTuiMode();
        await workload.WriteInputAsync("\x1b[?31u\x1b[1;5R"u8.ToArray());
        AssertCtrlF3(await ReadAsync(workload.InputEvents));
        Assert.IsNull(workload.ConfirmedEnhancedInput);
    }

    // The confirming reply and the first enhanced keys may share one read: keys after the
    // reply decode in the confirmed mode, while input before it stays legacy.
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task KittyIngress_ReplyAndFirstEnhancedKeysInOneRead_DecodeAfterConfirmation(bool raw)
    {
        await using var presentation = new QueuedPresentation();
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities { SupportsKittyKeyboard = true })
        {
            RawInputTimeProvider = new RawClock()
        };
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            EscapeSequenceTimeout = TimeSpan.Zero
        });
        workload.EnterTuiMode();
        await presentation.QueryDelivered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await WriteIngressAsync(raw, presentation, workload, "\x1b[1;5R\x1b[?31u\x1b[1;1P\x1b[1;1S\x1b[1;5Rz");
        AssertCtrlF3(await ReadAsync(workload.InputEvents));
        var f1 = TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents));
        Assert.AreEqual(Hex1bKey.F1, f1.Key);
        Assert.AreEqual(Hex1bModifiers.None, f1.Modifiers);
        var f4 = TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents));
        Assert.AreEqual(Hex1bKey.F4, f4.Key);
        Assert.AreEqual(Hex1bModifiers.None, f4.Modifiers);
        Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
        Assert.IsFalse(workload.InputEvents.TryRead(out _), "CSI 1;5R after confirmation is a cursor report.");
        Assert.AreEqual(true, workload.ConfirmedEnhancedInput);
    }

    // A read can finish a stale report prefix and then carry a fresh, genuine report.
    [TestMethod]
    public async Task WriteInputAsync_StaleReportCompletionAndFreshReportInOneRead_ConfirmsFreshReport()
    {
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities { SupportsKittyKeyboard = true });
        bool? reportsReleases = null;
        workload.SetInputSourceReportingHandler(value => reportsReleases = value);
        workload.EnterTuiMode();
        await workload.WriteInputAsync("\x1b[?3"u8.ToArray());
        DeliverQueuedOutput(workload);
        await workload.WriteInputAsync("1u\x1b[?31u\x1b[1;1Pz"u8.ToArray());
        Assert.AreEqual(Hex1bKey.F1, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Key);
        Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
        Assert.AreEqual(true, workload.ConfirmedEnhancedInput);
        Assert.AreEqual(true, reportsReleases);
        await workload.WriteInputAsync("\x1b[1;5Rz"u8.ToArray());
        Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
        Assert.IsFalse(workload.InputEvents.TryRead(out _));
    }

    [TestMethod]
    public async Task PresentationInput_StaleReportCompletionAndFreshReportInOneRead_ConfirmsFreshReport()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var presentation = new QueuedPresentation { PushGate = gate.Task };
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities { SupportsKittyKeyboard = true });
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            EscapeSequenceTimeout = TimeSpan.Zero
        });
        workload.EnterTuiMode();
        await presentation.PushEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await presentation.EnqueueAndAwaitReceiptAsync("\x1b[?3");
        gate.SetResult();
        await presentation.QueryDelivered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await presentation.EnqueueAndAwaitReceiptAsync("1u\x1b[?31u\x1b[1;1Pz");
        Assert.AreEqual(Hex1bKey.F1, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Key);
        Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
        Assert.AreEqual(true, workload.ConfirmedEnhancedInput);
        presentation.Enqueue("\x1b[1;5Rz"u8.ToArray());
        Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
        Assert.IsFalse(workload.InputEvents.TryRead(out _));
    }

    // A report read while the push/query write is still blocked predates delivery and cannot
    // confirm; the follow-up query lets a genuine reply confirm once delivery completes.
    [TestMethod]
    public async Task PresentationInput_ReportReadBeforeQueryDelivered_CannotConfirm()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var presentation = new QueuedPresentation { PushGate = gate.Task };
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities { SupportsKittyKeyboard = true });
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            EscapeSequenceTimeout = TimeSpan.Zero
        });
        workload.EnterTuiMode();
        await presentation.PushEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await presentation.EnqueueAndAwaitReceiptAsync("\x1b[?31uz");
        gate.SetResult();
        await presentation.QueryDelivered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
        Assert.IsNull(workload.ConfirmedEnhancedInput);
        presentation.Enqueue("\x1b[1;5R"u8.ToArray());
        AssertCtrlF3(await ReadAsync(workload.InputEvents));

        presentation.Enqueue("\x1b[?31u\x1b[1;5Rz"u8.ToArray());
        Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
        Assert.IsFalse(workload.InputEvents.TryRead(out _));
        Assert.AreEqual(true, workload.ConfirmedEnhancedInput);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PresentationInput_ReadCompletesBeforeQueryButResumesAfter_CannotConfirm(bool split)
    {
        var pushGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var read = new DeferredInputRead();
        await using var presentation = new QueuedPresentation
        {
            PushGate = pushGate.Task,
            InitialRead = read.ReadAsync()
        };
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities { SupportsKittyKeyboard = true });
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            EscapeSequenceTimeout = TimeSpan.Zero
        });
        try
        {
            await read.Registered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            workload.EnterTuiMode();
            await presentation.PushEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            read.Complete(split ? "\x1b[?3" : "\x1b[?31uz");
            await read.ContinuationBlocked.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(presentation.QueryDelivered.Task.IsCompleted);
            Assert.IsFalse(pushGate.Task.IsCompleted);

            pushGate.SetResult();
            await presentation.QueryDelivered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            read.Resume.SetResult();
            if (split)
                presentation.Enqueue("1uz"u8.ToArray());
            Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
            Assert.IsNull(workload.ConfirmedEnhancedInput,
                "A completed read must not acquire the query epoch while its continuation is suspended.");

            presentation.Enqueue("\x1b[1;5R"u8.ToArray());
            AssertCtrlF3(await ReadAsync(workload.InputEvents));
            presentation.Enqueue("\x1b[?31u\x1b[1;5Rz"u8.ToArray());
            Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
            Assert.IsFalse(workload.InputEvents.TryRead(out _));
            Assert.AreEqual(true, workload.ConfirmedEnhancedInput);
        }
        finally
        {
            pushGate.TrySetResult();
            read.Finish();
        }
    }

    [TestMethod]
    public async Task PresentationInput_ReadPendingAcrossQuery_AcceptsFirstReply()
    {
        var pushGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var read = new DeferredInputRead();
        await using var presentation = new QueuedPresentation
        {
            PushGate = pushGate.Task,
            InitialRead = read.ReadAsync()
        };
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities { SupportsKittyKeyboard = true });
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            EscapeSequenceTimeout = TimeSpan.Zero
        });
        try
        {
            await read.Registered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            workload.EnterTuiMode();
            await presentation.PushEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            pushGate.SetResult();
            await presentation.QueryDelivered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            read.Complete("\x1b[?31u\x1b[1;5R\x1b[1;1Pz");
            await read.ContinuationBlocked.Task.WaitAsync(TimeSpan.FromSeconds(5));
            read.Resume.SetResult();
            Assert.AreEqual(Hex1bKey.F1, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Key);
            Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
            Assert.IsFalse(workload.InputEvents.TryRead(out _));
            Assert.AreEqual(true, workload.ConfirmedEnhancedInput,
                "A still-pending read can receive a genuine reply even if it began before query delivery.");
        }
        finally
        {
            pushGate.TrySetResult();
            read.Finish();
        }
    }

    // Completes the adapter read while keeping its consumer's await continuation suspended.
    private sealed class DeferredInputRead : IValueTaskSource<ReadOnlyMemory<byte>>
    {
        private ManualResetValueTaskSourceCore<ReadOnlyMemory<byte>> _source = new()
        {
            RunContinuationsAsynchronously = true
        };
        public TaskCompletionSource Registered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ContinuationBlocked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _completed;

        public ValueTask<ReadOnlyMemory<byte>> ReadAsync() => new(this, _source.Version);
        public void Complete(string text)
        {
            Assert.AreEqual(0, Interlocked.Exchange(ref _completed, 1));
            _source.SetResult(Encoding.UTF8.GetBytes(text));
        }
        public void Finish()
        {
            if (Interlocked.Exchange(ref _completed, 1) == 0)
                _source.SetResult(default);
            Resume.TrySetResult();
        }
        public ReadOnlyMemory<byte> GetResult(short token) => _source.GetResult(token);
        public ValueTaskSourceStatus GetStatus(short token) => _source.GetStatus(token);
        public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            _source.OnCompleted(ignored =>
            {
                ContinuationBlocked.TrySetResult();
                _ = ResumeAsync(continuation, state);
            }, null, token, flags);
            Registered.TrySetResult();
        }
        private async Task ResumeAsync(Action<object?> continuation, object? state)
        {
            await Resume.Task;
            continuation(state);
        }
    }

    [TestMethod]
    public void InputSequenceParser_OversizedStringAcrossTimeouts_KeepsDiscardingUntilTerminator()
    {
        var parser = new InputSequenceParser();
        Assert.IsEmpty(parser.Parse("\x1b]0;" + new string('a', 4096), enhanced: false));
        Assert.IsTrue(parser.IsDiscarding);
        Assert.IsEmpty(parser.Parse("", enhanced: false, flush: true));
        Assert.IsEmpty(parser.Parse("", enhanced: false, flush: true));
        Assert.IsTrue(parser.IsDiscarding, "Timer expiry must not end a bounded discard.");
        Assert.IsFalse(parser.NeedsEscapeTimeout);
        var tokens = parser.Parse("payload\u0007z", enhanced: false);
        Assert.AreEqual("z", TestSeq.IsType<TextToken>(TestSeq.Single(tokens)).Text);
        Assert.IsFalse(parser.IsDiscarding);
    }

    [TestMethod]
    public async Task WriteInputAsync_TimedOutStringAndCsiSequences_KeepDiscardingAcrossTimeouts()
    {
        var clock = new RawClock();
        using var workload = new Hex1bAppWorkloadAdapter { RawInputTimeProvider = clock };
        var cases = new[]
        {
            // Oversize discard, then repeated timer expiry, then the payload remainder.
            new[] { "\x1b]0;a", new string('a', 4096), "!", "!", "payload\u0007z" },
            // Short partial OSC/APC/DCS timed out: the late remainder never leaks.
            new[] { "\x1b]0;ti", "!", "tle\u0007z" },
            new[] { "\x1b_Gabc", "!", "def\x1b", "\\z" },
            new[] { "\u009fGabc", "!", "def\u009cz" },
            new[] { "\x1bP1$qm", "!", "more\u0018z" },
            // An ESC that does not start ST resyncs to a new sequence.
            new[] { "\x1b]0;ab", "!", "cd\x1b[Az" },
            // A timed-out partial CSI consumes its late final byte.
            new[] { "\x1b[1;5", "!", "Rz" },
        };
        foreach (var chunks in cases)
        {
            foreach (var chunk in chunks)
            {
                if (chunk == "!")
                    clock.FireLast();
                else
                    await workload.WriteInputAsync(Encoding.UTF8.GetBytes(chunk));
            }
            var label = string.Join(" | ", chunks.Select(c => c.Length > 12 ? c[..12] : c));
            if (chunks[^1].Contains("[A", StringComparison.Ordinal))
                Assert.AreEqual(Hex1bKey.UpArrow, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Key, label);
            Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text, label);
            Assert.IsFalse(workload.InputEvents.TryRead(out _), label);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task KittyIngress_OversizedSequenceRemainder_IsDiscardedUntilTermination(bool raw)
    {
        await using var presentation = new QueuedPresentation();
        using var workload = new Hex1bAppWorkloadAdapter { RawInputTimeProvider = new RawClock() };
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            EscapeSequenceTimeout = TimeSpan.Zero
        });
        var payload = new string('1', 5000);
        var cases = new[]
        {
            new[] { "\x1b[" + new string('1', 4094), "123u" },
            new[] { "\x1b[" + new string('1', 4100), "123u" },
            new[] { "\x1b]0;" + payload[..4200], payload, "\u0007" },
            new[] { "\x1b_G" + payload[..4200], payload, "\x1b\\" },
            new[] { "\u009d0;" + payload[..4200], payload, "\u009c" },
            new[] { "\u009fG" + payload[..4200], payload, "\x1b\\" },
            new[] { "\u0090q" + payload[..4200], payload, "\u009c" },
        };
        foreach (var chunks in cases)
        {
            foreach (var chunk in chunks)
                await WriteIngressAsync(raw, presentation, workload, chunk);
            await WriteIngressAsync(raw, presentation, workload, "z");
            Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text,
                string.Join(" | ", chunks.Select(c => c.Length)));
        }
        Assert.IsFalse(workload.InputEvents.TryRead(out _));
    }

    [TestMethod]
    [DataRow(false, 4095)]
    [DataRow(false, 4096)]
    [DataRow(false, 4097)]
    [DataRow(true, 4095)]
    [DataRow(true, 4096)]
    [DataRow(true, 4097)]
    public async Task KittyIngress_SequenceLengthBoundary_MatchesCodecAcrossChunks(bool raw, int length)
    {
        const string prefix = "\x1b[97;1;";
        var bodyLength = length - prefix.Length - 1;
        var hundreds = Enumerable.Range(0, 3).First(m => (bodyLength + 1 - 4 * m) % 3 == 0);
        var groups = Enumerable.Repeat("100", hundreds)
            .Concat(Enumerable.Repeat("97", (bodyLength + 1 - 4 * hundreds) / 3));
        var sequence = prefix + string.Join(':', groups) + "u";
        Assert.AreEqual(length, sequence.Length);
        var expectedText = new string('d', hundreds) + new string('a', (bodyLength + 1 - 4 * hundreds) / 3);

        await using var presentation = new QueuedPresentation();
        using var workload = new Hex1bAppWorkloadAdapter { RawInputTimeProvider = new RawClock() };
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            EscapeSequenceTimeout = TimeSpan.Zero
        });
        foreach (var split in new[] { 1, length / 2, length - 1, Math.Min(4096, length - 1) })
        {
            await WriteIngressAsync(raw, presentation, workload, sequence[..split]);
            await WriteIngressAsync(raw, presentation, workload, sequence[split..] + "z");
            if (length <= 4096)
            {
                var key = TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents));
                Assert.AreEqual(Hex1bKey.A, key.Key, $"split {split}");
                Assert.AreEqual(expectedText, key.Text, $"split {split}");
            }
            Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text,
                $"split {split}");
        }
        Assert.IsFalse(workload.InputEvents.TryRead(out _));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task KittyIngress_C1ControlStrings_AreFramedAtEveryByteSplit(bool raw)
    {
        await using var presentation = new QueuedPresentation();
        using var workload = new Hex1bAppWorkloadAdapter { RawInputTimeProvider = new RawClock() };
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            EscapeSequenceTimeout = TimeSpan.Zero
        });
        var inputs = new[]
        {
            "\u009d0;title\u0007z", "\u009d0;title\x1b\\z", "\u009d0;title\u009cz",
            "\u009fGpayload\u009cz", "\u009fGpayload\x1b\\z", "\u0090qpayload\x1b\\z"
        };
        foreach (var input in inputs)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            for (var split = 1; split < bytes.Length; split++)
            {
                if (raw)
                {
                    await workload.WriteInputAsync(bytes[..split]);
                    await workload.WriteInputAsync(bytes[split..]);
                }
                else
                {
                    presentation.Enqueue(bytes[..split]);
                    presentation.Enqueue(bytes[split..]);
                }
                var key = TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents));
                Assert.AreEqual("z", key.Text, $"{Convert.ToHexString(bytes)} split {split}");
            }
        }
        Assert.IsFalse(workload.InputEvents.TryRead(out _));

        // Inside bracketed paste, C1 code points are opaque content rather than introducers.
        await WriteIngressAsync(raw, presentation, workload, "\x1b[200~a\u009db\u009fc\u0090d\x1b[201~z");
        var paste = TestSeq.IsType<Hex1bPasteEvent>(await ReadAsync(workload.InputEvents)).Paste;
        Assert.AreEqual("a\u009db\u009fc\u0090d", await paste.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.AreEqual("z", TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
    }

    private static void AssertCtrlF3(Hex1bEvent evt)
    {
        var key = TestSeq.IsType<Hex1bKeyEvent>(evt);
        Assert.AreEqual(Hex1bKey.F3, key.Key);
        Assert.AreEqual(Hex1bModifiers.Control, key.Modifiers);
    }

    private static async Task WriteIngressAsync(
        bool raw, QueuedPresentation presentation, Hex1bAppWorkloadAdapter workload, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        if (raw)
            await workload.WriteInputAsync(bytes);
        else
            presentation.Enqueue(bytes);
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
        await workload.WriteInputAsync("\x1b[97"u8.ToArray());
        clock.FireLast();
        Assert.IsFalse(workload.InputEvents.TryRead(out _), "Malformed timeout must not replay numeric shortcut text.");
        // Its late remainder is discarded through the CSI final byte, not replayed as keys.
        await workload.WriteInputAsync(";5uz"u8.ToArray());
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
