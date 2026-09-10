namespace Hex1b.Automation;

/// <summary>Provides token access, source information, and diagnostic reporting to a syntax extension.</summary>
public sealed class TapeParseContext
{
    private readonly List<TapeDiagnostic> _diagnostics;
    private readonly TapeToken _keyword;
    private readonly Func<TapeToken, TapeCommand?> _parseBuiltIn;

    internal TapeParseContext(TapeTokenReader reader, TapeToken keyword, List<TapeDiagnostic> diagnostics,
        Func<TapeToken, TapeCommand?> parseBuiltIn)
    {
        Reader = reader;
        _keyword = keyword;
        CommandSpan = keyword.Span;
        _diagnostics = diagnostics;
        _parseBuiltIn = parseBuiltIn;
    }

    /// <summary>Gets the token reader positioned after the extension keyword.</summary>
    /// <remarks>
    /// Consume only this command's operands. The reader is shared with the parser and advances during parsing;
    /// capture operand values, not this context or its reader, in the returned playback callback.
    /// </remarks>
    public TapeTokenReader Reader { get; }

    /// <summary>Gets the extension keyword's source range.</summary>
    public TapeSourceSpan CommandSpan { get; }

    /// <summary>Gets the caller-supplied source label.</summary>
    public string? SourceName => CommandSpan.SourceName;

    /// <summary>Reports a syntax diagnostic.</summary>
    /// <param name="diagnostic">The diagnostic to append.</param>
    public void Report(TapeDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        _diagnostics.Add(diagnostic);
    }

    internal TapeCommand? BuiltInCommand { get; private set; }

    internal Func<TapePlayContext, TapeCommandResult> ParseBuiltIn(string keyword)
    {
        BuiltInCommand = _parseBuiltIn(_keyword with { Kind = TapeTokenKind.Keyword, Value = keyword });
        return play => play.PrepareBuiltIn();
    }
}
