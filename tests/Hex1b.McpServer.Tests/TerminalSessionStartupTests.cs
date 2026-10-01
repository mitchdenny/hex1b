using System.Text.Json;

namespace Hex1b.McpServer.Tests;

[TestClass]
public class TerminalSessionStartupTests
{
    private static (string Command, string[] Arguments) GetWorkingDirectoryCommand() =>
        OperatingSystem.IsWindows()
            ? (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
                ["/d", "/c", "cd"])
            : ("/bin/pwd", ["-P"]);

    [TestMethod]
    [DataRow(0, 24, "Width")]
    [DataRow(80, 0, "Height")]
    public async Task StartAsync_InvalidDimensions_RejectsBeforeLaunching(int width, int height, string dimension)
    {
        var directory = Directory.CreateDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), $"mcp-startup-{Guid.NewGuid():N}"));
        try
        {
            var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
                TerminalSession.StartAsync(
                    "invalid-dimensions", Path.Combine(directory.FullName, "missing-command"), [],
                    width: width, height: height));

            Assert.AreEqual($"{dimension} must be greater than zero.", error.Message);
        }
        finally
        {
            Directory.Delete(directory.FullName);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task StartAsync_InvalidWorkingDirectory_ThrowsAndReleasesRecording(bool regularFile)
    {
        var directory = Directory.CreateDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), $"mcp-startup-{Guid.NewGuid():N}"));
        var badCwd = Path.Combine(directory.FullName, "badcwd");
        var recording = Path.Combine(directory.FullName, "failed.cast");
        var (command, arguments) = GetWorkingDirectoryCommand();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        TerminalSession? session = null;
        try
        {
            if (regularFile)
                await File.WriteAllTextAsync(badCwd, "Not a directory", timeout.Token);
            else
            {
                Directory.CreateDirectory(badCwd);
                Directory.Delete(badCwd);
            }

            var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            {
                session = await TerminalSession.StartAsync(
                    "failed-startup", command, arguments,
                    workingDirectory: badCwd,
                    asciinemaFilePath: recording,
                    ct: timeout.Token);
            });

            AssertWorkingDirectoryStartupError(error, badCwd);
            Assert.IsNull(session, "A failed launch must not publish a session.");

            // The recorder opens lazily. Its disposal writes the final header even
            // when startup failed before any terminal output could be recorded.
            Assert.IsTrue(File.Exists(recording), "Failed startup must dispose its recorder.");
            await using var releasedFile = new FileStream(
                recording, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            using var header = await JsonDocument.ParseAsync(releasedFile, cancellationToken: timeout.Token);
            Assert.AreEqual(command, header.RootElement.GetProperty("command").GetString());
        }
        finally
        {
            if (session is not null)
                await session.DisposeAsync();
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [TestMethod]
    public async Task StartAsync_CanceledBeforeStartup_PreservesCancellationAndReleasesRecording()
    {
        var directory = Directory.CreateDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), $"mcp-startup-{Guid.NewGuid():N}"));
        var recording = Path.Combine(directory.FullName, "canceled.cast");
        var (command, arguments) = GetWorkingDirectoryCommand();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        TerminalSession? session = null;
        try
        {
            var error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            {
                session = await TerminalSession.StartAsync(
                    "canceled-startup", command, arguments,
                    workingDirectory: directory.FullName,
                    asciinemaFilePath: recording,
                    ct: canceled.Token);
            });

            Assert.AreEqual(canceled.Token, error.CancellationToken);
            Assert.IsNull(session);
            Assert.IsTrue(File.Exists(recording), "Cleanup must not use the canceled startup token.");
            await using var releasedFile = new FileStream(
                recording, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.IsGreaterThan(0L, releasedFile.Length);
        }
        finally
        {
            if (session is not null)
                await session.DisposeAsync();
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [TestMethod]
    public async Task StartAsync_StartupAndRecordingCleanupFail_PreservesBothErrors()
    {
        var directory = Directory.CreateDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), $"mcp-startup-{Guid.NewGuid():N}"));
        var badCwd = Path.Combine(directory.FullName, "missing-cwd");
        var recording = Path.Combine(directory.FullName, "missing-recording-directory", "failed.cast");
        var (command, arguments) = GetWorkingDirectoryCommand();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        TerminalSession? session = null;
        try
        {
            var error = await Assert.ThrowsExactlyAsync<AggregateException>(async () =>
            {
                session = await TerminalSession.StartAsync(
                    "failed-startup-and-cleanup", command, arguments,
                    workingDirectory: badCwd,
                    asciinemaFilePath: recording,
                    ct: timeout.Token);
            });

            Assert.HasCount(2, error.InnerExceptions);
            Assert.IsInstanceOfType<InvalidOperationException>(error.InnerExceptions[0]);
            AssertWorkingDirectoryStartupError(error.InnerExceptions[0], badCwd);
            Assert.IsInstanceOfType<DirectoryNotFoundException>(error.InnerExceptions[1]);
            Assert.Contains(recording, error.InnerExceptions[1].Message);
            Assert.IsNull(session);
        }
        finally
        {
            if (session is not null)
                await session.DisposeAsync();
            Directory.Delete(directory.FullName, recursive: true);
        }

    }

    private static void AssertWorkingDirectoryStartupError(Exception error, string workingDirectory)
    {
        if (OperatingSystem.IsWindows())
            Assert.Contains("The Windows PTY shim failed to start the child process", error.ToString());
        else
            Assert.Contains(workingDirectory, error.ToString());
    }

    [TestMethod]
    public async Task StartAsync_ValidWorkingDirectory_CapturesImmediateOutputAndRecording()
    {
        var directory = Directory.CreateDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), $"mcp-startup-{Guid.NewGuid():N}"));
        var recording = Path.Combine(directory.FullName, "success.cast");
        var (command, arguments) = GetWorkingDirectoryCommand();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await using (var session = await TerminalSession.StartAsync(
                "successful-startup", command, arguments,
                workingDirectory: directory.FullName,
                asciinemaFilePath: recording,
                width: 512,
                ct: timeout.Token))
            {
                Assert.IsTrue(await session.WaitForTextAsync(
                    directory.Name, TimeSpan.FromSeconds(10), timeout.Token), session.CaptureText());
                Assert.AreEqual(0, await session.WaitForExitAsync(timeout.Token));
            }

            await using var releasedFile = new FileStream(
                recording, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            using var reader = new StreamReader(releasedFile);
            using var header = JsonDocument.Parse((await reader.ReadLineAsync(timeout.Token))!);
            Assert.AreEqual(512, header.RootElement.GetProperty("width").GetInt32());
            Assert.Contains(directory.Name, await reader.ReadToEndAsync(timeout.Token));
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }
}
