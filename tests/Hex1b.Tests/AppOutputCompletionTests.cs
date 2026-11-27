using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class AppOutputCompletionTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task RunAsync_AppStopsWithBlockedOutput_DrainsBeforeRawModeExitOrCancels(
        bool asyncBuilder, bool cancel)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = timeout.Token;
        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await using var presentation = new GatedPresentation();
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Hex1bAppWorkloadAdapter? workload = null;

        var builder = Hex1bTerminal.CreateBuilder()
            .WithPresentation(presentation)
            .WithMouse()
            .WithDimensions(80, 24);
        void Configure(Hex1bAppOptions options)
        {
            workload = TestSeq.IsType<Hex1bAppWorkloadAdapter>(options.WorkloadAdapter);
            workload.Disconnected += () => disconnected.TrySetResult();
        }
        Func<RootContext, Hex1bWidget> ConfigureApp(Hex1bApp app) => ctx =>
        {
            app.RequestStop();
            return ctx.Text("FINAL APP FRAME");
        };
        if (asyncBuilder)
        {
            builder.WithHex1bApp(Configure, app =>
            {
                var render = ConfigureApp(app);
                return ctx => Task.FromResult(render(ctx));
            });
        }
        else
        {
            builder.WithHex1bApp(Configure, ConfigureApp);
        }

        await using var terminal = builder.Build();
        var runTask = terminal.RunAsync(runCancellation.Token);
        try
        {
            await presentation.WriteStarted.Task.WaitAsync(ct);
            await disconnected.Task.WaitAsync(ct);
            Assert.IsFalse(runTask.IsCompleted,
                "App disposal must not bypass pending presentation output.");
            Assert.AreEqual(0, presentation.RawModeExits);

            if (cancel)
            {
                runCancellation.Cancel();
                await Assert.ThrowsAsync<OperationCanceledException>(() => runTask.WaitAsync(ct));
            }
            else
            {
                presentation.ReleaseWrite.TrySetResult();
                Assert.AreEqual(0, await runTask.WaitAsync(ct));
                var output = presentation.Output.ToString();
                StringAssert.Contains(output, "FINAL APP FRAME");
                StringAssert.Contains(output, "\x1b[?1003h");
                StringAssert.Contains(output, "\x1b[?1006h");
                Assert.IsTrue(output.LastIndexOf("\x1b[?1003l", StringComparison.Ordinal) >
                    output.LastIndexOf("\x1b[?1003h", StringComparison.Ordinal),
                    "Mouse tracking must be disabled after the last enable.");
                Assert.IsTrue(output.LastIndexOf("\x1b[?1006l", StringComparison.Ordinal) >
                    output.LastIndexOf("\x1b[?1006h", StringComparison.Ordinal),
                    "SGR mouse mode must be disabled after the last enable.");
                Assert.IsTrue(output.LastIndexOf("\x1b[?1049l", StringComparison.Ordinal) >
                    output.LastIndexOf("\x1b[?1049h", StringComparison.Ordinal));
                Assert.AreEqual(1, presentation.RawModeExits);
                Assert.AreEqual(output, presentation.OutputAtRawModeExit,
                    "All queued output must reach presentation before exiting raw mode.");
                Assert.AreEqual(output, presentation.FlushedOutputAtRawModeExit,
                    "Presentation output must be flushed before exiting raw mode.");
                Assert.AreEqual(0, workload!.OutputQueueDepth);
            }
        }
        finally
        {
            presentation.ReleaseWrite.TrySetResult();
            runCancellation.Cancel();
            try
            {
                await runTask.WaitAsync(ct);
            }
            catch (OperationCanceledException) when (runCancellation.IsCancellationRequested) { }
        }
    }

    [TestMethod]
    public async Task RunAsync_CallbackReturnsWithoutOutputEof_DrainsProducedOutputWithoutWaitingForEof()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = timeout.Token;
        await using var presentation = new GatedPresentation();
        presentation.ReleaseWrite.TrySetResult();
        await using var workload = new ChannelWorkload();

        var builder = Hex1bTerminal.CreateBuilder().WithPresentation(presentation);
        builder.SetWorkloadFactory(_ => new Hex1bTerminalBuildContext(workload, async _ =>
        {
            for (var index = 0; index < 50; index++)
                workload.Write($"chunk{index};");
            await Task.Yield();
            return 7;
        }));
        await using var terminal = builder.Build();

        Assert.AreEqual(7, await terminal.RunAsync(ct).WaitAsync(ct));
        Assert.AreEqual(Enumerable.Range(0, 50).Aggregate("", (text, index) => text + $"chunk{index};"),
            presentation.OutputAtRawModeExit);
        Assert.AreEqual(presentation.OutputAtRawModeExit, presentation.FlushedOutputAtRawModeExit);
    }

    [TestMethod]
    public async Task RunAsync_WorkloadReturnsEmptyRead_StopsReadingOutput()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = timeout.Token;
        await using var presentation = new GatedPresentation();
        presentation.ReleaseWrite.TrySetResult();
        await using var workload = new ChannelWorkload();

        var builder = Hex1bTerminal.CreateBuilder().WithPresentation(presentation);
        builder.SetWorkloadFactory(_ => new Hex1bTerminalBuildContext(workload, async _ =>
        {
            workload.Write("before");
            workload.Write("");
            workload.Write("after");
            await Task.Yield();
            return 0;
        }));
        await using var terminal = builder.Build();

        await terminal.RunAsync(ct).WaitAsync(ct);
        Assert.AreEqual("before", presentation.OutputAtRawModeExit);
        Assert.AreEqual(2, workload.Reads);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunAsync_CompletedOwnedOutputWithPendingRead_WaitsForEofOrCancellation(bool cancel)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        await using var presentation = new GatedPresentation();
        presentation.ReleaseWrite.TrySetResult();
        await using var workload = new DelayedCompletedWorkload();
        var completeRun = new TaskCompletionSource<int>();
        var builder = Hex1bTerminal.CreateBuilder().WithPresentation(presentation);
        builder.SetWorkloadFactory(_ => new Hex1bTerminalBuildContext(workload,
            _ => completeRun.Task, drainOutputToEof: true));
        await using var terminal = builder.Build();
        var run = terminal.RunAsync(runCancellation.Token);
        try
        {
            await workload.ReadStarted.Task.WaitAsync(timeout.Token);
            workload.OutputReady.TrySetResult();
            await workload.ItemConsumed.Task.WaitAsync(timeout.Token);
            completeRun.TrySetResult(0);
            if (cancel)
            {
                runCancellation.Cancel();
                await Assert.ThrowsAsync<OperationCanceledException>(() => run.WaitAsync(timeout.Token));
            }
            else
            {
                Assert.IsFalse(run.IsCompleted);
                workload.ReleaseRead.TrySetResult();
                await run.WaitAsync(timeout.Token);
                Assert.AreEqual("FINAL", presentation.OutputAtRawModeExit);
                Assert.AreEqual("FINAL", presentation.FlushedOutputAtRawModeExit);
            }
        }
        finally
        {
            workload.ReleaseRead.TrySetResult();
            runCancellation.Cancel();
        }
    }

    [TestMethod]
    public async Task RunAsync_CanceledInputRead_ReleasesReaderBeforeExitingRawMode()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var presentation = new GatedInputPresentation();
        await using var workload = new ChannelWorkload();
        var completed = new TaskCompletionSource<int>();
        var builder = Hex1bTerminal.CreateBuilder().WithPresentation(presentation);
        builder.SetWorkloadFactory(_ => new Hex1bTerminalBuildContext(workload, _ => completed.Task));
        await using var terminal = builder.Build();
        var run = terminal.RunAsync(timeout.Token);
        try
        {
            await presentation.ReadStarted.Task.WaitAsync(timeout.Token);
            completed.SetResult(0);
            await presentation.ReadCanceled.Task.WaitAsync(timeout.Token);
            Assert.IsFalse(run.IsCompleted);
            Assert.IsFalse(presentation.ExitedRawMode);
            presentation.ReleaseRead.TrySetResult();
            await run.WaitAsync(timeout.Token);
            Assert.IsTrue(presentation.ExitedRawMode);
        }
        finally
        {
            presentation.ReleaseRead.TrySetResult();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Dispose_AutomaticallyStartedInput_ReleasesReaderBeforeExitingRawMode(bool sync)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var presentation = new GatedInputPresentation();
        await using var workload = new ChannelWorkload();
        var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            WorkloadAdapter = workload,
            PresentationAdapter = presentation
        });
        await presentation.ReadStarted.Task.WaitAsync(timeout.Token);
        var context = new QueuedSynchronizationContext();
        var dispose = sync ? Task.Run(() =>
        {
            SynchronizationContext.SetSynchronizationContext(context);
            try { terminal.Dispose(); }
            finally { SynchronizationContext.SetSynchronizationContext(null); }
        }) : terminal.DisposeAsync().AsTask();
        try
        {
            await presentation.ReadCanceled.Task.WaitAsync(timeout.Token);
            Assert.IsFalse(dispose.IsCompleted);
            Assert.IsFalse(presentation.ExitedRawMode);
            presentation.ReleaseRead.TrySetResult();
            await dispose.WaitAsync(timeout.Token);
            Assert.IsTrue(presentation.ExitedRawMode);
        }
        finally
        {
            presentation.ReleaseRead.TrySetResult();
            context.Drain();
        }
    }

    private sealed class QueuedSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _callbacks = new();
        public override void Post(SendOrPostCallback d, object? state) => _callbacks.Enqueue((d, state));
        public void Drain()
        {
            while (_callbacks.TryDequeue(out var callback))
                callback.Callback(callback.State);
        }
    }

    private sealed class DelayedCompletedWorkload : IHex1bTerminalWorkloadAdapter
    {
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource OutputReady { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ItemConsumed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _delivered;
        public event Action? Disconnected { add { } remove { } }
        public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
        {
            if (_delivered) return ReadOnlyMemory<byte>.Empty;
            ReadStarted.TrySetResult();
            await OutputReady.Task.WaitAsync(ct);
            ItemConsumed.TrySetResult();
            await ReleaseRead.Task.WaitAsync(ct);
            _delivered = true;
            return "FINAL"u8.ToArray();
        }
        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class GatedInputPresentation : IHex1bTerminalPresentationAdapter
    {
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReadCanceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool ExitedRawMode { get; private set; }
        public int Width => 80;
        public int Height => 24;
        public TerminalCapabilities Capabilities => TerminalCapabilities.Modern;
        public event Action<int, int>? Resized { add { } remove { } }
        public event Action? Disconnected { add { } remove { } }
        public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
        {
            ReadStarted.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            ReadCanceled.TrySetResult();
            await ReleaseRead.Task;
            return ReadOnlyMemory<byte>.Empty;
        }
        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask EnterRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask ExitRawModeAsync(CancellationToken ct = default)
        {
            ExitedRawMode = true;
            return ValueTask.CompletedTask;
        }
        public (int Row, int Column) GetCursorPosition() => (0, 0);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class GatedPresentation : IHex1bTerminalPresentationAdapter
    {
        private readonly HeadlessPresentationAdapter _headless = new(80, 24);
        public TaskCompletionSource WriteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public StringBuilder Output { get; } = new();
        public string? OutputAtRawModeExit { get; private set; }
        public string? FlushedOutputAtRawModeExit { get; private set; }
        private string? _flushedOutput;
        public int RawModeExits { get; private set; }
        public int Width => _headless.Width;
        public int Height => _headless.Height;
        public TerminalCapabilities Capabilities => TerminalCapabilities.Modern;
        public event Action<int, int>? Resized { add { } remove { } }
        public event Action? Disconnected { add { } remove { } }

        public async ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            WriteStarted.TrySetResult();
            await ReleaseWrite.Task.WaitAsync(ct);
            Output.Append(Encoding.UTF8.GetString(data.Span));
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default) => _headless.ReadInputAsync(ct);
        public ValueTask FlushAsync(CancellationToken ct = default)
        {
            _flushedOutput = Output.ToString();
            return ValueTask.CompletedTask;
        }
        public ValueTask EnterRawModeAsync(CancellationToken ct = default) => _headless.EnterRawModeAsync(ct);
        public ValueTask ExitRawModeAsync(CancellationToken ct = default)
        {
            RawModeExits++;
            OutputAtRawModeExit = Output.ToString();
            FlushedOutputAtRawModeExit = _flushedOutput;
            return ValueTask.CompletedTask;
        }
        public (int Row, int Column) GetCursorPosition() => (0, 0);
        public ValueTask DisposeAsync() => _headless.DisposeAsync();
    }

    private sealed class ChannelWorkload : IHex1bTerminalWorkloadAdapter
    {
        private readonly Channel<byte[]> _output = Channel.CreateUnbounded<byte[]>();
        public int Reads { get; private set; }
        public event Action? Disconnected { add { } remove { } }

        public void Write(string text) => _output.Writer.TryWrite(Encoding.UTF8.GetBytes(text));

        public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
        {
            var data = await _output.Reader.ReadAsync(ct);
            Reads++;
            return data;
        }

        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            ValueTask.CompletedTask;
        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default) =>
            ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
