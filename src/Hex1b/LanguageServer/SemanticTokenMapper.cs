using Hex1b.Documents;
using Hex1b.LanguageServer.Protocol;
using Hex1b.Theming;

namespace Hex1b.LanguageServer;

/// <summary>
/// Maps LSP semantic token types to <see cref="TextDecoration"/> instances
/// using <see cref="SyntaxTheme"/> elements.
/// </summary>
internal static class SemanticTokenMapper
{
    /// <summary>
    /// Decodes the LSP semantic tokens data array into decoration spans.
    /// LSP encodes tokens as groups of 5 integers:
    /// [deltaLine, deltaStartChar, length, tokenType, tokenModifiers]
    /// </summary>
    public static IReadOnlyList<TextDecorationSpan> MapTokens(int[] data, string[] legend)
    {
        if (data.Length == 0) return [];

        var spans = new List<TextDecorationSpan>();
        var currentLine = 0;   // 0-based
        var currentChar = 0;   // 0-based

        for (var i = 0; i + 4 < data.Length; i += 5)
        {
            var deltaLine = data[i];
            var deltaChar = data[i + 1];
            var length = data[i + 2];
            var tokenType = data[i + 3];
            // var tokenModifiers = data[i + 4]; // Not used yet

            currentLine += deltaLine;
            currentChar = deltaLine > 0 ? deltaChar : currentChar + deltaChar;

            if (tokenType < 0 || tokenType >= legend.Length) continue;

            var decoration = GetDecorationForTokenType(legend[tokenType]);
            if (decoration == null) continue;

            // Convert from 0-based LSP positions to 1-based DocumentPositions
            spans.Add(new TextDecorationSpan(
                new DocumentPosition(currentLine + 1, currentChar + 1),
                new DocumentPosition(currentLine + 1, currentChar + length + 1),
                decoration,
                Priority: 1));
        }

        return spans;
    }

    private static TextDecoration? GetDecorationForTokenType(string tokenType) => tokenType switch
    {
        SemanticTokenTypes.Keyword => new TextDecoration { ForegroundThemeElement = SyntaxTheme.KeywordColor },
        SemanticTokenTypes.Comment => new TextDecoration { ForegroundThemeElement = SyntaxTheme.CommentColor },
        SemanticTokenTypes.String => new TextDecoration { ForegroundThemeElement = SyntaxTheme.StringColor },
        SemanticTokenTypes.Number => new TextDecoration { ForegroundThemeElement = SyntaxTheme.NumberColor },
        SemanticTokenTypes.Type or SemanticTokenTypes.Class or SemanticTokenTypes.Struct
            or SemanticTokenTypes.Enum or SemanticTokenTypes.Interface =>
            new TextDecoration { ForegroundThemeElement = SyntaxTheme.TypeColor },
        SemanticTokenTypes.Function or SemanticTokenTypes.Method =>
            new TextDecoration { ForegroundThemeElement = SyntaxTheme.FunctionColor },
        SemanticTokenTypes.Variable or SemanticTokenTypes.Parameter =>
            new TextDecoration { ForegroundThemeElement = SyntaxTheme.VariableColor },
        SemanticTokenTypes.Property => new TextDecoration { ForegroundThemeElement = SyntaxTheme.PropertyColor },
        SemanticTokenTypes.Namespace => new TextDecoration { ForegroundThemeElement = SyntaxTheme.NamespaceColor },
        SemanticTokenTypes.Operator => new TextDecoration { ForegroundThemeElement = SyntaxTheme.OperatorColor },
        SemanticTokenTypes.EnumMember => new TextDecoration { ForegroundThemeElement = SyntaxTheme.EnumMemberColor },
        _ => null,
    };
}
