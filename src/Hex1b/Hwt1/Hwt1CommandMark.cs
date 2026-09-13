namespace Hex1b;

/// <summary>
/// Wire projection of the most recently recorded <see cref="TerminalCommandMark"/>, atomic with
/// the rest of the frame. Unlike <see cref="TerminalCommandMark"/>'s server-side bounded history,
/// HWT1 does not transport a mark history or event log — only the latest one, mirroring how
/// <see cref="Hwt1ShellIntegration"/> exposes only the current phase. <c>rawParameters</c> is
/// sent verbatim; parsing into individual keys (e.g. <c>cmdline_url</c>) is left to the client.
/// </summary>
internal sealed record Hwt1CommandMark(string Phase, int? ExitCode, string? RawParameters)
{
    internal static Hwt1CommandMark? From(TerminalCommandMark? mark) => mark is null
        ? null
        : new(mark.Phase switch
        {
            TerminalShellIntegrationPhase.Unknown => "unknown",
            TerminalShellIntegrationPhase.Prompt => "prompt",
            TerminalShellIntegrationPhase.CommandLine => "commandLine",
            TerminalShellIntegrationPhase.Executing => "executing",
            TerminalShellIntegrationPhase.Finished => "finished",
            _ => throw new ArgumentOutOfRangeException(nameof(mark))
        }, mark.ExitCode, mark.RawParameters);
}
