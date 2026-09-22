namespace Hex1b.Tokens;

/// <summary>
/// Represents DECDC column deletion.
/// </summary>
public sealed record DeleteColumnsToken(int Count = 1) : AnsiToken;
