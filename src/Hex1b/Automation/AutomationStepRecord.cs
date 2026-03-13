namespace Hex1b.Automation;

/// <summary>
/// Immutable record of a completed automation step.
/// Used by <see cref="Hex1bTerminalAutomator"/> to track step history for diagnostic purposes.
/// </summary>
/// <param name="Index">1-based step number in the automation sequence.</param>
/// <param name="Description">Human-readable description of what the step did (e.g., "Key(Enter)", "WaitUntilText(\"File\")").</param>
/// <param name="Elapsed">How long this step took to execute.</param>
/// <param name="CallerFilePath">Source file path where the automator method was called.</param>
/// <param name="CallerLineNumber">Line number where the automator method was called.</param>
public sealed record AutomationStepRecord(
    int Index,
    string Description,
    TimeSpan Elapsed,
    string? CallerFilePath = null,
    int? CallerLineNumber = null)
{
    /// <summary>
    /// Formats this step record as a human-readable string for diagnostic output.
    /// </summary>
    public override string ToString()
    {
        var elapsed = Elapsed.TotalMilliseconds < 1
            ? "0ms"
            : $"{Elapsed.TotalMilliseconds:F0}ms";

        return $"[{Index}] {Description} — {elapsed}";
    }
}
