using Hex1b.Terminal;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the Hex1bTerminalBuilder.
/// </summary>
public class Hex1bTerminalBuilderTests
{
    [Fact]
    public void CreateBuilder_ReturnsNewBuilderInstance()
    {
        var builder = Hex1bTerminal.CreateBuilder();
        
        Assert.NotNull(builder);
        Assert.IsType<Hex1bTerminalBuilder>(builder);
    }

    [Fact]
    public void Build_WithWorkloadAdapter_CreatesTerminal()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithDimensions(80, 24)
            .Build();
        
        Assert.NotNull(terminal);
    }

    [Fact]
    public void Build_WithoutWorkload_ThrowsInvalidOperationException()
    {
        var builder = Hex1bTerminal.CreateBuilder();
        
        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("No workload configured", ex.Message);
    }

    [Fact]
    public void WithDimensions_SetsTerminalSize()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithDimensions(100, 50)
            .Build();
        
        var snapshot = terminal.CreateSnapshot();
        Assert.Equal(100, snapshot.Width);
        Assert.Equal(50, snapshot.Height);
    }

    [Fact]
    public void WithDimensions_InvalidWidth_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Hex1bTerminal.CreateBuilder().WithDimensions(0, 24));
    }

    [Fact]
    public void WithDimensions_InvalidHeight_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Hex1bTerminal.CreateBuilder().WithDimensions(80, 0));
    }

    [Fact]
    public void WithWorkload_NullAdapter_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Hex1bTerminal.CreateBuilder().WithWorkload(null!));
    }

    [Fact]
    public void AddWorkloadFilter_NullFilter_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Hex1bTerminal.CreateBuilder().AddWorkloadFilter(null!));
    }

    [Fact]
    public void AddPresentationFilter_NullFilter_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Hex1bTerminal.CreateBuilder().AddPresentationFilter(null!));
    }

    [Fact]
    public void AddWorkloadFilter_AddsFilterToTerminal()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        var filter = new TestWorkloadFilter();
        
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .AddWorkloadFilter(filter)
            .Build();
        
        // Filter should have been notified of session start
        Assert.True(filter.SessionStartCalled);
    }

    [Fact]
    public async Task RunAsync_WithRunCallback_ExecutesCallback()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        var callbackExecuted = false;
        
        // Use internal method to set run callback for testing
        var builder = Hex1bTerminal.CreateBuilder();
        builder.WithWorkload(workload);
        builder.SetWorkloadFactory(_ => new Hex1bTerminalBuildContext
        {
            WorkloadAdapter = workload,
            RunCallback = async ct =>
            {
                callbackExecuted = true;
                await Task.Yield();
                return 42;
            }
        });
        
        await using var terminal = builder.Build();
        var exitCode = await terminal.RunAsync();
        
        Assert.True(callbackExecuted);
        Assert.Equal(42, exitCode);
    }

    [Fact]
    public async Task RunAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        var cts = new CancellationTokenSource();
        
        var builder = Hex1bTerminal.CreateBuilder();
        builder.WithWorkload(workload);
        builder.SetWorkloadFactory(_ => new Hex1bTerminalBuildContext
        {
            WorkloadAdapter = workload,
            RunCallback = async ct =>
            {
                // Wait indefinitely
                await Task.Delay(Timeout.Infinite, ct);
                return 0;
            }
        });
        
        await using var terminal = builder.Build();
        
        // Cancel immediately
        cts.Cancel();
        
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => terminal.RunAsync(cts.Token));
    }

    [Fact]
    public async Task BuilderRunAsync_BuildsAndRunsTerminal()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        var callbackExecuted = false;
        
        var builder = Hex1bTerminal.CreateBuilder();
        builder.WithWorkload(workload);
        builder.SetWorkloadFactory(_ => new Hex1bTerminalBuildContext
        {
            WorkloadAdapter = workload,
            RunCallback = async ct =>
            {
                callbackExecuted = true;
                await Task.Yield();
                return 99;
            }
        });
        
        var exitCode = await builder.RunAsync();
        
        Assert.True(callbackExecuted);
        Assert.Equal(99, exitCode);
    }

    [Fact]
    public void FluentApi_AllMethodsReturnBuilder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        var filter = new TestWorkloadFilter();
        var presentationFilter = new TestPresentationFilter();
        
        var builder = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithDimensions(80, 24)
            .AddWorkloadFilter(filter)
            .AddPresentationFilter(presentationFilter)
            .WithTimeProvider(TimeProvider.System);
        
        Assert.IsType<Hex1bTerminalBuilder>(builder);
    }

    // === Test Helpers ===

    private class TestWorkloadFilter : IHex1bTerminalWorkloadFilter
    {
        public bool SessionStartCalled { get; private set; }
        public bool SessionEndCalled { get; private set; }

        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
        {
            SessionStartCalled = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
        {
            SessionEndCalled = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask OnOutputAsync(IReadOnlyList<Hex1b.Tokens.AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask OnFrameCompleteAsync(TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask OnInputAsync(IReadOnlyList<Hex1b.Tokens.AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
    }

    private class TestPresentationFilter : IHex1bTerminalPresentationFilter
    {
        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<Hex1b.Tokens.AnsiToken>> OnOutputAsync(
            IReadOnlyList<Hex1b.Tokens.AppliedToken> appliedTokens, 
            TimeSpan elapsed, 
            CancellationToken ct = default)
            => ValueTask.FromResult<IReadOnlyList<Hex1b.Tokens.AnsiToken>>(
                appliedTokens.Select(at => at.Token).ToList());

        public ValueTask OnInputAsync(IReadOnlyList<Hex1b.Tokens.AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
    }
}
