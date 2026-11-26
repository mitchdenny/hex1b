using System.Text;
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
}
