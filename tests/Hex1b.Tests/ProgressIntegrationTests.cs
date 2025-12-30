using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Terminal;
using Hex1b.Terminal.Automation;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Integration tests for the Progress widget using Hex1bApp.
/// Tests various scenarios, layout conditions, and exports SVG, HTML, ANSI, and asciinema recordings.
/// </summary>
public class ProgressIntegrationTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    private string GetTempFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hex1b_progress_test_{Guid.NewGuid()}.cast");
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); } catch { }
        }
    }

    #region Basic Rendering Tests

    [Fact]
    public async Task Progress_RendersBasicDeterminateBar()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 10);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Download Progress: 50%"),
                v.Progress(50)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Download Progress"), TimeSpan.FromSeconds(2))
            .Capture("progress-determinate-50")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Verify the progress bar characters are present
        Assert.True(snapshot.ContainsText("█") || snapshot.ContainsText("░"), 
            "Progress bar should contain filled or empty characters");
    }

    [Fact]
    public async Task Progress_RendersZeroPercent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 10);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Progress: 0%"),
                v.Progress(0)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Progress: 0%"), TimeSpan.FromSeconds(2))
            .Capture("progress-zero-percent")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Progress_Renders100Percent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 10);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Progress: 100%"),
                v.Progress(100)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Progress: 100%"), TimeSpan.FromSeconds(2))
            .Capture("progress-hundred-percent")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Progress_RendersCustomRange()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 10);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Bytes: 1500 / 5000"),
                v.Progress(current: 1500, min: 0, max: 5000)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Bytes:"), TimeSpan.FromSeconds(2))
            .Capture("progress-custom-range")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Progress_RendersNegativeRange()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 10);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Temperature: 20°C"),
                v.Progress(current: 20, min: -40, max: 50)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Temperature:"), TimeSpan.FromSeconds(2))
            .Capture("progress-negative-range")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Progress_RendersIndeterminate()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 10);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Loading..."),
                v.ProgressIndeterminate(0.3)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Loading..."), TimeSpan.FromSeconds(2))
            .Capture("progress-indeterminate")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    #endregion

    #region Layout Tests

    [Fact]
    public async Task Progress_FillsAvailableWidth()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 10);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Full width progress bar:"),
                v.Progress(75)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Full width"), TimeSpan.FromSeconds(2))
            .Capture("progress-full-width")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Progress_RespectsFixedWidth()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 10);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Fixed width (30 chars):"),
                v.Progress(60).FixedWidth(30)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Fixed width"), TimeSpan.FromSeconds(2))
            .Capture("progress-fixed-width")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Progress_InHStackWithFill()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 10);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Progress in HStack:"),
                v.HStack(h => [
                    h.Text("Loading: "),
                    h.Progress(45).Fill()
                ])
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Loading:"), TimeSpan.FromSeconds(2))
            .Capture("progress-hstack-fill")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Progress_MultipleInVStack()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 15);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Multiple Progress Bars:"),
                v.Text(""),
                v.Text("CPU Usage:"),
                v.Progress(85),
                v.Text(""),
                v.Text("Memory:"),
                v.Progress(42),
                v.Text(""),
                v.Text("Disk I/O:"),
                v.Progress(23)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Disk I/O:"), TimeSpan.FromSeconds(2))
            .Capture("progress-multiple-bars")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Progress_InBorder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 10);

        using var app = new Hex1bApp(
            ctx => ctx.Border(b => [
                b.Text("Download in Progress"),
                b.Text(""),
                b.Progress(67),
                b.Text(""),
                b.Text("Estimated time: 2:30")
            ], title: "File Transfer"),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File Transfer"), TimeSpan.FromSeconds(2))
            .Capture("progress-in-border")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Theory]
    [InlineData(40)]
    [InlineData(60)]
    [InlineData(80)]
    [InlineData(120)]
    public async Task Progress_RespondsToTerminalWidth(int width)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, width, 10);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text($"Terminal width: {width} columns"),
                v.Progress(50)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText($"width: {width}"), TimeSpan.FromSeconds(2))
            .Capture($"progress-width-{width}")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    #endregion

    #region Dynamic Update Tests

    [Fact]
    public async Task Progress_UpdatesDynamically()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 10);
        var progress = 0.0;

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text($"Progress: {progress:F0}%"),
                v.Progress(progress)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        // Wait for initial render
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Progress: 0%"), TimeSpan.FromSeconds(2))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Update progress several times
        for (int i = 25; i <= 100; i += 25)
        {
            progress = i;
            app.Invalidate();
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        var snapshot = terminal.CreateSnapshot();
        TestCaptureHelper.Capture(snapshot, "progress-dynamic-update");

        Assert.True(snapshot.ContainsText("Progress: 100%"));

        cts.Cancel();
        await runTask;
    }

    #endregion

    #region Theming Tests

    [Fact]
    public async Task Progress_RespectsCustomTheme()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 10);

        var customTheme = new Hex1bTheme("CustomProgress")
            .Set(ProgressTheme.FilledCharacter, '▓')
            .Set(ProgressTheme.EmptyCharacter, '░')
            .Set(ProgressTheme.FilledForegroundColor, Hex1bColor.Blue)
            .Set(ProgressTheme.EmptyForegroundColor, Hex1bColor.Gray);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Custom themed progress:"),
                v.Progress(65)
            ]),
            new Hex1bAppOptions 
            { 
                WorkloadAdapter = workload,
                Theme = customTheme
            }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Custom themed"), TimeSpan.FromSeconds(2))
            .Capture("progress-custom-theme")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    #endregion

    #region Asciinema Recording Tests

    [Fact]
    public async Task Progress_RecordsAnimatedIndeterminate()
    {
        var tempFile = GetTempFile();
        using var workload = new Hex1bAppWorkloadAdapter();
        var terminalOptions = new Hex1bTerminalOptions
        {
            Width = 60,
            Height = 10,
            WorkloadAdapter = workload
        };
        var recorder = terminalOptions.AddAsciinemaRecorder(tempFile, new AsciinemaRecorderOptions
        {
            Title = "Indeterminate Progress Animation",
            IdleTimeLimit = 0.5f
        });
        using var terminal = new Hex1bTerminal(terminalOptions);

        var animationPos = 0.0;

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Loading data..."),
                v.ProgressIndeterminate(animationPos)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        recorder.AddMarker("Animation Start");

        // Animate for about 2 seconds
        for (int i = 0; i < 40; i++)
        {
            animationPos = (animationPos + 0.05) % 1.0;
            app.Invalidate();
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        recorder.AddMarker("Animation End");

        var snapshot = terminal.CreateSnapshot();
        TestCaptureHelper.Capture(snapshot, "progress-indeterminate-animated");
        await TestCaptureHelper.CaptureCastAsync(recorder, "progress-indeterminate-animation", TestContext.Current.CancellationToken);

        cts.Cancel();
        await runTask;
    }

    [Fact]
    public async Task Progress_RecordsDownloadSimulation()
    {
        var tempFile = GetTempFile();
        using var workload = new Hex1bAppWorkloadAdapter();
        var terminalOptions = new Hex1bTerminalOptions
        {
            Width = 70,
            Height = 12,
            WorkloadAdapter = workload
        };
        var recorder = terminalOptions.AddAsciinemaRecorder(tempFile, new AsciinemaRecorderOptions
        {
            Title = "File Download Progress Simulation",
            IdleTimeLimit = 0.5f
        });
        using var terminal = new Hex1bTerminal(terminalOptions);

        var bytesDownloaded = 0L;
        var totalBytes = 10_000_000L;
        var speed = "0 KB/s";
        var eta = "Calculating...";

        using var app = new Hex1bApp(
            ctx => ctx.Border(b => [
                b.Text("Downloading: large-file.zip"),
                b.Text(""),
                b.Progress(current: bytesDownloaded, min: 0, max: totalBytes),
                b.Text(""),
                b.Text($"Downloaded: {bytesDownloaded / 1_000_000.0:F1} MB / {totalBytes / 1_000_000.0:F1} MB"),
                b.Text($"Speed: {speed}"),
                b.Text($"ETA: {eta}")
            ], title: "Download Manager"),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        recorder.AddMarker("Download Start");

        // Wait for initial render
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Downloading:"), TimeSpan.FromSeconds(2))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Simulate download progress
        var random = new Random(42);
        while (bytesDownloaded < totalBytes)
        {
            var chunk = random.Next(100_000, 500_000);
            bytesDownloaded = Math.Min(bytesDownloaded + chunk, totalBytes);
            
            var speedKb = chunk / 50; // KB per 50ms
            speed = $"{speedKb:N0} KB/s";
            
            var remaining = totalBytes - bytesDownloaded;
            var etaSeconds = speedKb > 0 ? remaining / 1000 / speedKb : 0;
            eta = etaSeconds > 0 ? $"{etaSeconds / 60}:{etaSeconds % 60:D2}" : "Complete!";
            
            app.Invalidate();
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        recorder.AddMarker("Download Complete");

        await Task.Delay(500, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        TestCaptureHelper.Capture(snapshot, "progress-download-complete");
        await TestCaptureHelper.CaptureCastAsync(recorder, "progress-download-simulation", TestContext.Current.CancellationToken);

        cts.Cancel();
        await runTask;
    }

    [Fact]
    public async Task Progress_RecordsMultipleProgressBars()
    {
        var tempFile = GetTempFile();
        using var workload = new Hex1bAppWorkloadAdapter();
        var terminalOptions = new Hex1bTerminalOptions
        {
            Width = 80,
            Height = 20,
            WorkloadAdapter = workload
        };
        var recorder = terminalOptions.AddAsciinemaRecorder(tempFile, new AsciinemaRecorderOptions
        {
            Title = "Multi-Task Progress",
            IdleTimeLimit = 0.5f
        });
        using var terminal = new Hex1bTerminal(terminalOptions);

        var task1Progress = 0.0;
        var task2Progress = 0.0;
        var task3Progress = 0.0;

        using var app = new Hex1bApp(
            ctx => ctx.Border(b => [
                b.Text("Build Pipeline"),
                b.Text(""),
                b.Text("Compiling source..."),
                b.Progress(task1Progress),
                b.Text(""),
                b.Text("Running tests..."),
                b.Progress(task2Progress),
                b.Text(""),
                b.Text("Generating docs..."),
                b.Progress(task3Progress)
            ], title: "Build Status"),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        recorder.AddMarker("Build Start");

        // Wait for initial render
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Build Pipeline"), TimeSpan.FromSeconds(2))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Simulate staggered task completion
        var random = new Random(42);
        for (int i = 0; i < 30; i++)
        {
            task1Progress = Math.Min(100, task1Progress + random.Next(2, 8));
            if (task1Progress > 30) task2Progress = Math.Min(100, task2Progress + random.Next(1, 6));
            if (task2Progress > 50) task3Progress = Math.Min(100, task3Progress + random.Next(1, 5));
            
            app.Invalidate();
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        // Complete all tasks
        task1Progress = 100;
        task2Progress = 100;
        task3Progress = 100;
        app.Invalidate();

        recorder.AddMarker("Build Complete");

        await Task.Delay(300, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        TestCaptureHelper.Capture(snapshot, "progress-multi-task");
        await TestCaptureHelper.CaptureCastAsync(recorder, "progress-multi-task", TestContext.Current.CancellationToken);

        cts.Cancel();
        await runTask;
    }

    [Fact]
    public async Task Progress_RecordsResizeScenario()
    {
        var tempFile = GetTempFile();
        using var workload = new Hex1bAppWorkloadAdapter();
        var terminalOptions = new Hex1bTerminalOptions
        {
            Width = 100,
            Height = 10,
            WorkloadAdapter = workload
        };
        var recorder = terminalOptions.AddAsciinemaRecorder(tempFile, new AsciinemaRecorderOptions
        {
            Title = "Progress Bar Resize Behavior",
            IdleTimeLimit = 1.0f
        });
        using var terminal = new Hex1bTerminal(terminalOptions);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Resize to see progress bar adapt:"),
                v.Progress(75),
                v.Text(""),
                v.Text("The progress bar fills available width")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        recorder.AddMarker("Initial Size (100 cols)");

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Resize"), TimeSpan.FromSeconds(2))
            .Wait(TimeSpan.FromMilliseconds(500))
            .Capture("wide")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Resize to medium
        recorder.AddMarker("Resize to 60 cols");
        await ((IHex1bTerminalWorkloadFilter)recorder).OnResizeAsync(60, 10, TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        terminal.Resize(60, 10);
        await workload.ResizeAsync(60, 10, TestContext.Current.CancellationToken);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        var mediumSnapshot = terminal.CreateSnapshot();
        TestCaptureHelper.Capture(mediumSnapshot, "progress-resize-medium");

        // Resize to narrow
        recorder.AddMarker("Resize to 40 cols");
        await ((IHex1bTerminalWorkloadFilter)recorder).OnResizeAsync(40, 10, TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        terminal.Resize(40, 10);
        await workload.ResizeAsync(40, 10, TestContext.Current.CancellationToken);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        var narrowSnapshot = terminal.CreateSnapshot();
        TestCaptureHelper.Capture(narrowSnapshot, "progress-resize-narrow");

        // Resize back to wide
        recorder.AddMarker("Resize to 120 cols");
        await ((IHex1bTerminalWorkloadFilter)recorder).OnResizeAsync(120, 10, TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        terminal.Resize(120, 10);
        await workload.ResizeAsync(120, 10, TestContext.Current.CancellationToken);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        var extraWideSnapshot = terminal.CreateSnapshot();
        TestCaptureHelper.Capture(extraWideSnapshot, "progress-resize-extrawide");

        await TestCaptureHelper.CaptureCastAsync(recorder, "progress-resize-demo", TestContext.Current.CancellationToken);

        cts.Cancel();
        await runTask;
    }

    #endregion
}
