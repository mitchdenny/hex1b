namespace Hex1b;

/// <summary>
/// Describes the current phase reported by a shell using OSC 133 markers.
/// </summary>
public enum TerminalShellIntegrationPhase
{
    /// <summary>No shell phase has been reported; this does not imply an idle shell.</summary>
    Unknown = 0,
    /// <summary>The shell has started displaying its prompt (marker A).</summary>
    Prompt = 1,
    /// <summary>The shell is accepting command-line input (marker B).</summary>
    CommandLine = 2,
    /// <summary>The shell has reported command execution (marker C).</summary>
    Executing = 3,
    /// <summary>The shell has reported command completion (marker D).</summary>
    Finished = 4
}
