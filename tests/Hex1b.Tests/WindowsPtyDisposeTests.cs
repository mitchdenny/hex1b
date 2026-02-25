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
}
