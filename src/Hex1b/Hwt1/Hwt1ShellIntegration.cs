namespace Hex1b;

internal sealed record Hwt1ShellIntegration(string Phase, int? LastExitCode)
{
    internal static Hwt1ShellIntegration From(TerminalShellIntegration shell) => new(shell.Phase switch
    {
        TerminalShellIntegrationPhase.Unknown => "unknown",
        TerminalShellIntegrationPhase.Prompt => "prompt",
        TerminalShellIntegrationPhase.CommandLine => "commandLine",
        TerminalShellIntegrationPhase.Executing => "executing",
        TerminalShellIntegrationPhase.Finished => "finished",
        _ => throw new ArgumentOutOfRangeException(nameof(shell))
    }, shell.LastExitCode);
}
