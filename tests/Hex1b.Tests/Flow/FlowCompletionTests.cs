using Hex1b.Flow;
using Hex1b.Tokens;
using Hex1b.Widgets;
using System.Text;

namespace Hex1b.Tests.Flow;

[TestClass]
public class FlowCompletionTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunAsync_CompletedFlowWithBlockedOutput_DrainsOrCancels(bool cancel)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = timeout.Token;
        await using var presentation = new GatedPresentation();
        await using var workload = new Hex1bAppWorkloadAdapter(TerminalCapabilities.Modern);
        await workload.ResizeAsync(100, 32, ct);
        workload.InputEvents.TryRead(out _);
        var runner = new Hex1bFlowRunner(async flow =>
        {
            var step = flow.Step(ctx => ctx.Surface(s => [
                s.Layer(surface => surface.WriteText(0, 0, "ACTIVE"))
            ]).Fill());
            await step.CompleteAsync(ctx => new VStackWidget([
                ctx.Surface(s => [s.Layer(surface =>
                {
                    surface.WriteText(0, 0, "FINAL CAPTION");
                    surface.WriteText(93, 30, "FOOTER");
                })]).FixedWidth(100).FixedHeight(31)
            ]), ct);
        }, new Hex1bFlowOptions { UseSoftWrapTombstones = true }, workload);
        await runner.RunAsync(ct);
        workload.CompleteOutput();

        // Start consumption only after the flow has queued all final bytes.
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            Width = 100,
            Height = 32,
            WorkloadAdapter = workload,
            PresentationAdapter = presentation,
            RunCallback = _ => Task.FromResult(0)
        });
        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var runTask = terminal.RunAsync(runCancellation.Token);
        try
        {
            await presentation.WriteStarted.Task.WaitAsync(ct);
            Assert.IsFalse(runTask.IsCompleted, "Completion must wait for the queued output to reach presentation.");
            if (cancel)
            {
                runCancellation.Cancel();
                await Assert.ThrowsAsync<OperationCanceledException>(() => runTask.WaitAsync(ct));
            }
            else
            {
                presentation.ReleaseWrite.SetResult();
                Assert.AreEqual(0, await runTask.WaitAsync(ct));
                using var snapshot = terminal.CreateSnapshot();
                Assert.IsTrue(snapshot.ContainsText("FINAL CAPTION"));
                Assert.IsTrue(snapshot.ContainsText("FOOTER"));
                await terminal.DisposeAsync();
                using var disposedSnapshot = terminal.CreateSnapshot();
                Assert.IsTrue(disposedSnapshot.ContainsText("FINAL CAPTION"));
                Assert.IsTrue(disposedSnapshot.ContainsText("FOOTER"));
            }
        }
        finally
        {
            presentation.ReleaseWrite.TrySetResult();
            runCancellation.Cancel();
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task CompleteAsync_FixedSurface_PreservesShellCursorAndContent(bool resize, bool syncDispose)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = timeout.Token;
        var started = new TaskCompletionSource<FlowStep>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finalWidth = resize ? 60 : 100;
        var finalHeight = (resize ? 24 : 32) - 1;
        var renderedWidth = 0;
        var renderedHeight = 0;
        var frame = 0;

        await using var host = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            Width = 100,
            Height = 32,
            WorkloadAdapter = new Hex1bAppWorkloadAdapter(),
            DeferStart = true
        });
        // A previous application can leave a saved cursor near the top-right.
        host.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;98H\x1b" + "7"));

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPresentation(new CursorRestoringPresentation(host))
            .WithDimensions(100, 32)
            .WithHex1bFlow(async flow =>
            {
                var step = flow.Step(ctx => ctx.Surface(s => [s.Layer(surface =>
                {
                    surface.WriteText(0, 0, $"ACTIVE {Interlocked.Increment(ref frame)}");
                    ctx.Step.Invalidate();
                })]).Fill());
                started.SetResult(step);
                await finish.Task.WaitAsync(ct);
                await step.CompleteAsync(ctx =>
                {
                    Hex1bWidget surface = ctx.Surface(s => [s.Layer(output =>
                    {
                        renderedWidth = output.Width;
                        renderedHeight = output.Height;
                        output.WriteText(0, 0, "FINAL CAPTION");
                        output.WriteText(output.Width - 7, output.Height - 1, "FOOTER");
                    })]).Fill();
                    return new VStackWidget([
                        surface.FixedWidth(flow.TerminalWidth).FixedHeight(flow.TerminalHeight - 1)
                    ]);
                }, ct);
            }, options =>
            {
                options.UseSoftWrapTombstones = true;
                options.CursorRowProvider = () => null;
            })
            .Build();

        var runTask = terminal.RunAsync(ct);
        var active = await started.Task.WaitAsync(ct);
        try
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("ACTIVE"), TimeSpan.FromSeconds(2), "active frame")
                .Build().ApplyAsync(terminal, ct);
            if (resize)
            {
                host.Resize(finalWidth, finalHeight + 1);
                await terminal.ResizeForAutomationAsync(finalWidth, finalHeight + 1, ct);
            }

            using var canceledWait = new CancellationTokenSource();
            canceledWait.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => active.WaitForCompletionAsync(canceledWait.Token));
            Assert.IsFalse(runTask.IsCompleted, "Canceling a wait must not stop the active step.");

            finish.SetResult();
            Assert.AreEqual(0, await runTask.WaitAsync(ct));
            Assert.AreEqual(finalWidth, renderedWidth);
            Assert.AreEqual(finalHeight, renderedHeight);
            using (var snapshot = terminal.CreateSnapshot())
            {
                Assert.IsTrue(snapshot.ContainsText("FINAL CAPTION"));
                Assert.IsTrue(snapshot.ContainsText("FOOTER"));
                Assert.AreEqual("F", snapshot.GetCell(0, 0).Character);
                Assert.AreEqual("F", snapshot.GetCell(finalWidth - 7, finalHeight - 1).Character);
                Assert.IsFalse(snapshot.ContainsText("ACTIVE"));
            }

            Assert.AreEqual(0, host.CursorX);
            Assert.AreEqual(finalHeight, host.CursorY, "The shell cursor should be below the final frame.");
            if (syncDispose)
                terminal.Dispose();
            else
                await terminal.DisposeAsync();
            using var disposedSnapshot = terminal.CreateSnapshot();
            Assert.IsTrue(disposedSnapshot.ContainsText("FINAL CAPTION"));
            Assert.IsTrue(disposedSnapshot.ContainsText("FOOTER"));
            Assert.AreEqual(0, host.CursorX, "Disposal must not restore an unrelated saved cursor.");
            Assert.AreEqual(finalHeight, host.CursorY);

            // Model a prompt that clears from its starting cursor to the bottom.
            host.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[J> "));
            using var shellSnapshot = host.CreateSnapshot();
            Assert.IsTrue(shellSnapshot.ContainsText("FINAL CAPTION"));
            Assert.IsTrue(shellSnapshot.ContainsText("FOOTER"));
            Assert.AreEqual(">", shellSnapshot.GetCell(0, finalHeight).Character);
        }
        finally
        {
            finish.TrySetResult();
            active.Complete();
            await runTask.WaitAsync(ct);
        }
    }

    private sealed class CursorRestoringPresentation(Hex1bTerminal host) : IHex1bTerminalPresentationAdapter
    {
        private readonly HeadlessPresentationAdapter _headless = new();
        public int Width => host.Width;
        public int Height => host.Height;
        public TerminalCapabilities Capabilities => TerminalCapabilities.Modern;
        public event Action<int, int>? Resized { add { } remove { } }
        public event Action? Disconnected { add { } remove { } }

        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            var output = Encoding.UTF8.GetString(data.Span);
            // xterm restores the saved cursor for every DECRST 1049, including
            // unmatched resets. Hex1b's own model ignores an unmatched reset.
            output = output.Replace("\x1b[?1049l", "\x1b[?1049l\x1b" + "8", StringComparison.Ordinal);
            host.ApplyTokens(AnsiTokenizer.Tokenize(output));
            return ValueTask.CompletedTask;
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default) => _headless.ReadInputAsync(ct);
        public ValueTask FlushAsync(CancellationToken ct = default) => _headless.FlushAsync(ct);
        public ValueTask EnterRawModeAsync(CancellationToken ct = default) => _headless.EnterRawModeAsync(ct);
        public ValueTask ExitRawModeAsync(CancellationToken ct = default) => _headless.ExitRawModeAsync(ct);
        public (int Row, int Column) GetCursorPosition() => (0, 0);
        public ValueTask DisposeAsync() => _headless.DisposeAsync();
    }

    private sealed class GatedPresentation : IHex1bTerminalPresentationAdapter
    {
        private readonly HeadlessPresentationAdapter _headless = new(100, 32);
        public TaskCompletionSource WriteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Width => _headless.Width;
        public int Height => _headless.Height;
        public TerminalCapabilities Capabilities => _headless.Capabilities;
        public event Action<int, int>? Resized { add { } remove { } }
        public event Action? Disconnected { add { } remove { } }

        public async ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            WriteStarted.TrySetResult();
            await ReleaseWrite.Task.WaitAsync(ct);
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default) => _headless.ReadInputAsync(ct);
        public ValueTask FlushAsync(CancellationToken ct = default) => _headless.FlushAsync(ct);
        public ValueTask EnterRawModeAsync(CancellationToken ct = default) => _headless.EnterRawModeAsync(ct);
        public ValueTask ExitRawModeAsync(CancellationToken ct = default) => _headless.ExitRawModeAsync(ct);
        public (int Row, int Column) GetCursorPosition() => (0, 0);
        public ValueTask DisposeAsync() => _headless.DisposeAsync();
    }
}
