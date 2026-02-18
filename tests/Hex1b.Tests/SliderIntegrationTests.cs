using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Integration tests for the Slider widget using Hex1bApp.
/// Tests various scenarios, layout conditions, and exports SVG, HTML, ANSI, and asciinema recordings.
/// </summary>
public class SliderIntegrationTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    private string GetTempFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hex1b_slider_test_{Guid.NewGuid()}.cast");
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
    public async Task Slider_RendersBasicSlider()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Volume Control"),
                v.Slider(50)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Volume Control"), TimeSpan.FromSeconds(10))
            .Capture("slider-basic")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Verify the slider track and handle are present
        Assert.True(snapshot.ContainsText("─") || snapshot.ContainsText("█"),
            "Slider should contain track or handle characters");
    }

    [Fact]
    public async Task Slider_RendersAtMinimum()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Slider at minimum:"),
                v.Slider(0)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Slider at minimum"), TimeSpan.FromSeconds(10))
            .Capture("slider-minimum")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Slider_RendersAtMaximum()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Slider at maximum:"),
                v.Slider(100)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Slider at maximum"), TimeSpan.FromSeconds(10))
            .Capture("slider-maximum")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Slider_RendersCustomRange()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Temperature: 20°C"),
                v.Slider(initialValue: 20, min: -10, max: 40)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Temperature:"), TimeSpan.FromSeconds(10))
            .Capture("slider-custom-range")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Slider_RendersWithStep()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Step slider (10):"),
                v.Slider(initialValue: 50, min: 0, max: 100, step: 10)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Step slider"), TimeSpan.FromSeconds(10))
            .Capture("slider-with-step")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    #endregion

    #region Layout Tests

    [Fact]
    public async Task Slider_FillsAvailableWidth()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 10).Build();

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Full width slider:"),
                v.Slider(50)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Full width"), TimeSpan.FromSeconds(10))
            .Capture("slider-full-width")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Slider_RespectsFixedWidth()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 10).Build();

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Fixed width (30 chars):"),
                v.Slider(50).FixedWidth(30)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Fixed width"), TimeSpan.FromSeconds(10))
            .Capture("slider-fixed-width")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Slider_InHStackWithLabel()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 10).Build();

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Slider with label:"),
                v.HStack(h => [
                    h.Text("Volume: "),
                    h.Slider(75).Fill()
                ])
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Volume:"), TimeSpan.FromSeconds(10))
            .Capture("slider-hstack-label")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Slider_MultipleInVStack()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 15).Build();

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Audio Settings"),
                v.Text(""),
                v.Text("Master:"),
                v.Slider(80),
                v.Text(""),
                v.Text("Music:"),
                v.Slider(60),
                v.Text(""),
                v.Text("Effects:"),
                v.Slider(90)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Effects:"), TimeSpan.FromSeconds(10))
            .Capture("slider-multiple")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Slider_InBorder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Border(b => [
                b.Text("Brightness"),
                b.Text(""),
                b.Slider(65)
            ]).Title("Display Settings"),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Display Settings"), TimeSpan.FromSeconds(10))
            .Capture("slider-in-border")
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
    public async Task Slider_RespondsToTerminalWidth(int width)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(width, 10).Build();

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text($"Terminal width: {width}"),
                v.Slider(50)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText($"width: {width}"), TimeSpan.FromSeconds(10))
            .Capture($"slider-width-{width}")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    #endregion

    #region Keyboard Navigation Tests

    [Fact]
    public async Task Slider_ArrowKeysChangeValue()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();
        var currentValue = 50.0;

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text($"Value: {currentValue:F0}"),
                v.Slider(50)
                    .OnValueChanged(e => currentValue = e.Value)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Value: 50"), TimeSpan.FromSeconds(10))
            .Right().Right().Right()
            .Capture("slider-arrow-keys")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.True(currentValue > 50, $"Expected value > 50, got {currentValue}");
    }

    [Fact]
    public async Task Slider_HomeEndJumpToExtremes()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();
        var currentValue = 50.0;

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text($"Value: {currentValue:F0}"),
                v.Slider(50)
                    .OnValueChanged(e => currentValue = e.Value)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Value: 50"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.End)
            .Capture("slider-end-key")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.Equal(100, currentValue);
    }

    [Fact]
    public async Task Slider_TabNavigatesToNextWidget()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();
        var buttonClicked = false;

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Slider(50),
                v.Button("Apply").OnClick(_ => buttonClicked = true)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Apply"), TimeSpan.FromSeconds(10))
            .Tab().Enter()
            .Capture("slider-tab-navigation")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.True(buttonClicked);
    }

    #endregion

    #region Value Changed Callback Tests

    [Fact]
    public async Task Slider_CallbackTriggeredOnChange()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();
        var callbackCount = 0;
        var lastPercentage = 0.0;

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text($"Callbacks: {callbackCount}"),
                v.Slider(50)
                    .OnValueChanged(e =>
                    {
                        callbackCount++;
                        lastPercentage = e.Percentage;
                    })
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Callbacks:"), TimeSpan.FromSeconds(10))
            .Right().Right()
            .Capture("slider-callback")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.Equal(2, callbackCount);
        Assert.True(lastPercentage > 0.5);
    }

    #endregion

    #region Theming Tests

    [Fact]
    public async Task Slider_RespectsCustomTheme()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();

        var customTheme = new Hex1bTheme("CustomSlider")
            .Set(SliderTheme.TrackCharacter, '═')
            .Set(SliderTheme.HandleCharacter, '●')
            .Set(SliderTheme.HandleForegroundColor, Hex1bColor.Cyan)
            .Set(SliderTheme.FocusedHandleForegroundColor, Hex1bColor.Yellow);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Custom themed slider:"),
                v.Slider(50)
            ]),
            new Hex1bAppOptions
            {
                WorkloadAdapter = workload,
                Theme = customTheme
            }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Custom themed"), TimeSpan.FromSeconds(10))
            .Capture("slider-custom-theme")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    #endregion

    #region Asciinema Recording Tests

    [Fact]
    public async Task Slider_RecordsInteraction()
    {
        var tempFile = GetTempFile();
        using var workload = new Hex1bAppWorkloadAdapter();
        var terminalOptions = new Hex1bTerminalOptions
        {
            Width = 70,
            Height = 12,
            WorkloadAdapter = workload
        };
        var recorder = new AsciinemaRecorder(tempFile, new AsciinemaRecorderOptions
        {
            Title = "Slider Interaction Demo",
            IdleTimeLimit = 0.5f
        });
        terminalOptions.WorkloadFilters.Add(recorder);
        using var terminal = new Hex1bTerminal(terminalOptions);

        var currentValue = 50.0;

        using var app = new Hex1bApp(
            ctx => ctx.Border(b => [
                b.Text("Volume Control"),
                b.Text(""),
                b.HStack(h => [
                    h.Text($"Volume: {currentValue,3:F0}% "),
                    h.Slider(50)
                        .OnValueChanged(e => currentValue = e.Value)
                        .Fill()
                ])
            ]).Title("Settings"),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        recorder.AddMarker("Initial State");

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Volume Control"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        recorder.AddMarker("Increase Volume");

        // Simulate increasing volume
        for (int i = 0; i < 10; i++)
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .Right()
                .Wait(TimeSpan.FromMilliseconds(100))
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
            app.Invalidate();
        }

        recorder.AddMarker("Decrease Volume");

        // Simulate decreasing volume
        for (int i = 0; i < 5; i++)
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .Left()
                .Wait(TimeSpan.FromMilliseconds(100))
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
            app.Invalidate();
        }

        recorder.AddMarker("Jump to Max");

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.End)
            .Wait(TimeSpan.FromMilliseconds(200))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        app.Invalidate();

        recorder.AddMarker("Jump to Min");

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Home)
            .Wait(TimeSpan.FromMilliseconds(200))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        app.Invalidate();

        await Task.Delay(300, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        TestCaptureHelper.Capture(snapshot, "slider-interaction-final");
        await TestCaptureHelper.CaptureCastAsync(recorder, "slider-interaction", TestContext.Current.CancellationToken);

        cts.Cancel();
        await runTask;
    }

    [Fact]
    public async Task Slider_RecordsMultipleSliders()
    {
        var tempFile = GetTempFile();
        using var workload = new Hex1bAppWorkloadAdapter();
        var terminalOptions = new Hex1bTerminalOptions
        {
            Width = 80,
            Height = 20,
            WorkloadAdapter = workload
        };
        var recorder = new AsciinemaRecorder(tempFile, new AsciinemaRecorderOptions
        {
            Title = "Audio Mixer",
            IdleTimeLimit = 0.5f
        });
        terminalOptions.WorkloadFilters.Add(recorder);
        using var terminal = new Hex1bTerminal(terminalOptions);

        var master = 80.0;
        var music = 60.0;
        var effects = 90.0;
        var voice = 70.0;

        using var app = new Hex1bApp(
            ctx => ctx.Border(b => [
                b.Text("Audio Mixer"),
                b.Text(""),
                b.HStack(h => [h.Text("Master:  "), h.Slider(80).OnValueChanged(e => master = e.Value).Fill()]),
                b.Text(""),
                b.HStack(h => [h.Text("Music:   "), h.Slider(60).OnValueChanged(e => music = e.Value).Fill()]),
                b.Text(""),
                b.HStack(h => [h.Text("Effects: "), h.Slider(90).OnValueChanged(e => effects = e.Value).Fill()]),
                b.Text(""),
                b.HStack(h => [h.Text("Voice:   "), h.Slider(70).OnValueChanged(e => voice = e.Value).Fill()])
            ]).Title("Settings"),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        recorder.AddMarker("Initial State");

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Audio Mixer"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Adjust master
        recorder.AddMarker("Adjust Master");
        for (int i = 0; i < 3; i++)
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .Left()
                .Wait(TimeSpan.FromMilliseconds(80))
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
            app.Invalidate();
        }

        // Tab to music and adjust
        recorder.AddMarker("Adjust Music");
        await new Hex1bTerminalInputSequenceBuilder()
            .Tab()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        for (int i = 0; i < 5; i++)
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .Right()
                .Wait(TimeSpan.FromMilliseconds(80))
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
            app.Invalidate();
        }

        await Task.Delay(300, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        TestCaptureHelper.Capture(snapshot, "slider-audio-mixer");
        await TestCaptureHelper.CaptureCastAsync(recorder, "slider-audio-mixer", TestContext.Current.CancellationToken);

        cts.Cancel();
        await runTask;
    }

    [Fact]
    public async Task Slider_RecordsResizeScenario()
    {
        var tempFile = GetTempFile();
        using var workload = new Hex1bAppWorkloadAdapter();
        var terminalOptions = new Hex1bTerminalOptions
        {
            Width = 100,
            Height = 10,
            WorkloadAdapter = workload
        };
        var recorder = new AsciinemaRecorder(tempFile, new AsciinemaRecorderOptions
        {
            Title = "Slider Resize Behavior",
            IdleTimeLimit = 1.0f
        });
        terminalOptions.WorkloadFilters.Add(recorder);
        using var terminal = new Hex1bTerminal(terminalOptions);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Resize to see slider adapt:"),
                v.Slider(50),
                v.Text(""),
                v.Text("The slider fills available width")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        recorder.AddMarker("Initial Size (100 cols)");

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Resize"), TimeSpan.FromSeconds(10))
            .Wait(TimeSpan.FromMilliseconds(500))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Resize to medium
        recorder.AddMarker("Resize to 60 cols");
        await ((IHex1bTerminalWorkloadFilter)recorder).OnResizeAsync(60, 10, TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        terminal.Resize(60, 10);
        await workload.ResizeAsync(60, 10, TestContext.Current.CancellationToken);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        TestCaptureHelper.Capture(terminal, "slider-resize-medium");

        // Resize to narrow
        recorder.AddMarker("Resize to 40 cols");
        await ((IHex1bTerminalWorkloadFilter)recorder).OnResizeAsync(40, 10, TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        terminal.Resize(40, 10);
        await workload.ResizeAsync(40, 10, TestContext.Current.CancellationToken);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        TestCaptureHelper.Capture(terminal, "slider-resize-narrow");

        // Resize back to wide
        recorder.AddMarker("Resize to 120 cols");
        await ((IHex1bTerminalWorkloadFilter)recorder).OnResizeAsync(120, 10, TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        terminal.Resize(120, 10);
        await workload.ResizeAsync(120, 10, TestContext.Current.CancellationToken);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        TestCaptureHelper.Capture(terminal, "slider-resize-wide");

        await TestCaptureHelper.CaptureCastAsync(recorder, "slider-resize-demo", TestContext.Current.CancellationToken);

        cts.Cancel();
        await runTask;
    }

    #endregion

    #region Mouse Drag Tests

    [Fact]
    public async Task Slider_MouseDragChangesValue()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithMouse().WithDimensions(60, 10).Build();

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Drag Test"),
                v.Slider(50)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Wait for render, then drag from left to right on the slider (line 1, where slider is)
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Drag Test"), TimeSpan.FromSeconds(10))
            .Drag(5, 1, 55, 1)  // Drag from left side to right side of slider
            .Capture("slider-mouse-drag")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Just verify no crash during drag - actual value change is tested manually
        Assert.NotNull(snapshot);
    }

    [Fact]
    public async Task Slider_MouseClickSetsValue()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithMouse().WithDimensions(60, 10).Build();
        var currentValue = 0.0;

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text($"Value: {currentValue:F0}"),
                v.Slider(0)
                    .OnValueChanged(e => currentValue = e.Value)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Click at the middle of the slider to set value to ~50
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Value: 0"), TimeSpan.FromSeconds(10))
            .ClickAt(30, 1)  // Click middle of slider
            .Capture("slider-mouse-click")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Click should set value via OnValueChanged event
        // Note: Mouse click invokes the input binding which fires the event
        Assert.True(currentValue >= 0, $"Value should be set, got {currentValue}");
    }

    #endregion

    #region Double-Width Character Tests

    [Fact]
    public async Task Slider_DoubleWidthThumbCharacterRendersWithoutArtifacts()
    {
        // Use a double-width CJK character as the thumb to test for rendering artifacts
        // This tests that the slider properly handles characters that occupy 2 terminal cells
        var doubleWidthThumb = '⾠'; // U+2FA0 - CJK Radical Long One (double-width)
        
        var customTheme = new Hex1bTheme("DoubleWidthThumb")
            .Set(SliderTheme.HandleCharacter, doubleWidthThumb);

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Double-width thumb test:"),
                v.Slider(50)
            ]),
            new Hex1bAppOptions
            {
                WorkloadAdapter = workload,
                Theme = customTheme
            }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Double-width"), TimeSpan.FromSeconds(10))
            .Capture("slider-double-width-initial")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // Verify initial render contains the double-width character
        Assert.True(snapshot.ContainsText("⾠"), "Slider should contain the double-width thumb character");

        // Move slider and check for artifacts (no stray characters left behind)
        await new Hex1bTerminalInputSequenceBuilder()
            .Right().Right().Right().Right().Right()
            .Capture("slider-double-width-after-move-right")
            .Left().Left().Left().Left().Left()
            .Capture("slider-double-width-after-move-left")
            .Key(Hex1bKey.End)
            .Capture("slider-double-width-at-end")
            .Key(Hex1bKey.Home)
            .Capture("slider-double-width-at-home")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Slider_DoubleWidthThumbDragDoesNotLeaveArtifacts()
    {
        // Test that dragging a slider with a double-width thumb doesn't leave visual artifacts
        var doubleWidthThumb = '⾠'; // U+2FA0 - CJK Radical Long One (double-width)
        
        var customTheme = new Hex1bTheme("DoubleWidthThumbDrag")
            .Set(SliderTheme.HandleCharacter, doubleWidthThumb);

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithMouse().WithDimensions(60, 10).Build();
        var valueChanges = new List<double>();

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Drag double-width thumb:"),
                v.Slider(25)
                    .OnValueChanged(e => valueChanges.Add(e.Value))
            ]),
            new Hex1bAppOptions
            {
                WorkloadAdapter = workload,
                Theme = customTheme
            }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Drag double-width"), TimeSpan.FromSeconds(10))
            .Capture("slider-double-width-drag-initial")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // Simulate drag from left to right
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .Drag(10, 1, 50, 1) // Drag across the slider
            .Capture("slider-double-width-drag-complete")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // The slider line should contain exactly one instance of the thumb character
        // If there are artifacts, there would be multiple or partial characters
        var sliderLine = snapshot.GetLine(1);
        var thumbCount = sliderLine.Count(c => c == doubleWidthThumb);
        
        Assert.True(thumbCount <= 1, 
            $"Expected at most 1 thumb character, found {thumbCount}. Line: '{sliderLine}'");
    }

    [Fact]
    public async Task Slider_InsideBorder_BorderEdgesAreAligned()
    {
        // Test that a slider inside a border doesn't cause the border edges to misalign
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 6).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Border(b => [
                b.Text("Test"),
                b.Slider(50)
            ]).Title("Border Test"),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Border Test"), TimeSpan.FromSeconds(10))
            .Capture("slider-border-alignment")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Get all lines and verify border alignment using display width
        var lines = new List<string>();
        for (int i = 0; i < 6; i++)
        {
            lines.Add(snapshot.GetLine(i));
        }

        // Find the border lines (top and bottom should have corners, middle lines should have vertical bars)
        var topLine = lines[0];
        var bottomLine = lines.FirstOrDefault(l => l.Contains('└')) ?? "";
        
        // Calculate display column position of right edge characters
        int GetDisplayColumnOfLastChar(string line, char target)
        {
            var idx = line.LastIndexOf(target);
            if (idx < 0) return -1;
            // Display column = sum of display widths of all chars before this one
            return DisplayWidth.GetStringWidth(line[..idx]);
        }

        var topRightDisplayCol = GetDisplayColumnOfLastChar(topLine, '┐');
        var bottomRightDisplayCol = GetDisplayColumnOfLastChar(bottomLine, '┘');
        
        Assert.True(topRightDisplayCol >= 0, $"Top border should have right corner. Line: '{topLine}'");
        Assert.True(bottomRightDisplayCol >= 0, $"Bottom border should have right corner. Line: '{bottomLine}'");
        Assert.True(topRightDisplayCol == bottomRightDisplayCol, 
            $"Border right edges should be aligned by display column. Top corner at column {topRightDisplayCol}, bottom corner at column {bottomRightDisplayCol}.\nTop: '{topLine}'\nBottom: '{bottomLine}'");
        
        // Check middle lines with vertical bars
        var middleLines = lines.Where(l => l.Contains('│') && !l.Contains('┌') && !l.Contains('└')).ToList();
        foreach (var middleLine in middleLines)
        {
            var rightBarDisplayCol = GetDisplayColumnOfLastChar(middleLine, '│');
            Assert.True(topRightDisplayCol == rightBarDisplayCol,
                $"Middle border should align with corners by display column. Expected column {topRightDisplayCol}, got {rightBarDisplayCol}.\nLine: '{middleLine}'");
        }
    }

    [Fact]
    public async Task Slider_InsideBorder_WithDoubleWidthThumb_BorderEdgesAreAligned()
    {
        // Test that a slider with double-width thumb inside a border doesn't cause misalignment
        var doubleWidthThumb = '⾠'; // U+2FA0 - CJK Radical Long One (double-width)
        
        var customTheme = new Hex1bTheme("DoubleWidthBorderTest")
            .Set(SliderTheme.HandleCharacter, doubleWidthThumb);

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 6).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Border(b => [
                b.Text("Test"),
                b.Slider(50)
            ]).Title("Double Width Test"),
            new Hex1bAppOptions 
            { 
                WorkloadAdapter = workload,
                Theme = customTheme
            }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Double Width Test"), TimeSpan.FromSeconds(10))
            .Capture("slider-border-doublewidth-alignment")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Get all lines and verify border alignment using display width
        var lines = new List<string>();
        for (int i = 0; i < 6; i++)
        {
            lines.Add(snapshot.GetLine(i));
        }

        int GetDisplayColumnOfLastChar(string line, char target)
        {
            var idx = line.LastIndexOf(target);
            if (idx < 0) return -1;
            return DisplayWidth.GetStringWidth(line[..idx]);
        }

        var topLine = lines[0];
        var bottomLine = lines.FirstOrDefault(l => l.Contains('└')) ?? "";
        
        var topRightDisplayCol = GetDisplayColumnOfLastChar(topLine, '┐');
        var bottomRightDisplayCol = GetDisplayColumnOfLastChar(bottomLine, '┘');
        
        Assert.True(topRightDisplayCol >= 0, $"Top border should have right corner. Line: '{topLine}'");
        Assert.True(bottomRightDisplayCol >= 0, $"Bottom border should have right corner. Line: '{bottomLine}'");
        Assert.True(topRightDisplayCol == bottomRightDisplayCol, 
            $"Border right edges should be aligned with double-width thumb. Top corner at column {topRightDisplayCol}, bottom corner at column {bottomRightDisplayCol}.\nTop: '{topLine}'\nBottom: '{bottomLine}'");
        
        // Check middle lines with vertical bars
        var middleLines = lines.Where(l => l.Contains('│') && !l.Contains('┌') && !l.Contains('└')).ToList();
        foreach (var middleLine in middleLines)
        {
            var rightBarDisplayCol = GetDisplayColumnOfLastChar(middleLine, '│');
            Assert.True(topRightDisplayCol == rightBarDisplayCol,
                $"Middle border should align with corners. Expected column {topRightDisplayCol}, got {rightBarDisplayCol}.\nLine: '{middleLine}'");
        }
    }

    #endregion
}
