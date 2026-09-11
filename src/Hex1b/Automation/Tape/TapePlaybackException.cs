namespace Hex1b.Automation;

/// <summary>Reports a failure after tape execution has begun.</summary>
public sealed class TapePlaybackException : Exception
{
    internal TapePlaybackException(TapeCommand? command, int count, string terminalText,
        IEnumerable<TapeArtifact> artifacts, Exception innerException)
        : base(command is null ? $"Tape playback failed: {innerException.Message}" :
            $"Tape playback failed at {command.Span.SourceName}:{command.Span.Line}:{command.Span.Column}: {innerException.Message}", innerException)
    {
        Command = command;
        CompletedCommandCount = count;
        TerminalText = terminalText;
        PartialArtifacts = Array.AsReadOnly(artifacts.ToArray());
    }

    /// <summary>Gets the source command whose execution failed, if known.</summary>
    public TapeCommand? Command { get; }

    /// <summary>Gets the number of commands completed before failure.</summary>
    public int CompletedCommandCount { get; }

    /// <summary>Gets text from the failure boundary without owning terminal resources.</summary>
    public string TerminalText { get; }

    /// <summary>Gets artifact paths that may contain partial recordings.</summary>
    public IReadOnlyList<TapeArtifact> PartialArtifacts { get; }
}
