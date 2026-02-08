namespace Hex1b.Automation;

/// <summary>
/// Exception thrown when a <see cref="WaitUntilStep"/> times out waiting for a condition.
/// Includes rich diagnostic information about the terminal state at the time of timeout.
/// </summary>
public sealed class WaitUntilTimeoutException : TimeoutException
{
    /// <summary>
    /// Gets the timeout duration that was exceeded.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Gets the description of the condition being waited on.
    /// This is either the caller-provided description or the auto-captured predicate expression.
    /// </summary>
    public string ConditionDescription { get; }

    /// <summary>
    /// Gets the terminal snapshot captured at the time of timeout, or <c>null</c> if unavailable.
    /// </summary>
    public Hex1bTerminalSnapshot? TerminalSnapshot { get; }

    /// <summary>
    /// Gets the source file path where the <c>WaitUntil</c> call was made.
    /// </summary>
    public string? CallerFilePath { get; }

    /// <summary>
    /// Gets the line number where the <c>WaitUntil</c> call was made.
    /// </summary>
    public int? CallerLineNumber { get; }

    internal WaitUntilTimeoutException(
        TimeSpan timeout,
        string conditionDescription,
        Hex1bTerminalSnapshot? snapshot,
        string? callerFilePath,
        int? callerLineNumber)
        : base(FormatMessage(timeout, conditionDescription, snapshot, callerFilePath, callerLineNumber))
    {
        Timeout = timeout;
        ConditionDescription = conditionDescription;
        TerminalSnapshot = snapshot;
        CallerFilePath = callerFilePath;
        CallerLineNumber = callerLineNumber;
    }

    private static string FormatMessage(
        TimeSpan timeout,
        string conditionDescription,
        Hex1bTerminalSnapshot? snapshot,
        string? callerFilePath,
        int? callerLineNumber)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append($"WaitUntil timed out after {timeout} waiting for: {conditionDescription}");

        if (callerFilePath is not null)
        {
            var fileName = System.IO.Path.GetFileName(callerFilePath);
            builder.Append(callerLineNumber.HasValue
                ? $"\n  at {fileName}:{callerLineNumber}"
                : $"\n  at {fileName}");
        }

        if (snapshot is not null)
        {
            var screenMode = snapshot.InAlternateScreen ? "alternate screen" : "normal screen";
            builder.Append($"\nTerminal ({snapshot.Width}x{snapshot.Height}, cursor at {snapshot.CursorX},{snapshot.CursorY}, {screenMode}):");
            builder.Append('\n');
            builder.Append(snapshot.GetText());
        }

        return builder.ToString();
    }
}
