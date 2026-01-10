using Hex1b.Terminal;
using Hex1b.Terminal.Automation;
using Hex1b.Widgets;

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
    public void WithHeadless_ReturnsBuilder()
    {
        var result = Hex1bTerminal.CreateBuilder().WithHeadless();
        
        Assert.IsType<Hex1bTerminalBuilder>(result);
    }

    [Fact]
    public void WithHeadless_CreatesWorkingTerminal()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();
        
        var snapshot = terminal.CreateSnapshot();
        Assert.Equal(80, snapshot.Width);
        Assert.Equal(24, snapshot.Height);
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
        builder.WithPresentation(new TestPresentationAdapter()); // Must provide explicit presentation for tests
        builder.SetWorkloadFactory(_ => new Hex1bTerminalBuildContext(
            workload,
            async ct =>
            {
                callbackExecuted = true;
                await Task.Yield();
                return 42;
            }));
        
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
        builder.WithPresentation(new TestPresentationAdapter()); // Must provide explicit presentation for tests
        builder.SetWorkloadFactory(_ => new Hex1bTerminalBuildContext(
            workload,
            async ct =>
            {
                // Wait indefinitely
                await Task.Delay(Timeout.Infinite, ct);
                return 0;
            }));
        
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
        builder.WithPresentation(new TestPresentationAdapter()); // Must provide explicit presentation for tests
        builder.SetWorkloadFactory(_ => new Hex1bTerminalBuildContext(
            workload,
            async ct =>
            {
                callbackExecuted = true;
                await Task.Yield();
                return 99;
            }));
        
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

    // === WithHex1bApp Tests ===

    [Fact]
    public void WithHex1bApp_NullBuilder_ThrowsArgumentNullException()
    {
        Func<RootContext, Hex1bWidget>? nullBuilder = null;
        
        Assert.Throws<ArgumentNullException>(() =>
            Hex1bTerminal.CreateBuilder().WithHex1bApp(nullBuilder!));
    }

    [Fact]
    public void WithHex1bApp_NullAsyncBuilder_ThrowsArgumentNullException()
    {
        Func<RootContext, Task<Hex1bWidget>>? nullBuilder = null;
        
        Assert.Throws<ArgumentNullException>(() =>
            Hex1bTerminal.CreateBuilder().WithHex1bApp(nullBuilder!));
    }

    [Fact]
    public void WithHex1bApp_ReturnsBuilder()
    {
        var result = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(ctx => ctx.Text("Hello"));
        
        Assert.IsType<Hex1bTerminalBuilder>(result);
    }

    [Fact]
    public void WithHex1bApp_CanBuild()
    {
        // Should not throw - uses explicit presentation
        var builder = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(ctx => ctx.Text("Hello"))
            .WithPresentation(new TestPresentationAdapter());
        
        using var terminal = builder.Build();
        Assert.NotNull(terminal);
    }

    [Fact]
    public async Task WithHex1bApp_AsyncBuilder_CanRun()
    {
        var builderCalled = false;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        
        var builder = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(async ctx =>
            {
                builderCalled = true;
                await Task.Yield();
                return ctx.Text("Hello");
            })
            .WithPresentation(new TestPresentationAdapter());
        
        // The TestPresentationAdapter returns empty input, which should
        // cause the app to exit naturally
        var exitCode = await builder.RunAsync(cts.Token);
        
        Assert.True(builderCalled);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task WithHex1bApp_SyncBuilder_CanRun()
    {
        var builderCalled = false;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        
        var builder = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(ctx =>
            {
                builderCalled = true;
                return ctx.Text("Hello");
            })
            .WithPresentation(new TestPresentationAdapter());
        
        var exitCode = await builder.RunAsync(cts.Token);
        
        Assert.True(builderCalled);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void WithMouse_ReturnsBuilder()
    {
        var result = Hex1bTerminal.CreateBuilder()
            .WithMouse(true);
        
        Assert.IsType<Hex1bTerminalBuilder>(result);
    }

    [Fact]
    public async Task WithHex1bApp_FluentChain_Works()
    {
        var filter = new TestWorkloadFilter();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        
        var builder = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(ctx => ctx.Text("Hello"))
            .WithMouse(true)
            .WithDimensions(100, 40)
            .AddWorkloadFilter(filter)
            .WithPresentation(new TestPresentationAdapter());
        
        await builder.RunAsync(cts.Token);
        
        Assert.True(filter.SessionStartCalled);
    }

    // === WithHex1bApp Capture Pattern Tests ===

    [Fact]
    public void WithHex1bApp_CapturePattern_NullConfigure_ThrowsArgumentNullException()
    {
        Func<Hex1bApp, Hex1bAppOptions, Func<RootContext, Hex1bWidget>>? nullConfigure = null;
        
        Assert.Throws<ArgumentNullException>(() =>
            Hex1bTerminal.CreateBuilder().WithHex1bApp(nullConfigure!));
    }

    [Fact]
    public void WithHex1bApp_CapturePattern_AsyncNullConfigure_ThrowsArgumentNullException()
    {
        Func<Hex1bApp, Hex1bAppOptions, Func<RootContext, Task<Hex1bWidget>>>? nullConfigure = null;
        
        Assert.Throws<ArgumentNullException>(() =>
            Hex1bTerminal.CreateBuilder().WithHex1bApp(nullConfigure!));
    }

    [Fact]
    public void WithHex1bApp_CapturePattern_ReturnsBuilder()
    {
        var result = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => ctx.Text("Hello"));
        
        Assert.IsType<Hex1bTerminalBuilder>(result);
    }

    [Fact]
    public async Task WithHex1bApp_CapturePattern_CanCaptureApp()
    {
        Hex1bApp? capturedApp = null;
        var pattern = new CellPatternSearcher().Find("Hello from captured app");
        
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) =>
            {
                capturedApp = app;
                return ctx => ctx.Text("Hello from captured app");
            })
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches, TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1b.Input.Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.NotNull(capturedApp);
    }

    [Fact]
    public async Task WithHex1bApp_CapturePattern_CanSetTheme()
    {
        var pattern = new CellPatternSearcher().Find("Themed content");
        
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) =>
            {
                // Setting Theme should work (it's not a restricted property)
                options.Theme = new Hex1b.Theming.Hex1bTheme("TestTheme");
                return ctx => ctx.Text("Themed content");
            })
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches, TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1b.Input.Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task WithHex1bApp_CapturePattern_AsyncBuilder_Works()
    {
        Hex1bApp? capturedApp = null;
        var pattern = new CellPatternSearcher().Find("Async Hello World");
        
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) =>
            {
                capturedApp = app;
                return async ctx =>
                {
                    await Task.Yield();
                    return ctx.Text("Async Hello World");
                };
            })
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches, TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1b.Input.Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.NotNull(capturedApp);
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

    private class TestPresentationAdapter : IHex1bTerminalPresentationAdapter
    {
        public TerminalCapabilities Capabilities => TerminalCapabilities.Modern;

        public int Width => 80;
        public int Height => 24;

#pragma warning disable CS0067 // Event is never used
        public event Action<int, int>? Resized;
        public event Action? Disconnected;
#pragma warning restore CS0067

        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
        {
            // Return empty input to signal disconnection (test ends immediately)
            return ValueTask.FromResult<ReadOnlyMemory<byte>>(ReadOnlyMemory<byte>.Empty);
        }

        public ValueTask FlushAsync(CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask EnterRawModeAsync(CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask ExitRawModeAsync(CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
