using Hex1b.Terminal;
using Hex1b.Terminal.Automation;
using Hex1b.Tests.TestHelpers;
using Hex1b.Widgets;
using System.Diagnostics;
using System.Text;

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

    // === WithPtyShell and WithPtyProcess Tests ===

    [Fact]
    public void WithPtyShell_ReturnsBuilder()
    {
        var result = Hex1bTerminal.CreateBuilder().WithPtyShell("dotnet");
        
        Assert.IsType<Hex1bTerminalBuilder>(result);
    }

    [Fact]
    public void WithPtyShell_NullShell_UsesDefaultShell()
    {
        // Should not throw when shell is null (uses default shell)
        var builder = Hex1bTerminal.CreateBuilder().WithPtyShell();
        
        Assert.NotNull(builder);
    }

    [Fact]
    public void WithPtyProcess_ReturnsBuilder()
    {
        var result = Hex1bTerminal.CreateBuilder().WithPtyProcess("dotnet", "--version");
        
        Assert.IsType<Hex1bTerminalBuilder>(result);
    }

    [Fact]
    public void WithPtyProcess_NullFileName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Hex1bTerminal.CreateBuilder().WithPtyProcess((string)null!));
    }

    [Fact]
    public void WithPtyProcess_OptionsOverload_ReturnsBuilder()
    {
        var result = Hex1bTerminal.CreateBuilder()
            .WithPtyProcess(options =>
            {
                options.FileName = "dotnet";
                options.Arguments = ["--version"];
            });
        
        Assert.IsType<Hex1bTerminalBuilder>(result);
    }

    [Fact]
    public void WithPtyProcess_Options_NullConfigure_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Hex1bTerminal.CreateBuilder().WithPtyProcess((Action<Hex1bTerminalProcessOptions>)null!));
    }

    [Fact]
    public void WithPtyProcess_Options_EmptyFileName_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Hex1bTerminal.CreateBuilder()
                .WithPtyProcess(options => { /* FileName not set */ })
                .Build());
        
        Assert.Contains("FileName", ex.Message);
    }

    [Fact]
    public void WithPtyProcess_Options_CanSetEnvironment()
    {
        // Should not throw when setting environment variables
        var builder = Hex1bTerminal.CreateBuilder()
            .WithPtyProcess(options =>
            {
                options.FileName = "dotnet";
                options.Environment = new Dictionary<string, string>
                {
                    ["MY_VAR"] = "my_value"
                };
            });
        
        Assert.NotNull(builder);
    }

    [Fact]
    public void WithPtyProcess_Options_CanSetWorkingDirectory()
    {
        // Should not throw when setting working directory
        var builder = Hex1bTerminal.CreateBuilder()
            .WithPtyProcess(options =>
            {
                options.FileName = "dotnet";
                options.WorkingDirectory = Path.GetTempPath();
            });
        
        Assert.NotNull(builder);
    }

    [Fact]
    public async Task WithPtyProcess_ExecutesProcess()
    {
        // Inline C# script for cross-platform testing
        const string script = """
            if (args.Length >= 2 && int.TryParse(args[0], out var delayMs))
            {
                await Task.Delay(delayMs);
                Console.WriteLine(string.Join(" ", args.Skip(1)));
            }
            """;

        using var workspace = TestWorkspace.Create("pty_exec");
        var scriptFile = workspace.CreateCSharpProgram("delay.cs", script);
        
        var pattern = new CellPatternSearcher().Find("Hello from test program");
        
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPtyProcess("dotnet", "run", scriptFile.FullName, "50", "Hello from test program")
            .WithHeadless()
            .WithDimensions(80, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches, TimeSpan.FromSeconds(30))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var exitCode = await runTask;
        
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task WithPtyProcess_InteractiveProcess_RespondsToInput()
    {
        // Inline C# script for interactive input test
        const string script = """
            Console.WriteLine("Ready");
            Console.ReadKey(intercept: true);
            Console.WriteLine("Done");
            """;

        using var workspace = TestWorkspace.Create("pty_interactive");
        var scriptFile = workspace.CreateCSharpProgram("wait-input.cs", script);
        
        var readyPattern = new CellPatternSearcher().Find("Ready");
        var exitPattern = new CellPatternSearcher().Find("Done");
        
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPtyProcess("dotnet", "run", scriptFile.FullName)
            .WithHeadless()
            .WithDimensions(80, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(readyPattern).HasMatches, TimeSpan.FromSeconds(30))
            .Type("q")
            .WaitUntil(s => s.SearchPattern(exitPattern).HasMatches, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var exitCode = await runTask;
        
        Assert.Equal(0, exitCode);
    }

    // === WithProcess (Standard .NET Process) Tests ===

    [Fact]
    public void WithProcess_ReturnsBuilder()
    {
        var result = Hex1bTerminal.CreateBuilder().WithProcess("dotnet", "--version");
        
        Assert.IsType<Hex1bTerminalBuilder>(result);
    }

    [Fact]
    public void WithProcess_NullFileName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Hex1bTerminal.CreateBuilder().WithProcess((string)null!));
    }

    [Fact]
    public async Task WithProcess_ExecutesProcess()
    {
        // Inline C# echo script
        const string script = """Console.WriteLine(string.Join(" ", args));""";
        
        using var workspace = TestWorkspace.Create("process_exec");
        var scriptFile = workspace.CreateCSharpProgram("echo.cs", script);
        
        var pattern = new CellPatternSearcher().Find("Hello from process");
        
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithProcess("dotnet", "run", scriptFile.FullName, "Hello", "from", "process")
            .WithHeadless()
            .WithDimensions(60, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches, TimeSpan.FromSeconds(30))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var exitCode = await runTask;
        
        Assert.Equal(0, exitCode);
    }

    // === WithProcess(ProcessStartInfo) Tests ===

    [Fact]
    public void WithProcess_ProcessStartInfo_ReturnsBuilder()
    {
        var startInfo = new ProcessStartInfo("dotnet", "--version");
        var builder = Hex1bTerminal.CreateBuilder()
            .WithProcess(startInfo);
        
        Assert.NotNull(builder);
    }

    [Fact]
    public void WithProcess_ProcessStartInfo_NullThrows()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Hex1bTerminal.CreateBuilder()
                .WithProcess((ProcessStartInfo)null!));
    }

    [Fact]
    public async Task WithProcess_ProcessStartInfo_AdapterCapturesOutput()
    {
        // Inline C# echo script
        const string script = """Console.WriteLine(string.Join(" ", args));""";
        
        using var workspace = TestWorkspace.Create("adapter_output");
        var scriptFile = workspace.CreateCSharpProgram("echo.cs", script);
        
        var startInfo = new ProcessStartInfo("dotnet", $"run {scriptFile.FullName} AdapterTestOutput");
        var adapter = new StandardProcessWorkloadAdapter(startInfo);
        
        await adapter.StartAsync();
        
        // Read output
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var output = new StringBuilder();
        
        while (!cts.Token.IsCancellationRequested)
        {
            var data = await adapter.ReadOutputAsync(cts.Token);
            if (data.IsEmpty)
            {
                if (adapter.HasExited) break;
                continue;
            }
            output.Append(Encoding.UTF8.GetString(data.Span));
        }
        
        var exitCode = await adapter.WaitForExitAsync();
        await adapter.DisposeAsync();
        
        Assert.Equal(0, exitCode);
        Assert.Contains("AdapterTestOutput", output.ToString());
    }

    [Fact]
    public async Task WithProcess_ProcessStartInfo_ExecutesProcess()
    {
        // Inline C# echo script
        const string script = """Console.WriteLine(string.Join(" ", args));""";
        
        using var workspace = TestWorkspace.Create("psi_exec");
        var scriptFile = workspace.CreateCSharpProgram("echo.cs", script);
        
        var startInfo = new ProcessStartInfo("dotnet");
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add(scriptFile.FullName);
        startInfo.ArgumentList.Add("Hello");
        startInfo.ArgumentList.Add("from");
        startInfo.ArgumentList.Add("ProcessStartInfo");
        
        var pattern = new CellPatternSearcher().Find("Hello from ProcessStartInfo");
        
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithProcess(startInfo)
            .WithHeadless()
            .WithDimensions(80, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches, TimeSpan.FromSeconds(30))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var exitCode = await runTask;
        
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task WithProcess_ProcessStartInfo_PreservesWorkingDirectory()
    {
        // Inline C# pwd script
        const string script = """Console.WriteLine(Environment.CurrentDirectory);""";
        
        using var workspace = TestWorkspace.Create("psi_workdir");
        var scriptFile = workspace.CreateCSharpProgram("pwd.cs", script);
        
        var tempDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = tempDir
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add(scriptFile.FullName);
        
        var pattern = new CellPatternSearcher().Find(tempDir);
        
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithProcess(startInfo)
            .WithHeadless()
            .WithDimensions(120, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches, TimeSpan.FromSeconds(30))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var exitCode = await runTask;
        
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task WithProcess_ProcessStartInfo_PreservesEnvironmentVariables()
    {
        // Inline C# env script
        const string script = """
            if (args.Length > 0)
            {
                var value = Environment.GetEnvironmentVariable(args[0]);
                if (value != null)
                    Console.WriteLine(value);
            }
            """;
        
        using var workspace = TestWorkspace.Create("psi_env");
        var scriptFile = workspace.CreateCSharpProgram("env.cs", script);
        
        var startInfo = new ProcessStartInfo("dotnet");
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add(scriptFile.FullName);
        startInfo.ArgumentList.Add("MY_CUSTOM_VAR");
        startInfo.Environment["MY_CUSTOM_VAR"] = "TestValue12345";
        
        var pattern = new CellPatternSearcher().Find("TestValue12345");
        
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithProcess(startInfo)
            .WithHeadless()
            .WithDimensions(80, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches, TimeSpan.FromSeconds(30))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var exitCode = await runTask;
        
        Assert.Equal(0, exitCode);
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

    // === Diagnostic Tests for PTY + Headless Investigation ===

    [Fact]
    public async Task Diagnostic_EchoCommand_OutputAppearsInBuffer()
    {
        // Inline C# delay script that outputs a marker after a delay
        const string script = """
            await Task.Delay(100);
            Console.WriteLine("DIAGNOSTIC_MARKER_12345");
            """;

        using var workspace = TestWorkspace.Create("diag_echo");
        var scriptFile = workspace.CreateCSharpProgram("delay-echo.cs", script);
        
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPtyProcess("dotnet", "run", scriptFile.FullName)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        // Start RunAsync in background - this starts the process
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = terminal.RunAsync(cts.Token);

        // Poll the screen buffer directly
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromSeconds(5);
        var foundMarker = false;
        var diagnosticOutput = new StringBuilder();
        
        while (DateTime.UtcNow - startTime < timeout)
        {
            var snapshot = terminal.CreateSnapshot();
            var screenText = snapshot.GetScreenText();
            
            diagnosticOutput.AppendLine($"[{DateTime.UtcNow - startTime:ss\\.fff}] Screen: '{screenText.Replace("\n", "\\n").Replace("\r", "\\r")}'");
            
            if (screenText.Contains("DIAGNOSTIC_MARKER_12345"))
            {
                foundMarker = true;
                break;
            }

            // Check if process exited
            if (runTask.IsCompleted)
            {
                diagnosticOutput.AppendLine($"[{DateTime.UtcNow - startTime:ss\\.fff}] Process exited with code: {runTask.Result}");
                // Give one more chance to read output after process exits
                await Task.Delay(100);
                snapshot = terminal.CreateSnapshot();
                screenText = snapshot.GetScreenText();
                diagnosticOutput.AppendLine($"[{DateTime.UtcNow - startTime:ss\\.fff}] Final screen: '{screenText.Replace("\n", "\\n").Replace("\r", "\\r")}'");
                if (screenText.Contains("DIAGNOSTIC_MARKER_12345"))
                {
                    foundMarker = true;
                }
                break;
            }

            await Task.Delay(50, cts.Token);
        }

        TestContext.Current.TestOutputHelper?.WriteLine(diagnosticOutput.ToString());

        // Wait for process to complete
        var exitCode = 0;
        if (!runTask.IsCompleted)
        {
            cts.Cancel();
            try { exitCode = await runTask; } catch { }
        }
        else
        {
            exitCode = await runTask;
        }

        Assert.True(foundMarker, 
            $"Expected to find 'DIAGNOSTIC_MARKER_12345' in screen buffer.\n" +
            $"Diagnostics:\n{diagnosticOutput}");
    }
}
