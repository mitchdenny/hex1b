using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Hex1b.Tests;

[TestClass]
[TestCategory("Unix")]
public class UnixPtyStartupTests
{
    [TestMethod]
    [DataRow(false, "missing", 2)]
    [DataRow(true, "missing", 2)]
    [DataRow(false, "file", 20)]
    [DataRow(true, "file", 20)]
    [DataRow(false, "inaccessible", 13)]
    [DataRow(true, "inaccessible", 13)]
    [DataRow(false, "deleted-after-validation", 2)]
    [DataRow(true, "deleted-after-validation", 2)]
    public async Task StartAsync_UnavailableWorkingDirectory_FailsWithoutExecutingTarget(bool explicitArguments, string scenario, int expectedError)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Assert.Inconclusive("Requires a Unix PTY.");
            return;
        }
        if (scenario == "inaccessible" && GetEffectiveUserId() == 0)
            Assert.Inconclusive("Root can bypass directory search permissions.");

        var root = Directory.CreateTempSubdirectory("hex1b-startup-");
        var cwd = Path.Combine(root.FullName, "cwd");
        var target = Path.Combine(root.FullName, "controlled-target");
        var marker = Path.Combine(root.FullName, "target-started");
        try
        {
            await File.WriteAllTextAsync(target, "#!/bin/sh\nprintf started > \"$HEX1B_TEST_MARKER\"\npwd -P\n");
            File.SetUnixFileMode(target, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            if (scenario == "file")
                await File.WriteAllTextAsync(cwd, "not a directory");
            else if (scenario == "inaccessible")
            {
                Directory.CreateDirectory(cwd);
                File.SetUnixFileMode(cwd, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            else if (scenario == "deleted-after-validation")
            {
                Directory.CreateDirectory(cwd);
                Assert.IsTrue(Directory.Exists(cwd));
                Directory.Delete(cwd);
            }

            await using var process = new Hex1bTerminalChildProcess(target,
                explicitArguments ? ["argument"] : [], workingDirectory: cwd,
                environment: new() { ["HEX1B_TEST_MARKER"] = marker });
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var read = process.ReadOutputAsync(cancellation.Token).AsTask();
            var write = process.WriteInputAsync(Encoding.UTF8.GetBytes("ignored"), cancellation.Token).AsTask();

            var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => process.StartAsync(cancellation.Token));
            Assert.AreEqual(expectedError, TestSeq.IsType<Win32Exception>(error.InnerException).NativeErrorCode);
            Assert.Contains(cwd, error.Message);
            Assert.Contains("could not enter the working directory", error.Message);
            Assert.IsFalse(process.HasStarted);
            Assert.IsFalse(process.HasExited);
            Assert.AreEqual(-1, process.ProcessId);
            Assert.IsFalse(File.Exists(marker), "The target must never execute after failed chdir.");
            Assert.AreSame(error, await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => read));
            Assert.AreSame(error, await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => write));
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => process.WaitForExitAsync(cancellation.Token));
        }
        finally
        {
            if (scenario == "inaccessible" && Directory.Exists(cwd))
                File.SetUnixFileMode(cwd, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            root.Delete(recursive: true);
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task StartAsync_ValidWorkingDirectory_UsesAbsoluteOrRelativeUnicodePath(bool explicitArguments, bool relative)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            Assert.Inconclusive("Requires a Unix PTY.");
        var root = Directory.CreateTempSubdirectory("hex1b-startup-");
        var cwd = Directory.CreateDirectory(Path.Combine(root.FullName, "space \u03bb"));
        try
        {
            var requestedCwd = relative ? Path.GetRelativePath(Environment.CurrentDirectory, cwd.FullName) : cwd.FullName;
            await using var process = new Hex1bTerminalChildProcess("/bin/pwd",
                explicitArguments ? ["-P"] : [], workingDirectory: requestedCwd);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await process.StartAsync(cancellation.Token);
            var output = await ReadToEndAsync(process, cancellation.Token);
            Assert.AreEqual(0, await process.WaitForExitAsync(cancellation.Token));
            var expected = await ReadPhysicalDirectoryAsync(cwd.FullName, cancellation.Token);
            Assert.AreEqual(expected, output.Trim());
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task StartAsync_DefaultWorkingDirectory_PreservesCurrentDirectory(string? cwd)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            Assert.Inconclusive("Requires a Unix PTY.");
        await using var process = new Hex1bTerminalChildProcess("/bin/pwd", ["-P"], workingDirectory: cwd);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await process.StartAsync(cancellation.Token);
        var actual = (await ReadToEndAsync(process, cancellation.Token)).Trim();
        Assert.AreEqual(0, await process.WaitForExitAsync(cancellation.Token));
        Assert.AreEqual(await ReadPhysicalDirectoryAsync(Environment.CurrentDirectory, cancellation.Token), actual);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(125)]
    [DataRow(127)]
    public async Task StartAsync_RealTargetExit_IsNotAStartupFailure(int exitCode)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            Assert.Inconclusive("Requires a Unix PTY.");
        await using var process = new Hex1bTerminalChildProcess("/bin/sh", ["-c", "exit \"$1\"", "fixture", exitCode.ToString()]);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await process.StartAsync(cancellation.Token);
        Assert.IsTrue(process.HasStarted);
        await ReadToEndAsync(process, cancellation.Token);
        Assert.AreEqual(exitCode, await process.WaitForExitAsync(cancellation.Token));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task StartAsync_MissingExecutable_ReportsExecFailure(bool explicitArguments)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            Assert.Inconclusive("Requires a Unix PTY.");
        var missingExecutable = Path.Combine(Path.GetTempPath(), "hex1b-missing-target-" + Guid.NewGuid().ToString("N"));
        await using var process = new Hex1bTerminalChildProcess(missingExecutable, explicitArguments ? ["argument"] : []);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => process.StartAsync(cancellation.Token));
        Assert.Contains("could not execute the target", error.Message);
        Assert.AreEqual(2, TestSeq.IsType<Win32Exception>(error.InnerException).NativeErrorCode);
        Assert.IsFalse(process.HasStarted);
        Assert.AreEqual(-1, process.ProcessId);
    }

    [TestMethod]
    public async Task StartAsync_EmbeddedNulInWorkingDirectory_RejectsTruncation()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            Assert.Inconclusive("Requires a Unix PTY.");
        await using var process = new Hex1bTerminalChildProcess("/bin/pwd", ["-P"],
            workingDirectory: Environment.CurrentDirectory + "\0ignored");
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => process.StartAsync());
        Assert.IsFalse(process.HasStarted);
        Assert.AreEqual(-1, process.ProcessId);
    }

    [TestMethod]
    public async Task StartAsync_ConcurrentSuccessAndFailure_KeepLaunchesIndependent()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            Assert.Inconclusive("Requires a Unix PTY.");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await Task.WhenAll(Enumerable.Range(0, 12).Select(async index =>
        {
            if (index % 2 == 0)
            {
                await using var process = new Hex1bTerminalChildProcess("/bin/sh",
                    ["-c", "printf '%s' \"$HEX1B_MARKER\""],
                    environment: new() { ["HEX1B_MARKER"] = $"child-{index}" });
                await process.StartAsync(cancellation.Token);
                Assert.AreEqual($"child-{index}", await ReadToEndAsync(process, cancellation.Token));
                Assert.AreEqual(0, await process.WaitForExitAsync(cancellation.Token));
            }
            else
            {
                var missing = Path.Combine(Path.GetTempPath(), "hex1b-missing-cwd-" + Guid.NewGuid().ToString("N"));
                await using var process = new Hex1bTerminalChildProcess("/bin/pwd", ["-P"], workingDirectory: missing);
                await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => process.StartAsync(cancellation.Token));
                Assert.AreEqual(-1, process.ProcessId);
            }
        }));
    }

    [TestMethod]
    public async Task RunAsync_UnavailableWorkingDirectory_PropagatesUsefulError()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            Assert.Inconclusive("Requires a Unix PTY.");
        var missing = Path.Combine(Path.GetTempPath(), "hex1b-missing-cwd-" + Guid.NewGuid().ToString("N"));
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPtyProcess(options =>
            {
                options.FileName = "/bin/pwd";
                options.Arguments = ["-P"];
                options.WorkingDirectory = missing;
            })
            .WithHeadless().Build();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var error = await Assert.ThrowsAsync<Exception>(() => terminal.RunAsync(cancellation.Token));
        Assert.Contains(missing, error.ToString());
        Assert.Contains("could not enter the working directory", error.ToString());
    }

    private static async Task<string> ReadToEndAsync(Hex1bTerminalChildProcess process, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        while (true)
        {
            var bytes = await process.ReadOutputAsync(ct);
            ct.ThrowIfCancellationRequested();
            if (bytes.IsEmpty)
                return Encoding.UTF8.GetString(buffer.ToArray());
            buffer.Write(bytes.Span);
        }
    }

    private static async Task<string> ReadPhysicalDirectoryAsync(string directory, CancellationToken ct)
    {
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("/bin/pwd", "-P")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            WorkingDirectory = directory
        })!;
        try
        {
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            Assert.AreEqual(0, process.ExitCode);
            return output.Trim();
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill();
                await process.WaitForExitAsync();
            }
        }
    }

    [DllImport("libc", EntryPoint = "geteuid")]
    private static extern uint GetEffectiveUserId();
}
