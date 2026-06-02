namespace Hex1b.Tests;

[TestClass]
public class DockerContainerOptionsTests
{
    [TestMethod]
    public void DefaultImage_IsDotnetSdk()
    {
        var options = new DockerContainerOptions();
        Assert.AreEqual(DockerContainerArgBuilder.DefaultImage, options.Image);
    }

    [TestMethod]
    public void DefaultShell_IsBashNorc()
    {
        var options = new DockerContainerOptions();
        Assert.AreEqual("/bin/bash", options.Shell);
        TestSeq.AreEqual(["--norc"], options.ShellArgs);
    }

    [TestMethod]
    public void AutoRemove_DefaultsTrue()
    {
        var options = new DockerContainerOptions();
        Assert.IsTrue(options.AutoRemove);
    }

    [TestMethod]
    public void MountDockerSocket_DefaultsFalse()
    {
        var options = new DockerContainerOptions();
        Assert.IsFalse(options.MountDockerSocket);
    }

    [TestMethod]
    public void DockerfilePath_DefaultsNull()
    {
        var options = new DockerContainerOptions();
        Assert.IsNull(options.DockerfilePath);
    }

    [TestMethod]
    public void BuildContext_DefaultsNull()
    {
        var options = new DockerContainerOptions();
        Assert.IsNull(options.BuildContext);
    }

    [TestMethod]
    public void Name_DefaultsNull()
    {
        var options = new DockerContainerOptions();
        Assert.IsNull(options.Name);
    }

    [TestMethod]
    public void WorkingDirectory_DefaultsNull()
    {
        var options = new DockerContainerOptions();
        Assert.IsNull(options.WorkingDirectory);
    }

    [TestMethod]
    public void Network_DefaultsNull()
    {
        var options = new DockerContainerOptions();
        Assert.IsNull(options.Network);
    }

    [TestMethod]
    public void Environment_InitiallyEmpty()
    {
        var options = new DockerContainerOptions();
        Assert.IsEmpty(options.Environment);
    }

    [TestMethod]
    public void Volumes_InitiallyEmpty()
    {
        var options = new DockerContainerOptions();
        Assert.IsEmpty(options.Volumes);
    }

    [TestMethod]
    public void BuildArgs_InitiallyEmpty()
    {
        var options = new DockerContainerOptions();
        Assert.IsEmpty(options.BuildArgs);
    }

    [TestMethod]
    public void Environment_CanAddEntries()
    {
        var options = new DockerContainerOptions();
        options.Environment["FOO"] = "bar";
        options.Environment["BAZ"] = "qux";
        Assert.AreEqual(2, options.Environment.Count);
        Assert.AreEqual("bar", options.Environment["FOO"]);
    }

    [TestMethod]
    public void Volumes_CanAddEntries()
    {
        var options = new DockerContainerOptions();
        options.Volumes.Add("/host:/container");
        options.Volumes.Add("/tmp:/tmp:ro");
        Assert.AreEqual(2, options.Volumes.Count);
    }

    [TestMethod]
    public void BuildArgs_CanAddEntries()
    {
        var options = new DockerContainerOptions();
        options.BuildArgs["SDK_VERSION"] = "10.0";
        TestSeq.Single(options.BuildArgs);
    }

    [TestMethod]
    public void ShellArgs_CanBeOverridden()
    {
        var options = new DockerContainerOptions
        {
            ShellArgs = ["-l", "-i"]
        };
        TestSeq.AreEqual(["-l", "-i"], options.ShellArgs);
    }

    [TestMethod]
    public void ShellArgs_CanBeSetToEmpty()
    {
        var options = new DockerContainerOptions
        {
            ShellArgs = []
        };
        Assert.IsEmpty(options.ShellArgs);
    }
}
