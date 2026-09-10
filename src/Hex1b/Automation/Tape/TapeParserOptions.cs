namespace Hex1b.Automation;

/// <summary>Configures Tape syntax callbacks without executing instructions during parsing.</summary>
public sealed class TapeParserOptions
{
    /// <summary>Gets the mutable, case-sensitive mapping of command keywords to parsing callbacks, populated with VHS commands by default.</summary>
    /// <remarks>
    /// The parser validates keywords and callbacks and snapshots this dictionary when constructed.
    /// Remove a keyword to disable it, or use the indexer to replace a built-in or custom callback.
    /// Keys must be bare words; setting names, units, and boolean literals cannot be command keywords.
    /// Each callback receives a <see cref="TapeParseContext"/>, reads operands through <see cref="TapeParseContext.Reader"/>,
    /// and returns a deferred callback receiving <see cref="TapePlayContext"/>. That callback returns a
    /// <see cref="TapeCommandResult"/> containing a sequence builder or an error that prevents playback.
    /// Capture operand values, not the parse context or reader; preparation can run repeatedly during validation and playback
    /// and must not execute input. Null callbacks or results are implementation errors.
    /// Do not modify the dictionary concurrently with parser construction.
    /// </remarks>
    public IDictionary<string, Func<TapeParseContext, Func<TapePlayContext, TapeCommandResult>>>
        SyntaxExtensions { get; } = TapeBuiltinSyntax.Create();
}
