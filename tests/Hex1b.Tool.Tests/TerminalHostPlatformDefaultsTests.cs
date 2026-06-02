using Hex1b.Tool.Hosting;

namespace Hex1b.Tool.Tests;

[TestClass]
public class TerminalHostPlatformDefaultsTests
{
    [TestMethod]
    public void GetDefaultCommandLine_ReturnsPlatformAppropriateShell()
    {
        var commandLine = TerminalHostPlatformDefaults.GetDefaultCommandLine();

        Assert.IsNotEmpty(commandLine);

        if (OperatingSystem.IsWindows())
        {
            Assert.AreEqual("powershell.exe", commandLine[0], ignoreCase: true);
            Assert.Contains("-NoLogo", commandLine, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("-NoProfile", commandLine, StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            Assert.AreEqual("/bin/bash", commandLine[0]);
            Assert.HasCount(1, commandLine);
        }
    }

    [TestMethod]
    public void TerminalHostConfig_DefaultsMatchPlatformShell()
    {
        var config = new TerminalHostConfig();
        var expected = TerminalHostPlatformDefaults.GetDefaultCommandLine();

        Assert.AreEqual(expected[0], config.Command);
        TestSeq.AreEqual(expected.Skip(1), config.Arguments);
    }

    [TestMethod]
    public void NormalizeCommandLine_AddsStablePowerShellArgs_WhenNoArgumentsProvided()
    {
        var normalized = TerminalHostPlatformDefaults.NormalizeCommandLine(["pwsh.exe"]);

        if (OperatingSystem.IsWindows())
        {
            TestSeq.AreEqual(["pwsh.exe", "-NoLogo", "-NoProfile"], normalized);
        }
        else
        {
            TestSeq.AreEqual(["pwsh.exe"], normalized);
        }
    }
}
