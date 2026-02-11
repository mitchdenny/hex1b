using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for WaitUntilTimeoutException diagnostics.
/// </summary>
public class WaitUntilTimeoutTests
{
    [Fact]
    public async Task WaitUntil_Timeout_ThrowsWaitUntilTimeoutException()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new TextBlockWidget("Hello"))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var ex = await Assert.ThrowsAsync<WaitUntilTimeoutException>(async () =>
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("This text will never appear"), TimeSpan.FromMilliseconds(250))
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        });

        // Should still be catchable as TimeoutException
        Assert.IsAssignableFrom<TimeoutException>(ex);
    }

    [Fact]
    public async Task WaitUntil_Timeout_MessageContainsPredicateExpression()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new TextBlockWidget("Hello"))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var ex = await Assert.ThrowsAsync<WaitUntilTimeoutException>(async () =>
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("NonExistent"), TimeSpan.FromMilliseconds(250))
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        });

        // CallerArgumentExpression should capture the predicate text
        Assert.Contains("ContainsText(\"NonExistent\")", ex.Message);
        Assert.Contains("ContainsText(\"NonExistent\")", ex.ConditionDescription);
    }

    [Fact]
    public async Task WaitUntil_Timeout_MessageContainsExplicitDescription()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new TextBlockWidget("Hello"))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var ex = await Assert.ThrowsAsync<WaitUntilTimeoutException>(async () =>
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Nope"), TimeSpan.FromMilliseconds(250), "my custom description")
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        });

        // Explicit description should take priority over predicate expression
        Assert.Contains("my custom description", ex.Message);
        Assert.Equal("my custom description", ex.ConditionDescription);
    }

    [Fact]
    public async Task WaitUntil_Timeout_MessageContainsCallerLocation()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new TextBlockWidget("Hello"))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var ex = await Assert.ThrowsAsync<WaitUntilTimeoutException>(async () =>
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Nope"), TimeSpan.FromMilliseconds(250))
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        });

        // Should contain the test file name and a line number
        Assert.Contains("WaitUntilTimeoutTests.cs", ex.Message);
        Assert.NotNull(ex.CallerFilePath);
        Assert.Contains("WaitUntilTimeoutTests.cs", ex.CallerFilePath);
        Assert.True(ex.CallerLineNumber > 0);
    }

    [Fact]
    public async Task WaitUntil_Timeout_MessageContainsFullTerminalGrid()
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

        // Wait for the app to render
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello World"), TimeSpan.FromSeconds(2))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<WaitUntilTimeoutException>(async () =>
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Nope"), TimeSpan.FromMilliseconds(250))
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        });

        // Message should contain the terminal content
        Assert.Contains("Hello World", ex.Message);
        // Message should contain terminal dimensions
        Assert.Contains("40x10", ex.Message);

        // Clean exit
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;
    }

    [Fact]
    public async Task WaitUntil_Timeout_ExposesTerminalSnapshot()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TextBlockWidget("Snapshot Test")),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Wait for the app to render
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Snapshot Test"), TimeSpan.FromSeconds(2))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<WaitUntilTimeoutException>(async () =>
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Nope"), TimeSpan.FromMilliseconds(250))
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        });

        // Exception should expose the snapshot as a structured property
        Assert.NotNull(ex.TerminalSnapshot);
        Assert.Equal(40, ex.TerminalSnapshot.Width);
        Assert.Equal(10, ex.TerminalSnapshot.Height);
        Assert.True(ex.TerminalSnapshot.ContainsText("Snapshot Test"));

        // Clean exit
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;
    }

    [Fact]
    public async Task WaitUntil_Timeout_MessageContainsTimeoutDuration()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new TextBlockWidget("Hello"))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var timeout = TimeSpan.FromMilliseconds(250);
        var multiplier = Hex1bTerminalInputSequenceOptions.Default.TimeoutMultiplier;
        var effectiveTimeout = timeout * multiplier;
        var ex = await Assert.ThrowsAsync<WaitUntilTimeoutException>(async () =>
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Nope"), timeout)
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        });

        Assert.Contains(effectiveTimeout.ToString(), ex.Message);
        Assert.Equal(effectiveTimeout, ex.Timeout);
    }
}
