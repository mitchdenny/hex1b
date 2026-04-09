using System.Diagnostics;
using System.Net.Sockets;
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
    public void WindowsPtyShimLocator_WithExplicitPath_ResolvesOverride()
    {
        using var workspace = TestWorkspace.Create("pty_shim_locator");
        var shimFile = workspace.CreateFile("hex1bpty.exe", string.Empty);

        Assert.True(WindowsPtyShimLocator.TryResolve(shimFile.FullName, out var resolvedPath));
        Assert.Equal(shimFile.FullName, resolvedPath, ignoreCase: true);
    }

    [Fact]
    public void WindowsPtyShimLocator_WithPackagedRuntimePath_ResolvesShim()
    {
        using var workspace = TestWorkspace.Create("pty_shim_locator_packaged");
        var baseDirectory = workspace.GetPath("tool-base");
        var runtimeDirectory = Path.Combine(baseDirectory, "runtimes", "win-x64", "native");
        Directory.CreateDirectory(runtimeDirectory);

        var shimPath = Path.Combine(runtimeDirectory, "hex1bpty.exe");
        File.WriteAllText(shimPath, string.Empty);

        Assert.True(WindowsPtyShimLocator.TryResolveFromBaseDirectory(baseDirectory, explicitPath: null, out var resolvedPath));
        Assert.Equal(shimPath, resolvedPath, ignoreCase: true);
    }

    [Fact]
    public void WindowsPtySocketPaths_CreateSocketPath_UsesPrivateSocketDirectory()
    {
        using var workspace = TestWorkspace.Create("pty_socket_dir");
        var original = Environment.GetEnvironmentVariable("HEX1B_PTY_SHIM_SOCKET_DIR");
        string? socketPath = null;

        try
        {
            var overrideDirectory = workspace.GetPath("private-sockets");
            Environment.SetEnvironmentVariable("HEX1B_PTY_SHIM_SOCKET_DIR", overrideDirectory);

            socketPath = WindowsPtySocketPaths.CreateSocketPath();

            Assert.StartsWith(overrideDirectory, socketPath, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(".socket", socketPath, StringComparison.OrdinalIgnoreCase);
            Assert.True(Directory.Exists(overrideDirectory));
        }
        finally
        {
            Environment.SetEnvironmentVariable("HEX1B_PTY_SHIM_SOCKET_DIR", original);
            WindowsPtySocketPaths.DeleteSocketFile(socketPath);
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

        var shimPath = Path.Combine(AppContext.BaseDirectory, "hex1bpty.exe");
        Assert.True(File.Exists(shimPath), $"Expected PTY shim at {shimPath}");

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPtyProcess(options =>
            {
                options.FileName = "cmd.exe";
                options.Arguments =
                [
                    "/c",
                    "if \"%HEX1B_PTY_SHIM_ACTIVE%\"==\"1\" (exit 17) else (exit 23)"
                ];
                options.WindowsPtyMode = WindowsPtyMode.RequireProxy;
                options.WindowsPtyHostPath = shimPath;
            })
            .WithHeadless()
            .WithDimensions(80, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var exitCode = await runTask.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        Assert.Equal(17, exitCode);
    }

    [Fact]
    [Trait("Category", "Windows")]
    public async Task WithPtyProcess_WhenShimAvailable_StreamsOutputInputAndResize()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var shimPath = Path.Combine(AppContext.BaseDirectory, "hex1bpty.exe");
        Assert.True(File.Exists(shimPath), $"Expected PTY shim at {shimPath}");

        await using var process = new Hex1bTerminalChildProcess(
            "cmd.exe",
            ["/q", "/d", "/k", "prompt PTYTEST$G"],
            workingDirectory: null,
            environment: null,
            inheritEnvironment: true,
            initialWidth: 80,
            initialHeight: 12,
            ptyHandleFactory: () => Hex1bTerminalChildProcess.CreatePtyHandle(
                WindowsPtyMode.RequireProxy,
                shimPath));

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

    [Fact]
    [Trait("Category", "Windows")]
    public async Task DirectPtyProcess_DisposeAsync_TerminatesChildProcess()
    {
        if (!OperatingSystem.IsWindows())
            return;

        await using var process = new Hex1bTerminalChildProcess(
            "cmd.exe",
            ["/q", "/d", "/k", "prompt PTYDISPOSE$G"],
            workingDirectory: null,
            environment: null,
            inheritEnvironment: true,
            initialWidth: 80,
            initialHeight: 12,
            ptyHandleFactory: () => Hex1bTerminalChildProcess.CreatePtyHandle(WindowsPtyMode.Direct));

        await process.StartAsync(TestContext.Current.CancellationToken);
        var pid = process.ProcessId;
        Assert.True(pid > 0, "Expected the PTY child process to start.");

        await process.DisposeAsync();

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (IsProcessRunning(pid) && !timeoutCts.IsCancellationRequested)
        {
            await Task.Delay(50, timeoutCts.Token);
        }

        Assert.False(IsProcessRunning(pid), $"Process {pid} should have terminated after dispose.");
    }

    [Fact]
    [Trait("Category", "Windows")]
    public async Task Hex1bPtyHost_MismatchedLaunchToken_IsRejectedAndLogged()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var workspace = TestWorkspace.Create("pty_shim_auth");
        var shimPath = Path.Combine(AppContext.BaseDirectory, "hex1bpty.exe");
        Assert.True(File.Exists(shimPath), $"Expected PTY shim at {shimPath}");

        var socketPath = WindowsPtySocketPaths.CreateSocketPath();
        var logPath = workspace.GetPath("hex1bpty.log");
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(shimPath)
                {
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            process.StartInfo.ArgumentList.Add("--socket");
            process.StartInfo.ArgumentList.Add(socketPath);
            process.StartInfo.ArgumentList.Add("--token");
            process.StartInfo.ArgumentList.Add("expected-launch-token");
            process.StartInfo.ArgumentList.Add("--logfile");
            process.StartInfo.ArgumentList.Add(logPath);

            Assert.True(process.Start(), "Failed to launch hex1bpty.exe.");

            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await ConnectWithRetriesAsync(
                socket,
                new UnixDomainSocketEndPoint(socketPath),
                process,
                TestContext.Current.CancellationToken);

            await using var stream = new NetworkStream(socket, ownsSocket: true);
            var launchRequest = new WindowsPtyShimLaunchRequest(
                "cmd.exe",
                ["/c", "echo SHOULD_NOT_RUN"],
                Environment.CurrentDirectory,
                new Dictionary<string, string>(),
                80,
                24,
                "wrong-launch-token");

            await WindowsPtyShimProtocol.WriteJsonAsync(
                stream,
                WindowsPtyShimFrameType.LaunchRequest,
                launchRequest,
                TestContext.Current.CancellationToken);

            var frame = await WindowsPtyShimProtocol.ReadFrameAsync(stream, TestContext.Current.CancellationToken);
            Assert.NotNull(frame);
            Assert.Equal(WindowsPtyShimFrameType.Error, frame.Value.Type);

            var error = WindowsPtyShimProtocol.ReadJson<WindowsPtyShimErrorResponse>(frame.Value.Payload);
            Assert.Contains("token", error.Message, StringComparison.OrdinalIgnoreCase);

            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            Assert.Equal(1, process.ExitCode);
            Assert.True(File.Exists(logPath));

            var logContents = await ReadAllTextSharedAsync(logPath, TestContext.Current.CancellationToken);
            Assert.Contains("Rejected PTY launch request", logContents, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            WindowsPtySocketPaths.DeleteSocketFile(socketPath);
        }
    }

    [Fact]
    [Trait("Category", "Windows")]
    public async Task Hex1bPtyHost_InvalidLogfilePath_DoesNotPreventStartup()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var workspace = TestWorkspace.Create("pty_shim_badlog");
        var shimPath = Path.Combine(AppContext.BaseDirectory, "hex1bpty.exe");
        Assert.True(File.Exists(shimPath), $"Expected PTY shim at {shimPath}");

        var socketPath = WindowsPtySocketPaths.CreateSocketPath();
        var invalidLogPath = workspace.BaseDirectory.FullName; // Directory path, not a file path.
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(shimPath)
                {
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            process.StartInfo.ArgumentList.Add("--socket");
            process.StartInfo.ArgumentList.Add(socketPath);
            process.StartInfo.ArgumentList.Add("--token");
            process.StartInfo.ArgumentList.Add("expected-launch-token");
            process.StartInfo.ArgumentList.Add("--logfile");
            process.StartInfo.ArgumentList.Add(invalidLogPath);

            Assert.True(process.Start(), "Failed to launch hex1bpty.exe.");

            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await ConnectWithRetriesAsync(
                socket,
                new UnixDomainSocketEndPoint(socketPath),
                process,
                TestContext.Current.CancellationToken);

            await using var stream = new NetworkStream(socket, ownsSocket: true);
            var launchRequest = new WindowsPtyShimLaunchRequest(
                "cmd.exe",
                ["/c", "echo SHOULD_NOT_RUN"],
                Environment.CurrentDirectory,
                new Dictionary<string, string>(),
                80,
                24,
                "wrong-launch-token");

            await WindowsPtyShimProtocol.WriteJsonAsync(
                stream,
                WindowsPtyShimFrameType.LaunchRequest,
                launchRequest,
                TestContext.Current.CancellationToken);

            var frame = await WindowsPtyShimProtocol.ReadFrameAsync(stream, TestContext.Current.CancellationToken);
            Assert.NotNull(frame);
            Assert.Equal(WindowsPtyShimFrameType.Error, frame.Value.Type);

            var error = WindowsPtyShimProtocol.ReadJson<WindowsPtyShimErrorResponse>(frame.Value.Payload);
            Assert.Contains("token", error.Message, StringComparison.OrdinalIgnoreCase);

            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            Assert.Equal(1, process.ExitCode);
        }
        finally
        {
            WindowsPtySocketPaths.DeleteSocketFile(socketPath);
        }
    }

    [Fact]
    [Trait("Category", "Windows")]
    public async Task Hex1bPtyHost_ClientDisconnect_ShutsDownHelperProcess()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var workspace = TestWorkspace.Create("pty_shim_disconnect");
        var shimPath = Path.Combine(AppContext.BaseDirectory, "hex1bpty.exe");
        Assert.True(File.Exists(shimPath), $"Expected PTY shim at {shimPath}");

        var socketPath = WindowsPtySocketPaths.CreateSocketPath();
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(shimPath)
                {
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            process.StartInfo.ArgumentList.Add("--socket");
            process.StartInfo.ArgumentList.Add(socketPath);
            process.StartInfo.ArgumentList.Add("--token");
            process.StartInfo.ArgumentList.Add("expected-launch-token");

            Assert.True(process.Start(), "Failed to launch hex1bpty.exe.");

            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await ConnectWithRetriesAsync(
                socket,
                new UnixDomainSocketEndPoint(socketPath),
                process,
                TestContext.Current.CancellationToken);

            await using (var stream = new NetworkStream(socket, ownsSocket: true))
            {
                var launchRequest = new WindowsPtyShimLaunchRequest(
                    "cmd.exe",
                    ["/q", "/d", "/k", "prompt ORPHAN$G"],
                    Environment.CurrentDirectory,
                    new Dictionary<string, string>(),
                    80,
                    24,
                    "expected-launch-token");

                await WindowsPtyShimProtocol.WriteJsonAsync(
                    stream,
                    WindowsPtyShimFrameType.LaunchRequest,
                    launchRequest,
                    TestContext.Current.CancellationToken);

                var frame = await WindowsPtyShimProtocol.ReadFrameAsync(stream, TestContext.Current.CancellationToken);
                Assert.NotNull(frame);
                Assert.Equal(WindowsPtyShimFrameType.Started, frame.Value.Type);

                socket.Shutdown(SocketShutdown.Both);
            }

            await process.WaitForExitAsync(TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);

            Assert.True(process.HasExited, "hex1bpty.exe should exit when its client disconnects.");
        }
        finally
        {
            WindowsPtySocketPaths.DeleteSocketFile(socketPath);
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

    private static async Task ConnectWithRetriesAsync(
        Socket socket,
        UnixDomainSocketEndPoint endpoint,
        Process? helperProcess,
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            if (helperProcess is { HasExited: true })
            {
                var message = $"The PTY shim exited before opening the socket. Exit code: {helperProcess.ExitCode}.";
                if (helperProcess.StartInfo.RedirectStandardError)
                {
                    var stderr = await helperProcess.StandardError.ReadToEndAsync(ct);
                    if (!string.IsNullOrWhiteSpace(stderr))
                    {
                        message += $"{Environment.NewLine}stderr:{Environment.NewLine}{stderr}";
                    }
                }

                throw new IOException(message, lastError);
            }

            try
            {
                await socket.ConnectAsync(endpoint, ct);
                return;
            }
            catch (Exception ex) when (ex is SocketException or IOException)
            {
                lastError = ex;
                if (DateTime.UtcNow >= deadline)
                {
                    break;
                }

                await Task.Delay(100, ct);
            }
        }

        throw new IOException("Timed out connecting to the PTY shim socket for authentication testing.", lastError);
    }

    private static async Task<string> ReadAllTextSharedAsync(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(ct);
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

        var missingShimPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "hex1bpty.exe");

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPtyProcess(options =>
            {
                options.FileName = "cmd.exe";
                options.Arguments =
                [
                    "/c",
                    "if \"%HEX1B_PTY_SHIM_ACTIVE%\"==\"1\" (exit 17) else (exit 23)"
                ];
                options.WindowsPtyMode = WindowsPtyMode.PreferProxy;
                options.WindowsPtyHostPath = missingShimPath;
            })
            .WithHeadless()
            .WithDimensions(80, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var exitCode = await runTask.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        Assert.Equal(23, exitCode);
    }

    [Fact]
    [Trait("Category", "Windows")]
    public async Task WithPtyProcess_MissingRequiredShim_ThrowsInsteadOfFallingBack()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var missingShimPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "hex1bpty.exe");

        await using var process = new Hex1bTerminalChildProcess(
            "cmd.exe",
            ["/c", "echo SHOULD_NOT_RUN"],
            workingDirectory: null,
            environment: null,
            inheritEnvironment: true,
            initialWidth: 80,
            initialHeight: 10,
            ptyHandleFactory: () => Hex1bTerminalChildProcess.CreatePtyHandle(
                WindowsPtyMode.RequireProxy,
                missingShimPath));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => process.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProcessRunning(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
