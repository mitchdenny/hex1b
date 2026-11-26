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
