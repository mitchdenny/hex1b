namespace Hex1b.Automation;

/// <summary>Contains either a command's input-sequence builder or an error that prevents playback.</summary>
/// <remarks>
/// The player prepares every command before executing any input. An accepted builder is snapshotted
/// during preparation; rejection errors are reported with the command's source span. Acceptance does not
/// indicate that the command has executed.
/// </remarks>
public sealed class TapeCommandResult
{
    private TapeCommandResult(Hex1bTerminalInputSequenceBuilder? sequenceBuilder, string? errorMessage)
    {
        SequenceBuilder = sequenceBuilder;
        ErrorMessage = errorMessage;
    }

    /// <summary>Gets the accepted sequence builder, or null if the command was rejected.</summary>
    public Hex1bTerminalInputSequenceBuilder? SequenceBuilder { get; }

    /// <summary>Gets the rejection message, or null if the command was accepted.</summary>
    public string? ErrorMessage { get; }

    /// <summary>Accepts a sequence builder for preparation without executing it.</summary>
    /// <param name="builder">The builder containing this command's deferred input steps.</param>
    /// <returns>An accepted command result.</returns>
    public static TapeCommandResult Accept(Hex1bTerminalInputSequenceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return new(builder, null);
    }

    /// <summary>Rejects a command with an error that prevents the entire tape from executing.</summary>
    /// <param name="message">The explanation to include in preparation diagnostics.</param>
    /// <returns>A rejected command result.</returns>
    public static TapeCommandResult Reject(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new(null, message);
    }

    internal string DiagnosticCode { get; init; } = "TAPE_COMMAND";
    internal TapeDiagnosticStage DiagnosticStage { get; init; } = TapeDiagnosticStage.Compilation;
    internal TapeCaptureControl CaptureControl { get; init; }
    internal string? GoldenPath { get; init; }
    internal bool SkipCommand { get; init; }

    internal static TapeCommandResult NoInput(TapeCaptureControl captureControl = TapeCaptureControl.None,
        string? goldenPath = null, bool skip = false) =>
        new(new Hex1bTerminalInputSequenceBuilder(), null)
        {
            CaptureControl = captureControl,
            GoldenPath = goldenPath,
            SkipCommand = skip
        };

    internal static TapeCommandResult Unsupported(string message) =>
        new(null, message) { DiagnosticCode = "TAPE_UNSUPPORTED" };

    internal static TapeCommandResult PreflightError(string message) =>
        new(null, message) { DiagnosticCode = "TAPE_PREFLIGHT", DiagnosticStage = TapeDiagnosticStage.Preflight };
}
