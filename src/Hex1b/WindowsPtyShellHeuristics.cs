namespace Hex1b;

internal static class WindowsPtyShellHeuristics
{
    public static bool RequiresPromptWarmup(string fileName, IReadOnlyList<string> arguments)
    {
        var executable = Path.GetFileName(fileName);

        if (executable.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase) ||
            executable.Equals("cmd", StringComparison.OrdinalIgnoreCase))
        {
            if (HasArgument(arguments, "/k"))
            {
                return true;
            }

            return !HasArgument(arguments, "/c");
        }

        if (executable.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase) ||
            executable.Equals("powershell", StringComparison.OrdinalIgnoreCase) ||
            executable.Equals("pwsh.exe", StringComparison.OrdinalIgnoreCase) ||
            executable.Equals("pwsh", StringComparison.OrdinalIgnoreCase))
        {
            if (HasArgument(arguments, "-NoExit", "-noexit"))
            {
                return true;
            }

            if (HasArgument(arguments, "-Command", "-command", "-c", "/c", "-File", "-file", "-f", "-EncodedCommand", "-encodedcommand"))
            {
                return false;
            }

            return !HasArgument(arguments, "-NonInteractive", "-noninteractive");
        }

        return false;
    }

    private static bool HasArgument(IReadOnlyList<string> arguments, params string[] values)
    {
        foreach (var argument in arguments)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                continue;
            }

            foreach (var value in values)
            {
                if (argument.Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
