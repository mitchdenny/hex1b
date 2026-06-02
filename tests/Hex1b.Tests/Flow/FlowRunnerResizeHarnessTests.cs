using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Hex1b;
using Hex1b.Flow;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests.Flow;

/// <summary>
/// Minimal in-process harness that wires up <see cref="Hex1bFlowRunner"/>
/// against a fake parent adapter (<see cref="RecordingParentAdapter"/>)
/// which captures every byte the runner writes during the lifecycle.
/// Designed to answer one question: <b>what side effects does the runner
/// emit to the parent terminal during a resize event burst?</b>
/// </summary>
/// <remarks>
/// The harness deliberately ignores everything the inner Hex1bApp/step
/// adapter writes (those bytes are captured separately and asserted on
/// their own) so test assertions can focus on the runner's own
/// resize-handling discipline without being noised up by the inner app's
/// continuous render output.
/// </remarks>
[TestClass]
public class FlowRunnerResizeHarnessTests
{
    [TestMethod]
    public async Task PerEventDuringDrag_DoesNotEmitBytesThatWouldScrollHostTerminal()
    {
        var adapter = new RecordingParentAdapter(width: 80, height: 24);

        var stepStarted = new TaskCompletionSource();
        FlowStep? step = null;

        var runner = new Hex1bFlowRunner(
            flowCallback: async flow =>
            {
                await flow.ShowAsync(ctx => ctx.Text("HEADER"));

                step = flow.Step(ctx => ctx.Text("ACTIVE"));
                stepStarted.SetResult();
                await step.WaitForCompletionAsync();
            },
            options: new Hex1bFlowOptions
            {
                UseSoftWrapTombstones = true,
                InitialCursorRow = 0,
                ResizeSettleDelay = TimeSpan.FromMilliseconds(60),
            },
            parentAdapter: adapter);

        var runTask = runner.RunAsync(CancellationToken.None);

        // Let the flow get into the active step.
        await stepStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(50); // small grace for the step to render its first frame.

        var beforeBurst = adapter.SnapshotWrites();

        // Simulate a drag: shrink the terminal across multiple events.
        // Width shrinks each tick — the very pattern that, in the bug,
        // caused the runner's bottom-overflow handler to emit a "\n" at
        // the bottom row and scroll the tombstones above off-screen.
        for (int w = 78; w >= 40; w -= 2)
        {
            adapter.SetSize(w, 24);
            await adapter.PushEventAsync(new Hex1bResizeEvent(w, 24));
            await Task.Delay(5); // tighter than the 60ms settle window
        }

        // Capture writes that the runner emitted during the burst,
        // BEFORE the settle pass fires. The settle pass writes its own
        // (legitimate) clear+repaint sequence which we don't want to
        // mistake for a per-event scroll.
        var duringBurstWrites = adapter.SnapshotWritesSince(beforeBurst);
        var duringBurst = string.Concat(duringBurstWrites);

        // The runner's per-event discipline: at most a cursor-hide. No
        // newlines, no ESC[J, no ESC[2K, no SetCursorPosition.
        Assert.DoesNotContain("\n", duringBurst);
        Assert.DoesNotContain("\x1b[J", duringBurst);
        Assert.DoesNotContain("\x1b[2K", duringBurst);
        Assert.DoesNotContain("\x1b[K", duringBurst);
        Assert.IsFalse(Regex.IsMatch(duringBurst, @"\x1b\[\d+;\d+H"), "Runner must not emit CUP (cursor position) during a resize burst.");

        // Let settle fire.
        await Task.Delay(150);

        step!.Complete();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public async Task DuringDrag_RunnerDisablesDECAWM_SoStaleInnerAppOutputCannotScrollTombstones()
    {
        // The inner Hex1bApp keeps emitting frames during a drag (focus
        // blink, animations, etc.). If the terminal has been shrunk and
        // DECAWM is on, that stale wide content wraps at the new right
        // edge — and any wrap that lands on the bottom row scrolls the
        // buffer, pushing the tombstones above off-screen. The runner
        // must defensively turn DECAWM off for the duration of the drag.
        var adapter = new RecordingParentAdapter(width: 80, height: 24);

        var stepStarted = new TaskCompletionSource();
        FlowStep? step = null;

        var runner = new Hex1bFlowRunner(
            flowCallback: async flow =>
            {
                await flow.ShowAsync(ctx => ctx.Text("HEADER"));

                step = flow.Step(ctx => ctx.Text("ACTIVE"));
                stepStarted.SetResult();
                await step.WaitForCompletionAsync();
            },
            options: new Hex1bFlowOptions
            {
                UseSoftWrapTombstones = true,
                InitialCursorRow = 0,
                ResizeSettleDelay = TimeSpan.FromMilliseconds(60),
            },
            parentAdapter: adapter);

        var runTask = runner.RunAsync(CancellationToken.None);

        await stepStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(50);

        var beforeBurst = adapter.SnapshotWrites();

        adapter.SetSize(60, 24);
        await adapter.PushEventAsync(new Hex1bResizeEvent(60, 24));
        await Task.Delay(5);

        // Per-event must have disabled DECAWM. We assert this BEFORE the
        // settle window fires so the assertion can't be satisfied by the
        // settle pass's own DECAWM toggling.
        var perEventWrites = string.Concat(adapter.SnapshotWritesSince(beforeBurst));
        Assert.Contains("\x1b[?7l", perEventWrites);

        // And the per-event pass must NOT have re-enabled it (otherwise
        // the rest of the drag is unprotected). Only the settle pass is
        // allowed to turn DECAWM back on.
        Assert.DoesNotContain("\x1b[?7h", perEventWrites);

        // Wait for settle and assert DECAWM is re-enabled.
        await Task.Delay(150);
        var fullWrites = string.Concat(adapter.SnapshotWritesSince(beforeBurst));
        Assert.Contains("\x1b[?7h", fullWrites);

        step!.Complete();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));
    }
}

/// <summary>
/// Records every <c>Write</c> call against a synthetic parent adapter and
/// exposes a settable <c>Width</c>/<c>Height</c> plus a writable input
/// event channel — the minimum surface the flow runner needs to observe a
/// "resize burst" in a unit test.
/// </summary>
internal sealed class RecordingParentAdapter : IHex1bAppTerminalWorkloadAdapter
{
    private readonly Channel<Hex1bEvent> _inputChannel = Channel.CreateUnbounded<Hex1bEvent>();
    private readonly ConcurrentQueue<string> _writes = new();
    private int _width;
    private int _height;

    public RecordingParentAdapter(int width, int height)
    {
        _width = width;
        _height = height;
    }

    public int Width => _width;
    public int Height => _height;
    public TerminalCapabilities Capabilities { get; } = TerminalCapabilities.Modern;
    public ChannelReader<Hex1bEvent> InputEvents => _inputChannel.Reader;
    public int OutputQueueDepth => 0;

    public event Action? Disconnected;

    public void Write(string text) => _writes.Enqueue(text);
    public void Write(ReadOnlySpan<byte> data) => _writes.Enqueue(Encoding.UTF8.GetString(data));
    public void Flush() { }

    public void EnterTuiMode() { }
    public void ExitTuiMode() { }
    public void Clear() => _writes.Enqueue("\x1b[2J");
    public void SetCursorPosition(int left, int top) =>
        _writes.Enqueue($"\x1b[{top + 1};{left + 1}H");

    public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
        => ValueTask.FromResult<ReadOnlyMemory<byte>>(ReadOnlyMemory<byte>.Empty);
    public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        => ValueTask.CompletedTask;
    public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
    {
        _width = width;
        _height = height;
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _inputChannel.Writer.TryComplete();
        Disconnected?.Invoke();
        return ValueTask.CompletedTask;
    }

    public void SetSize(int width, int height)
    {
        _width = width;
        _height = height;
    }

    public ValueTask PushEventAsync(Hex1bEvent evt) => _inputChannel.Writer.WriteAsync(evt);

    /// <summary>Returns a snapshot count of writes so far.</summary>
    public int SnapshotWrites() => _writes.Count;

    /// <summary>Returns all writes captured since the supplied snapshot index.</summary>
    public IReadOnlyList<string> SnapshotWritesSince(int snapshotIndex)
    {
        var all = _writes.ToArray();
        if (snapshotIndex >= all.Length) return Array.Empty<string>();
        return all.AsSpan(snapshotIndex).ToArray();
    }
}
