namespace Hex1b.Automation;

/// <summary>Represents an immutable Tape instruction, including application-defined instructions.</summary>
/// <param name="Span">The instruction's source range.</param>
public abstract record TapeCommand(TapeSourceSpan Span)
{
    internal Func<TapePlayContext, TapeCommandResult>? PrepareCallback { get; init; }
    internal bool IsIncludedOutput { get; init; }
}
