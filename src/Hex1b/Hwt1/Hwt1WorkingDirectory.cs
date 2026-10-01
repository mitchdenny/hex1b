namespace Hex1b;

internal sealed record Hwt1WorkingDirectory(string? Uri, string? Host, string? Path)
{
    internal static Hwt1WorkingDirectory From(TerminalWorkingDirectory workingDirectory) =>
        new(workingDirectory.Uri, workingDirectory.Host, workingDirectory.Path);
}
