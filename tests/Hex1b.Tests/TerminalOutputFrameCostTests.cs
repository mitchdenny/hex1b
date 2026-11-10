using System.Diagnostics.Metrics;
using Hex1b.Automation;
using Hex1b.Diagnostics;
using Hex1b.Tokens;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class TerminalOutputFrameCostTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ContentOnlyOutput_CharacterizesFullFrameCostPerBatch()
    {
        using var metrics = new Hex1bMetrics();
        using var listener = new MeterListener();
        var gate = new object();
        var builds = 0;
        var reconciles = 0;
        var frames = 0;
        var buildMs = 0.0;
        var reconcileMs = 0.0;
        var frameMs = 0.0;
        TaskCompletionSource? frameCompleted = null;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (ReferenceEquals(instrument.Meter, metrics.Meter))
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, _, _) =>
        {
            lock (gate)
            {
                switch (instrument.Name)
                {
                    case "hex1b.frame.build.duration":
                        builds++;
                        buildMs += value;
                        break;
                    case "hex1b.frame.reconcile.duration":
                        reconciles++;
                        reconcileMs += value;
                        break;
                    case "hex1b.frame.duration":
                        frames++;
                        frameMs += value;
                        frameCompleted?.TrySetResult();
                        break;
                }
            }
        });
        listener.Start();

        await using var handle = new TerminalWidgetHandle(60, 16);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                options => options.Metrics = metrics,
                app => context => new VStackWidget([
                    new TextBlockWidget("Host Header"),
                    new BorderWidget(new TerminalWidget(handle)).Title("Inner Terminal"),
                ]).Fill())
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        var runTask = terminal.RunAsync(cts.Token);

        async Task WriteAndObserveAsync(string text)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (gate)
                frameCompleted = completion;

            var impacts = text.Select((character, x) => new CellImpact(
                x, 0, new TerminalCell(character.ToString(), null, null, CellAttributes.None, 0))).ToArray();
            await handle.WriteOutputWithImpactsAsync([
                new AppliedToken(new TextToken(text), impacts, 0, 0, text.Length, 0),
            ], cts.Token);
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(10), cts.Token);
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(snapshot => snapshot.ContainsText(text)
                    && snapshot.ContainsText("Host Header")
                    && snapshot.ContainsText("Inner Terminal"),
                    TimeSpan.FromSeconds(10), "output batch and host chrome rendered")
                .Build()
                .ApplyAsync(terminal, cts.Token);
        }

        try
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(snapshot => snapshot.ContainsText("Host Header")
                    && snapshot.ContainsText("Inner Terminal"),
                    TimeSpan.FromSeconds(10), "initial host frame")
                .Build()
                .ApplyAsync(terminal, cts.Token);
            await WriteAndObserveAsync("warmup");
            lock (gate)
            {
                builds = reconciles = frames = 0;
                buildMs = reconcileMs = frameMs = 0;
            }

            // Wait for each batch rather than assuming a wall-clock output cadence.
            // Timings characterize this runner; they are not performance budgets.
            const int batches = 60;
            for (var batch = 0; batch < batches; batch++)
                await WriteAndObserveAsync($"batch-{batch:D3}");

            lock (gate)
            {
                Assert.IsTrue(builds >= batches, "Every independently observed output batch rebuilds the host.");
                Assert.AreEqual(builds, reconciles, "Each host build runs the reconciliation phase.");
                Assert.AreEqual(builds, frames, "Each measured full frame includes build and reconciliation.");
                Assert.IsTrue(frameMs > 0);
                TestContext.WriteLine(
                    $"Content-only output: {batches} independently observed batches; " +
                    $"{builds} builds, {reconciles} reconciliation phases, {frames} full frames.");
                TestContext.WriteLine(
                    $"Per output batch: {builds / (double)batches:F3} builds, " +
                    $"{reconciles / (double)batches:F3} reconciliation phases, " +
                    $"{frameMs / batches:F3} ms total frame work " +
                    $"(build {buildMs / batches:F3} ms, reconcile {reconcileMs / batches:F3} ms).");
            }
        }
        finally
        {
            cts.Cancel();
            try
            {
                await runTask.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
            }
        }
    }
}
