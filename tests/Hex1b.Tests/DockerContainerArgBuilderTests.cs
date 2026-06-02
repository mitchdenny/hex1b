namespace Hex1b.Tests;

[TestClass]
public class DockerContainerArgBuilderTests
{
    [TestMethod]
    public void BuildRunArgs_MinimalOptions_StartsWithRun()
    {
        var options = new DockerContainerOptions { Image = "ubuntu:24.04" };
        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test-ctr");

        Assert.AreEqual("run", args[0]);
    }

    [TestMethod]
    public void BuildRunArgs_MinimalOptions_IncludesInteractiveFlag()
    {
        var options = new DockerContainerOptions { Image = "ubuntu:24.04" };
        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test-ctr");

        Assert.Contains("-it", args);
    }

    [TestMethod]
    public void BuildRunArgs_AutoRemoveTrue_IncludesRmFlag()
    {
        var options = new DockerContainerOptions { AutoRemove = true };
        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test-ctr");

        Assert.Contains("--rm", args);
    }

    [TestMethod]
    public void BuildRunArgs_AutoRemoveFalse_OmitsRmFlag()
    {
        var options = new DockerContainerOptions { AutoRemove = false };
        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test-ctr");

        Assert.DoesNotContain("--rm", args);
    }

    [TestMethod]
    public void BuildRunArgs_IncludesContainerName()
    {
        var options = new DockerContainerOptions();
        var args = DockerContainerArgBuilder.BuildRunArgs(options, "my-container");

        var nameIndex = Array.IndexOf(args, "--name");
        Assert.IsTrue(nameIndex >= 0, "Expected --name flag");
        Assert.AreEqual("my-container", args[nameIndex + 1]);
    }

    [TestMethod]
    public void BuildRunArgs_WithEnvironment_AddsEnvFlags()
    {
        var options = new DockerContainerOptions();
        options.Environment["FOO"] = "bar";
        options.Environment["BAZ"] = "qux";
        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test");

        Assert.Contains("-e", args);
        Assert.Contains("FOO=bar", args);
        Assert.Contains("BAZ=qux", args);
    }

    [TestMethod]
    public void BuildRunArgs_WithEnvironment_EachVarHasOwnFlag()
    {
        var options = new DockerContainerOptions();
        options.Environment["A"] = "1";
        options.Environment["B"] = "2";
        options.Environment["C"] = "3";
        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test");

        var envFlagCount = args.Count(a => a == "-e");
        Assert.AreEqual(3, envFlagCount);
    }

    [TestMethod]
    public void BuildRunArgs_WithVolumes_AddsVolumeFlags()
    {
        var options = new DockerContainerOptions();
        options.Volumes.Add("/host/path:/container/path");
        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test");

        Assert.Contains("-v", args);
        Assert.Contains("/host/path:/container/path", args);
    }

    [TestMethod]
    public void BuildRunArgs_WithMultipleVolumes_EachHasOwnFlag()
    {
        var options = new DockerContainerOptions();
        options.Volumes.Add("/a:/a");
        options.Volumes.Add("/b:/b");
        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test");

        var volumeFlagCount = args.Count(a => a == "-v");
        Assert.AreEqual(2, volumeFlagCount);
    }

    [TestMethod]
    public void BuildRunArgs_MountDockerSocket_AddsSocketVolume()
    {
        var options = new DockerContainerOptions { MountDockerSocket = true };
        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test");

        Assert.Contains("/var/run/docker.sock:/var/run/docker.sock", args);
    }

    [TestMethod]
    public void BuildRunArgs_MountDockerSocketFalse_NoSocketVolume()
    {
        var options = new DockerContainerOptions { MountDockerSocket = false };
        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test");

        Assert.DoesNotContain("/var/run/docker.sock:/var/run/docker.sock", args);
    }

    [TestMethod]
    public void BuildRunArgs_MountDockerSocket_WithExistingVolumes_IncludesBoth()
    {
        var options = new DockerContainerOptions { MountDockerSocket = true };
        options.Volumes.Add("/host:/container");
        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test");

        Assert.Contains("/host:/container", args);
        Assert.Contains("/var/run/docker.sock:/var/run/docker.sock", args);
    }

    [TestMethod]
    public void BuildRunArgs_WithWorkingDirectory_AddsWorkdirFlag()
    {
        var options = new DockerContainerOptions { WorkingDirectory = "/app" };
        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test");

        var wIndex = Array.IndexOf(args, "-w");
        Assert.IsTrue(wIndex >= 0, "Expected -w flag");
        Assert.AreEqual("/app", args[wIndex + 1]);
    }

    [TestMethod]
    public void BuildRunArgs_NoWorkingDirectory_OmitsWorkdirFlag()
    {
        var options = new DockerContainerOptions();
        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test");

        Assert.DoesNotContain("-w", args);
    }

    [TestMethod]
    public void BuildRunArgs_WithNetwork_AddsNetworkFlag()
    {
        var options = new DockerContainerOptions { Network = "host" };
        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test");

        var netIndex = Array.IndexOf(args, "--network");
        Assert.IsTrue(netIndex >= 0, "Expected --network flag");
        Assert.AreEqual("host", args[netIndex + 1]);
    }

    [TestMethod]
    public void BuildRunArgs_NoNetwork_OmitsNetworkFlag()
    {
        var options = new DockerContainerOptions();
        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test");

        Assert.DoesNotContain("--network", args);
    }

    [TestMethod]
    public void BuildRunArgs_ImageIsAfterFlags()
    {
        var options = new DockerContainerOptions
        {
            Image = "alpine:3.21",
            WorkingDirectory = "/app",
            Network = "bridge"
        };
        options.Environment["X"] = "Y";
        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test");

        var imageIndex = Array.IndexOf(args, "alpine:3.21");
        var nameIndex = Array.IndexOf(args, "--name");
        var networkIndex = Array.IndexOf(args, "--network");

        Assert.IsTrue(imageIndex > nameIndex, "Image should come after --name");
        Assert.IsTrue(imageIndex > networkIndex, "Image should come after --network");
    }

    [TestMethod]
    public void BuildRunArgs_ShellFollowsImage()
    {
        var options = new DockerContainerOptions { Image = "ubuntu:24.04" };
        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test");

        var imageIndex = Array.IndexOf(args, "ubuntu:24.04");
        var shellIndex = Array.IndexOf(args, "/bin/bash");

        Assert.AreEqual(imageIndex + 1, shellIndex);
    }

    [TestMethod]
    public void BuildRunArgs_ShellArgsFollowShell()
    {
        var options = new DockerContainerOptions
        {
            Image = "ubuntu:24.04",
            Shell = "/bin/bash",
            ShellArgs = ["--norc", "--noprofile"]
        };
        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test");

        var shellIndex = Array.IndexOf(args, "/bin/bash");
        Assert.AreEqual("--norc", args[shellIndex + 1]);
        Assert.AreEqual("--noprofile", args[shellIndex + 2]);
    }

    [TestMethod]
    public void BuildRunArgs_ShellArgsAreLastElements()
    {
        var options = new DockerContainerOptions
        {
            Shell = "/bin/sh",
            ShellArgs = ["-i"]
        };
        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test");

        Assert.AreEqual("-i", args[^1]);
        Assert.AreEqual("/bin/sh", args[^2]);
    }

    [TestMethod]
    public void BuildRunArgs_EmptyShellArgs_ShellIsLast()
    {
        var options = new DockerContainerOptions { ShellArgs = [] };
        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test");

        Assert.AreEqual(options.Shell, args[^1]);
    }

    [TestMethod]
    public void BuildRunArgs_CustomShell_UsesCustomShell()
    {
        var options = new DockerContainerOptions
        {
            Shell = "/bin/zsh",
            ShellArgs = ["-l"]
        };
        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test");

        Assert.Contains("/bin/zsh", args);
        Assert.DoesNotContain("/bin/bash", args);
    }

    [TestMethod]
    public void BuildRunArgs_AllOptions_ProducesValidArgs()
    {
        var options = new DockerContainerOptions
        {
            Image = "alpine:3.21",
            WorkingDirectory = "/workspace",
            Network = "bridge",
            MountDockerSocket = true,
            AutoRemove = true,
            Shell = "/bin/sh",
            ShellArgs = ["-l"]
        };
        options.Environment["A"] = "1";
        options.Environment["B"] = "2";
        options.Volumes.Add("/tmp:/tmp");
        options.Volumes.Add("/data:/data:ro");

        var args = DockerContainerArgBuilder.BuildRunArgs(options, "full-test");

        Assert.AreEqual("run", args[0]);
        Assert.Contains("-it", args);
        Assert.Contains("--rm", args);
        Assert.Contains("--name", args);
        Assert.Contains("full-test", args);
        Assert.Contains("-e", args);
        Assert.Contains("A=1", args);
        Assert.Contains("B=2", args);
        Assert.Contains("-v", args);
        Assert.Contains("/tmp:/tmp", args);
        Assert.Contains("/data:/data:ro", args);
        Assert.Contains("/var/run/docker.sock:/var/run/docker.sock", args);
        Assert.Contains("-w", args);
        Assert.Contains("/workspace", args);
        Assert.Contains("--network", args);
        Assert.Contains("bridge", args);
        Assert.Contains("alpine:3.21", args);
        Assert.Contains("/bin/sh", args);
        Assert.Contains("-l", args);
    }

    [TestMethod]
    public void BuildRunArgs_ManyEnvironmentVars_AllIncluded()
    {
        var options = new DockerContainerOptions();
        for (int i = 0; i < 10; i++)
            options.Environment[$"VAR_{i}"] = $"val_{i}";

        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test");
        var envCount = args.Count(a => a == "-e");
        Assert.AreEqual(10, envCount);

        for (int i = 0; i < 10; i++)
            Assert.Contains($"VAR_{i}=val_{i}", args);
    }

    [TestMethod]
    public void BuildRunArgs_EnvironmentWithSpecialChars_PreservedExactly()
    {
        var options = new DockerContainerOptions();
        options.Environment["PATH"] = "/usr/bin:/usr/local/bin";
        options.Environment["MSG"] = "hello world";

        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test");

        Assert.Contains("PATH=/usr/bin:/usr/local/bin", args);
        Assert.Contains("MSG=hello world", args);
    }

    // --- BuildBuildArgs tests ---

    [TestMethod]
    public void BuildBuildArgs_StartsWithBuild()
    {
        var options = new DockerContainerOptions
        {
            DockerfilePath = "./Dockerfile",
            BuildContext = "."
        };
        var (args, _) = DockerContainerArgBuilder.BuildBuildArgs(options, "abc123");

        Assert.AreEqual("build", args[0]);
    }

    [TestMethod]
    public void BuildBuildArgs_IncludesDockerfilePath()
    {
        var options = new DockerContainerOptions
        {
            DockerfilePath = "./test/Dockerfile",
            BuildContext = "."
        };
        var (args, _) = DockerContainerArgBuilder.BuildBuildArgs(options, "abc");

        var fIndex = Array.IndexOf(args, "-f");
        Assert.IsTrue(fIndex >= 0, "Expected -f flag");
        Assert.AreEqual("./test/Dockerfile", args[fIndex + 1]);
    }

    [TestMethod]
    public void BuildBuildArgs_IncludesTag()
    {
        var options = new DockerContainerOptions
        {
            DockerfilePath = "./Dockerfile",
            BuildContext = "."
        };
        var (args, imageTag) = DockerContainerArgBuilder.BuildBuildArgs(options, "abc123");

        Assert.AreEqual("hex1b-test-abc123", imageTag);
        Assert.Contains("-t", args);
        Assert.Contains("hex1b-test-abc123", args);
    }

    [TestMethod]
    public void BuildBuildArgs_IncludesBuildContext()
    {
        var options = new DockerContainerOptions
        {
            DockerfilePath = "./Dockerfile",
            BuildContext = "./my-context"
        };
        var (args, _) = DockerContainerArgBuilder.BuildBuildArgs(options, "abc");

        Assert.AreEqual("./my-context", args[^1]);
    }

    [TestMethod]
    public void BuildBuildArgs_NullBuildContext_UsesDockerfileDirectory()
    {
        var options = new DockerContainerOptions
        {
            DockerfilePath = "/some/path/Dockerfile",
            BuildContext = null
        };
        var (args, _) = DockerContainerArgBuilder.BuildBuildArgs(options, "abc");

        Assert.AreEqual(Path.GetDirectoryName(options.DockerfilePath), args[^1]);
    }

    [TestMethod]
    public void BuildBuildArgs_WithBuildArgs_AddsBuildArgFlags()
    {
        var options = new DockerContainerOptions
        {
            DockerfilePath = "./Dockerfile",
            BuildContext = "."
        };
        options.BuildArgs["SDK_VERSION"] = "10.0";
        options.BuildArgs["VARIANT"] = "alpine";

        var (args, _) = DockerContainerArgBuilder.BuildBuildArgs(options, "abc");

        Assert.Contains("--build-arg", args);
        Assert.Contains("SDK_VERSION=10.0", args);
        Assert.Contains("VARIANT=alpine", args);
    }

    [TestMethod]
    public void BuildBuildArgs_WithMultipleBuildArgs_EachHasOwnFlag()
    {
        var options = new DockerContainerOptions
        {
            DockerfilePath = "./Dockerfile",
            BuildContext = "."
        };
        options.BuildArgs["A"] = "1";
        options.BuildArgs["B"] = "2";
        options.BuildArgs["C"] = "3";

        var (args, _) = DockerContainerArgBuilder.BuildBuildArgs(options, "abc");

        var flagCount = args.Count(a => a == "--build-arg");
        Assert.AreEqual(3, flagCount);
    }

    [TestMethod]
    public void BuildBuildArgs_NoBuildArgs_OmitsBuildArgFlag()
    {
        var options = new DockerContainerOptions
        {
            DockerfilePath = "./Dockerfile",
            BuildContext = "."
        };
        var (args, _) = DockerContainerArgBuilder.BuildBuildArgs(options, "abc");

        Assert.DoesNotContain("--build-arg", args);
    }

    [TestMethod]
    public void BuildBuildArgs_BuildContextIsLastArg()
    {
        var options = new DockerContainerOptions
        {
            DockerfilePath = "./Dockerfile",
            BuildContext = "/context"
        };
        options.BuildArgs["X"] = "Y";

        var (args, _) = DockerContainerArgBuilder.BuildBuildArgs(options, "abc");

        Assert.AreEqual("/context", args[^1]);
    }

    // --- Hash computation tests ---

    [TestMethod]
    public void ComputeDockerfileHash_SameContent_SameHash()
    {
        var hash1 = DockerContainerArgBuilder.ComputeDockerfileHash("FROM ubuntu:24.04\nRUN apt-get update");
        var hash2 = DockerContainerArgBuilder.ComputeDockerfileHash("FROM ubuntu:24.04\nRUN apt-get update");
        Assert.AreEqual(hash1, hash2);
    }

    [TestMethod]
    public void ComputeDockerfileHash_DifferentContent_DifferentHash()
    {
        var hash1 = DockerContainerArgBuilder.ComputeDockerfileHash("FROM ubuntu:24.04");
        var hash2 = DockerContainerArgBuilder.ComputeDockerfileHash("FROM alpine:3.21");
        Assert.AreNotEqual(hash1, hash2);
    }

    [TestMethod]
    public void ComputeDockerfileHash_Returns12Chars()
    {
        var hash = DockerContainerArgBuilder.ComputeDockerfileHash("FROM ubuntu:24.04");
        Assert.AreEqual(12, hash.Length);
    }

    [TestMethod]
    public void ComputeDockerfileHash_ReturnsLowercaseHex()
    {
        var hash = DockerContainerArgBuilder.ComputeDockerfileHash("FROM ubuntu:24.04");
        TestSeq.Matches("^[0-9a-f]{12}$", hash);
    }

    [TestMethod]
    public void ComputeDockerfileHash_EmptyContent_DoesNotThrow()
    {
        var hash = DockerContainerArgBuilder.ComputeDockerfileHash("");
        Assert.AreEqual(12, hash.Length);
    }

    // --- Container name generation tests ---

    [TestMethod]
    public void GenerateContainerName_StartsWithPrefix()
    {
        var name = DockerContainerArgBuilder.GenerateContainerName();
        Assert.StartsWith("hex1b-test-", name);
    }

    [TestMethod]
    public void GenerateContainerName_HasFixedLength()
    {
        var name = DockerContainerArgBuilder.GenerateContainerName();
        Assert.AreEqual(32, name.Length);
    }

    [TestMethod]
    public void GenerateContainerName_IsUnique()
    {
        var names = Enumerable.Range(0, 100)
            .Select(_ => DockerContainerArgBuilder.GenerateContainerName())
            .ToHashSet();

        Assert.AreEqual(100, names.Count);
    }

    // --- AdditionalRunArgs tests ---

    [TestMethod]
    public void BuildRunArgs_WithAdditionalRunArgs_IncludesArgs()
    {
        var options = new DockerContainerOptions();
        options.AdditionalRunArgs.Add("--privileged");
        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test");

        Assert.Contains("--privileged", args);
    }

    [TestMethod]
    public void BuildRunArgs_WithMultipleAdditionalRunArgs_IncludesAll()
    {
        var options = new DockerContainerOptions();
        options.AdditionalRunArgs.AddRange(["--privileged", "--cgroupns=host"]);
        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test");

        Assert.Contains("--privileged", args);
        Assert.Contains("--cgroupns=host", args);
    }

    [TestMethod]
    public void BuildRunArgs_AdditionalRunArgs_BeforeImage()
    {
        var options = new DockerContainerOptions { Image = "alpine:3.21" };
        options.AdditionalRunArgs.Add("--privileged");
        var args = DockerContainerArgBuilder.BuildRunArgs(options, "test");

        var privilegedIndex = Array.IndexOf(args, "--privileged");
        var imageIndex = Array.IndexOf(args, "alpine:3.21");
        Assert.IsTrue(privilegedIndex < imageIndex, "Additional args should come before the image");
    }

    [TestMethod]
    public void BuildRunArgs_EmptyAdditionalRunArgs_NoExtraArgs()
    {
        var options = new DockerContainerOptions { Image = "ubuntu:24.04" };
        var argsWithout = DockerContainerArgBuilder.BuildRunArgs(options, "test");

        // Verify no stray args - count should match a minimal run
        var optionsWithEmpty = new DockerContainerOptions { Image = "ubuntu:24.04" };
        var argsWith = DockerContainerArgBuilder.BuildRunArgs(optionsWithEmpty, "test");

        Assert.AreEqual(argsWithout.Length, argsWith.Length);
    }

    [TestMethod]
    public void BuildRunArgs_AllOptions_IncludesAdditionalRunArgs()
    {
        var options = new DockerContainerOptions
        {
            Image = "alpine:3.21",
            WorkingDirectory = "/workspace",
            Network = "bridge",
            MountDockerSocket = true,
            AutoRemove = true,
            Shell = "/bin/sh",
            ShellArgs = ["-l"]
        };
        options.Environment["A"] = "1";
        options.Volumes.Add("/tmp:/tmp");
        options.AdditionalRunArgs.AddRange(["--privileged", "--cgroupns=host"]);

        var args = DockerContainerArgBuilder.BuildRunArgs(options, "full-test");

        Assert.Contains("--privileged", args);
        Assert.Contains("--cgroupns=host", args);
        Assert.Contains("alpine:3.21", args);
        Assert.Contains("/bin/sh", args);
    }
}
