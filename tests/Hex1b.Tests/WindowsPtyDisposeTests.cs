using System.Text;
using Hex1b.Tests.TestHelpers;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the PTY disposal race condition in WindowsPtyHandle.
///
/// WindowsPtyHandle.DisposeAsync joins read/write threads for 2 seconds, then
/// disposes the CancellationTokenSource. If a thread is still running past the
/// join timeout (e.g., blocked in a retry loop writing to a full channel), it
/// accesses _cts.Token after _cts.Dispose() — throwing ObjectDisposedException
/// on a background thread, which crashes the process.
///
/// The fix caches _cts.Token as a local CancellationToken (a struct) at the top
/// of ReadThreadProc/WriteThreadProc. The cached copy remains valid and correctly
/// reflects cancellation even after the source is disposed.
/// </summary>
public class WindowsPtyDisposeTests
{
    [Fact]
    public void WindowsPtyShimLocator_WithOverridePath_ResolvesOverride()
    {
        using var workspace = TestWorkspace.Create("pty_shim_locator");
        var shimFile = workspace.CreateFile("hex1bpty.exe", string.Empty);
        var original = Environment.GetEnvironmentVariable("HEX1B_PTY_SHIM_PATH");

        try
        {
            Environment.SetEnvironmentVariable("HEX1B_PTY_SHIM_PATH", shimFile.FullName);

            Assert.True(WindowsPtyShimLocator.TryResolve(out var resolvedPath));
            Assert.Equal(shimFile.FullName, resolvedPath, ignoreCase: true);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HEX1B_PTY_SHIM_PATH", original);
        }
    }

    [Theory]
    [InlineData("cmd.exe", new[] { "/c", "echo hi" }, false)]
    [InlineData("cmd.exe", new[] { "/k", "echo hi" }, true)]
    [InlineData("powershell.exe", new[] { "-NoLogo", "-NoProfile" }, true)]
    [InlineData("powershell.exe", new[] { "-NoLogo", "-Command", "Write-Output hi" }, false)]
    [InlineData("pwsh.exe", new[] { "-NoExit", "-Command", "Write-Output hi" }, true)]
    public void WindowsPtyShellHeuristics_RecognizesInteractiveWarmupScenarios(
        string fileName,
        string[] arguments,
        bool expected)
    {
        var actual = WindowsPtyShellHeuristics.RequiresPromptWarmup(fileName, arguments);
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Category", "Windows")]
    public async Task WithPtyProcess_WhenShimBinaryAvailable_UsesWindowsPtyShim()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var originalShimPath = Environment.GetEnvironmentVariable("HEX1B_PTY_SHIM_PATH");
        var originalDisable = Environment.GetEnvironmentVariable("HEX1B_DISABLE_WINDOWS_PTY_SHIM");

        try
        {
            var shimPath = Path.Combine(AppContext.BaseDirectory, "hex1bpty.exe");
            Assert.True(File.Exists(shimPath), $"Expected PTY shim at {shimPath}");

            Environment.SetEnvironmentVariable("HEX1B_DISABLE_WINDOWS_PTY_SHIM", null);
            Environment.SetEnvironmentVariable("HEX1B_PTY_SHIM_PATH", shimPath);

            await using var terminal = Hex1bTerminal.CreateBuilder()
                .WithPtyProcess(
                    "cmd.exe",
                    "/c",
                    "if \"%HEX1B_PTY_SHIM_ACTIVE%\"==\"1\" (exit 17) else (exit 23)")
                .WithHeadless()
                .WithDimensions(80, 10)
                .Build();

            var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
            var exitCode = await runTask.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
            Assert.Equal(17, exitCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HEX1B_PTY_SHIM_PATH", originalShimPath);
            Environment.SetEnvironmentVariable("HEX1B_DISABLE_WINDOWS_PTY_SHIM", originalDisable);
        }
    }

    [Theory]
    [InlineData("hex1bpty.exe")]
    [InlineData("hex1bpty-managed.exe")]
    [Trait("Category", "Windows")]
    public async Task WithPtyProcess_WhenSelectedShimAvailable_StreamsOutputInputAndResize(string shimFileName)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var originalShimPath = Environment.GetEnvironmentVariable("HEX1B_PTY_SHIM_PATH");
        var originalDisable = Environment.GetEnvironmentVariable("HEX1B_DISABLE_WINDOWS_PTY_SHIM");

        try
        {
            var shimPath = Path.Combine(AppContext.BaseDirectory, shimFileName);
            if (!File.Exists(shimPath))
            {
                return;
            }

            Environment.SetEnvironmentVariable("HEX1B_DISABLE_WINDOWS_PTY_SHIM", null);
            Environment.SetEnvironmentVariable("HEX1B_PTY_SHIM_PATH", shimPath);

            await using var process = new Hex1bTerminalChildProcess(
                "cmd.exe",
                ["/q", "/d", "/k", "prompt PTYTEST$G"],
                inheritEnvironment: true,
                initialWidth: 80,
                initialHeight: 12);

            await process.StartAsync(TestContext.Current.CancellationToken);
            Assert.Equal("WindowsShimPtyHandle", GetActivePtyHandleTypeName(process));

            var startup = await ReadUntilContainsAsync(
                process,
                "PTYTEST>",
                TimeSpan.FromSeconds(15),
                TestContext.Current.CancellationToken);
            Assert.Contains("PTYTEST>", startup, StringComparison.Ordinal);

            await process.WriteInputAsync(Encoding.UTF8.GetBytes("echo UDS_SHIM_OK\r\n"), TestContext.Current.CancellationToken);
            var echoed = await ReadUntilContainsAsync(
                process,
                "UDS_SHIM_OK",
                TimeSpan.FromSeconds(15),
                TestContext.Current.CancellationToken);
            Assert.Contains("UDS_SHIM_OK", echoed, StringComparison.Ordinal);

            await process.ResizeAsync(123, 37, TestContext.Current.CancellationToken);
            await process.WriteInputAsync(
                Encoding.UTF8.GetBytes("powershell -NoLogo -NoProfile -Command \"Write-Output ([Console]::WindowWidth.ToString() + 'x' + [Console]::WindowHeight)\"\r\n"),
                TestContext.Current.CancellationToken);

            var resizeReport = await ReadUntilContainsAsync(
                process,
                "123x37",
                TimeSpan.FromSeconds(20),
                TestContext.Current.CancellationToken);
            Assert.Contains("123x37", resizeReport, StringComparison.Ordinal);

            await process.WriteInputAsync(Encoding.UTF8.GetBytes("exit\r\n"), TestContext.Current.CancellationToken);
            var exitCode = await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            Assert.Equal(0, exitCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HEX1B_PTY_SHIM_PATH", originalShimPath);
            Environment.SetEnvironmentVariable("HEX1B_DISABLE_WINDOWS_PTY_SHIM", originalDisable);
        }
    }

    /// <summary>
    /// Rapidly disposing a PTY terminal while the child process is still running
    /// must not throw ObjectDisposedException on background threads.
    /// </summary>
    [Fact]
    [Trait("Category", "Windows")]
    public async Task DisposeAsync_WhileProcessRunning_DoesNotThrowObjectDisposedException()
    {
        if (!OperatingSystem.IsWindows())
            return; // WindowsPtyHandle is Windows-only

        // Track any unhandled exceptions from background threads
        var unhandledExceptions = new List<Exception>();
        void Handler(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                unhandledExceptions.Add(ex);
        }

        AppDomain.CurrentDomain.UnhandledException += Handler;
        try
        {
            // Run multiple iterations to increase the chance of hitting the race
            for (var i = 0; i < 5; i++)
            {
                // Launch a long-running process (ping runs for several seconds)
                var terminal = Hex1bTerminal.CreateBuilder()
                    .WithPtyProcess("cmd.exe", "/c", "ping -n 10 127.0.0.1")
                    .WithTerminalWidget(out _)
                    .WithHeadless()
                    .WithDimensions(80, 24)
                    .Build();

                // Start RunAsync — this kicks off the PTY read/write threads
                var runTask = terminal.RunAsync(CancellationToken.None);

                // Brief delay to let the process start producing output,
                // filling the channel and exercising the retry loops
                await Task.Delay(200);

                // Dispose immediately while the process is still running —
                // this triggers the race: DisposeAsync cancels CTS, closes
                // streams, joins threads for 2s, then disposes CTS. If
                // threads are still alive, they must not access _cts.Token.
                await terminal.DisposeAsync();

                // Give background threads a moment to surface any exceptions
                await Task.Delay(100);
            }

            // Verify no ObjectDisposedException escaped to AppDomain handler
            var odeExceptions = unhandledExceptions
                .Where(ex => ex is ObjectDisposedException)
                .ToList();

            Assert.Empty(odeExceptions);
        }
        finally
        {
            AppDomain.CurrentDomain.UnhandledException -= Handler;
        }
    }

    private static async Task<string> ReadUntilContainsAsync(
        Hex1bTerminalChildProcess process,
        string text,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        var output = new StringBuilder();

        while (DateTime.UtcNow < deadline)
        {
            using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            readCts.CancelAfter(TimeSpan.FromMilliseconds(100));

            ReadOnlyMemory<byte> data;
            try
            {
                data = await process.ReadOutputAsync(readCts.Token).AsTask();
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                data = ReadOnlyMemory<byte>.Empty;
            }

            if (!data.IsEmpty)
            {
                output.Append(Encoding.UTF8.GetString(data.Span));
                if (output.ToString().Contains(text, StringComparison.Ordinal))
                {
                    return output.ToString();
                }
            }
        }

        Assert.Fail($"Timed out waiting for \"{text}\". Output so far:{Environment.NewLine}{output}");
        return output.ToString();
    }

    private static string GetActivePtyHandleTypeName(Hex1bTerminalChildProcess process)
    {
        var processType = typeof(Hex1bTerminalChildProcess);
        var ptyHandleField = processType.GetField("_ptyHandle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var proxyHandle = ptyHandleField?.GetValue(process);
        if (proxyHandle is null)
        {
            return "<null>";
        }

        var activeHandleField = proxyHandle.GetType().GetField("_activeHandle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var activeHandle = activeHandleField?.GetValue(proxyHandle);
        return activeHandle?.GetType().Name ?? proxyHandle.GetType().Name;
    }

    /// <summary>
    /// Disposing a PTY terminal after the child process has exited naturally
    /// should complete without exceptions.
    /// </summary>
    [Fact]
    [Trait("Category", "Windows")]
    public async Task DisposeAsync_AfterProcessExit_CompletesCleanly()
    {
        if (!OperatingSystem.IsWindows())
            return;

        // Launch a process that exits quickly
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithPtyProcess("cmd.exe", "/c", "echo done")
            .WithTerminalWidget(out _)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        var runTask = terminal.RunAsync(CancellationToken.None);

        // Wait for the process to exit naturally
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!runTask.IsCompleted && !cts.IsCancellationRequested)
            await Task.Delay(100, cts.Token);

        // Dispose after process has exited — should not throw
        var exception = await Record.ExceptionAsync(async () =>
            await terminal.DisposeAsync());

        Assert.Null(exception);
    }

    /// <summary>
    /// Rapidly creating and disposing PTY terminals in sequence must not
    /// leak unhandled exceptions from thread cleanup.
    /// </summary>
    [Fact]
    [Trait("Category", "Windows")]
    public async Task RapidCreateDispose_DoesNotLeakExceptions()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var unhandledExceptions = new List<Exception>();
        void Handler(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                unhandledExceptions.Add(ex);
        }

        AppDomain.CurrentDomain.UnhandledException += Handler;
        try
        {
            for (var i = 0; i < 10; i++)
            {
                var terminal = Hex1bTerminal.CreateBuilder()
                    .WithPtyProcess("cmd.exe", "/c", "echo iteration " + i)
                    .WithTerminalWidget(out _)
                    .WithHeadless()
                    .WithDimensions(80, 24)
                    .Build();

                _ = terminal.RunAsync(CancellationToken.None);

                // Dispose immediately — no delay
                await terminal.DisposeAsync();
            }

            // Wait for any lingering thread exceptions
            await Task.Delay(500);

            Assert.Empty(unhandledExceptions);
        }
        finally
        {
            AppDomain.CurrentDomain.UnhandledException -= Handler;
        }
    }

    /// <summary>
    /// If the Windows PTY shim binary is unavailable, WithPtyProcess should fall
    /// back to the in-process WindowsPtyHandle so callers do not lose PTY support.
    /// </summary>
    [Fact]
    [Trait("Category", "Windows")]
    public async Task WithPtyProcess_MissingShimBinary_FallsBackToInProcessPty()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var originalShimPath = Environment.GetEnvironmentVariable("HEX1B_PTY_SHIM_PATH");
        var originalDisable = Environment.GetEnvironmentVariable("HEX1B_DISABLE_WINDOWS_PTY_SHIM");

        try
        {
            Environment.SetEnvironmentVariable("HEX1B_DISABLE_WINDOWS_PTY_SHIM", null);
            Environment.SetEnvironmentVariable(
                "HEX1B_PTY_SHIM_PATH",
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "hex1bpty.exe"));

            await using var terminal = Hex1bTerminal.CreateBuilder()
                .WithPtyProcess(
                    "cmd.exe",
                    "/c",
                    "if \"%HEX1B_PTY_SHIM_ACTIVE%\"==\"1\" (exit 17) else (exit 23)")
                .WithHeadless()
                .WithDimensions(80, 10)
                .Build();

            var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
            var exitCode = await runTask.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
            Assert.Equal(23, exitCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HEX1B_PTY_SHIM_PATH", originalShimPath);
            Environment.SetEnvironmentVariable("HEX1B_DISABLE_WINDOWS_PTY_SHIM", originalDisable);
        }
    }
}
