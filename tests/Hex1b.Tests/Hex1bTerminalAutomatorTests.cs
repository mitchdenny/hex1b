using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for <see cref="Hex1bTerminalAutomator"/>.
/// </summary>
public class Hex1bTerminalAutomatorTests
{
    [Fact]
    public async Task WaitUntilTextAsync_WaitsForText()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new TextBlockWidget("Hello World"))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(5));

        await auto.WaitUntilTextAsync("Hello World");

        Assert.Single(auto.CompletedSteps);
        Assert.Contains("WaitUntilText(\"Hello World\")", auto.CompletedSteps[0].Description);

        await auto.Ctrl().KeyAsync(Hex1bKey.C);
        await runTask;
    }

    [Fact]
    public async Task WaitUntilTextAsync_Timeout_ThrowsHex1bAutomationException()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new TextBlockWidget("Hello"))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromMilliseconds(250));

        var ex = await Assert.ThrowsAsync<Hex1bAutomationException>(async () =>
        {
            await auto.WaitUntilTextAsync("NonExistent");
        });

        Assert.Equal(1, ex.FailedStepIndex);
        Assert.Contains("WaitUntilText(\"NonExistent\")", ex.FailedStepDescription);
        Assert.IsType<WaitUntilTimeoutException>(ex.InnerException);
        Assert.NotNull(ex.TerminalSnapshot);

        await auto.Ctrl().KeyAsync(Hex1bKey.C);
        await runTask;
    }

    [Fact]
    public async Task WaitUntilTextAsync_Timeout_ExceptionMessageContainsStepHistory()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TextBlockWidget("Hello World")),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(5));

        // Complete some steps successfully first
        await auto.WaitUntilTextAsync("Hello World");
        await auto.EnterAsync();

        // Now fail on a WaitUntil
        var ex = await Assert.ThrowsAsync<Hex1bAutomationException>(async () =>
        {
            await auto.WaitUntilTextAsync("NonExistent", timeout: TimeSpan.FromMilliseconds(250));
        });

        // Verify step index reflects that 2 steps completed before the failure
        Assert.Equal(3, ex.FailedStepIndex);

        // Verify completed steps are captured
        Assert.Equal(2, ex.CompletedSteps.Count);
        Assert.Contains("WaitUntilText(\"Hello World\")", ex.CompletedSteps[0].Description);
        Assert.Contains("Key(Enter)", ex.CompletedSteps[1].Description);

        // Verify the message contains the breadcrumb
        Assert.Contains("Step 3 of 3 failed", ex.Message);
        Assert.Contains("WaitUntilText(\"Hello World\")", ex.Message);
        Assert.Contains("Key(Enter)", ex.Message);
        Assert.Contains("FAILED", ex.Message);

        // Verify terminal snapshot is in the message
        Assert.Contains("Terminal snapshot at failure", ex.Message);
        Assert.Contains("Hello World", ex.Message);

        // Verify caller info
        Assert.NotNull(ex.CallerFilePath);
        Assert.Contains("Hex1bTerminalAutomatorTests.cs", ex.CallerFilePath);
        Assert.True(ex.CallerLineNumber > 0);

        // Clean up
        await auto.Ctrl().KeyAsync(Hex1bKey.C);
        await runTask;
    }

    [Fact]
    public async Task EnterAsync_SendsEnterKey()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var enterPressed = false;
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new ButtonWidget("Click Me").OnClick(_ => { enterPressed = true; })),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(5));

        await auto.WaitUntilTextAsync("Click Me");
        await auto.EnterAsync();

        // Give the app a moment to process
        await auto.WaitAsync(50);

        Assert.True(enterPressed);
        Assert.Equal(3, auto.CompletedSteps.Count);

        await auto.Ctrl().KeyAsync(Hex1bKey.C);
        await runTask;
    }

    [Fact]
    public async Task TypeAsync_TypesText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var typedText = "";
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new TextBoxWidget("").OnTextChanged(args =>
                {
                    typedText = args.NewText;
                    return Task.CompletedTask;
                })),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(5));

        await auto.WaitUntilAlternateScreenAsync();
        await auto.TypeAsync("Hello");
        await auto.WaitUntilTextAsync("Hello");

        Assert.Equal("Hello", typedText);
        Assert.Contains("Type(\"Hello\")", auto.CompletedSteps[1].Description);

        await auto.Ctrl().KeyAsync(Hex1bKey.C);
        await runTask;
    }

    [Fact]
    public async Task Ctrl_KeyAsync_SendsModifiedKey()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TextBlockWidget("Test")),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(5));

        await auto.WaitUntilTextAsync("Test");

        // Ctrl+C should exit the app
        await auto.Ctrl().KeyAsync(Hex1bKey.C);

        await runTask;

        // Verify modifier was recorded in description
        var lastStep = auto.CompletedSteps[^1];
        Assert.Contains("Ctrl+", lastStep.Description);
    }

    [Fact]
    public async Task WaitUntilAsync_WithCustomPredicate_Works()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new TextBlockWidget("Count: 42"))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(5));

        await auto.WaitUntilAsync(
            s => s.ContainsText("Count: 42"),
            description: "count to be 42");

        Assert.Single(auto.CompletedSteps);
        Assert.Contains("WaitUntil(\"count to be 42\")", auto.CompletedSteps[0].Description);

        await auto.Ctrl().KeyAsync(Hex1bKey.C);
        await runTask;
    }

    [Fact]
    public async Task WaitUntilNoTextAsync_WaitsForTextToDisappear()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var showText = true;
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                showText ? new TextBlockWidget("Visible") : new TextBlockWidget("Hidden")),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(5));

        await auto.WaitUntilTextAsync("Visible");

        // Toggle the text off
        showText = false;
        app.Invalidate();

        await auto.WaitUntilNoTextAsync("Visible");

        Assert.Equal(2, auto.CompletedSteps.Count);
        Assert.Contains("WaitUntilNoText(\"Visible\")", auto.CompletedSteps[1].Description);

        await auto.Ctrl().KeyAsync(Hex1bKey.C);
        await runTask;
    }

    [Fact]
    public async Task SequenceAsync_WithBuilderAction_ExecutesSequence()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var typedText = "";
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new TextBoxWidget("").OnTextChanged(args =>
                {
                    typedText = args.NewText;
                    return Task.CompletedTask;
                })),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(5));

        await auto.WaitUntilAlternateScreenAsync();

        // Use SequenceAsync with a builder action
        await auto.SequenceAsync(
            b => b.Type("Hi").Enter(),
            description: "Type and submit");

        await auto.WaitAsync(50);

        Assert.Contains("Type and submit", auto.CompletedSteps[1].Description);

        await auto.Ctrl().KeyAsync(Hex1bKey.C);
        await runTask;
    }

    [Fact]
    public async Task SequenceAsync_WithPrebuiltSequence_ExecutesSequence()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new TextBoxWidget("").OnTextChanged(args => Task.CompletedTask)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(5));

        await auto.WaitUntilAlternateScreenAsync();

        // Build a reusable sequence
        var typeSequence = new Hex1bTerminalInputSequenceBuilder()
            .Type("Reusable")
            .Build();

        await auto.SequenceAsync(typeSequence, description: "Type reusable text");

        await auto.WaitUntilTextAsync("Reusable");

        Assert.Contains("Type reusable text", auto.CompletedSteps[1].Description);

        await auto.Ctrl().KeyAsync(Hex1bKey.C);
        await runTask;
    }

    [Fact]
    public async Task CompletedSteps_TracksCallerInfo()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new TextBlockWidget("Test"))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(5));

        await auto.WaitUntilTextAsync("Test");

        var step = auto.CompletedSteps[0];
        Assert.NotNull(step.CallerFilePath);
        Assert.Contains("Hex1bTerminalAutomatorTests.cs", step.CallerFilePath);
        Assert.True(step.CallerLineNumber > 0);
        Assert.True(step.Elapsed >= TimeSpan.Zero);

        await auto.Ctrl().KeyAsync(Hex1bKey.C);
        await runTask;
    }

    [Fact]
    public async Task AutomationException_TotalElapsed_SumsAllSteps()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new TextBlockWidget("Hello"))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromMilliseconds(250));

        await auto.WaitUntilTextAsync("Hello", timeout: TimeSpan.FromSeconds(5));

        var ex = await Assert.ThrowsAsync<Hex1bAutomationException>(async () =>
        {
            await auto.WaitUntilTextAsync("Never");
        });

        // TotalElapsed should be >= the timeout since the failing step waited that long
        Assert.True(ex.TotalElapsed >= TimeSpan.FromMilliseconds(200),
            $"TotalElapsed was {ex.TotalElapsed.TotalMilliseconds}ms, expected >= 200ms");

        await auto.Ctrl().KeyAsync(Hex1bKey.C);
        await runTask;
    }

    [Fact]
    public async Task CreateSnapshot_ReturnsCurrentState()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new TextBlockWidget("Snapshot Test"))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(5));

        await auto.WaitUntilTextAsync("Snapshot Test");

        using var snapshot = auto.CreateSnapshot();
        Assert.True(snapshot.ContainsText("Snapshot Test"));

        await auto.Ctrl().KeyAsync(Hex1bKey.C);
        await runTask;
    }

    [Fact]
    public async Task WaitUntilAsync_WithPredicateExpression_CapturesExpression()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new TextBlockWidget("Hello"))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromMilliseconds(250));

        var ex = await Assert.ThrowsAsync<Hex1bAutomationException>(async () =>
        {
            await auto.WaitUntilAsync(s => s.ContainsText("Nope"));
        });

        // The predicate expression should be captured
        Assert.Contains("ContainsText(\"Nope\")", ex.FailedStepDescription);

        await auto.Ctrl().KeyAsync(Hex1bKey.C);
        await runTask;
    }

    [Fact]
    public async Task MultipleModifiers_AreStackedCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TextBlockWidget("Test")),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(5));

        await auto.WaitUntilTextAsync("Test");

        // Stack Ctrl+Shift
        await auto.Ctrl().Shift().KeyAsync(Hex1bKey.Z);

        var lastStep = auto.CompletedSteps[^1];
        Assert.Contains("Ctrl+", lastStep.Description);
        Assert.Contains("Shift+", lastStep.Description);

        await auto.Ctrl().KeyAsync(Hex1bKey.C);
        await runTask;
    }

    [Fact]
    public async Task WaitAsync_PausesForDuration()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new TextBlockWidget("Test"))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(5));

        await auto.WaitUntilTextAsync("Test");
        await auto.WaitAsync(50);

        Assert.Equal(2, auto.CompletedSteps.Count);
        Assert.Contains("Wait(50ms)", auto.CompletedSteps[1].Description);
        Assert.True(auto.CompletedSteps[1].Elapsed >= TimeSpan.FromMilliseconds(40));

        await auto.Ctrl().KeyAsync(Hex1bKey.C);
        await runTask;
    }
}
