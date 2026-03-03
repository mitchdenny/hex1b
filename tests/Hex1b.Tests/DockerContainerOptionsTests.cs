namespace Hex1b.Tests;

public class DockerContainerOptionsTests
{
    [Fact]
    public void DefaultImage_IsDotnetSdk()
    {
        var options = new DockerContainerOptions();
        Assert.Equal("mcr.microsoft.com/dotnet/sdk:10.0", options.Image);
    }

    [Fact]
    public void DefaultShell_IsBashNorc()
    {
        var options = new DockerContainerOptions();
        Assert.Equal("/bin/bash", options.Shell);
        Assert.Equal(["--norc"], options.ShellArgs);
    }

    [Fact]
    public void AutoRemove_DefaultsTrue()
    {
        var options = new DockerContainerOptions();
        Assert.True(options.AutoRemove);
    }

    [Fact]
    public void MountDockerSocket_DefaultsFalse()
    {
        var options = new DockerContainerOptions();
        Assert.False(options.MountDockerSocket);
    }

    [Fact]
    public void DockerfilePath_DefaultsNull()
    {
        var options = new DockerContainerOptions();
        Assert.Null(options.DockerfilePath);
    }

    [Fact]
    public void BuildContext_DefaultsNull()
    {
        var options = new DockerContainerOptions();
        Assert.Null(options.BuildContext);
    }

    [Fact]
    public void Name_DefaultsNull()
    {
        var options = new DockerContainerOptions();
        Assert.Null(options.Name);
    }

    [Fact]
    public void WorkingDirectory_DefaultsNull()
    {
        var options = new DockerContainerOptions();
        Assert.Null(options.WorkingDirectory);
    }

    [Fact]
    public void Network_DefaultsNull()
    {
        var options = new DockerContainerOptions();
        Assert.Null(options.Network);
    }

    [Fact]
    public void Environment_InitiallyEmpty()
    {
        var options = new DockerContainerOptions();
        Assert.Empty(options.Environment);
    }

    [Fact]
    public void Volumes_InitiallyEmpty()
    {
        var options = new DockerContainerOptions();
        Assert.Empty(options.Volumes);
    }

    [Fact]
    public void BuildArgs_InitiallyEmpty()
    {
        var options = new DockerContainerOptions();
        Assert.Empty(options.BuildArgs);
    }

    [Fact]
    public void Environment_CanAddEntries()
    {
        var options = new DockerContainerOptions();
        options.Environment["FOO"] = "bar";
        options.Environment["BAZ"] = "qux";
        Assert.Equal(2, options.Environment.Count);
        Assert.Equal("bar", options.Environment["FOO"]);
    }

    [Fact]
    public void Volumes_CanAddEntries()
    {
        var options = new DockerContainerOptions();
        options.Volumes.Add("/host:/container");
        options.Volumes.Add("/tmp:/tmp:ro");
        Assert.Equal(2, options.Volumes.Count);
    }

    [Fact]
    public void BuildArgs_CanAddEntries()
    {
        var options = new DockerContainerOptions();
        options.BuildArgs["SDK_VERSION"] = "10.0";
        Assert.Single(options.BuildArgs);
    }

    [Fact]
    public void ShellArgs_CanBeOverridden()
    {
        var options = new DockerContainerOptions
        {
            ShellArgs = ["-l", "-i"]
        };
        Assert.Equal(["-l", "-i"], options.ShellArgs);
    }

    [Fact]
    public void ShellArgs_CanBeSetToEmpty()
    {
        var options = new DockerContainerOptions
        {
            ShellArgs = []
        };
        Assert.Empty(options.ShellArgs);
    }
}
