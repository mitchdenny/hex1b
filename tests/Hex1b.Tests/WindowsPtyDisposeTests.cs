using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
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
[DoNotParallelize]
[TestClass]
public class WindowsPtyDisposeTests
{
    [TestMethod]
    public async Task ConnectWithRetriesAsync_CanceledBeforeConnect_PreservesCancellationToken()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var helper = Process.GetCurrentProcess();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var failure = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            WindowsShimPtyHandle.ConnectWithRetriesAsync(socket,
                new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0), helper, cancellation.Token));

        Assert.AreEqual(cancellation.Token, failure.CancellationToken);
        Assert.IsFalse(socket.Connected);
    }

    [TestMethod]
    [TestCategory("Windows")]
    [DataRow(WindowsPtyMode.Direct)]
    [DataRow(WindowsPtyMode.RequireProxy)]
    public async Task StartAsync_MissingWorkingDirectory_FaultsStartupAndWaitingIo(WindowsPtyMode mode)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var missingDirectory = Path.Combine(Environment.CurrentDirectory, $"missing-pty-cwd-{Guid.NewGuid():N}");
        var shimPath = mode == WindowsPtyMode.RequireProxy ? ResolveShimPath() : null;
        await using var process = new Hex1bTerminalChildProcess(
            "cmd.exe", ["/d", "/c", "exit 0"], missingDirectory, null, true, 80, 24,
            _ => Hex1bTerminalChildProcess.CreatePtyHandle(mode, shimPath));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var read = process.ReadOutputAsync(cancellation.Token).AsTask();
        var write = process.WriteInputAsync(new byte[] { 1 }, cancellation.Token).AsTask();

        var failure = await Assert.ThrowsAsync<Exception>(() => process.StartAsync(cancellation.Token));
        Assert.IsFalse(failure is OperationCanceledException, "Missing cwd must fail startup, not time out.");
        Assert.AreSame(failure, await Assert.ThrowsAsync<Exception>(() => read.WaitAsync(cancellation.Token)));
        Assert.AreSame(failure, await Assert.ThrowsAsync<Exception>(() => write.WaitAsync(cancellation.Token)));
        Assert.IsFalse(process.HasStarted);
        Assert.IsFalse(process.HasExited);
        Assert.AreEqual(-1, process.ProcessId);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => process.WaitForExitAsync(cancellation.Token));
    }

    private static string ResolveShimPath()
    {
        Assert.IsTrue(WindowsPtyShimLocator.TryResolve(out var path), "Expected PTY shim to be resolvable via WindowsPtyShimLocator");
        return path;
    }

    [TestMethod]
    public void WindowsPtyShimLocator_WithExplicitPath_ResolvesOverride()
    {
        using var workspace = TestWorkspace.Create("pty_shim_locator");
        var shimFile = workspace.CreateFile("hex1bpty.exe", string.Empty);

        Assert.IsTrue(WindowsPtyShimLocator.TryResolve(shimFile.FullName, out var resolvedPath));
        Assert.AreEqual(shimFile.FullName, resolvedPath, ignoreCase: true);
    }

    [TestMethod]
    public void WindowsPtyShimLocator_WithPackagedRuntimePath_ResolvesShim()
    {
        using var workspace = TestWorkspace.Create("pty_shim_locator_packaged");
        var baseDirectory = workspace.GetPath("tool-base");
        var runtimeDirectory = Path.Combine(baseDirectory, "runtimes", "win-x64", "native");
        Directory.CreateDirectory(runtimeDirectory);

        var shimPath = Path.Combine(runtimeDirectory, "hex1bpty.exe");
        File.WriteAllText(shimPath, string.Empty);

        Assert.IsTrue(WindowsPtyShimLocator.TryResolveFromBaseDirectory(baseDirectory, explicitPath: null, out var resolvedPath));
        Assert.AreEqual(shimPath, resolvedPath, ignoreCase: true);
    }

    [TestMethod]
    public void WindowsPtyShimLocator_WithTrailingSampleOutputPath_ResolvesRepositoryBuildShim()
    {
        using var workspace = TestWorkspace.Create("pty_shim_locator_repository");
        var repositoryRoot = workspace.GetPath("repo");
        var baseDirectory = Path.Combine(
            repositoryRoot, "samples", "WebTerminalDemo", "bin", "Debug", "net10.0") +
            Path.DirectorySeparatorChar;
        var rid = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64";
        var shimDirectory = Path.Combine(
            repositoryRoot, "src", "Hex1b", "obj", "windows-pty-shim", rid, "native");
        Directory.CreateDirectory(baseDirectory);
        Directory.CreateDirectory(shimDirectory);
        var shimPath = Path.Combine(shimDirectory, "hex1bpty.exe");
        File.WriteAllText(shimPath, string.Empty);

        Assert.IsTrue(WindowsPtyShimLocator.TryResolveFromBaseDirectory(
            baseDirectory, explicitPath: null, out var resolvedPath));
        Assert.AreEqual(shimPath, resolvedPath, ignoreCase: true);
    }

    [TestMethod]
    [DataRow("cmd.exe", new[] { "/c", "echo hi" }, false)]
    [DataRow("cmd.exe", new[] { "/k", "echo hi" }, true)]
    [DataRow("powershell.exe", new[] { "-NoLogo", "-NoProfile" }, true)]
    [DataRow("powershell.exe", new[] { "-NoLogo", "-Command", "Write-Output hi" }, false)]
    [DataRow("pwsh.exe", new[] { "-NoExit", "-Command", "Write-Output hi" }, true)]
    public void WindowsPtyShellHeuristics_RecognizesInteractiveWarmupScenarios(
        string fileName,
        string[] arguments,
        bool expected)
    {
        var actual = WindowsPtyShellHeuristics.RequiresPromptWarmup(fileName, arguments);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [TestCategory("Windows")]
    public async Task WithPtyProcess_WhenShimBinaryAvailable_UsesWindowsPtyShim()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var shimPath = ResolveShimPath();

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
        Assert.AreEqual(17, exitCode);
    }

    [TestMethod]
    [TestCategory("Windows")]
    public async Task WithPtyProcess_WhenShimAvailable_StreamsOutputInputAndResize()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var shimPath = ResolveShimPath();

        await using var process = new Hex1bTerminalChildProcess(
            Path.Combine(AppContext.BaseDirectory, "Hex1b.Tests.exe"),
            ["--filter", "FullyQualifiedName=Hex1b.Tests.WindowsConsoleProbeTests.PtyIoChild", "--no-progress", "--no-ansi"],
            workingDirectory: AppContext.BaseDirectory,
            environment: new Dictionary<string, string> { [WindowsConsoleProbeTests.PtyIoChildEnvironmentVariable] = "1" },
            inheritEnvironment: true,
            initialWidth: 80,
            initialHeight: 12,
            ptyHandleFactory: _ => Hex1bTerminalChildProcess.CreatePtyHandle(
                WindowsPtyMode.RequireProxy,
                shimPath));

        await process.StartAsync(TestContext.Current.CancellationToken);
        Assert.AreEqual("WindowsShimPtyHandle", GetActivePtyHandleTypeName(process));

        await ReadUntilContainsAsync(
            process.ReadOutputAsync,
            "PTY_READY:80x12;",
            TimeSpan.FromSeconds(15),
            TestContext.Current.CancellationToken);

        // The acknowledgement is produced only after the child consumes input, not by console echo.
        await process.WriteInputAsync("a"u8.ToArray(), TestContext.Current.CancellationToken);
        await ReadUntilContainsAsync(
            process.ReadOutputAsync,
            "PTY_INPUT:a:80x12;",
            TimeSpan.FromSeconds(15),
            TestContext.Current.CancellationToken);

        foreach (var (width, height, input) in new[] { (123, 37, "b"), (64, 16, "c") })
        {
            await process.ResizeAsync(width, height, TestContext.Current.CancellationToken);
            // ResizeAsync queues a shim frame; wait for the running child to observe the new size.
            await ReadUntilContainsAsync(
                process.ReadOutputAsync,
                $"PTY_RESIZED:{width}x{height};",
                TimeSpan.FromSeconds(20),
                TestContext.Current.CancellationToken);

            await process.WriteInputAsync(Encoding.UTF8.GetBytes(input), TestContext.Current.CancellationToken);
            await ReadUntilContainsAsync(
                process.ReadOutputAsync,
                $"PTY_INPUT:{input}:{width}x{height};",
                TimeSpan.FromSeconds(15),
                TestContext.Current.CancellationToken);
        }

        await process.WriteInputAsync("q"u8.ToArray(), TestContext.Current.CancellationToken);
        var exitCode = await process.WaitForExitAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    [TestCategory("Windows")]
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
            ptyHandleFactory: _ => Hex1bTerminalChildProcess.CreatePtyHandle(WindowsPtyMode.Direct));

        await process.StartAsync(TestContext.Current.CancellationToken);
        var pid = process.ProcessId;
        Assert.IsTrue(pid > 0, "Expected the PTY child process to start.");

        await process.DisposeAsync();

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (IsProcessRunning(pid) && !timeoutCts.IsCancellationRequested)
        {
            await Task.Delay(50, timeoutCts.Token);
        }

        Assert.IsFalse(IsProcessRunning(pid), $"Process {pid} should have terminated after dispose.");
    }

    [TestMethod]
    [TestCategory("Windows")]
    public async Task Hex1bPtyHost_MismatchedLaunchToken_IsRejectedAndLogged()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var workspace = TestWorkspace.Create("pty_shim_auth");
        var shimPath = ResolveShimPath();

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

            Assert.IsTrue(process.Start(), "Failed to launch hex1bpty.exe.");

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
            Assert.IsNotNull(frame);
            Assert.AreEqual(WindowsPtyShimFrameType.Error, frame.Value.Type);

            var error = WindowsPtyShimProtocol.ReadJson<WindowsPtyShimErrorResponse>(frame.Value.Payload);
            Assert.Contains("token", error.Message, StringComparison.OrdinalIgnoreCase);

            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            Assert.AreEqual(1, process.ExitCode);
            Assert.IsTrue(File.Exists(logPath));

            var logContents = await ReadAllTextSharedAsync(logPath, TestContext.Current.CancellationToken);
            Assert.Contains("Rejected PTY launch request", logContents, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            WindowsPtySocketPaths.DeleteSocketFile(socketPath);
        }
    }

    [TestMethod]
    [TestCategory("Windows")]
    public async Task Hex1bPtyHost_InvalidLogfilePath_DoesNotPreventStartup()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var workspace = TestWorkspace.Create("pty_shim_badlog");
        var shimPath = ResolveShimPath();

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

            Assert.IsTrue(process.Start(), "Failed to launch hex1bpty.exe.");

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
            Assert.IsNotNull(frame);
            Assert.AreEqual(WindowsPtyShimFrameType.Error, frame.Value.Type);

            var error = WindowsPtyShimProtocol.ReadJson<WindowsPtyShimErrorResponse>(frame.Value.Payload);
            Assert.Contains("token", error.Message, StringComparison.OrdinalIgnoreCase);

            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            Assert.AreEqual(1, process.ExitCode);
        }
        finally
        {
            WindowsPtySocketPaths.DeleteSocketFile(socketPath);
        }
    }

    [TestMethod]
    [TestCategory("Windows")]
    public async Task Hex1bPtyHost_ClientDisconnect_ShutsDownHelperProcess()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var workspace = TestWorkspace.Create("pty_shim_disconnect");
        var shimPath = ResolveShimPath();

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

            Assert.IsTrue(process.Start(), "Failed to launch hex1bpty.exe.");

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
                Assert.IsNotNull(frame);
                Assert.AreEqual(WindowsPtyShimFrameType.Started, frame.Value.Type);

                socket.Shutdown(SocketShutdown.Both);
            }

            await process.WaitForExitAsync(TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);

            Assert.IsTrue(process.HasExited, "hex1bpty.exe should exit when its client disconnects.");
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
    [TestMethod]
    [TestCategory("Windows")]
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

            Assert.IsEmpty(odeExceptions);
        }
        finally
        {
            AppDomain.CurrentDomain.UnhandledException -= Handler;
        }
    }

    [TestMethod]
    public async Task ReadUntilContainsAsync_FragmentedChildReport_DoesNotMatchEchoOrHostResize()
    {
        string[] chunks = ["b\x1b[8;37;123tPTY_RESIZED:123x370;", "PTY_RES", "IZED:123x", "37;"];
        var reads = 0;
        var output = await ReadUntilContainsAsync(
            _ => ValueTask.FromResult<ReadOnlyMemory<byte>>(Encoding.UTF8.GetBytes(chunks[reads++])),
            "PTY_RESIZED:123x37;",
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken);

        Assert.AreEqual(chunks.Length, reads);
        Assert.AreEqual(string.Concat(chunks), output);
    }

    [TestMethod]
    public async Task ReadUntilContainsAsync_OutputEnds_FailsWithTranscript()
    {
        var reads = 0;
        var failure = await Assert.ThrowsExactlyAsync<AssertFailedException>(() => ReadUntilContainsAsync(
            _ =>
            {
                reads++;
                Assert.IsTrue(reads <= 2, "An ended output stream must not be polled again.");
                return ValueTask.FromResult<ReadOnlyMemory<byte>>(reads == 1 ? "child failed"u8.ToArray() : []);
            },
            "PTY_READY",
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken));

        Assert.Contains("Output ended", failure.Message, StringComparison.Ordinal);
        Assert.Contains("child failed", failure.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task ReadUntilContainsAsync_CallerCanceledWithEmptyRead_PreservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var failure = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => ReadUntilContainsAsync(
            _ =>
            {
                cancellation.Cancel();
                return ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
            },
            "PTY_READY",
            TimeSpan.FromSeconds(1),
            cancellation.Token));

        Assert.AreEqual(cancellation.Token, failure.CancellationToken);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReadUntilContainsAsync_DeadlineExpires_FailsWithTranscript(bool swallowCancellation)
    {
        var reads = 0;
        async ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct)
        {
            if (++reads == 1)
                return "PTY_READY:80x12"u8.ToArray();

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }
            catch (OperationCanceledException) when (swallowCancellation)
            {
                // Native PTY reads return empty on cancellation.
            }
            return ReadOnlyMemory<byte>.Empty;
        }

        var failure = await Assert.ThrowsExactlyAsync<AssertFailedException>(() => ReadUntilContainsAsync(
            ReadAsync, "PTY_RESIZED:123x37", TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken));

        Assert.AreEqual(2, reads);
        Assert.Contains("Timed out", failure.Message, StringComparison.Ordinal);
        Assert.Contains("PTY_READY:80x12", failure.Message, StringComparison.Ordinal);
    }

    private static async Task<string> ReadUntilContainsAsync(
        Func<CancellationToken, ValueTask<ReadOnlyMemory<byte>>> readAsync,
        string text,
        TimeSpan timeout,
        CancellationToken ct)
    {
        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        readCts.CancelAfter(timeout);
        var output = new StringBuilder();

        while (!readCts.IsCancellationRequested)
        {
            ReadOnlyMemory<byte> data;
            try
            {
                data = await readAsync(readCts.Token);
            }
            catch (OperationCanceledException) when (readCts.IsCancellationRequested)
            {
                break;
            }

            ct.ThrowIfCancellationRequested();
            if (readCts.IsCancellationRequested)
                break;

            if (data.IsEmpty)
            {
                Assert.Fail($"Output ended before \"{text}\". Output so far:{Environment.NewLine}{output}");
            }

            output.Append(Encoding.UTF8.GetString(data.Span));
            if (output.ToString().Contains(text, StringComparison.Ordinal))
                return output.ToString();
        }

        ct.ThrowIfCancellationRequested();
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
    [TestMethod]
    [TestCategory("Windows")]
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
        var exception = await TestSeq.RecordExceptionAsync(async () =>
            await terminal.DisposeAsync());

        Assert.IsNull(exception);
    }

    /// <summary>
    /// Rapidly creating and disposing PTY terminals in sequence must not
    /// leak unhandled exceptions from thread cleanup.
    /// </summary>
    [TestMethod]
    [TestCategory("Windows")]
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

            Assert.IsEmpty(unhandledExceptions);
        }
        finally
        {
            AppDomain.CurrentDomain.UnhandledException -= Handler;
        }
    }

    /// <summary>
    /// Direct mode should continue to use the in-process WindowsPtyHandle even when
    /// a proxy path is configured but unavailable.
    /// </summary>
    [TestMethod]
    [TestCategory("Windows")]
    public async Task WithPtyProcess_DirectMode_UsesInProcessPty_WhenShimPathIsMissing()
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
                options.WindowsPtyMode = WindowsPtyMode.Direct;
                options.WindowsPtyHostPath = missingShimPath;
            })
            .WithHeadless()
            .WithDimensions(80, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var exitCode = await runTask.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        Assert.AreEqual(23, exitCode);
    }

    [TestMethod]
    [TestCategory("Windows")]
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
            ptyHandleFactory: _ => Hex1bTerminalChildProcess.CreatePtyHandle(
                WindowsPtyMode.RequireProxy,
                missingShimPath));

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => process.StartAsync(TestContext.Current.CancellationToken));

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
