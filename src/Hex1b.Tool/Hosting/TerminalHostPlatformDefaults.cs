namespace Hex1b.Tool.Hosting;

/// <summary>
/// Platform-specific defaults for hosted terminal sessions.
/// </summary>
internal static class TerminalHostPlatformDefaults
{
    /// <summary>
    /// Gets the default interactive shell command line for the current platform.
    /// </summary>
    public static string[] GetDefaultCommandLine()
    {
        if (OperatingSystem.IsWindows())
        {
            return ["powershell.exe", "-NoLogo", "-NoProfile"];
        }

        return ["/bin/bash"];
    }

    /// <summary>
    /// Normalizes platform-specific interactive shell invocations so hosted sessions start
    /// from a predictable baseline instead of loading arbitrary user shell profiles.
    /// </summary>
    public static string[] NormalizeCommandLine(string[] commandLine)
    {
        if (!OperatingSystem.IsWindows() || commandLine.Length == 0)
        {
            return commandLine;
        }

        var executable = Path.GetFileName(commandLine[0]);
        if (!executable.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase) &&
            !executable.Equals("powershell", StringComparison.OrdinalIgnoreCase) &&
            !executable.Equals("pwsh.exe", StringComparison.OrdinalIgnoreCase) &&
            !executable.Equals("pwsh", StringComparison.OrdinalIgnoreCase))
        {
            return commandLine;
        }

        if (commandLine.Length > 1)
        {
            return commandLine;
        }

        return [commandLine[0], "-NoLogo", "-NoProfile"];
    }
}
