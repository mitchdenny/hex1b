using Hex1b;
using Hex1b.Documents;
using Hex1b.Theming;

namespace CloudTermDemo;

/// <summary>
/// Lightweight YAML syntax highlighter using the ITextDecorationProvider interface.
/// Highlights keys, values, comments, strings, booleans, numbers, and anchors.
/// </summary>
internal sealed class YamlSyntaxHighlighter : ITextDecorationProvider
{
    private static readonly TextDecoration KeyDecoration = new()
    {
        ForegroundThemeElement = SyntaxTheme.KeywordColor,
    };

    private static readonly TextDecoration StringDecoration = new()
    {
        ForegroundThemeElement = SyntaxTheme.StringColor,
    };

    private static readonly TextDecoration CommentDecoration = new()
    {
        ForegroundThemeElement = SyntaxTheme.CommentColor,
    };

    private static readonly TextDecoration NumberDecoration = new()
    {
        ForegroundThemeElement = SyntaxTheme.NumberColor,
    };

    private static readonly TextDecoration BoolDecoration = new()
    {
        ForegroundThemeElement = SyntaxTheme.TypeColor,
    };

    private static readonly TextDecoration AnchorDecoration = new()
    {
        ForegroundThemeElement = SyntaxTheme.FunctionColor,
    };

    public IReadOnlyList<TextDecorationSpan> GetDecorations(
        int startLine, int endLine, IHex1bDocument document)
    {
        var spans = new List<TextDecorationSpan>();

        for (var line = startLine; line <= Math.Min(endLine, document.LineCount); line++)
        {
            var text = document.GetLineText(line);
            HighlightLine(text, line, spans);
        }

        return spans;
    }

    private static void HighlightLine(string text, int line, List<TextDecorationSpan> spans)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var trimmed = text.TrimStart();

        // Full-line comment
        if (trimmed.StartsWith('#'))
        {
            var commentStart = text.IndexOf('#');
            spans.Add(new TextDecorationSpan(
                new DocumentPosition(line, commentStart + 1),
                new DocumentPosition(line, text.Length + 1),
                CommentDecoration));
            return;
        }

        // --- document separator
        if (trimmed.StartsWith("---"))
        {
            spans.Add(new TextDecorationSpan(
                new DocumentPosition(line, 1),
                new DocumentPosition(line, text.Length + 1),
                CommentDecoration));
            return;
        }

        // Key: value pattern
        var colonIdx = text.IndexOf(':');
        if (colonIdx > 0 && !text.TrimStart().StartsWith('-'))
        {
            // Key part
            var keyStart = 0;
            while (keyStart < colonIdx && text[keyStart] == ' ')
                keyStart++;

            spans.Add(new TextDecorationSpan(
                new DocumentPosition(line, keyStart + 1),
                new DocumentPosition(line, colonIdx + 1),
                KeyDecoration));

            // Value part
            if (colonIdx + 1 < text.Length)
            {
                var valueStr = text[(colonIdx + 1)..].Trim();
                var valueStart = text.IndexOf(valueStr[0], colonIdx + 1);

                HighlightValue(valueStr, line, valueStart, spans);
            }
        }
        else if (trimmed.StartsWith("- "))
        {
            // List item — highlight the value after "- "
            var dashIdx = text.IndexOf('-');
            var valueStart = dashIdx + 2;
            if (valueStart < text.Length)
            {
                var valueStr = text[valueStart..].Trim();
                if (valueStr.Length > 0)
                {
                    var actualStart = text.IndexOf(valueStr[0], valueStart);

                    // Check if it's a key: value inside a list item
                    var innerColon = valueStr.IndexOf(':');
                    if (innerColon > 0)
                    {
                        spans.Add(new TextDecorationSpan(
                            new DocumentPosition(line, actualStart + 1),
                            new DocumentPosition(line, actualStart + innerColon + 1),
                            KeyDecoration));

                        if (innerColon + 1 < valueStr.Length)
                        {
                            var innerValue = valueStr[(innerColon + 1)..].Trim();
                            if (innerValue.Length > 0)
                            {
                                var innerStart = text.IndexOf(innerValue[0], actualStart + innerColon + 1);
                                HighlightValue(innerValue, line, innerStart, spans);
                            }
                        }
                    }
                    else
                    {
                        HighlightValue(valueStr, line, actualStart, spans);
                    }
                }
            }
        }

        // Inline comment
        var inlineComment = FindInlineComment(text);
        if (inlineComment >= 0)
        {
            spans.Add(new TextDecorationSpan(
                new DocumentPosition(line, inlineComment + 1),
                new DocumentPosition(line, text.Length + 1),
                CommentDecoration));
        }
    }

    private static void HighlightValue(string value, int line, int startCol, List<TextDecorationSpan> spans)
    {
        if (value.Length == 0) return;

        // Anchors and aliases
        if (value.StartsWith('&') || value.StartsWith('*'))
        {
            spans.Add(new TextDecorationSpan(
                new DocumentPosition(line, startCol + 1),
                new DocumentPosition(line, startCol + value.Length + 1),
                AnchorDecoration));
            return;
        }

        // Quoted strings
        if ((value.StartsWith('"') && value.EndsWith('"')) ||
            (value.StartsWith('\'') && value.EndsWith('\'')))
        {
            spans.Add(new TextDecorationSpan(
                new DocumentPosition(line, startCol + 1),
                new DocumentPosition(line, startCol + value.Length + 1),
                StringDecoration));
            return;
        }

        // Booleans
        if (value is "true" or "false" or "yes" or "no" or "True" or "False" or "null" or "~")
        {
            spans.Add(new TextDecorationSpan(
                new DocumentPosition(line, startCol + 1),
                new DocumentPosition(line, startCol + value.Length + 1),
                BoolDecoration));
            return;
        }

        // Numbers
        if (double.TryParse(value, out _))
        {
            spans.Add(new TextDecorationSpan(
                new DocumentPosition(line, startCol + 1),
                new DocumentPosition(line, startCol + value.Length + 1),
                NumberDecoration));
            return;
        }

        // Plain strings — use string color
        spans.Add(new TextDecorationSpan(
            new DocumentPosition(line, startCol + 1),
            new DocumentPosition(line, startCol + value.Length + 1),
            StringDecoration));
    }

    private static int FindInlineComment(string text)
    {
        var inString = false;
        var stringChar = ' ';
        for (var i = 0; i < text.Length; i++)
        {
            if (!inString && (text[i] == '"' || text[i] == '\''))
            {
                inString = true;
                stringChar = text[i];
            }
            else if (inString && text[i] == stringChar)
            {
                inString = false;
            }
            else if (!inString && text[i] == '#' && i > 0 && text[i - 1] == ' ')
            {
                return i;
            }
        }
        return -1;
    }
}
