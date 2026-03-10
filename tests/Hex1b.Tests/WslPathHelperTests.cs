namespace Hex1b.Tests;

public class WslPathHelperTests
{
    [Theory]
    [InlineData(@"C:\Users\me\code", "/mnt/c/Users/me/code")]
    [InlineData(@"D:\projects\app", "/mnt/d/projects/app")]
    [InlineData(@"c:\lower", "/mnt/c/lower")]
    [InlineData(@"E:\", "/mnt/e/")]
    public void ConvertToWslPath_WindowsAbsolutePath_ConvertsCorrectly(string input, string expected)
    {
        Assert.Equal(expected, WslPathHelper.ConvertToWslPath(input));
    }

    [Theory]
    [InlineData("/usr/bin", "/usr/bin")]
    [InlineData("relative/path", "relative/path")]
    [InlineData("./Dockerfile", "./Dockerfile")]
    [InlineData("", "")]
    public void ConvertToWslPath_NonWindowsPath_ReturnsUnchanged(string input, string expected)
    {
        Assert.Equal(expected, WslPathHelper.ConvertToWslPath(input));
    }

    [Theory]
    [InlineData(@"C:\Users\me\code:/workspace", "/mnt/c/Users/me/code:/workspace")]
    [InlineData(@"D:\data:/data:ro", "/mnt/d/data:/data:ro")]
    [InlineData(@"C:\tmp:/tmp:rw", "/mnt/c/tmp:/tmp:rw")]
    public void ConvertVolumeSpec_WindowsHostPath_ConvertsHostOnly(string input, string expected)
    {
        Assert.Equal(expected, WslPathHelper.ConvertVolumeSpec(input));
    }

    [Theory]
    [InlineData("/host/path:/container/path", "/host/path:/container/path")]
    [InlineData("/a:/b:ro", "/a:/b:ro")]
    public void ConvertVolumeSpec_LinuxPath_ReturnsUnchanged(string input, string expected)
    {
        Assert.Equal(expected, WslPathHelper.ConvertVolumeSpec(input));
    }

    [Fact]
    public void ConvertVolumeSpec_WindowsPathNoContainer_ConvertsWholeThing()
    {
        var result = WslPathHelper.ConvertVolumeSpec(@"C:\Users\me");
        Assert.Equal("/mnt/c/Users/me", result);
    }

    [Theory]
    [InlineData(@"C:\some\path", "/mnt/c/some/path")]
    [InlineData("./relative", "./relative")]
    [InlineData("ubuntu:24.04", "ubuntu:24.04")]
    [InlineData("--rm", "--rm")]
    public void ConvertArgIfPath_ConvertsOnlyWindowsPaths(string input, string expected)
    {
        Assert.Equal(expected, WslPathHelper.ConvertArgIfPath(input));
    }
}
