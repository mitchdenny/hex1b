using System.Text;
using System.Threading.Channels;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Tokens;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class KittyEmbeddedInputTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RouteInput_ChildNegotiationAndCopyMode_OnlyReleasesForwardedPresses(bool childEnhanced)
    {
        await using var childWorkload = new GatedWorkload();
        await using var child = Hex1bTerminal.CreateBuilder()
            .WithWorkload(childWorkload).WithDimensions(24, 4).WithTerminalWidget(out var handle).Build();
        if (childEnhanced) child.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[>11u"));
        var node = new TerminalNode { Handle = handle, IsFocused = true };
        var ring = new FocusRing();
        ring.Rebuild(node);
        var state = new InputRouterState();
        var copyEvents = new List<Hex1bEvent>();
        handle.CopyModeInput += evt =>
        {
            copyEvents.Add(evt);
            if (evt is Hex1bKeyEvent { Key: Hex1bKey.F6 })
            {
                handle.EnterCopyMode();
                return true;
            }
            return handle.IsInCopyMode;
        };
        var actions = 0;
        node.BindingsConfigurator = b => b.Key(Hex1bKey.B).Action(() => actions++);

        var entry = Hex1bKeyEvent.Plain(Hex1bKey.F6);
        await InputRouter.RouteInputAsync(node, entry, ring, state);
        await InputRouter.RouteInputAsync(node, entry with { EventType = Hex1bKeyEventType.Release }, ring, state);
        Assert.IsTrue(handle.IsInCopyMode);
        Assert.HasCount(1, copyEvents, "Releases must never execute copy-mode actions.");
        handle.ExitCopyMode();

        var shortcut = Hex1bKeyEvent.Plain(Hex1bKey.B);
        await InputRouter.RouteInputAsync(node, shortcut, ring, state);
        await InputRouter.RouteInputAsync(node, shortcut with { EventType = Hex1bKeyEventType.Release }, ring, state);
        Assert.AreEqual(1, actions);
        Assert.IsFalse(childWorkload.Input.Reader.TryRead(out _), "Intercepted presses must not produce child releases.");

        handle.EnterCopyMode();
        var intercepted = Hex1bKeyEvent.Plain(Hex1bKey.C, 'c');
        await InputRouter.RouteInputAsync(node, intercepted, ring, state);
        handle.ExitCopyMode();
        await InputRouter.RouteInputAsync(node, intercepted with { EventType = Hex1bKeyEventType.Release }, ring, state);
        Assert.IsFalse(childWorkload.Input.Reader.TryRead(out _), "A press consumed inside copy mode must not escape on release.");

        var press = new Hex1bKeyEvent(Hex1bKey.A, "a", Hex1bModifiers.None) { UnicodeKeyCode = 97 };
        await InputRouter.RouteInputAsync(node, press, ring, state);
        handle.EnterCopyMode();
        await InputRouter.RouteInputAsync(node, press with { Text = "", EventType = Hex1bKeyEventType.Release }, ring, state);
        Assert.AreEqual(childEnhanced ? "\x1b[97u" : "a", await childWorkload.ReadInputAsync());
        if (childEnhanced) Assert.AreEqual("\x1b[97;1:3u", await childWorkload.ReadInputAsync());
        Assert.HasCount(3, copyEvents, "Only F6, the copy-mode C press and the forwarded A press should reach the copy handler.");
        Assert.IsFalse(childWorkload.Input.Reader.TryRead(out _));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunAsync_EmbeddedBackpressure_PreservesPressRepeatReleaseOrder(bool enhancedHost)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var childWorkload = new GatedWorkload { BlockFirstWrite = true };
        await using var child = Hex1bTerminal.CreateBuilder()
            .WithWorkload(childWorkload).WithDimensions(24, 4).WithTerminalWidget(out var handle).Build();
        child.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[>11u"));
        Hex1bAppWorkloadAdapter? appInput = null;
        var drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                options => appInput = TestSeq.IsType<Hex1bAppWorkloadAdapter>(options.WorkloadAdapter),
                _ => new VStackWidget([
                    new TextBlockWidget("Embedded ready"),
                    new TerminalWidget(handle)
                ]).InputBindings(b => b.Key(Hex1bKey.F8).Global().OverridesCapture().Action(() => drained.TrySetResult())))
            .WithHeadless().WithDimensions(30, 8).Build();
        var run = host.RunAsync(cancellation.Token);
        try
        {
            using var ready = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Embedded ready"), TimeSpan.FromSeconds(5), "embedded app ready")
                .Build().ApplyAsync(host);
            if (enhancedHost) host.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[>31u"));
            Assert.AreEqual(enhancedHost ? 31 : 0, host.InputModes.KittyKeyboardFlags);
            Assert.AreEqual(11, child.InputModes.KittyKeyboardFlags);
            var press = Hex1bKeyEvent.Plain(Hex1bKey.A, 'a');
            appInput!.TryWriteInputEvent(press);
            await childWorkload.FirstWrite.Task.WaitAsync(TimeSpan.FromSeconds(5));
            appInput.TryWriteInputEvent(press with { EventType = Hex1bKeyEventType.Repeat });
            appInput.TryWriteInputEvent(press with { Text = "", EventType = Hex1bKeyEventType.Release });
            appInput.SendKey(Hex1bKey.F8);
            Assert.IsFalse(childWorkload.Input.Reader.TryRead(out _));
            Assert.IsFalse(drained.Task.IsCompleted);
            childWorkload.Unblock.TrySetResult();
            await drained.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual("\x1b[97u", await childWorkload.ReadInputAsync());
            Assert.AreEqual("\x1b[97;1:2u", await childWorkload.ReadInputAsync());
            Assert.AreEqual("\x1b[97;1:3u", await childWorkload.ReadInputAsync());
            Assert.IsFalse(childWorkload.Input.Reader.TryRead(out _));
        }
        finally
        {
            childWorkload.Unblock.TrySetResult();
            cancellation.Cancel();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunAsync_EmbeddedWriteFailure_IsNotMistakenForClosedAppInput(bool channelClosed)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Exception expected = channelClosed ? new ChannelClosedException() : new IOException("child write failed");
        await using var workload = new GatedWorkload { WriteFailure = expected };
        await using var child = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithDimensions(24, 4).WithTerminalWidget(out var handle).Build();
        Hex1bAppWorkloadAdapter? appInput = null;
        await using var host = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                options => appInput = TestSeq.IsType<Hex1bAppWorkloadAdapter>(options.WorkloadAdapter),
                _ => new VStackWidget([new TextBlockWidget("Write failure ready"), new TerminalWidget(handle)]))
            .WithHeadless().WithDimensions(30, 8).Build();
        var run = host.RunAsync(cancellation.Token);
        try
        {
            using var ready = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Write failure ready"), TimeSpan.FromSeconds(5), "embedded failure app ready")
                .Build().ApplyAsync(host);
            appInput!.SendKey(Hex1bKey.A, 'a');
            try
            {
                await run.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Fail("Embedded write failure must propagate from the app.");
            }
            catch (Exception ex) when (ReferenceEquals(ex, expected)) { }
        }
        finally
        {
            cancellation.Cancel();
            try { await run.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception ex) when (ReferenceEquals(ex, expected)) { }
        }
    }

    [TestMethod]
    public async Task RouteInput_EmbeddedWriteFailure_PropagatesToCaller()
    {
        var expected = new IOException("child write failed");
        await using var workload = new GatedWorkload { WriteFailure = expected };
        await using var child = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithDimensions(24, 4).WithTerminalWidget(out var handle).Build();
        var node = new TerminalNode { Handle = handle, IsFocused = true };
        var ring = new FocusRing();
        ring.Rebuild(node);
        var error = await Assert.ThrowsAsync<IOException>(() => InputRouter.RouteInputAsync(
            node, Hex1bKeyEvent.Plain(Hex1bKey.A, 'a'), ring, new InputRouterState()));
        Assert.AreSame(expected, error);
    }

    [TestMethod]
    [DataRow("ancestor")]
    [DataRow("global")]
    public async Task RouteInput_ReleaseOfPressConsumedByBinding_BubblesToCustomHandlerWithoutChildRelease(string binding)
    {
        await using var childWorkload = new GatedWorkload();
        await using var child = CreateEnhancedChild(childWorkload, out var handle);
        var terminal = new TerminalNode { Handle = handle, IsFocused = true };
        var root = new RecordingContainerNode(terminal);
        var actions = 0;
        root.BindingsConfig = b =>
        {
            var step = b.Key(Hex1bKey.B);
            if (binding == "global") step.Global().Action(() => actions++);
            else step.Action(() => actions++);
        };
        var ring = new FocusRing();
        ring.Rebuild(root);
        var state = new InputRouterState();

        var press = Hex1bKeyEvent.Plain(Hex1bKey.B, 'b');
        await InputRouter.RouteInputAsync(root, press, ring, state);
        var release = press with { Text = "", EventType = Hex1bKeyEventType.Release };
        await InputRouter.RouteInputAsync(root, release, ring, state);

        Assert.AreEqual(1, actions, "The release must not execute the binding again.");
        Assert.AreSame(release, TestSeq.Single(root.ReceivedInputs), "Custom ancestor handlers must still see the release.");
        Assert.IsFalse(childWorkload.Input.Reader.TryRead(out _), "The child never received the press, so it must not get a release.");
    }

    [TestMethod]
    [DataRow("focus")]
    [DataRow("capture")]
    [DataRow("remove")]
    [DataRow("replace-handle")]
    public async Task RouteInput_HandoffBeforeRelease_ReleasesOriginalChildExactlyOnce(string handoff)
    {
        await using var firstWorkload = new GatedWorkload();
        await using var first = CreateEnhancedChild(firstWorkload, out var firstHandle);
        await using var secondWorkload = new GatedWorkload();
        await using var second = CreateEnhancedChild(secondWorkload, out var secondHandle);
        await using var thirdWorkload = new GatedWorkload();
        await using var third = CreateEnhancedChild(thirdWorkload, out var thirdHandle);
        var firstNode = new TerminalNode { Handle = firstHandle };
        var secondNode = new TerminalNode { Handle = secondHandle };
        var root = new RecordingContainerNode(firstNode, secondNode);
        var ring = new FocusRing();
        ring.Rebuild(root);
        ring.Focus(firstNode);
        var state = new InputRouterState();

        // Shift is held for the press but already lifted by the release; identity ignores modifiers.
        var press = new Hex1bKeyEvent(Hex1bKey.A, "A", Hex1bModifiers.Shift) { UnicodeKeyCode = 97 };
        await InputRouter.RouteInputAsync(root, press, ring, state);
        Assert.AreEqual("\x1b[97;2u", await firstWorkload.ReadInputAsync());

        switch (handoff)
        {
            case "focus":
                ring.Focus(secondNode);
                break;
            case "capture":
                ring.CaptureInput(secondNode);
                break;
            case "remove":
                root.Children.Remove(firstNode);
                firstNode.Parent = null;
                ring.Rebuild(root);
                ring.Focus(secondNode);
                break;
            case "replace-handle":
                firstNode.Handle = thirdHandle;
                break;
        }

        var release = new Hex1bKeyEvent(Hex1bKey.A, "", Hex1bModifiers.None)
        {
            UnicodeKeyCode = 97,
            EventType = Hex1bKeyEventType.Release
        };
        Assert.AreEqual(InputResult.Handled, await InputRouter.RouteInputAsync(root, release, ring, state));
        Assert.AreEqual("\x1b[97;1:3u", await firstWorkload.ReadInputAsync());

        await InputRouter.RouteInputAsync(root, release, ring, state);
        Assert.IsFalse(firstWorkload.Input.Reader.TryRead(out _), "The original child is released exactly once.");
        Assert.IsFalse(secondWorkload.Input.Reader.TryRead(out _), "The new focus target never saw the press.");
        Assert.IsFalse(thirdWorkload.Input.Reader.TryRead(out _), "A replacement handle never saw the press.");
        Assert.HasCount(1, root.ReceivedInputs, "Only the unmatched duplicate release bubbles to custom handlers.");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RouteInput_ChildChangesEventReportingMidHold_UsesCurrentModeEncoding(bool enable)
    {
        await using var workload = new GatedWorkload();
        await using var child = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithDimensions(24, 4).WithTerminalWidget(out var handle).Build();
        if (!enable) child.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[>11u"));
        var node = new TerminalNode { Handle = handle, IsFocused = true };
        var ring = new FocusRing();
        ring.Rebuild(node);
        var state = new InputRouterState();

        var press = Hex1bKeyEvent.Plain(Hex1bKey.A, 'a') with { UnicodeKeyCode = 97 };
        await InputRouter.RouteInputAsync(node, press, ring, state);
        Assert.AreEqual(enable ? "a" : "\x1b[97u", await workload.ReadInputAsync());

        child.ApplyTokens(AnsiTokenizer.Tokenize(enable ? "\x1b[>11u" : "\x1b[<u"));
        await InputRouter.RouteInputAsync(node, press with { EventType = Hex1bKeyEventType.Repeat }, ring, state);
        Assert.AreEqual(enable ? "\x1b[97;1:2u" : "a", await workload.ReadInputAsync());
        await InputRouter.RouteInputAsync(node, press with { Text = "", EventType = Hex1bKeyEventType.Release }, ring, state);
        if (enable)
            Assert.AreEqual("\x1b[97;1:3u", await workload.ReadInputAsync());
        Assert.IsFalse(workload.Input.Reader.TryRead(out _), "Legacy mode has no release encoding.");
        Assert.IsFalse(handle.HasAnyForwardedKeys);
    }

    [TestMethod]
    public async Task RouteInput_HostWithoutReleases_BoundsTrackedPresses()
    {
        await using var workload = new GatedWorkload();
        await using var child = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithDimensions(24, 4).WithTerminalWidget(out var handle).Build();
        var node = new TerminalNode { Handle = handle, IsFocused = true };
        var ring = new FocusRing();
        ring.Rebuild(node);
        var state = new InputRouterState();
        var count = TerminalWidgetHandle.MaxForwardedKeys + 8;
        for (var i = 0; i < count; i++)
        {
            var ch = (char)('!' + i);
            await InputRouter.RouteInputAsync(node, Hex1bKeyEvent.FromText(ch.ToString()) with { UnicodeKeyCode = ch }, ring, state);
        }

        Assert.IsFalse(handle.HasForwardedKey((Hex1bKey.None, '!')), "The oldest stale press is evicted.");
        var last = (char)('!' + count - 1);
        Assert.IsTrue(handle.HasForwardedKey(InputRouterState.KeyIdentity(
            Hex1bKeyEvent.FromText(last.ToString()) with { UnicodeKeyCode = last })));
    }

    [TestMethod]
    [DataRow("stop")]
    [DataRow("cancel")]
    [DataRow("dispose")]
    [DataRow("released-first")]
    [DataRow("legacy-host")]
    public async Task RunAsync_ShutdownWithHeldEmbeddedKey_ReleasesExternallyOwnedChildOnce(string scenario)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var childWorkload = new GatedWorkload();
        await using var child = CreateEnhancedChild(childWorkload, out var handle);
        Hex1bApp? app = null;
        Hex1bAppWorkloadAdapter? appInput = null;
        await using var host = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                options => appInput = TestSeq.IsType<Hex1bAppWorkloadAdapter>(options.WorkloadAdapter),
                instance =>
                {
                    app = instance;
                    return _ => new VStackWidget([new TextBlockWidget("Shutdown ready"), new TerminalWidget(handle)]);
                })
            .WithHeadless().WithDimensions(30, 8).Build();
        var run = host.RunAsync(cancellation.Token);
        try
        {
            using var ready = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Shutdown ready"), TimeSpan.FromSeconds(5), "embedded shutdown app ready")
                .Build().ApplyAsync(host);
            var other = Hex1bKeyEvent.Plain(Hex1bKey.X, 'x') with { UnicodeKeyCode = 120 };
            if (scenario != "legacy-host")
            {
                // A delivered release shows the host reports event types.
                appInput!.TryWriteInputEvent(other);
                appInput.TryWriteInputEvent(other with { Text = "", EventType = Hex1bKeyEventType.Release });
                Assert.AreEqual("\x1b[120u", await childWorkload.ReadInputAsync());
                Assert.AreEqual("\x1b[120;1:3u", await childWorkload.ReadInputAsync());
            }
            var press = Hex1bKeyEvent.Plain(Hex1bKey.A, 'a') with { UnicodeKeyCode = 97 };
            appInput!.TryWriteInputEvent(press);
            Assert.AreEqual("\x1b[97u", await childWorkload.ReadInputAsync());
            if (scenario == "released-first")
            {
                appInput.TryWriteInputEvent(press with { Text = "", EventType = Hex1bKeyEventType.Release });
                Assert.AreEqual("\x1b[97;1:3u", await childWorkload.ReadInputAsync());
            }

            switch (scenario)
            {
                case "cancel": cancellation.Cancel(); break;
                case "dispose": await app!.DisposeAsync(); break;
                default: app!.RequestStop(); break;
            }
            await run.WaitAsync(TimeSpan.FromSeconds(5));

            if (scenario is "stop" or "cancel" or "dispose")
                Assert.AreEqual("\x1b[97;1:3u", await childWorkload.ReadInputAsync());
            Assert.IsFalse(childWorkload.Input.Reader.TryRead(out _), "Shutdown must not duplicate or invent releases.");
            Assert.IsFalse(handle.HasAnyForwardedKeys);
            Assert.AreNotEqual(TerminalState.Completed, handle.State, "The externally owned child outlives the app.");
        }
        finally
        {
            cancellation.Cancel();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RunAsync_ChildChannelClosedAfterAppInputCompletes_PropagatesChildFailure(bool childFails)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var expected = new ChannelClosedException();
        await using var workload = new GatedWorkload { BlockFirstWrite = true, WriteFailure = childFails ? expected : null };
        await using var child = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithDimensions(24, 4).WithTerminalWidget(out var handle).Build();
        Hex1bAppWorkloadAdapter? appInput = null;
        await using var host = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                options => appInput = TestSeq.IsType<Hex1bAppWorkloadAdapter>(options.WorkloadAdapter),
                _ => new VStackWidget([new TextBlockWidget("Closed race ready"), new TerminalWidget(handle)]))
            .WithHeadless().WithDimensions(30, 8).Build();
        var run = host.RunAsync(cancellation.Token);
        try
        {
            using var ready = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Closed race ready"), TimeSpan.FromSeconds(5), "closed race app ready")
                .Build().ApplyAsync(host);
            if (childFails)
            {
                appInput!.SendKey(Hex1bKey.A, 'a');
                await workload.FirstWrite.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }

            // The final app event is already dequeued; app input now completes with nothing left to drain.
            await appInput!.DisposeAsync();
            Assert.IsTrue(appInput.InputEvents.Completion.IsCompleted);
            workload.Unblock.TrySetResult();

            if (childFails)
            {
                var error = await Assert.ThrowsAsync<ChannelClosedException>(() => run.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.AreSame(expected, error);
            }
            else
            {
                await run.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        finally
        {
            workload.Unblock.TrySetResult();
            cancellation.Cancel();
            try { await run.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (ChannelClosedException ex) when (ReferenceEquals(ex, expected)) { }
        }
    }

    private static Hex1bTerminal CreateEnhancedChild(GatedWorkload workload, out TerminalWidgetHandle handle)
    {
        var child = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithDimensions(24, 4).WithTerminalWidget(out handle).Build();
        child.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[>11u"));
        return child;
    }

    private sealed class RecordingContainerNode : Hex1bNode
    {
        public RecordingContainerNode(params Hex1bNode[] children)
        {
            foreach (var child in children)
            {
                Children.Add(child);
                child.Parent = this;
            }
        }

        public List<Hex1bNode> Children { get; } = [];
        public List<Hex1bEvent> ReceivedInputs { get; } = [];
        public Action<InputBindingsBuilder>? BindingsConfig { get; set; }

        public override InputResult HandleInput(Hex1bEvent inputEvent)
        {
            ReceivedInputs.Add(inputEvent);
            return InputResult.NotHandled;
        }

        public override void ConfigureDefaultBindings(InputBindingsBuilder bindings) => BindingsConfig?.Invoke(bindings);
        protected override Layout.Size MeasureCore(Layout.Constraints constraints) => new(80, 24);
        public override void Render(Hex1bRenderContext context) { }
        public override IEnumerable<Hex1bNode> GetChildren() => Children;
        public override IEnumerable<Hex1bNode> GetFocusableNodes() => Children.SelectMany(c => c.GetFocusableNodes());
    }

    private sealed class GatedWorkload : IHex1bTerminalWorkloadAdapter
    {
        private readonly Channel<ReadOnlyMemory<byte>> _output = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        internal Channel<string> Input { get; } = Channel.CreateUnbounded<string>();
        internal TaskCompletionSource FirstWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Unblock { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool BlockFirstWrite { get; init; }
        internal Exception? WriteFailure { get; init; }
        private int _writes;

        internal async Task<string> ReadInputAsync()
            => await Input.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
            => await _output.Reader.ReadAsync(ct);

        public async ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            var first = Interlocked.Increment(ref _writes) == 1;
            if (first) FirstWrite.TrySetResult();
            if (first && BlockFirstWrite) await Unblock.Task.WaitAsync(ct);
            if (WriteFailure is not null) throw WriteFailure;
            await Input.Writer.WriteAsync(Encoding.UTF8.GetString(data.Span), ct);
        }

        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default) => ValueTask.CompletedTask;
        public event Action? Disconnected { add { } remove { } }
        public ValueTask DisposeAsync()
        {
            Unblock.TrySetResult();
            _output.Writer.TryComplete();
            Input.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
