using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public partial class InputRouterTests
{
    private sealed record InputProbeWidget(MockFocusableNode Node) : Hex1bWidget
    {
        internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
            => Task.FromResult<Hex1bNode>(Node);

        internal override Type GetExpectedNodeType() => typeof(MockFocusableNode);
    }

    [TestMethod]
    public async Task RunAsync_Release_ReachesCustomHandleInputWithOriginalMetadata()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var node = new MockFocusableNode();
        Hex1bAppWorkloadAdapter? workload = null;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var routed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                options => workload = TestSeq.IsType<Hex1bAppWorkloadAdapter>(options.WorkloadAdapter),
                _ => new VStackWidget([
                    new InputProbeWidget(node),
                    new TestWidget().OnRender(_ => ready.TrySetResult())
                ]).InputBindings(b => b.Key(Hex1bKey.F8).Global().Action(() => routed.TrySetResult())))
            .WithHeadless().WithDimensions(24, 4).Build();
        var run = terminal.RunAsync(cancellation.Token);
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var release = new Hex1bKeyEvent(Hex1bKey.None, "retained", Hex1bModifiers.Meta | Hex1bModifiers.CapsLock)
            {
                EventType = Hex1bKeyEventType.Release,
                UnicodeKeyCode = 57442,
                BaseLayoutKeyCode = 99
            };
            workload!.TryWriteInputEvent(release);
            workload.SendKey(Hex1bKey.F8);
            await routed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreSame(release, TestSeq.Single(node.ReceivedInputs));
        }
        finally
        {
            cancellation.Cancel();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestMethod]
    public async Task RunAsync_EscapeRelease_DoesNotCancelActivePaste()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var paste = new PasteContext();
        Hex1bAppWorkloadAdapter? workload = null;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sentinels = System.Threading.Channels.Channel.CreateUnbounded<bool>();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                options => workload = TestSeq.IsType<Hex1bAppWorkloadAdapter>(options.WorkloadAdapter),
                _ => new TextBoxWidget("Paste ready")
                    .OnPaste(async _ =>
                    {
                        started.TrySetResult();
                        await finish.Task;
                        completed.TrySetResult();
                    })
                    .InputBindings(b => b.Key(Hex1bKey.F8).Action(() => sentinels.Writer.TryWrite(true))))
            .WithHeadless().WithDimensions(24, 4).Build();
        var run = terminal.RunAsync(cancellation.Token);
        try
        {
            using var ready = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Paste ready"), TimeSpan.FromSeconds(5), "paste handler ready")
                .Build().ApplyAsync(terminal);
            workload!.TryWriteInputEvent(new Hex1bPasteEvent(paste));
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            workload.TryWriteInputEvent(Hex1bKeyEvent.Plain(Hex1bKey.Escape) with { EventType = Hex1bKeyEventType.Release });
            workload.SendKey(Hex1bKey.F8);
            await sentinels.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(paste.IsCancelled);
            workload.SendKey(Hex1bKey.Escape);
            workload.SendKey(Hex1bKey.F8);
            await sentinels.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(paste.IsCancelled);
        }
        finally
        {
            finish.TrySetResult();
            cancellation.Cancel();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RouteInput_UnhandledRelease_BubblesOnceWithMetadata(bool captured)
    {
        var node = new MockFocusableNode { IsFocused = true, Result = InputResult.NotHandled };
        var root = new MockContainerNode();
        root.Children.Add(node);
        node.Parent = root;
        var ring = new FocusRing();
        ring.Rebuild(root);
        if (captured) ring.CaptureInput(node);
        var release = new Hex1bKeyEvent(Hex1bKey.None, "", Hex1bModifiers.Control)
        {
            EventType = Hex1bKeyEventType.Release,
            UnicodeKeyCode = 57442
        };
        await InputRouter.RouteInputAsync(root, release, ring, new InputRouterState());
        Assert.AreSame(release, TestSeq.Single(node.ReceivedInputs));
        Assert.AreSame(release, TestSeq.Single(root.ReceivedInputs));
    }

    [TestMethod]
    public void KeyStep_EnhancedModifiersAndLocks_NormalizeConsistentlyInChords()
    {
        var builder = new InputBindingsBuilder();
        builder.Super().Hyper().Meta().Key(Hex1bKey.A).Then().Key(Hex1bKey.B).Action(() => { });
        var binding = TestSeq.Single(builder.Build());
        Assert.AreEqual("Super+Hyper+Meta+A", binding.FirstStep.ToString());
        var first = new Hex1bKeyEvent(Hex1bKey.A, "a",
            Hex1bModifiers.Super | Hex1bModifiers.Hyper | Hex1bModifiers.Meta | Hex1bModifiers.CapsLock | Hex1bModifiers.NumLock);
        Assert.IsTrue(binding.FirstStep.Matches(first));
        Assert.IsFalse(binding.FirstStep.Matches(first with { EventType = Hex1bKeyEventType.Release }));
        var chord = ChordTrie.Build([binding]);
        Assert.IsTrue(chord.Lookup(first).Node!.Lookup(
            Hex1bKeyEvent.Plain(Hex1bKey.B) with { Modifiers = Hex1bModifiers.NumLock }).IsLeaf);
        Assert.IsTrue(chord.Lookup(first with { EventType = Hex1bKeyEventType.Release }).IsNoMatch);
    }

    [TestMethod]
    [DataRow("normal")]
    [DataRow("global")]
    [DataRow("capture")]
    [DataRow("character")]
    public async Task RouteInput_Release_PreservesCustomMetadataWithoutExecutingActions(string bindingKind)
    {
        var actions = 0;
        var node = new MockFocusableNode { IsFocused = true };
        node.BindingsConfig = bindings =>
        {
            if (bindingKind == "character")
                bindings.AnyCharacter().Action(_ => actions++);
            else
            {
                var key = bindings.Key(Hex1bKey.A);
                if (bindingKind == "global") key.Global();
                if (bindingKind == "capture") key.OverridesCapture();
                key.Action(() => actions++);
            }
        };
        var ring = new FocusRing();
        ring.Rebuild(node);
        if (bindingKind == "capture") ring.CaptureInput(node);
        var state = new InputRouterState();
        var release = new Hex1bKeyEvent(Hex1bKey.A, "associated", Hex1bModifiers.CapsLock)
        {
            EventType = Hex1bKeyEventType.Release,
            UnicodeKeyCode = 97,
            ShiftedKeyCode = 65,
            BaseLayoutKeyCode = 97
        };

        await InputRouter.RouteInputAsync(node, release, ring, state);

        Assert.AreEqual(0, actions);
        Assert.AreSame(release, TestSeq.Single(node.ReceivedInputs));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RouteInput_ReleasesDuringChord_DoNotCancelOrAdvanceChord(bool capture)
    {
        var actions = 0;
        var node = new MockFocusableNode { IsFocused = true };
        node.BindingsConfig = bindings =>
        {
            var chord = bindings.Ctrl().Key(Hex1bKey.K).Then().Ctrl().Key(Hex1bKey.C);
            if (capture) chord.OverridesCapture();
            chord.Action(() => actions++);
        };
        var ring = new FocusRing();
        ring.Rebuild(node);
        if (capture) ring.CaptureInput(node);
        var state = new InputRouterState();

        await InputRouter.RouteInputAsync(node, Hex1bKeyEvent.WithCtrl(Hex1bKey.K), ring, state);
        Assert.IsTrue(state.IsInChord);
        await InputRouter.RouteInputAsync(node, Hex1bKeyEvent.Plain(Hex1bKey.Escape) with { EventType = Hex1bKeyEventType.Release }, ring, state);
        await InputRouter.RouteInputAsync(node, Hex1bKeyEvent.WithCtrl(Hex1bKey.C) with { EventType = Hex1bKeyEventType.Release }, ring, state);
        Assert.IsTrue(state.IsInChord);
        Assert.AreEqual(0, actions);
        await InputRouter.RouteInputAsync(node, Hex1bKeyEvent.WithCtrl(Hex1bKey.C), ring, state);
        Assert.AreEqual(1, actions);
        Assert.IsFalse(state.IsInChord);
        Assert.HasCount(2, node.ReceivedInputs);
    }

    [TestMethod]
    public async Task RouteInput_RepeatWithExtraModifiersAndLocks_ExecutesMatchingBindingWithMetadata()
    {
        Hex1bKeyEvent? received = null;
        var node = new MockFocusableNode { IsFocused = true };
        node.BindingsConfig = b => b.Super().Hyper().Meta().Ctrl().Alt().Shift().Key(Hex1bKey.A)
            .Action(context => received = context.KeyEvent);
        var ring = new FocusRing();
        ring.Rebuild(node);
        var repeat = new Hex1bKeyEvent(Hex1bKey.A, "A",
            Hex1bModifiers.Super | Hex1bModifiers.Hyper | Hex1bModifiers.Meta |
            Hex1bModifiers.Control | Hex1bModifiers.Alt | Hex1bModifiers.Shift |
            Hex1bModifiers.CapsLock | Hex1bModifiers.NumLock) { EventType = Hex1bKeyEventType.Repeat };

        await InputRouter.RouteInputAsync(node, repeat, ring, new InputRouterState());
        Assert.AreSame(repeat, received);
    }

    [TestMethod]
    public async Task RouteInput_HeldKey_RepeatsDoNotDuplicateAndReleaseIgnoresChangedModifiers()
    {
        var started = System.Threading.Channels.Channel.CreateUnbounded<CancellationToken>();
        var calls = 0;
        var node = new MockFocusableNode { IsFocused = true };
        node.BindingsConfig = b => b.Ctrl().Key(Hex1bKey.A).OnHold(async (_, token) =>
        {
            Interlocked.Increment(ref calls);
            started.Writer.TryWrite(token);
            await Task.Delay(Timeout.Infinite, token);
        });
        var ring = new FocusRing();
        ring.Rebuild(node);
        var state = new InputRouterState();
        var press = Hex1bKeyEvent.WithCtrl(Hex1bKey.A);
        try
        {
            await InputRouter.RouteInputAsync(node, press, ring, state);
            var first = await started.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            await InputRouter.RouteInputAsync(node, press with { EventType = Hex1bKeyEventType.Repeat }, ring, state);
            await InputRouter.RouteInputAsync(node, press, ring, state);
            Assert.AreEqual(1, Volatile.Read(ref calls));
            await InputRouter.RouteInputAsync(node, Hex1bKeyEvent.Plain(Hex1bKey.A) with { EventType = Hex1bKeyEventType.Release }, ring, state);
            Assert.IsTrue(first.IsCancellationRequested);
            await InputRouter.RouteInputAsync(node, press, ring, state);
            var second = await started.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual(2, Volatile.Read(ref calls));
            Assert.IsFalse(second.IsCancellationRequested);
        }
        finally
        {
            await state.StopKeyHoldsAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestMethod]
    public async Task RouteInput_HeldKey_OrphanRepeatDoesNotStartHold()
    {
        var calls = 0;
        var node = new MockFocusableNode { IsFocused = true };
        node.BindingsConfig = b => b.Key(Hex1bKey.A).OnHold((_, _) => Interlocked.Increment(ref calls));
        var ring = new FocusRing();
        ring.Rebuild(node);
        var state = new InputRouterState();
        await InputRouter.RouteInputAsync(node,
            Hex1bKeyEvent.Plain(Hex1bKey.A) with { EventType = Hex1bKeyEventType.Repeat }, ring, state);
        await state.StopKeyHoldsAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    [DataRow("next")]
    [DataRow("previous")]
    [DataRow("focus")]
    [DataRow("capture")]
    [DataRow("release-capture")]
    [DataRow("rebuild")]
    public async Task RouteInput_HeldKey_OwnershipChangesCancelWithoutReleaseReporting(string change)
    {
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = new MockFocusableNode { IsFocused = true };
        var second = new MockFocusableNode();
        var root = new MockContainerNode();
        root.Children.AddRange([first, second]);
        first.Parent = second.Parent = root;
        root.BindingsConfig = b => b.Key(Hex1bKey.A).Global().OnHold((_, token) => started.TrySetResult(token));
        var ring = new FocusRing();
        ring.Rebuild(root);
        if (change == "release-capture") ring.CaptureInput(first);
        var state = new InputRouterState();
        try
        {
            await InputRouter.RouteInputAsync(root, Hex1bKeyEvent.Plain(Hex1bKey.A), ring, state);
            var token = await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            switch (change)
            {
                case "next": ring.FocusNext(); break;
                case "previous": ring.FocusPrevious(); break;
                case "focus": ring.Focus(second); break;
                case "capture": ring.CaptureInput(first); break;
                case "release-capture": ring.ReleaseCapture(); break;
                case "rebuild": root.Children.Remove(first); ring.Rebuild(root); break;
            }
            Assert.IsTrue(token.IsCancellationRequested);
        }
        finally
        {
            await state.StopKeyHoldsAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestMethod]
    public async Task RouteInput_BlockingCancellationRegistration_DoesNotBlockReleaseOrNextInput()
    {
        using var unblock = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelling = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var actions = 0;
        var node = new MockFocusableNode { IsFocused = true };
        node.BindingsConfig = b =>
        {
            b.Key(Hex1bKey.A).OnHold((_, token) =>
            {
                token.Register(() =>
                {
                    cancelling.TrySetResult();
                    Assert.IsTrue(unblock.Wait(TimeSpan.FromSeconds(10)), "Test must release the registration gate.");
                });
                started.TrySetResult();
            });
            b.Key(Hex1bKey.B).Action(() => actions++);
        };
        var ring = new FocusRing();
        ring.Rebuild(node);
        var state = new InputRouterState();
        try
        {
            await InputRouter.RouteInputAsync(node, Hex1bKeyEvent.Plain(Hex1bKey.A), ring, state);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            // Run on a worker so a regression to synchronous Cancel cannot hang the test runner.
            await Task.Run(() => InputRouter.RouteInputAsync(node,
                    Hex1bKeyEvent.Plain(Hex1bKey.A) with { EventType = Hex1bKeyEventType.Release }, ring, state))
                .WaitAsync(TimeSpan.FromSeconds(5));
            await cancelling.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await InputRouter.RouteInputAsync(node, Hex1bKeyEvent.Plain(Hex1bKey.B), ring, state)
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual(1, actions);
            var stop = state.StopKeyHoldsAsync();
            Assert.IsFalse(stop.IsCompleted, "Disposal must wait for active cancellation registrations.");
            unblock.Set();
            await stop.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            unblock.Set();
            await state.StopKeyHoldsAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RouteInput_HoldFailure_SurfacesHandlerAndCancellationErrors(bool cancellationFailure)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = new InvalidOperationException("held-key failure");
        var node = new MockFocusableNode { IsFocused = true };
        node.BindingsConfig = b => b.Key(Hex1bKey.A).OnHold((_, token) =>
        {
            if (cancellationFailure)
                token.Register(() => throw expected);
            started.TrySetResult();
            if (!cancellationFailure) throw expected;
        });
        var ring = new FocusRing();
        ring.Rebuild(node);
        var state = new InputRouterState();
        await InputRouter.RouteInputAsync(node, Hex1bKeyEvent.Plain(Hex1bKey.A), ring, state);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await InputRouter.RouteInputAsync(node, Hex1bKeyEvent.Plain(Hex1bKey.A) with { EventType = Hex1bKeyEventType.Release }, ring, state);
        var failure = await state.HoldFailure.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(ReferenceEquals(expected, failure) ||
            failure is AggregateException aggregate && aggregate.InnerExceptions.Contains(expected));
        await Assert.ThrowsAsync<InvalidOperationException>(() => state.StopKeyHoldsAsync());
    }

    [TestMethod]
    public async Task RunAsync_ReleasesDoNotActivateWidgetsOrExit_RepeatRemainsActionable()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Hex1bAppWorkloadAdapter? workload = null;
        var clicks = 0;
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                options => workload = TestSeq.IsType<Hex1bAppWorkloadAdapter>(options.WorkloadAdapter),
                _ => new ButtonWidget("Ready").OnClick(_ => clicks++))
            .WithHeadless().WithDimensions(24, 4).Build();
        var run = terminal.RunAsync(cancellation.Token);
        try
        {
            using var ready = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Ready"), TimeSpan.FromSeconds(5), "button rendered")
                .Build().ApplyAsync(terminal);
            workload!.TryWriteInputEvent(Hex1bKeyEvent.Plain(Hex1bKey.Enter) with { EventType = Hex1bKeyEventType.Release });
            workload.TryWriteInputEvent(Hex1bKeyEvent.WithCtrl(Hex1bKey.C) with { EventType = Hex1bKeyEventType.Release });
            workload.TryWriteInputEvent(Hex1bKeyEvent.Plain(Hex1bKey.Enter) with { EventType = Hex1bKeyEventType.Repeat });
            using var clicked = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(_ => Volatile.Read(ref clicks) == 1, TimeSpan.FromSeconds(5), "repeat activated exactly once")
                .Build().ApplyAsync(terminal);
            Assert.IsFalse(run.IsCompleted);
        }
        finally
        {
            cancellation.Cancel();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestMethod]
    [DataRow("stop")]
    [DataRow("cancel")]
    [DataRow("dispose")]
    public async Task RunAsync_HeldKey_ShutdownCancelsAndDrainsCallback(string shutdown)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Hex1bApp? app = null;
        Hex1bAppWorkloadAdapter? workload = null;
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                options => workload = TestSeq.IsType<Hex1bAppWorkloadAdapter>(options.WorkloadAdapter),
                instance =>
                {
                    app = instance;
                    return _ => new ButtonWidget("Hold ready").InputBindings(b =>
                        b.Key(Hex1bKey.A).OnHold(async (_, token) =>
                        {
                            started.TrySetResult(token);
                            try { await Task.Delay(Timeout.Infinite, token); }
                            finally { finished.TrySetResult(); }
                        }));
                })
            .WithHeadless().WithDimensions(24, 4).Build();
        var run = terminal.RunAsync(cancellation.Token);
        try
        {
            using var ready = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Hold ready"), TimeSpan.FromSeconds(5), "hold binding ready")
                .Build().ApplyAsync(terminal);
            workload!.SendKey(Hex1bKey.A);
            var token = await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            switch (shutdown)
            {
                case "stop": app!.RequestStop(); break;
                case "cancel": cancellation.Cancel(); break;
                case "dispose": await app!.DisposeAsync(); break;
            }
            await run.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(token.IsCancellationRequested);
            Assert.IsTrue(finished.Task.IsCompletedSuccessfully);
        }
        finally
        {
            cancellation.Cancel();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunAsync_HeldKeyFailure_WakesIdleAppAndPropagates(bool cancellationFailure)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Hex1bAppWorkloadAdapter? workload = null;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = new InvalidOperationException("app hold failure");
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                options => workload = TestSeq.IsType<Hex1bAppWorkloadAdapter>(options.WorkloadAdapter),
                _ => new ButtonWidget("Failure ready").InputBindings(b => b.Key(Hex1bKey.A).OnHold((_, token) =>
                {
                    if (cancellationFailure) token.Register(() => throw expected);
                    started.TrySetResult();
                    if (!cancellationFailure) throw expected;
                })))
            .WithHeadless().WithDimensions(24, 4).Build();
        var run = terminal.RunAsync(cancellation.Token);
        try
        {
            using var ready = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Failure ready"), TimeSpan.FromSeconds(5), "failing binding ready")
                .Build().ApplyAsync(terminal);
            workload!.SendKey(Hex1bKey.A);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (cancellationFailure)
                workload.TryWriteInputEvent(Hex1bKeyEvent.Plain(Hex1bKey.A) with { EventType = Hex1bKeyEventType.Release });
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => run.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.AreEqual("A held-key input handler failed.", error.Message);
            Assert.IsTrue(ReferenceEquals(expected, error.InnerException) ||
                error.InnerException is AggregateException aggregate && aggregate.InnerExceptions.Contains(expected));
        }
        finally
        {
            cancellation.Cancel();
            try { await run.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (InvalidOperationException ex) when (ex.Message == "A held-key input handler failed.") { }
        }
    }
}
