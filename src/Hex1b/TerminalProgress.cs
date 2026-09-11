namespace Hex1b;

/// <summary>
/// Contains the current progress indicator reported by a terminal workload.
/// </summary>
/// <remarks>
/// Progress is independent of shell integration and terminal-process lifetime.
/// No progress is inferred from output, and process exit does not clear it.
/// </remarks>
public sealed record TerminalProgress
{
    internal static TerminalProgress Default { get; } = new(TerminalProgressState.None, null);

    internal TerminalProgress(TerminalProgressState state, int? percentage)
    {
        State = state;
        Percentage = percentage;
    }

    /// <summary>Gets the reported progress indicator state.</summary>
    public TerminalProgressState State { get; }

    /// <summary>
    /// Gets the reported percentage from 0 to 100, or null for hidden or indeterminate progress.
    /// </summary>
    public int? Percentage { get; }
}
