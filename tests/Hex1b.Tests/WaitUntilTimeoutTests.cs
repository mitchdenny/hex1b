using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for WaitUntilTimeoutException diagnostics.
/// </summary>
[TestClass]
public class WaitUntilTimeoutTests
{
    [TestMethod]
    public async Task WaitUntil_Timeout_ThrowsWaitUntilTimeoutException()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(ctx => new TextBlockWidget("Hello"))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var ex = await Assert.ThrowsExactlyAsync<WaitUntilTimeoutException>(async () =>
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("This text will never appear"), TimeSpan.FromMilliseconds(250))
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        });

        // Should still be catchable as TimeoutException
        TestSeq.IsType<TimeoutException>(ex);
    }

    [TestMethod]
    public async Task WaitUntil_Timeout_MessageContainsPredicateExpression()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(ctx => new TextBlockWidget("Hello"))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var ex = await Assert.ThrowsExactlyAsync<WaitUntilTimeoutException>(async () =>
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

    [TestMethod]
    public async Task WaitUntil_Timeout_MessageContainsExplicitDescription()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(ctx => new TextBlockWidget("Hello"))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var ex = await Assert.ThrowsExactlyAsync<WaitUntilTimeoutException>(async () =>
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Nope"), TimeSpan.FromMilliseconds(250), "my custom description")
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        });

        // Explicit description should take priority over predicate expression
        Assert.Contains("my custom description", ex.Message);
        Assert.AreEqual("my custom description", ex.ConditionDescription);
    }

    [TestMethod]
    public async Task WaitUntil_Timeout_MessageContainsCallerLocation()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(ctx => new TextBlockWidget("Hello"))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var ex = await Assert.ThrowsExactlyAsync<WaitUntilTimeoutException>(async () =>
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Nope"), TimeSpan.FromMilliseconds(250))
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        });

        // Should contain the test file name and a line number
        Assert.Contains("WaitUntilTimeoutTests.cs", ex.Message);
        Assert.IsNotNull(ex.CallerFilePath);
        Assert.Contains("WaitUntilTimeoutTests.cs", ex.CallerFilePath);
        Assert.IsTrue(ex.CallerLineNumber > 0);
    }

    [TestMethod]
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
            .WaitUntil(s => s.ContainsText("Hello World"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsExactlyAsync<WaitUntilTimeoutException>(async () =>
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

    [TestMethod]
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
            .WaitUntil(s => s.ContainsText("Snapshot Test"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsExactlyAsync<WaitUntilTimeoutException>(async () =>
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Nope"), TimeSpan.FromMilliseconds(250))
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        });

        // Exception should expose the snapshot as a structured property
        Assert.IsNotNull(ex.TerminalSnapshot);
        Assert.AreEqual(40, ex.TerminalSnapshot.Width);
        Assert.AreEqual(10, ex.TerminalSnapshot.Height);
        Assert.IsTrue(ex.TerminalSnapshot.ContainsText("Snapshot Test"));

        // Clean exit
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;
    }

    [TestMethod]
    public async Task WaitUntil_Timeout_MessageContainsTimeoutDuration()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(ctx => new TextBlockWidget("Hello"))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var timeout = TimeSpan.FromMilliseconds(250);
        var ex = await Assert.ThrowsExactlyAsync<WaitUntilTimeoutException>(async () =>
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Nope"), timeout)
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        });

        Assert.Contains(timeout.ToString(), ex.Message);
        Assert.AreEqual(timeout, ex.Timeout);
    }
}
