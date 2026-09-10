using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Hex1b.Automation;

/// <summary>Parses VHS v0.11.0 Tape syntax into an editable document without executing instructions.</summary>
/// <remarks>
/// Syntax follows upstream c073383b5de0b1f57bf514113029c306bc986539. Source instructions
/// remain AST nodes; only the explicit FileInfo overload opens a source file. Numeric
/// conversion, output support, executable lookup, and source expansion are execution concerns.
/// UTF-8 byte-order marks are preserved as source characters and rejected by the pinned grammar.
/// </remarks>
/// <example>
/// <code>
/// using Hex1b.Automation;
///
/// var parser = new TapeParser();
/// var document = parser.Parse("Type 'hello' Enter Sleep 500ms", "example");
/// foreach (var command in document.Commands)
///     Console.WriteLine(command);
/// </code>
/// </example>
public sealed class TapeParser
{
    private readonly Dictionary<string, Func<TapeParseContext, Func<TapePlayContext, TapeCommandResult>>> _extensions = new(StringComparer.Ordinal);

    /// <summary>Creates a parser and snapshots its syntax-extension registrations.</summary>
    /// <param name="options">Optional syntax extensions.</param>
    /// <exception cref="ArgumentException">An extension keyword is invalid or reserved, or its parsing callback is null.</exception>
    public TapeParser(TapeParserOptions? options = null)
    {
        options ??= new();
        foreach (var (keyword, parse) in options.SyntaxExtensions)
        {
            ArgumentException.ThrowIfNullOrEmpty(keyword);
            if (parse is null)
                throw new ArgumentException($"The '{keyword}' syntax extension has no parsing callback.", nameof(options));
            var lexer = new TapeLexer(keyword, null);
            var token = lexer.Next();
            if (token.Kind is not (TapeTokenKind.String or TapeTokenKind.Keyword) || token.RawText != keyword ||
                token.Value != keyword || lexer.Next().Kind != TapeTokenKind.EndOfFile ||
                keyword[0] is '"' or '\'' or '`')
                throw new ArgumentException($"The syntax-extension keyword '{keyword}' is reserved or not a bare word.", nameof(options));

            _extensions.Add(keyword, parse);
        }
    }

    /// <summary>Parses Tape content without opening files or executing instructions.</summary>
    /// <param name="text">The Tape content, never a filename.</param>
    /// <param name="sourceName">An optional source label for diagnostics.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="TapeParseException">The content has syntax errors.</exception>
    public TapeDocument Parse(string text, string? sourceName = null)
    {
        if (!TryParse(text, out var document, out var diagnostics, sourceName))
            throw new TapeParseException(diagnostics);
        return document;
    }

    /// <summary>Attempts to parse Tape content.</summary>
    /// <param name="text">The Tape content.</param>
    /// <param name="document">The document on success, or null on syntax failure.</param>
    /// <param name="sourceName">An optional source label.</param>
    /// <returns>True when parsing succeeds.</returns>
    public bool TryParse(string text, [NotNullWhen(true)] out TapeDocument? document, string? sourceName = null) =>
        TryParse(text, out document, out _, sourceName);

    /// <summary>Attempts to parse Tape content and returns all reported diagnostics.</summary>
    /// <param name="text">The Tape content.</param>
    /// <param name="document">The document on success, or null on syntax failure.</param>
    /// <param name="diagnostics">An immutable diagnostic snapshot.</param>
    /// <param name="sourceName">An optional source label.</param>
    /// <returns>True when no error diagnostics were reported.</returns>
    /// <remarks>Exceptions from caller-supplied syntax extensions are not swallowed.</remarks>
    public bool TryParse(string text, [NotNullWhen(true)] out TapeDocument? document,
        out IReadOnlyList<TapeDiagnostic> diagnostics, string? sourceName = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new TapeSyntaxParser(text, sourceName, _extensions, this).TryParse(out document, out diagnostics);
    }

    /// <summary>Reads UTF-8 Tape content from the current stream position through EOF, leaving the stream open.</summary>
    /// <param name="stream">The readable stream, which need not support seeking.</param>
    /// <param name="sourceName">An optional source label.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="TapeParseException">The content has syntax errors.</exception>
    public async Task<TapeDocument> ParseAsync(Stream stream, string? sourceName = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();
        using var reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        return await ParseAsync(reader, sourceName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads Tape content through EOF, leaving the reader open even on failure or cancellation.</summary>
    /// <param name="reader">The borrowed reader.</param>
    /// <param name="sourceName">An optional source label.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="TapeParseException">The content has syntax errors.</exception>
    public async Task<TapeDocument> ParseAsync(TextReader reader, string? sourceName = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        cancellationToken.ThrowIfCancellationRequested();
        var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!new TapeSyntaxParser(text, sourceName, _extensions, this, cancellationToken).TryParse(out var document, out var diagnostics))
            throw new TapeParseException(diagnostics);
        cancellationToken.ThrowIfCancellationRequested();
        return document;
    }

    /// <summary>Opens, reads, and closes a UTF-8 Tape file, using its full path as the source label.</summary>
    /// <param name="file">The file to parse.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="TapeParseException">The file has syntax errors.</exception>
    public async Task<TapeDocument> ParseAsync(FileInfo file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        cancellationToken.ThrowIfCancellationRequested();
        await using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read,
            4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await ParseAsync(stream, file.FullName, cancellationToken).ConfigureAwait(false);
    }
}
