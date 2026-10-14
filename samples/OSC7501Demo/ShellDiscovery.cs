namespace OSC7501Demo;

internal sealed record ShellOption(string Name, string FileName, string[] Arguments);

internal static class ShellDiscovery
{
    internal static IReadOnlyList<ShellOption> Detect()
        => Detect((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator),
            OperatingSystem.IsWindows());

    internal static IReadOnlyList<ShellOption> Detect(IEnumerable<string> directories, bool windows)
    {
        var paths = directories.Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim('"')).ToArray();
        var candidates = new[]
        {
            new ShellOption("bash", windows ? "bash.exe" : "bash", ["-i"]),
            new ShellOption("zsh", windows ? "zsh.exe" : "zsh", ["-i"]),
            new ShellOption("cmd.exe", "cmd.exe", []),
            new ShellOption("PowerShell", windows ? "pwsh.exe" : "pwsh", ["-NoLogo"])
        };
        var result = new List<ShellOption>();
        foreach (var candidate in candidates)
        {
            if (!windows && candidate.Name == "cmd.exe")
                continue;
            foreach (var path in paths)
            {
                var fileName = Path.GetFullPath(Path.Combine(path, candidate.FileName));
                if (!File.Exists(fileName))
                    continue;
                if (!windows && !OperatingSystem.IsWindows() &&
                    (File.GetUnixFileMode(fileName) &
                        (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0)
                    continue;
                result.Add(candidate with { FileName = fileName });
                break;
            }
        }
        return result;
    }
}
