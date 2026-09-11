namespace Hex1b.Automation;

/// <summary>Represents a Ctrl, Alt, or Shift instruction and its ordered operands.</summary>
public sealed record TapeChordCommand : TapeCommand
{
    /// <summary>Creates a modifier instruction, copying the ordered operands.</summary>
    /// <param name="modifier">The leading modifier name.</param>
    /// <param name="parts">The remaining modifier and key names, in source order.</param>
    /// <param name="span">The instruction's source range.</param>
    public TapeChordCommand(string modifier, IEnumerable<string> parts, TapeSourceSpan span) : base(span)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        ArgumentNullException.ThrowIfNull(parts);
        Modifier = modifier;
        Parts = Array.AsReadOnly(parts.ToArray());
    }

    /// <summary>Gets the leading modifier name.</summary>
    public string Modifier { get; }

    /// <summary>Gets the ordered operands, excluding the leading modifier.</summary>
    public IReadOnlyList<string> Parts { get; }
}
