namespace Hex1b.Tests;

public class DockerContainerValidationTests
{
    [Fact]
    public void Validate_DefaultOptions_NoThrow()
    {
        var options = new DockerContainerOptions();
        DockerContainerArgBuilder.Validate(options);
    }

    [Fact]
    public void Validate_CustomImageOnly_NoThrow()
    {
        var options = new DockerContainerOptions { Image = "ubuntu:24.04" };
        DockerContainerArgBuilder.Validate(options);
    }

    [Fact]
    public void Validate_DockerfileOnly_NoThrow()
    {
        var options = new DockerContainerOptions
        {
            DockerfilePath = "./Dockerfile"
        };
        DockerContainerArgBuilder.Validate(options);
    }

    [Fact]
    public void Validate_DockerfileWithBuildContext_NoThrow()
    {
        var options = new DockerContainerOptions
        {
            DockerfilePath = "./Dockerfile",
            BuildContext = "./context"
        };
        DockerContainerArgBuilder.Validate(options);
    }

    [Fact]
    public void Validate_DockerfileWithBuildArgs_NoThrow()
    {
        var options = new DockerContainerOptions
        {
            DockerfilePath = "./Dockerfile"
        };
        options.BuildArgs["SDK"] = "10.0";
        DockerContainerArgBuilder.Validate(options);
    }

    [Fact]
    public void Validate_CustomImageAndDockerfile_Throws()
    {
        var options = new DockerContainerOptions
        {
            Image = "custom:latest",
            DockerfilePath = "./Dockerfile"
        };

        var ex = Assert.Throws<InvalidOperationException>(
            () => DockerContainerArgBuilder.Validate(options));

        Assert.Contains("Image", ex.Message);
        Assert.Contains("DockerfilePath", ex.Message);
    }

    [Fact]
    public void Validate_BuildContextWithoutDockerfile_Throws()
    {
        var options = new DockerContainerOptions
        {
            BuildContext = "/some/context"
        };

        var ex = Assert.Throws<InvalidOperationException>(
            () => DockerContainerArgBuilder.Validate(options));

        Assert.Contains("BuildContext", ex.Message);
        Assert.Contains("DockerfilePath", ex.Message);
    }

    [Fact]
    public void Validate_BuildArgsWithoutDockerfile_Throws()
    {
        var options = new DockerContainerOptions();
        options.BuildArgs["SDK"] = "10.0";

        var ex = Assert.Throws<InvalidOperationException>(
            () => DockerContainerArgBuilder.Validate(options));

        Assert.Contains("BuildArgs", ex.Message);
        Assert.Contains("DockerfilePath", ex.Message);
    }

    [Fact]
    public void Validate_NullOptions_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => DockerContainerArgBuilder.Validate(null!));
    }

    [Fact]
    public void Validate_DefaultImageWithDockerfile_NoThrow()
    {
        // Default image + Dockerfile is OK — Dockerfile overrides the default
        var options = new DockerContainerOptions
        {
            DockerfilePath = "./Dockerfile"
        };
        DockerContainerArgBuilder.Validate(options);
    }

    [Fact]
    public void Validate_AllContainerOptionsWithoutDockerfile_NoThrow()
    {
        // Setting all container-level options without Dockerfile is fine
        var options = new DockerContainerOptions
        {
            Image = "alpine:3.21",
            WorkingDirectory = "/app",
            Network = "host",
            MountDockerSocket = true,
            AutoRemove = false,
            Name = "my-test",
            Shell = "/bin/sh",
            ShellArgs = ["-l"]
        };
        options.Environment["X"] = "1";
        options.Volumes.Add("/tmp:/tmp");

        DockerContainerArgBuilder.Validate(options);
    }
}
