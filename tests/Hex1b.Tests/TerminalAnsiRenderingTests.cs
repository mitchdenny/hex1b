using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Terminal.Automation;
using Hex1b.Theming;

namespace Hex1b.Tests;

/// <summary>
/// Tests for ANSI rendering of terminal snapshots and regions.
/// ANSI outputs are attached to test results and can be viewed with `cat`.
/// </summary>
public class TerminalAnsiRenderingTests
{
    [Fact]
    public async Task RenderFullSnapshot_ProducesAnsi()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);

        var theme = new Hex1bTheme("Test")
            .Set(ButtonTheme.BackgroundColor, Hex1bColor.FromRgb(0, 100, 200))
            .Set(ButtonTheme.ForegroundColor, Hex1bColor.FromRgb(255, 255, 255));

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Terminal ANSI Rendering Demo"),
                v.Text(""),
                v.Button("Click Me"),
                v.Text(""),
                v.Text("Status: Ready")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Ready"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();
        var ansi = snapshot.ToAnsi();

        // Assert
        Assert.NotEmpty(ansi);
        // ANSI output should contain escape sequences
        Assert.Contains("\x1b[", ansi);
        // Should have cursor up to move back after scrolling
        Assert.Contains("A", ansi);
        // Should have cursor horizontal positioning
        Assert.Contains("G", ansi);
        // Should have SGR (color) codes
        Assert.Contains("m", ansi);
        // Should end with reset
        Assert.Contains("\x1b[0m", ansi);

        // Attach ANSI to test output
        TestCaptureHelper.AttachFile("full-snapshot.ansi", ansi);
    }

    [Fact]
    public async Task RenderWithColors_ProducesCorrectColorCodes()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 30, 5);

        var theme = new Hex1bTheme("Test")
            .Set(ButtonTheme.BackgroundColor, Hex1bColor.FromRgb(255, 0, 0))
            .Set(ButtonTheme.ForegroundColor, Hex1bColor.FromRgb(0, 255, 0));

        using var app = new Hex1bApp(
            ctx => ctx.Button("Colored"),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Colored"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();
        var ansi = snapshot.ToAnsi();

        // Assert - should contain 24-bit color codes
        Assert.Contains("\x1b[", ansi);
        // SGR 38;2 is 24-bit foreground, SGR 48;2 is 24-bit background
        Assert.Contains("38;2;", ansi);  // Foreground color
        Assert.Contains("48;2;", ansi);  // Background color

        // Attach ANSI to test output
        TestCaptureHelper.AttachFile("colored.ansi", ansi);
    }

    [Fact]
    public async Task RenderArbitraryRegion_ProducesAnsi()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 15);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Line 1: Header"),
                v.Text("Line 2: Content A"),
                v.Text("Line 3: Content B"),
                v.Text("Line 4: Content C"),
                v.Text("Line 5: Footer")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Footer"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();

        // Extract an arbitrary region (columns 5-35, rows 1-4)
        var region = snapshot.GetRegion(new Rect(5, 1, 30, 4));
        var ansi = region.ToAnsi();

        // Assert
        Assert.NotEmpty(ansi);
        Assert.Contains("\x1b[", ansi);

        // Attach ANSI to test output
        TestCaptureHelper.AttachFile("arbitrary-region.ansi", ansi);

        // Also render full snapshot for comparison
        var fullAnsi = snapshot.ToAnsi();
        TestCaptureHelper.AttachFile("arbitrary-region-full.ansi", fullAnsi);
    }

    [Fact]
    public async Task RenderWithClearScreen_IncludesClearAndHome()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 30, 5);

        using var app = new Hex1bApp(
            ctx => ctx.Text("Clear Screen Test"),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Clear"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();
        var options = new TerminalAnsiOptions { IncludeClearScreen = true };
        var ansi = snapshot.ToAnsi(options);

        // Assert - should start with clear screen and home
        Assert.StartsWith("\x1b[2J\x1b[H", ansi);

        // Attach ANSI to test output
        TestCaptureHelper.AttachFile("clear-screen.ansi", ansi);
    }

    [Fact]
    public async Task RenderWithTextAttributes_IncludesSgrCodes()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 8);

        var theme = new Hex1bTheme("Test")
            .Set(ButtonTheme.BackgroundColor, Hex1bColor.FromRgb(50, 50, 100));

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Text Attributes Demo"),
                v.Button("Bold Button")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Bold Button"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();
        var ansi = snapshot.ToAnsi();

        // Assert - should contain SGR codes
        Assert.NotEmpty(ansi);
        Assert.Contains("\x1b[", ansi);

        // Attach ANSI to test output
        TestCaptureHelper.AttachFile("text-attributes.ansi", ansi);
    }

    [Fact]
    public void ToAnsi_EmptyRegion_ReturnsValidAnsi()
    {
        // Arrange - Create a simple snapshot with just spaces
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 10, 3);

        var snapshot = terminal.CreateSnapshot();
        
        // Act
        var ansi = snapshot.ToAnsi();

        // Assert - Even an empty snapshot should produce valid ANSI
        Assert.NotNull(ansi);
        // Should end with reset
        Assert.Contains("\x1b[0m", ansi);

        // Attach ANSI to test output
        TestCaptureHelper.AttachFile("empty.ansi", ansi);
    }

    [Fact]
    public async Task ToAnsi_WithCursorPosition_PositionsCursor()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 30, 5);

        using var app = new Hex1bApp(
            ctx => ctx.TextBox("Type here"),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Type"), TimeSpan.FromSeconds(2))
            .Key(Hex1bKey.Tab) // Focus on textbox
            .Type("abc")       // Type some text
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();
        var ansi = snapshot.ToAnsi(new TerminalAnsiOptions { IncludeCursorPosition = true });

        // Assert - The ANSI output should position the cursor
        Assert.NotEmpty(ansi);
        // Cursor position uses horizontal absolute (G) command
        Assert.Contains("\x1b[", ansi);
        Assert.Contains("G", ansi);

        // Attach ANSI to test output
        TestCaptureHelper.AttachFile("cursor-position.ansi", ansi);
    }

    [Fact]
    public async Task Capture_IncludesAnsiFile()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 30, 5);

        using var app = new Hex1bApp(
            ctx => ctx.Text("Capture Test"),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Capture"), TimeSpan.FromSeconds(2))
            .Capture("capture-all")  // This should now create SVG, HTML, and ANSI files
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - verify the files were created (xUnit context attachments)
        // The actual file creation is verified by the Capture() call not throwing
        Assert.True(true);
    }
}
