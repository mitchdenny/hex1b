using Hex1b.Tool.Hosting;

namespace Hex1b.Tool.Tests;

public class TerminalHostPlatformDefaultsTests
{
    [Fact]
    public void GetDefaultCommandLine_ReturnsPlatformAppropriateShell()
    {
        var commandLine = TerminalHostPlatformDefaults.GetDefaultCommandLine();

        Assert.NotEmpty(commandLine);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("powershell.exe", commandLine[0], ignoreCase: true);
            Assert.Contains("-NoLogo", commandLine, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("-NoProfile", commandLine, StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            Assert.Equal("/bin/bash", commandLine[0]);
            Assert.Single(commandLine);
        }
    }

    [Fact]
    public void TerminalHostConfig_DefaultsMatchPlatformShell()
    {
        var config = new TerminalHostConfig();
        var expected = TerminalHostPlatformDefaults.GetDefaultCommandLine();

        Assert.Equal(expected[0], config.Command);
        Assert.Equal(expected.Skip(1), config.Arguments);
    }

    [Fact]
    public void NormalizeCommandLine_AddsStablePowerShellArgs_WhenNoArgumentsProvided()
    {
        var normalized = TerminalHostPlatformDefaults.NormalizeCommandLine(["pwsh.exe"]);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(["pwsh.exe", "-NoLogo", "-NoProfile"], normalized);
        }
        else
        {
            Assert.Equal(["pwsh.exe"], normalized);
        }
    }
}
