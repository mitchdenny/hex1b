namespace Hex1b.Tokens;

/// <summary>
/// Represents an unrecognized or unsupported escape sequence.
/// </summary>
/// <param name="Sequence">The raw sequence including the ESC character.</param>
/// <remarks>
/// <para>
/// This is a fallback for sequences that the tokenizer doesn't explicitly handle.
/// Filters can choose to pass these through unchanged or drop them.
/// </para>
/// <para>
/// Using this token type indicates the sequence wasn't parsed, so filters
/// cannot meaningfully transform it - only pass through or discard.
/// </para>
/// </remarks>
public sealed record UnrecognizedSequenceToken(string Sequence) : AnsiToken;
