using System.Text;

namespace Hex1b.Automation;

/// <summary>
/// Exception thrown by <see cref="Hex1bTerminalAutomator"/> when an automation step fails.
/// Includes rich diagnostic information including the full step history, terminal snapshot,
/// and source location to help pinpoint the failure.
/// </summary>
public sealed class Hex1bAutomationException : Exception
{
    /// <summary>
    /// Gets the 1-based index of the step that failed.
    /// </summary>
    public int FailedStepIndex { get; }

    /// <summary>
    /// Gets the description of the step that failed (e.g., "WaitUntilText(\"File\")").
    /// </summary>
    public string FailedStepDescription { get; }

    /// <summary>
    /// Gets all steps that completed successfully before the failure.
    /// </summary>
    public IReadOnlyList<AutomationStepRecord> CompletedSteps { get; }

    /// <summary>
    /// Gets how long the failed step was executing before it failed.
    /// </summary>
    public TimeSpan FailedStepElapsed { get; }

    /// <summary>
    /// Gets the total elapsed time across all steps (completed + failed).
    /// </summary>
    public TimeSpan TotalElapsed { get; }

    /// <summary>
    /// Gets the terminal snapshot captured at the time of failure.
    /// This is taken from the inner <see cref="WaitUntilTimeoutException"/> when available,
    /// or captured directly from the terminal otherwise.
    /// </summary>
    public Hex1bTerminalSnapshot? TerminalSnapshot { get; }

    /// <summary>
    /// Gets the source file path where the failing automator method was called.
    /// </summary>
    public string? CallerFilePath { get; }

    /// <summary>
    /// Gets the line number where the failing automator method was called.
    /// </summary>
    public int? CallerLineNumber { get; }

    internal Hex1bAutomationException(
        int failedStepIndex,
        string failedStepDescription,
        IReadOnlyList<AutomationStepRecord> completedSteps,
        TimeSpan failedStepElapsed,
        Hex1bTerminalSnapshot? terminalSnapshot,
        string? callerFilePath,
        int? callerLineNumber,
        Exception innerException)
        : base(FormatMessage(
            failedStepIndex,
            failedStepDescription,
            completedSteps,
            failedStepElapsed,
            terminalSnapshot,
            callerFilePath,
            callerLineNumber,
            innerException), innerException)
    {
        FailedStepIndex = failedStepIndex;
        FailedStepDescription = failedStepDescription;
        CompletedSteps = completedSteps;
        FailedStepElapsed = failedStepElapsed;
        TerminalSnapshot = terminalSnapshot;
        CallerFilePath = callerFilePath;
        CallerLineNumber = callerLineNumber;

        var totalMs = failedStepElapsed.TotalMilliseconds;
        foreach (var step in completedSteps)
        {
            totalMs += step.Elapsed.TotalMilliseconds;
        }
        TotalElapsed = TimeSpan.FromMilliseconds(totalMs);
    }

    private static string FormatMessage(
        int failedStepIndex,
        string failedStepDescription,
        IReadOnlyList<AutomationStepRecord> completedSteps,
        TimeSpan failedStepElapsed,
        Hex1bTerminalSnapshot? terminalSnapshot,
        string? callerFilePath,
        int? callerLineNumber,
        Exception innerException)
    {
        var sb = new StringBuilder();

        // Header with step context
        sb.Append($"Step {failedStepIndex} of {failedStepIndex} failed — {failedStepDescription}");

        // Inner exception message (e.g., timeout details)
        if (innerException is WaitUntilTimeoutException waitEx)
        {
            sb.Append($"\n  Timed out after {waitEx.Timeout} waiting for: {waitEx.ConditionDescription}");
        }
        else
        {
            sb.Append($"\n  {innerException.GetType().Name}: {innerException.Message}");
        }

        // Source location
        if (callerFilePath is not null)
        {
            var fileName = Path.GetFileName(callerFilePath);
            sb.Append(callerLineNumber.HasValue
                ? $"\n  at {fileName}:{callerLineNumber}"
                : $"\n  at {fileName}");
        }

        // Completed steps breadcrumb
        if (completedSteps.Count > 0)
        {
            sb.Append($"\n\nCompleted steps ({completedSteps.Count} of {failedStepIndex}):");
            foreach (var step in completedSteps)
            {
                var elapsed = FormatElapsed(step.Elapsed);
                var location = FormatStepLocation(step);
                sb.Append($"\n  [{step.Index}] {step.Description} — {elapsed} ✓{location}");
            }
        }

        // Failed step
        sb.Append($"\n  [{failedStepIndex}] {failedStepDescription} — FAILED after {FormatElapsed(failedStepElapsed)}");

        // Total elapsed
        var totalMs = failedStepElapsed.TotalMilliseconds;
        foreach (var step in completedSteps)
        {
            totalMs += step.Elapsed.TotalMilliseconds;
        }
        sb.Append($"\n\nTotal elapsed: {FormatElapsed(TimeSpan.FromMilliseconds(totalMs))}");

        // Terminal snapshot
        if (terminalSnapshot is not null)
        {
            var screenMode = terminalSnapshot.InAlternateScreen ? "alternate screen" : "normal screen";
            sb.Append($"\n\nTerminal snapshot at failure ({terminalSnapshot.Width}x{terminalSnapshot.Height}, cursor at {terminalSnapshot.CursorX},{terminalSnapshot.CursorY}, {screenMode}):");
            sb.Append('\n');
            sb.Append(terminalSnapshot.GetText());
        }

        return sb.ToString();
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalMilliseconds < 1)
            return "0ms";
        if (elapsed.TotalSeconds < 10)
            return $"{elapsed.TotalMilliseconds:F0}ms";
        return elapsed.ToString(@"m\:ss\.fff");
    }

    private static string FormatStepLocation(AutomationStepRecord step)
    {
        if (step.CallerFilePath is null)
            return "";

        var fileName = Path.GetFileName(step.CallerFilePath);
        return step.CallerLineNumber.HasValue
            ? $" ({fileName}:{step.CallerLineNumber})"
            : $" ({fileName})";
    }
}
