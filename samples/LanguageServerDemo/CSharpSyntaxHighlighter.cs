using Hex1b;
using Hex1b.Documents;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace LanguageServerDemo;

/// <summary>
/// A context-aware C# syntax highlighter that uses look-ahead/behind heuristics
/// to distinguish keywords, types, methods, strings, comments, and numbers.
/// Handles verbatim strings, interpolated strings, character literals, block comments,
/// and preprocessor directives. Not a real parser — uses structural context for accuracy.
/// </summary>
internal sealed class CSharpSyntaxHighlighter : ITextDecorationProvider
{
    private static readonly HashSet<string> Keywords =
    [
        // Control flow
        "if", "else", "for", "foreach", "while", "do", "switch", "case", "default",
        "break", "continue", "return", "goto", "throw", "try", "catch", "finally",
        "yield", "when",
        // Declarations
        "class", "struct", "interface", "enum", "record", "namespace", "using",
        "delegate", "event",
        // Access modifiers
        "public", "private", "protected", "internal", "file",
        // Member modifiers
        "static", "readonly", "const", "volatile", "extern", "sealed", "abstract",
        "virtual", "override", "new", "partial", "async", "unsafe", "fixed",
        // Type keywords
        "void", "var", "dynamic", "object", "nint", "nuint",
        // Operators / expressions
        "is", "as", "in", "out", "ref", "params", "this", "base", "typeof",
        "sizeof", "nameof", "stackalloc", "default", "checked", "unchecked",
        // Literals
        "true", "false", "null",
        // Contextual
        "await", "where", "get", "set", "init", "value", "required", "global",
        "scoped", "not", "and", "or", "with",
        // Linq
        "from", "select", "let", "orderby", "ascending", "descending",
        "group", "by", "into", "join", "on", "equals",
    ];

    // Built-in type keywords get keyword color
    private static readonly HashSet<string> BuiltInTypes =
    [
        "int", "uint", "long", "ulong", "short", "ushort", "byte", "sbyte",
        "float", "double", "decimal", "bool", "char", "string", "object",
    ];

    // Known framework type names
    private static readonly HashSet<string> KnownTypes =
    [
        "Console", "String", "Int32", "Int64", "Boolean", "Char", "Byte",
        "Double", "Single", "Decimal", "Object", "Type", "Attribute",
        "Task", "ValueTask", "List", "Dictionary", "HashSet", "Queue", "Stack",
        "IEnumerable", "IReadOnlyList", "IReadOnlyCollection", "IList", "ICollection",
        "IDisposable", "IAsyncDisposable",
        "Action", "Func", "Predicate", "Comparison",
        "Exception", "ArgumentException", "ArgumentNullException",
        "InvalidOperationException", "NotSupportedException", "NotImplementedException",
        "KeyNotFoundException", "IndexOutOfRangeException", "NullReferenceException",
        "StringBuilder", "StringComparison", "CancellationToken", "CancellationTokenSource",
        "Stream", "MemoryStream", "FileStream", "StreamReader", "StreamWriter",
        "Math", "Convert", "Encoding", "Guid", "DateTime", "TimeSpan",
        "Array", "Span", "ReadOnlySpan", "Memory", "ReadOnlyMemory",
        "File", "Path", "Directory", "Environment",
        "EventArgs", "EventHandler",
    ];

    // Words that declare a type name as the next identifier
    private static readonly HashSet<string> TypeDeclarators =
    [
        "class", "struct", "interface", "enum", "record", "namespace",
    ];

    private bool _inBlockComment;

    public void Activate(IEditorSession session)
    {
        _inBlockComment = false;
    }

    public IReadOnlyList<TextDecorationSpan> GetDecorations(int startLine, int endLine, IHex1bDocument document)
    {
        var spans = new List<TextDecorationSpan>();

        // Track block comment state from document start for accuracy
        _inBlockComment = false;
        var scanEnd = Math.Min(endLine, document.LineCount);

        // Pre-scan lines before viewport for block comment state
        for (var line = 1; line < startLine && line <= document.LineCount; line++)
        {
            var text = document.GetLineText(line);
            TrackBlockCommentState(text);
        }

        for (var line = startLine; line <= scanEnd; line++)
        {
            var text = document.GetLineText(line);
            HighlightLine(text, line, spans);
        }

        return spans;
    }

    private void TrackBlockCommentState(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (_inBlockComment)
            {
                if (i + 1 < text.Length && text[i] == '*' && text[i + 1] == '/')
                {
                    _inBlockComment = false;
                    i++;
                }
            }
            else
            {
                if (i + 1 < text.Length && text[i] == '/' && text[i + 1] == '*')
                {
                    _inBlockComment = true;
                    i++;
                }
                else if (i + 1 < text.Length && text[i] == '/' && text[i + 1] == '/')
                    break; // rest of line is comment
                else if (text[i] == '"')
                    i = SkipStringLiteral(text, i);
                else if (text[i] == '\'')
                    i = SkipCharLiteral(text, i);
            }
        }
    }

    private void HighlightLine(string text, int line, List<TextDecorationSpan> spans)
    {
        var i = 0;
        string? previousWord = null;

        // Continue block comment from previous line
        if (_inBlockComment)
        {
            var end = FindBlockCommentEnd(text, 0);
            spans.Add(new TextDecorationSpan(
                new DocumentPosition(line, 1),
                new DocumentPosition(line, end + 1),
                new TextDecoration { ForegroundThemeElement = SyntaxTheme.CommentColor }));
            if (end >= text.Length)
                return; // entire line is in block comment
            i = end;
        }

        while (i < text.Length)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                i++;
                continue;
            }

            // Preprocessor directives
            if (text[i] == '#' && text[..i].Trim().Length == 0)
            {
                spans.Add(new TextDecorationSpan(
                    new DocumentPosition(line, i + 1),
                    new DocumentPosition(line, text.Length + 1),
                    new TextDecoration { ForegroundThemeElement = SyntaxTheme.PreprocessorColor }));
                break;
            }

            // Block comments /* ... */
            if (i + 1 < text.Length && text[i] == '/' && text[i + 1] == '*')
            {
                var start = i;
                _inBlockComment = true;
                var end = FindBlockCommentEnd(text, i + 2);
                spans.Add(new TextDecorationSpan(
                    new DocumentPosition(line, start + 1),
                    new DocumentPosition(line, end + 1),
                    new TextDecoration { ForegroundThemeElement = SyntaxTheme.CommentColor }));
                if (end >= text.Length)
                    return;
                i = end;
                continue;
            }

            // Line comments
            if (i + 1 < text.Length && text[i] == '/' && text[i + 1] == '/')
            {
                spans.Add(new TextDecorationSpan(
                    new DocumentPosition(line, i + 1),
                    new DocumentPosition(line, text.Length + 1),
                    new TextDecoration { ForegroundThemeElement = SyntaxTheme.CommentColor }));
                break;
            }

            // Verbatim string @"..."
            if (text[i] == '@' && i + 1 < text.Length && text[i + 1] == '"')
            {
                var start = i;
                i += 2;
                while (i < text.Length)
                {
                    if (text[i] == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                            i += 2; // escaped quote
                        else
                        {
                            i++;
                            break;
                        }
                    }
                    else
                        i++;
                }
                spans.Add(new TextDecorationSpan(
                    new DocumentPosition(line, start + 1),
                    new DocumentPosition(line, i + 1),
                    new TextDecoration { ForegroundThemeElement = SyntaxTheme.StringColor }));
                previousWord = null;
                continue;
            }

            // Interpolated string $"..."
            if (text[i] == '$' && i + 1 < text.Length && text[i + 1] == '"')
            {
                var start = i;
                i += 2;
                while (i < text.Length && text[i] != '"')
                {
                    if (text[i] == '\\' && i + 1 < text.Length) i++;
                    i++;
                }
                if (i < text.Length) i++;
                spans.Add(new TextDecorationSpan(
                    new DocumentPosition(line, start + 1),
                    new DocumentPosition(line, i + 1),
                    new TextDecoration { ForegroundThemeElement = SyntaxTheme.StringColor }));
                previousWord = null;
                continue;
            }

            // Regular string literals
            if (text[i] == '"')
            {
                var start = i;
                i++;
                while (i < text.Length && text[i] != '"')
                {
                    if (text[i] == '\\' && i + 1 < text.Length) i++;
                    i++;
                }
                if (i < text.Length) i++;
                spans.Add(new TextDecorationSpan(
                    new DocumentPosition(line, start + 1),
                    new DocumentPosition(line, i + 1),
                    new TextDecoration { ForegroundThemeElement = SyntaxTheme.StringColor }));
                previousWord = null;
                continue;
            }

            // Character literals
            if (text[i] == '\'')
            {
                var start = i;
                i++;
                if (i < text.Length && text[i] == '\\' && i + 1 < text.Length) i++;
                if (i < text.Length) i++;
                if (i < text.Length && text[i] == '\'') i++;
                spans.Add(new TextDecorationSpan(
                    new DocumentPosition(line, start + 1),
                    new DocumentPosition(line, i + 1),
                    new TextDecoration { ForegroundThemeElement = SyntaxTheme.StringColor }));
                previousWord = null;
                continue;
            }

            // Numbers (decimal, hex 0x, binary 0b, with underscores and suffixes)
            if (char.IsDigit(text[i]))
            {
                var start = i;
                if (text[i] == '0' && i + 1 < text.Length && (text[i + 1] == 'x' || text[i + 1] == 'X'))
                {
                    i += 2;
                    while (i < text.Length && (IsHexDigit(text[i]) || text[i] == '_')) i++;
                }
                else if (text[i] == '0' && i + 1 < text.Length && (text[i + 1] == 'b' || text[i + 1] == 'B'))
                {
                    i += 2;
                    while (i < text.Length && (text[i] == '0' || text[i] == '1' || text[i] == '_')) i++;
                }
                else
                {
                    while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.' || text[i] == '_')) i++;
                    if (i < text.Length && (text[i] == 'e' || text[i] == 'E'))
                    {
                        i++;
                        if (i < text.Length && (text[i] == '+' || text[i] == '-')) i++;
                        while (i < text.Length && char.IsDigit(text[i])) i++;
                    }
                }
                // Type suffixes: f, d, m, u, l, ul, lu
                while (i < text.Length && "fFdDmMuUlL".Contains(text[i])) i++;
                spans.Add(new TextDecorationSpan(
                    new DocumentPosition(line, start + 1),
                    new DocumentPosition(line, i + 1),
                    new TextDecoration { ForegroundThemeElement = SyntaxTheme.NumberColor }));
                previousWord = null;
                continue;
            }

            // Identifiers / keywords
            if (char.IsLetter(text[i]) || text[i] == '_')
            {
                var start = i;
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
                    i++;
                var word = text[start..i];

                // Look ahead: skip whitespace to see what follows
                var nextNonSpace = i;
                while (nextNonSpace < text.Length && text[nextNonSpace] == ' ') nextNonSpace++;
                var nextChar = nextNonSpace < text.Length ? text[nextNonSpace] : '\0';

                // Check for generic type args: word<
                var followedByGeneric = nextChar == '<';
                // Check for method call: word(
                var followedByParen = nextChar == '(';
                // Check for member access context: previous char was .
                var afterDot = start > 0 && text[start - 1] == '.';

                if (BuiltInTypes.Contains(word))
                {
                    spans.Add(MakeSpan(line, start, i, SyntaxTheme.KeywordColor));
                }
                else if (Keywords.Contains(word))
                {
                    spans.Add(MakeSpan(line, start, i, SyntaxTheme.KeywordColor));
                }
                else if (previousWord != null && TypeDeclarators.Contains(previousWord))
                {
                    // Word immediately after class/struct/interface/enum/record
                    spans.Add(MakeSpan(line, start, i, SyntaxTheme.TypeColor));
                }
                else if (KnownTypes.Contains(word))
                {
                    spans.Add(MakeSpan(line, start, i, SyntaxTheme.TypeColor));
                }
                else if (word.Length > 1 && word[0] == 'I' && char.IsUpper(word[1]))
                {
                    // IFoo pattern — likely an interface type
                    spans.Add(MakeSpan(line, start, i, SyntaxTheme.TypeColor));
                }
                else if (followedByGeneric && !afterDot)
                {
                    // Foo<T> at start of expression is likely a type
                    spans.Add(MakeSpan(line, start, i, SyntaxTheme.TypeColor));
                }
                else if (followedByParen)
                {
                    // word( is a method/function call
                    spans.Add(MakeSpan(line, start, i, SyntaxTheme.FunctionColor));
                }

                // No decoration for plain identifiers (variables, parameters, etc.)
                previousWord = word;
                continue;
            }

            previousWord = null;
            i++;
        }
    }

    private int FindBlockCommentEnd(string text, int start)
    {
        for (var i = start; i + 1 < text.Length; i++)
        {
            if (text[i] == '*' && text[i + 1] == '/')
            {
                _inBlockComment = false;
                return i + 2;
            }
        }
        return text.Length;
    }

    private static int SkipStringLiteral(string text, int i)
    {
        i++;
        while (i < text.Length && text[i] != '"')
        {
            if (text[i] == '\\' && i + 1 < text.Length) i++;
            i++;
        }
        return i;
    }

    private static int SkipCharLiteral(string text, int i)
    {
        i++;
        if (i < text.Length && text[i] == '\\') i++;
        if (i < text.Length) i++;
        if (i < text.Length && text[i] == '\'') i++;
        return i;
    }

    private static bool IsHexDigit(char c) =>
        char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    private static TextDecorationSpan MakeSpan(int line, int start, int end, Hex1bThemeElement<Hex1bColor> element) =>
        new(new DocumentPosition(line, start + 1),
            new DocumentPosition(line, end + 1),
            new TextDecoration { ForegroundThemeElement = element });
}
