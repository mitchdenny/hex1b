namespace Hex1b;

/// <summary>
/// Describes the progress indicator reported by a terminal workload using OSC 9;4.
/// </summary>
public enum TerminalProgressState
{
    /// <summary>No progress indicator is displayed.</summary>
    None = 0,
    /// <summary>Normal determinate progress is displayed.</summary>
    Normal = 1,
    /// <summary>Determinate progress indicates an error.</summary>
    Error = 2,
    /// <summary>Activity is reported without a percentage.</summary>
    Indeterminate = 3,
    /// <summary>Determinate progress indicates a warning.</summary>
    Warning = 4
}
