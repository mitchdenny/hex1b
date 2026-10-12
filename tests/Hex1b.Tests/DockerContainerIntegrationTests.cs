using Hex1b.Automation;

namespace Hex1b.Tests;

/// <summary>
/// Condition attribute that skips a test when Docker with Linux containers is not available.
/// </summary>
public sealed class DockerAvailableConditionAttribute : ConditionBaseAttribute
{
    private static readonly string? s_skipReason = GetSkipReason();

    public DockerAvailableConditionAttribute()
        : base(ConditionMode.Include)
    {
        IgnoreMessage = s_skipReason;
    }

    public override bool IsConditionMet => s_skipReason is null;

    public override string GroupName => nameof(DockerAvailableConditionAttribute);

    private static string? GetSkipReason()
    {
        if (!TryRunDockerCommand(["version", "--format", "{{.Server.Version}}"], out _))
        {
            return "Docker is not available on this machine.";
        }

        if (!TryRunDockerCommand(["info", "--format", "{{.OSType}}"], out var osType))
        {
            return "Docker is installed but the daemon is not ready.";
        }

        osType = osType.Trim();
        if (!string.Equals(osType, "linux", StringComparison.OrdinalIgnoreCase))
        {
            return $"Docker daemon is configured for {osType} containers; these tests require Linux containers.";
        }

        return null;
    }

    private static bool TryRunDockerCommand(string[] args, out string output)
    {
        output = string.Empty;

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("docker", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) return false;

            if (!proc.WaitForExit(TimeSpan.FromSeconds(5)))
            {
                try
                {
                    proc.Kill(entireProcessTree: true);
                }
                catch
                {
                }

                return false;
            }

            output = proc.StandardOutput.ReadToEnd();
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}

[TestClass]
public class DockerContainerIntegrationTests
{
    [TestMethod]
    [DockerAvailableCondition]
    public async Task WithDockerContainer_RunsShellInContainer()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(30));
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithHeadless()
            .WithDimensions(120, 24)
            .WithDockerContainer(c =>
            {
                c.Image = "ubuntu:24.04";
                c.Environment["PS1"] = "hex1b-docker> ";
                c.Shell = "/bin/bash";
                c.ShellArgs = ["--norc", "--noprofile", "-i"];
            })
            .Build();

        var runTask = Task.Run(() => terminal.RunAsync(cancellation.Token));
        try
        {
            using var snapshot = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("hex1b-docker>"), TimeSpan.FromSeconds(15), "container shell ready")
                .Type("uname -s").Enter()
                .WaitUntil(s => s.GetNonEmptyLines().Any(line => line.Trim() == "Linux"),
                    TimeSpan.FromSeconds(10), "uname output from container")
                .Build()
                .ApplyAsync(terminal, cancellation.Token);

            Assert.IsTrue(snapshot.GetNonEmptyLines().Any(line => line.Trim() == "Linux"));
            await new Hex1bTerminalInputSequenceBuilder()
                .Type("exit").Enter()
                .Build()
                .ApplyAsync(terminal, cancellation.Token);
            Assert.AreEqual(0, await runTask.WaitAsync(TimeSpan.FromSeconds(5), cancellation.Token));
        }
        finally
        {
            cancellation.Cancel();
            await Task.Run(async () => await terminal.DisposeAsync()).WaitAsync(TimeSpan.FromSeconds(10));
            try
            {
                await runTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (OperationCanceledException) when (runTask.IsCanceled && cancellation.IsCancellationRequested)
            {
            }
        }
    }

    [TestMethod]
    [DockerAvailableCondition]
    public async Task WithDockerContainer_EnvironmentVariablesPassedToContainer()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHeadless()
            .WithDimensions(120, 24)
            .WithDockerContainer(c =>
            {
                c.Image = "ubuntu:24.04";
                c.Environment["HEX1B_TEST_VAR"] = "hello_from_hex1b";
                c.Shell = "/bin/bash";
                c.ShellArgs = ["--norc", "--noprofile"];
            })
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);

        var envPattern = new CellPatternSearcher().Find("hello_from_hex1b");

        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromSeconds(3))
            .Type("echo $HEX1B_TEST_VAR").Enter()
            .WaitUntil(s => envPattern.Search(s).Count > 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var found = envPattern.Search(snapshot);
        Assert.IsTrue(found.Count > 0, "Expected environment variable value in output");

        await new Hex1bTerminalInputSequenceBuilder()
            .Type("exit").Enter()
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [TestMethod]
    [DockerAvailableCondition]
    public async Task WithDockerContainer_ContainerRemovedAfterDispose()
    {
        string containerName = $"hex1b-test-dispose-{Guid.NewGuid():N}"[..32];

        {
            await using var terminal = Hex1bTerminal.CreateBuilder()
                .WithHeadless()
                .WithDimensions(120, 24)
                .WithDockerContainer(c =>
                {
                    c.Image = "ubuntu:24.04";
                    c.Name = containerName;
                    c.Shell = "/bin/bash";
                    c.ShellArgs = ["--norc", "--noprofile"];
                })
                .Build();

            var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);

            await new Hex1bTerminalInputSequenceBuilder()
                .Wait(TimeSpan.FromSeconds(3))
                .Type("exit").Enter()
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);

            await runTask;
        }

        // After dispose, verify container is gone
        await Task.Delay(1000);

        var psi = new System.Diagnostics.ProcessStartInfo("docker", ["ps", "-a", "--filter", $"name={containerName}", "--format", "{{.Names}}"])
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = System.Diagnostics.Process.Start(psi)!;
        var output = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();

        Assert.DoesNotContain(containerName, output);
    }

    [TestMethod]
    [DockerAvailableCondition]
    public async Task WithDockerContainer_DefaultOptions_UsesDefaultImage()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHeadless()
            .WithDimensions(120, 24)
            .WithDockerContainer()
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromSeconds(5))
            .Type("dotnet --version").Enter()
            .Wait(TimeSpan.FromSeconds(3))
            .Type("exit").Enter()
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Default image is dotnet/sdk:10.0, so dotnet should be available
        var found = new CellPatternSearcher().Find("10.").Search(snapshot);
        Assert.IsTrue(found.Count > 0, "Expected dotnet version output in container");
    }
}
