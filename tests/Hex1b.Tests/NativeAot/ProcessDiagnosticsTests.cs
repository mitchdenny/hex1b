using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hex1b.Tests.NativeAot;

[TestClass]
[TestCategory("Infrastructure")]
public sealed class ProcessDiagnosticsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Publish_MissingProject_FailsWithCommandExitCodeAndRetainedLogs()
    {
        var directory = ScenarioRunner.CreateArtifacts("Publish Failure " + Guid.NewGuid().ToString("N"));
        var log = Path.Combine(directory, "publish.log");
        var missing = Path.Combine(directory, "MissingScenario.csproj");
        var error = await Assert.ThrowsExactlyAsync<TestProcessException>(() => TestProcess.RunAsync("dotnet",
            ["publish", missing, "-c", "Release", "-p:PublishAot=true"], ScenarioRunner.RepositoryRoot,
            log, TimeSpan.FromSeconds(30), TestContext.CancellationToken));
        StringAssert.Contains(error.Message, missing);
        StringAssert.Contains(error.Message, "exit code: 1");
        StringAssert.Contains(error.Message, "MSB1009");
        StringAssert.Contains(error.Message, log);
        StringAssert.Contains(await File.ReadAllTextAsync(log, TestContext.CancellationToken), "MSB1009");
        TestContext.AddResultFile(log);
    }

    [TestMethod]
    public async Task Run_MissingExecutable_FailsWithActionableLaunchDiagnostics()
    {
        var directory = ScenarioRunner.CreateArtifacts("MissingPrerequisite-" + Guid.NewGuid().ToString("N"));
        var executable = Path.Combine(directory, "missing-dotnet");
        var log = Path.Combine(directory, "launch.log");
        var error = await Assert.ThrowsExactlyAsync<TestProcessException>(() => TestProcess.RunAsync(executable,
            ["publish"], ScenarioRunner.RepositoryRoot, log, TimeSpan.FromSeconds(30), TestContext.CancellationToken));
        Assert.IsNull(error.Outcome.ExitCode);
        Assert.AreEqual("launch/wait failed", error.Outcome.Status);
        StringAssert.Contains(error.Message, executable);
        StringAssert.Contains(error.Message, log);
        StringAssert.Contains(await File.ReadAllTextAsync(log, TestContext.CancellationToken), "launch/wait failed");
        TestContext.AddResultFile(log);
    }

    [TestMethod]
    public async Task Run_PreCancelled_FailsAndPreservesDiagnosticsWithoutLaunching()
    {
        var directory = ScenarioRunner.CreateArtifacts("Cancellation-" + Guid.NewGuid().ToString("N"));
        var log = Path.Combine(directory, "cancelled.log");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var error = await Assert.ThrowsExactlyAsync<TestProcessException>(() => TestProcess.RunAsync("dotnet",
            ["--version"], ScenarioRunner.RepositoryRoot, log, TimeSpan.FromSeconds(30), cancellation.Token));
        Assert.IsNull(error.Outcome.ExitCode);
        Assert.AreEqual("cancelled", error.Outcome.Status);
        StringAssert.Contains(await File.ReadAllTextAsync(log, TestContext.CancellationToken), "cancelled");
        TestContext.AddResultFile(log);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Run_StalledOwnedProcess_TerminatesAndRetainsOutput(bool cancel)
    {
        var directory = ScenarioRunner.CreateArtifacts("Stall-" + cancel + "-" + Guid.NewGuid().ToString("N"));
        var log = Path.Combine(directory, "stall.log");
        var executable = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe")
            : Path.Combine(Path.DirectorySeparatorChar.ToString(), "bin", "sh");
        string[] arguments = OperatingSystem.IsWindows()
            ? ["/d", "/c", "echo probe-ready & for /L %i in (1,1,2147483647) do @rem"]
            : ["-c", "printf 'probe-ready\\n'; while :; do :; done"];
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        if (cancel)
            cancellation.CancelAfter(TimeSpan.FromSeconds(2));
        var error = await Assert.ThrowsExactlyAsync<TestProcessException>(() => TestProcess.RunAsync(executable,
            arguments, ScenarioRunner.RepositoryRoot, log,
            cancel ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(2), cancellation.Token));
        Assert.IsNotNull(error.Outcome.ExitCode, "The owned process must have been terminated and reaped.");
        StringAssert.StartsWith(error.Outcome.Status, cancel ? "cancelled" : "timed out");
        StringAssert.Contains(error.Outcome.StandardOutput, "probe-ready");
        StringAssert.Contains(await File.ReadAllTextAsync(log, TestContext.CancellationToken), "probe-ready");
        TestContext.AddResultFile(log);
    }

    [TestMethod]
    public async Task Run_NonzeroExit_CapturesBothStreamsAndExitCode()
    {
        var directory = ScenarioRunner.CreateArtifacts("RuntimeFailure-" + Guid.NewGuid().ToString("N"));
        var log = Path.Combine(directory, "runtime.log");
        var executable = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe")
            : Path.Combine(Path.DirectorySeparatorChar.ToString(), "bin", "sh");
        string[] arguments = OperatingSystem.IsWindows()
            ? ["/d", "/c", "echo stdout-marker & echo stderr-marker 1>&2 & exit /b 7"]
            : ["-c", "printf 'stdout-marker\\n'; printf 'stderr-marker\\n' >&2; exit 7"];
        var error = await Assert.ThrowsExactlyAsync<TestProcessException>(() => TestProcess.RunAsync(executable,
            arguments, ScenarioRunner.RepositoryRoot, log, TimeSpan.FromSeconds(30), TestContext.CancellationToken));
        Assert.AreEqual(7, error.Outcome.ExitCode);
        StringAssert.Contains(error.Outcome.StandardOutput, "stdout-marker");
        StringAssert.Contains(error.Outcome.StandardError, "stderr-marker");
        var content = await File.ReadAllTextAsync(log, TestContext.CancellationToken);
        StringAssert.Contains(content, "stdout-marker");
        StringAssert.Contains(content, "stderr-marker");
        TestContext.AddResultFile(log);
    }
}
