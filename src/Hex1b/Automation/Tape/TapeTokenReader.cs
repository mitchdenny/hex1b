namespace Hex1b.Automation;

/// <summary>Provides forward-only access to Tape tokens for syntax recognition.</summary>
public sealed class TapeTokenReader
{
    private readonly TapeLexer _lexer;
    private readonly Queue<TapeToken> _buffer = new();
    private readonly IReadOnlySet<string>? _commandKeywords;

    internal TapeTokenReader(string text, string? sourceName, CancellationToken cancellationToken = default,
        IReadOnlySet<string>? commandKeywords = null)
    {
        _lexer = new(text, sourceName, cancellationToken);
        _commandKeywords = commandKeywords;
    }

    /// <summary>Gets the next unread token.</summary>
    public TapeToken Current => Peek();

    /// <summary>Gets a token without consuming input.</summary>
    /// <param name="offset">The non-negative lookahead offset; zero denotes the current token.</param>
    /// <returns>The requested token, or the end-of-input token.</returns>
    public TapeToken Peek(int offset = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        while (_buffer.Count <= offset)
        {
            if (_buffer.Count > 0 && _buffer.Last().Kind == TapeTokenKind.EndOfFile)
                return _buffer.Last();
            var token = _lexer.Next();
            if (token.Kind == TapeTokenKind.String && token.RawText == token.Value &&
                _commandKeywords?.Contains(token.Value) == true)
                token = token with { Kind = TapeTokenKind.Keyword };
            _buffer.Enqueue(token);
        }
        return _buffer.ElementAt(offset);
    }

    /// <summary>Consumes and returns the current token; at EOF, returns EOF without advancing.</summary>
    /// <returns>The consumed token.</returns>
    public TapeToken Read()
    {
        var token = Current;
        if (token.Kind != TapeTokenKind.EndOfFile)
        {
            _buffer.Dequeue();
        }
        LastRead = token;
        return token;
    }

    internal TapeToken? LastRead { get; private set; }
}
