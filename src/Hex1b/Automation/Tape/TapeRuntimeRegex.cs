using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

internal static class TapeRuntimeRegex
{
    private const string Surrogates = @"\uD800-\uDFFF";
    private const string ScalarPair = @"[\uD800-\uDBFF][\uDC00-\uDFFF]";

    internal static bool TryCompile(string pattern, out Regex? regex, out string? error)
    {
        regex = null;
        if (!TapeGoRegexValidator.TryValidate(pattern, out error))
            return false;
        var translated = new StringBuilder();
        var flags = (Singleline: false, Multiline: false);
        var groups = new Stack<(bool Singleline, bool Multiline)>();
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '\\' && i + 1 < pattern.Length)
            {
                var escaped = pattern[++i];
                if (escaped is 'b' or 'B' or 'Q' or 'E' or 'p' or 'P')
                {
                    error = $"The valid Go regex escape \\{escaped} is not supported by this playback engine.";
                    return false;
                }
                if (TryOctalEscape(pattern, ref i, out var octal))
                {
                    translated.Append(octal);
                    continue;
                }
                var replacement = escaped switch
                {
                    'd' => "[0-9]",
                    'D' => ScalarComplement("0-9"),
                    's' => @"[\t\n\f\r ]",
                    'S' => ScalarComplement(@"\t\n\f\r "),
                    'w' => "[A-Za-z0-9_]",
                    'W' => ScalarComplement("A-Za-z0-9_"),
                    _ => "\\" + escaped
                };
                translated.Append(replacement);
                continue;
            }
            if (c == '[')
            {
                var negated = i + 1 < pattern.Length && pattern[i + 1] == '^';
                if (negated)
                    i++;
                var elements = new StringBuilder();
                var first = true;
                var closed = false;
                while (++i < pattern.Length)
                {
                    c = pattern[i];
                    if (c == '[' || char.IsSurrogate(c) || (c == ']' && first))
                    {
                        error = "Nested/POSIX classes, supplementary class literals, and leading literal ']' are not supported.";
                        return false;
                    }
                    if (c == ']')
                    {
                        closed = true;
                        break;
                    }
                    if (c == '\\' && i + 1 < pattern.Length)
                    {
                        var escaped = pattern[++i];
                        if (escaped is 'D' or 'S' or 'W' or 'p' or 'P' or 'Q' or 'E' or 'b' or 'B')
                        {
                            error = $"The class escape \\{escaped} is not supported by this playback engine.";
                            return false;
                        }
                        elements.Append(TryOctalEscape(pattern, ref i, out var octal) ? octal : escaped switch
                        {
                            'd' => "0-9",
                            's' => @"\t\n\f\r ",
                            'w' => "A-Za-z0-9_",
                            _ => "\\" + escaped
                        });
                    }
                    else if (c == '-' && (first || (i + 1 < pattern.Length && pattern[i + 1] == ']')))
                        elements.Append(@"\-");
                    else
                        elements.Append(c);
                    first = false;
                }
                if (!closed)
                {
                    error = "Unclosed regular-expression character class.";
                    return false;
                }
                translated.Append(negated ? ScalarComplement(elements.ToString())
                    : $"[{elements}-[{Surrogates}]]");
                continue;
            }
            if (c == '(')
            {
                if (i + 2 < pattern.Length && pattern[i + 1] == '?')
                {
                    var j = i + 2;
                    while (j < pattern.Length && pattern[j] is 'i' or 'm' or 's' or 'U' or '-')
                        j++;
                    if (j < pattern.Length && pattern[j] is ':' or ')')
                    {
                        if (pattern[j] == ':')
                            groups.Push(flags);
                        var enabled = true;
                        for (var k = i + 2; k < j; k++)
                        {
                            if (pattern[k] == '-')
                                enabled = false;
                            else if (pattern[k] == 's')
                                flags.Singleline = enabled;
                            else if (pattern[k] == 'm')
                                flags.Multiline = enabled;
                            else if (pattern[k] == 'i' && enabled)
                            {
                                error = "Go case-insensitive Unicode folding is not supported by this playback engine.";
                                return false;
                            }
                            else if (pattern[k] == 'U')
                            {
                                error = "The Go regex flag 'U' is not supported by this playback engine.";
                                return false;
                            }
                        }
                        translated.Append(pattern.AsSpan(i, j - i + 1));
                        i = j;
                        continue;
                    }
                    if (pattern.AsSpan(i).StartsWith("(?P<", StringComparison.Ordinal))
                    {
                        groups.Push(flags);
                        translated.Append("(?<");
                        i += 3;
                        continue;
                    }
                }
                groups.Push(flags);
            }
            else if (c == ')' && groups.Count > 0)
                flags = groups.Pop();
            if (char.IsSurrogate(c))
            {
                if (!char.IsHighSurrogate(c) || i + 1 >= pattern.Length || !char.IsLowSurrogate(pattern[i + 1]))
                {
                    error = "An unpaired surrogate is not a valid Go regex scalar.";
                    return false;
                }
                translated.Append("(?:").Append(c).Append(pattern[++i]).Append(')');
                continue;
            }
            translated.Append(c switch
            {
                '.' => ScalarComplement(flags.Singleline ? "" : @"\n"),
                '$' when !flags.Multiline => @"\z",
                _ => c.ToString()
            });
        }
        try
        {
            regex = new Regex(translated.ToString(), RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
                TimeSpan.FromSeconds(1));
            return true;
        }
        catch (ArgumentException ex)
        {
            error = $"The Go regex cannot be executed faithfully by this engine: {ex.Message}";
            return false;
        }
        catch (NotSupportedException ex)
        {
            error = $"The Go regex cannot be executed faithfully by this engine: {ex.Message}";
            return false;
        }
    }

    private static string ScalarComplement(string excluded) => $"(?:{ScalarPair}|[^{excluded}{Surrogates}])";

    private static bool TryOctalEscape(string pattern, ref int offset, out string value)
    {
        value = "";
        if (pattern[offset] is < '0' or > '7')
            return false;
        var codePoint = pattern[offset] - '0';
        for (var count = 1; count < 3 && offset + 1 < pattern.Length && pattern[offset + 1] is >= '0' and <= '7'; count++)
            codePoint = codePoint * 8 + pattern[++offset] - '0';
        value = @"\u" + codePoint.ToString("X4", CultureInfo.InvariantCulture);
        return true;
    }
}
