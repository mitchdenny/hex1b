namespace Hex1b.Automation;

/// <summary>Identifies a range of Tape source text.</summary>
/// <param name="SourceName">The optional file name or caller-supplied source label.</param>
/// <param name="Offset">The zero-based UTF-16 offset.</param>
/// <param name="Length">The length in UTF-16 code units.</param>
/// <param name="Line">The one-based lexical line counter; as in upstream, newlines consumed inside literals do not increment it.</param>
/// <param name="Column">The one-based UTF-8 byte column, matching Tape's lexical convention.</param>
public readonly record struct TapeSourceSpan(string? SourceName, int Offset, int Length, int Line, int Column);
