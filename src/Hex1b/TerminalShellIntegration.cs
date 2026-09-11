namespace Hex1b;

/// <summary>
/// Contains the current shell phase and latest reported command result.
/// </summary>
/// <remarks>
/// This is current state, not command history or the terminal process's lifecycle.
/// Markers can arrive out of order; missing markers are not synthesized.
/// </remarks>
public sealed record TerminalShellIntegration
{
    internal static TerminalShellIntegration Default { get; } =
        new(TerminalShellIntegrationPhase.Unknown, null);

    internal TerminalShellIntegration(TerminalShellIntegrationPhase phase, int? lastExitCode)
    {
        Phase = phase;
        LastExitCode = lastExitCode;
    }

    /// <summary>Gets the latest phase reported by the shell.</summary>
    public TerminalShellIntegrationPhase Phase { get; }

    /// <summary>
    /// Gets the latest exit code reported by marker D, or null when no result was reported.
    /// </summary>
    /// <remarks>
    /// Prompt, command-line, and execution markers retain this value. A completion marker
    /// with no exit code replaces the previous result with null, not success.
    /// </remarks>
    public int? LastExitCode { get; }
}
