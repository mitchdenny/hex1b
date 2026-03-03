using Hex1b.Automation;

namespace Hex1b.Tests;

/// <summary>
/// Custom xUnit Fact attribute that skips the test when Docker is not available.
/// </summary>
public sealed class DockerAvailableFactAttribute : FactAttribute
{
    private static readonly bool s_dockerAvailable = CheckDockerAvailable();

    public DockerAvailableFactAttribute(
        [System.Runtime.CompilerServices.CallerFilePath] string? sourceFilePath = null,
        [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!s_dockerAvailable)
        {
            Skip = "Docker is not available on this machine.";
        }
    }

    private static bool CheckDockerAvailable()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("docker", ["version", "--format", "{{.Server.Version}}"])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) return false;

            proc.WaitForExit(TimeSpan.FromSeconds(5));
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}

public class DockerContainerIntegrationTests
{
    [DockerAvailableFact]
    public async Task WithDockerContainer_RunsShellInContainer()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHeadless()
            .WithDimensions(120, 24)
            .WithDockerContainer(c =>
            {
                c.Image = "ubuntu:24.04";
                c.Shell = "/bin/bash";
                c.ShellArgs = ["--norc", "--noprofile"];
            })
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);

        var linuxPattern = new CellPatternSearcher().Find("Linux");

        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromSeconds(3))
            .Type("uname -s").Enter()
            .WaitUntil(s => linuxPattern.Search(s).Count > 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Verify pattern was found (WaitUntil would have thrown on timeout)
        var snapshot = terminal.CreateSnapshot();
        var found = linuxPattern.Search(snapshot);
        Assert.True(found.Count > 0, "Expected 'Linux' in terminal output");

        await new Hex1bTerminalInputSequenceBuilder()
            .Type("exit").Enter()
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [DockerAvailableFact]
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
        Assert.True(found.Count > 0, "Expected environment variable value in output");

        await new Hex1bTerminalInputSequenceBuilder()
            .Type("exit").Enter()
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [DockerAvailableFact]
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

    [DockerAvailableFact]
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
        Assert.True(found.Count > 0, "Expected dotnet version output in container");
    }
}
