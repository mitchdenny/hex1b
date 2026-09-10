namespace Hex1b.Automation;

internal sealed record TapeShellConfiguration(string FileName, string[] Arguments,
    IReadOnlyDictionary<string, string> Defaults)
{
    internal static TapeShellConfiguration? ForName(string name) => name switch
    {
        "bash" => new("bash", ["--noprofile", "--norc", "--login", "+o", "history"],
            new Dictionary<string, string> { ["PS1"] = "> ", ["BASH_SILENCE_DEPRECATION_WARNING"] = "1" }),
        "zsh" => new("zsh", ["--histnostore", "--no-rcs"],
            new Dictionary<string, string> { ["PROMPT"] = "> " }),
        "fish" => new("fish", ["--login", "--no-config", "--private",
            "-C", "function fish_greeting; end", "-C", "function fish_prompt; echo -n '> '; end"],
            new Dictionary<string, string>()),
        "cmd" => new("cmd.exe", ["/k", "prompt=^> "], new Dictionary<string, string>()),
        "powershell" => new("powershell", ["-NoLogo", "-NoExit", "-NoProfile", "-Command",
            "Set-PSReadLineOption -HistorySaveStyle SaveNothing; function prompt { '> ' }"],
            new Dictionary<string, string>()),
        "pwsh" => new("pwsh", ["-Login", "-NoLogo", "-NoExit", "-NoProfile", "-Command",
            "Set-PSReadLineOption -HistorySaveStyle SaveNothing; function prompt { '> ' }"],
            new Dictionary<string, string>()),
        "nu" => new("nu", ["--execute", "$env.PROMPT_COMMAND = {'> '}; $env.PROMPT_COMMAND_RIGHT = {''}"],
            new Dictionary<string, string>()),
        "osh" => new("osh", ["--norc"], new Dictionary<string, string> { ["PS1"] = "> " }),
        "xonsh" => new("xonsh", ["--no-rc", "-D", "PROMPT=> "], new Dictionary<string, string>()),
        _ => null
    };

    internal static string? FindExecutable(string program, string workingDirectory,
        IReadOnlyDictionary<string, string> environment)
    {
        if (string.IsNullOrEmpty(program) || program.Contains('\0'))
            return null;
        var directories = program.Contains(Path.DirectorySeparatorChar) || program.Contains(Path.AltDirectorySeparatorChar)
            ? new[] { workingDirectory }
            : environment.TryGetValue("PATH", out var path)
                ? path.Split(Path.PathSeparator).Select(p => p.Length == 0 ? workingDirectory : Path.GetFullPath(p, workingDirectory))
                : [];
        var extensions = OperatingSystem.IsWindows() && Path.GetExtension(program).Length == 0
            ? (environment.TryGetValue("PATHEXT", out var pathExt) ? pathExt : ".COM;.EXE;.BAT;.CMD").Split(';')
            : [""];
        foreach (var directory in directories)
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.GetFullPath(Path.Combine(directory, program + extension), workingDirectory);
                if (!File.Exists(candidate))
                    continue;
                if (OperatingSystem.IsWindows() ||
                    (File.GetUnixFileMode(candidate) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0)
                    return candidate;
            }
        }
        return null;
    }
}
