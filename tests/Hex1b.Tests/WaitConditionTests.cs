namespace Hex1b.Tests;

/// <summary>
/// Tests for WaitUntilAsync, WaitWhile, and WaitWhileAsync methods.
/// </summary>
public class WaitConditionTests
{
    [Fact]
    public async Task WaitUntilAsync_WaitsForAsyncCondition()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();
        
        workload.Write("Hello World");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntilAsync(
                async s =>
                {
                    await Task.Yield(); // Simulate async work
                    return s.ContainsText("Hello World");
                },
                TimeSpan.FromSeconds(2),
                "async hello world condition")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        Assert.True(snapshot.ContainsText("Hello World"));
    }

    [Fact]
    public async Task WaitUntilAsync_ThrowsTimeoutExceptionWhenConditionNotMet()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();
        
        workload.Write("Hello");
        
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntilAsync(
                    async s =>
                    {
                        await Task.Yield();
                        return s.ContainsText("Never appears");
                    },
                    TimeSpan.FromMilliseconds(100),
                    "async condition that won't be met")
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task WaitWhile_WaitsWhileConditionIsTrue()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();
        
        // First show "Loading..."
        workload.Write("Loading...");
        
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Loading..."), TimeSpan.FromSeconds(1), "initial loading text")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        // Schedule "Done!" to appear after a short delay
        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            workload.Write("\x1b[2J\x1b[H"); // Clear screen and move cursor home
            workload.Write("Done!");
        });
        
        // Wait while "Loading..." is visible (i.e., until it's gone)
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitWhile(s => s.ContainsText("Loading..."), TimeSpan.FromSeconds(2), "loading to disappear")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        Assert.False(snapshot.ContainsText("Loading..."));
        Assert.True(snapshot.ContainsText("Done!"));
    }

    [Fact]
    public async Task WaitWhile_ThrowsTimeoutExceptionWhenConditionStaysTrue()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();
        
        workload.Write("Persistent content");
        
        // First wait for the content to appear
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Persistent"), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitWhile(
                    s => s.ContainsText("Persistent"),
                    TimeSpan.FromMilliseconds(100),
                    "content that never goes away")
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task WaitWhileAsync_WaitsWhileAsyncConditionIsTrue()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();
        
        // First show "Processing..."
        workload.Write("Processing...");
        
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Processing..."), TimeSpan.FromSeconds(1), "initial processing text")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        // Schedule "Complete!" to appear after a short delay
        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            workload.Write("\x1b[2J\x1b[H"); // Clear screen and move cursor home
            workload.Write("Complete!");
        });
        
        // Wait while async "Processing..." check is true
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitWhileAsync(
                async s =>
                {
                    await Task.Yield(); // Simulate async work
                    return s.ContainsText("Processing...");
                },
                TimeSpan.FromSeconds(2),
                "async processing to complete")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        Assert.False(snapshot.ContainsText("Processing..."));
        Assert.True(snapshot.ContainsText("Complete!"));
    }

    [Fact]
    public async Task WaitWhileAsync_ThrowsTimeoutExceptionWhenAsyncConditionStaysTrue()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();
        
        workload.Write("Async persistent content");
        
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitWhileAsync(
                    async s =>
                    {
                        await Task.Yield();
                        return s.ContainsText("Async persistent");
                    },
                    TimeSpan.FromMilliseconds(100),
                    "async content that never goes away")
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task WaitWhile_CompletesImmediatelyWhenConditionIsFalse()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();
        
        workload.Write("Hello");
        
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello"), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        // Wait while "NotPresent" is visible - should complete immediately since it's not there
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitWhile(s => s.ContainsText("NotPresent"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        Assert.True(snapshot.ContainsText("Hello"));
    }

    [Fact]
    public async Task WaitWhileAsync_CompletesImmediatelyWhenAsyncConditionIsFalse()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();
        
        workload.Write("Hello");
        
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello"), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        // Wait while async "NotPresent" check is true - should complete immediately
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitWhileAsync(
                async s =>
                {
                    await Task.Yield();
                    return s.ContainsText("NotPresent");
                },
                TimeSpan.FromSeconds(2))
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        Assert.True(snapshot.ContainsText("Hello"));
    }
}
