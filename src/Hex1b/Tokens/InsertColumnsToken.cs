namespace Hex1b.Tokens;

/// <summary>
/// Represents DECIC or DECDC column editing.
/// </summary>
public sealed record InsertColumnsToken(int Count = 1) : AnsiToken;
