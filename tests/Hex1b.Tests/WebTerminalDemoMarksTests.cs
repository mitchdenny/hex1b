using Hex1b.Reflow;
using WebTerminalDemo;

namespace Hex1b.Tests;

[TestClass]
public class WebTerminalDemoMarksTests
{
    [TestMethod]
    public async Task RunAsync_MarksScene_RetainsEveryCommandAcrossLargeHistory()
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var workload = new DemoWorkload("marks", 80, 24);
        var builder = Hex1bTerminal.CreateBuilder()
            .WithPresentation(new Hwt1PresentationAdapter(80, 24).WithReflow(GhosttyReflowStrategy.Instance))
            .WithDimensions(80, 24).WithScrollback(DemoWorkload.MarkScenarioScrollbackCapacity);
        builder.SetWorkloadFactory(_ => new Hex1bTerminalBuildContext(workload, async ct =>
        {
            await workload.RunAsync(ct);
            return 0;
        }));
        await using var terminal = builder.Build();
        var run = terminal.RunAsync(stop.Token);
        try
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(snapshot => snapshot.ContainsText("MARKS_READY") &&
                    snapshot.ContainsText("Fixed dataset"), TimeSpan.FromSeconds(20), "complete marked history")
                .Build().ApplyAsync(terminal, stop.Token);

            Assert.IsFalse(run.IsCompleted, "The populated scene must stay alive for later attachments.");
            Assert.AreEqual(48, terminal.CommandMarks.Count);
            Assert.IsTrue(terminal.TryCaptureBrowserSnapshot(new Hwt1ViewState(),
                out var snapshot, out var history, out _, out _));
            snapshot.Dispose();
            Assert.AreEqual(20111, history.TotalRows);
            Assert.AreEqual(48, history.Markers.Length);
            var row = 4;
            for (var command = 0; command < 12; command++)
            {
                var lines = (command % 4) switch { 0 => 0, 1 => 120, 2 => 960, _ => 5580 };
                var marks = history.Markers.Skip(command * 4).Take(4).ToArray();
                TestSeq.AreEqual(new[] { "prompt", "commandLine", "executing", "finished" },
                    marks.Select(mark => mark.Phase));
                TestSeq.AreEqual(new int?[] { row, row, row + 1, row + 1 + lines },
                    marks.Select(mark => mark.Row));
                TestSeq.AreEqual(new[] { 0, 2, 0, 0 }, marks.Select(mark => mark.Column));
                Assert.AreEqual((command + 1) % 5 == 0 ? 1 : 0, marks[3].ExitCode);
                row += lines + 2;
            }

            var bytes = workload.BytesRead;
            terminal.Resize(90, 7);
            await workload.ResizeAsync(90, 7, stop.Token);
            await workload.WriteInputAsync("ignored input\r"u8.ToArray(), stop.Token);
            Assert.IsTrue(terminal.TryCaptureBrowserSnapshot(new Hwt1ViewState(),
                out var resized, out var after, out _, out _));
            resized.Dispose();
            Assert.AreEqual(history.TotalRows, after.TotalRows);
            TestSeq.AreEqual(history.Markers, after.Markers);
            Assert.AreEqual(bytes, workload.BytesRead);
            Assert.IsFalse(run.IsCompleted);
        }
        finally
        {
            await stop.CancelAsync();
            try { await run.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        }
    }

    [TestMethod]
    public async Task RunAsync_MarksSceneCancelledWhileSeeding_Completes()
    {
        using var stop = new CancellationTokenSource();
        await using var workload = new DemoWorkload("marks", 80, 24);
        var run = workload.RunAsync(stop.Token);
        Assert.IsFalse(run.IsCompleted);
        await stop.CancelAsync();
        await run.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
