namespace Hex1b.Automation;

/// <summary>Represents a custom keyword and its deferred playback-result callback.</summary>
/// <remarks>
/// Created from <see cref="TapeParserOptions.SyntaxExtensions"/> during parsing. The document exposes
/// <see cref="Keyword"/> and <see cref="TapeCommand.Span"/>; operand values are captured by the callback,
/// not exposed as typed properties. The callback produces its result during preparation, not parsing.
/// </remarks>
public sealed record TapeExtensionCommand : TapeCommand
{
    internal TapeExtensionCommand(string keyword, Func<TapePlayContext, TapeCommandResult> prepare,
        TapeSourceSpan span) : base(span)
    {
        Keyword = keyword;
        PrepareCallback = prepare;
    }

    /// <summary>Gets the case-sensitive keyword that produced this command.</summary>
    public string Keyword { get; }

}
