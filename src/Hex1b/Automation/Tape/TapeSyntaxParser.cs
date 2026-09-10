using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Hex1b.Automation;

internal sealed class TapeSyntaxParser(
    string text, string? sourceName,
    IReadOnlyDictionary<string, Func<TapeParseContext, Func<TapePlayContext, TapeCommandResult>>> extensions, TapeParser parser,
    CancellationToken cancellationToken = default)
{
    private readonly TapeTokenReader _reader = new(text, sourceName, cancellationToken,
        new HashSet<string>(extensions.Keys, StringComparer.Ordinal));
    private readonly List<TapeDiagnostic> _diagnostics = [];

    internal bool TryParse([NotNullWhen(true)] out TapeDocument? document, out IReadOnlyList<TapeDiagnostic> diagnostics)
    {
        var commands = new List<TapeCommand>();
        while (_reader.Current.Kind != TapeTokenKind.EndOfFile)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var token = _reader.Read();
            if (token.Kind == TapeTokenKind.Comment)
                continue;
            var command = ParseCommand(token);
            if (command is not null)
                commands.Add(command with { Span = ThroughLast(token.Span) });
        }
        diagnostics = Array.AsReadOnly(_diagnostics.ToArray());
        document = _diagnostics.Any(diagnostic => diagnostic.Severity == TapeDiagnosticSeverity.Error)
            ? null : new(commands, sourceName) { Parser = parser };
        return document is not null;
    }

    private TapeCommand? ParseCommand(TapeToken token)
    {
        if (token.Kind is TapeTokenKind.String or TapeTokenKind.Keyword && extensions.TryGetValue(token.Value, out var extension) &&
            token.RawText == token.Value)
        {
            var context = new TapeParseContext(_reader, token, _diagnostics, ParseBuiltInCommand);
            var prepare = extension(context)
                ?? throw new InvalidOperationException($"The '{token.Value}' syntax parser returned no playback callback.");
            var command = context.BuiltInCommand ?? new TapeExtensionCommand(token.Value, prepare, token.Span);
            return command with { PrepareCallback = prepare };
        }
        Error(token, "TAPE001", $"Invalid command: {token.Value}");
        return null;
    }

    private TapeCommand? ParseBuiltInCommand(TapeToken token)
    {
        var span = token.Span;
        switch (token.Value)
        {
            case "Type":
                var delay = ParseSpeed();
                return new TapeTypeCommand(ParseStrings(), delay, span);
            case "Copy":
                return new TapeCopyCommand(ParseStrings(), span);
            case "Sleep":
                return new TapeSleepCommand(ParseDuration(), span);
            case "Set":
                return ParseSet(span);
            case "Wait":
                return ParseWait(span);
            case "Ctrl":
            case "Alt":
            case "Shift":
                return ParseChord(token);
            case "Hide": return new TapeHideCommand(span);
            case "Show": return new TapeShowCommand(span);
            case "Paste": return new TapePasteCommand(span);
            case "Env":
                var name = _reader.Read().Value;
                return new TapeEnvCommand(name, ReadString().Value, span);
            case "Require": return new TapeRequireCommand(ReadString().Value, span);
            case "Source":
            case "Screenshot":
            case "Output":
                var path = _reader.Current;
                if (path.Kind != TapeTokenKind.String)
                {
                    Error(token, "TAPE002", "Expected a file path.");
                    if (token.Value != "Output")
                        _reader.Read();
                    return null;
                }
                _reader.Read();
                var expected = token.Value == "Source" ? ".tape" : ".png";
                var extensionName = FileExtension(path.Value);
                if (token.Value == "Output")
                {
                    if (extensionName.Length == 0 && !path.Value.EndsWith('/'))
                        Error(path, "TAPE004", "An extensionless Output path must end with '/'.");
                }
                else if (extensionName != expected)
                    Error(path, "TAPE004", $"{token.Value} expects a path with the {expected} extension.");
                return token.Value switch
                {
                    "Source" => new TapeSourceCommand(path.Value, span),
                    "Screenshot" => new TapeScreenshotCommand(path.Value, span),
                    _ => new TapeOutputCommand(path.Value, span)
                };
            case "Backspace": case "Delete": case "Insert": case "Enter": case "Escape":
            case "Tab": case "Space": case "Up": case "Down": case "Left": case "Right":
            case "PageUp": case "PageDown": case "ScrollUp": case "ScrollDown":
                var speed = ParseSpeed();
                TapeNumberLiteral? count = null;
                if (_reader.Current.Kind == TapeTokenKind.Number)
                {
                    var number = _reader.Read();
                    count = new(number.Value, number.RawText, number.Span);
                }
                return new TapeKeyCommand(token.Value, count, speed, span);
            default:
                Error(token, "TAPE001", $"Invalid command: {token.Value}");
                return null;
        }
    }

    private TapeSetCommand ParseSet(TapeSourceSpan span)
    {
        var name = _reader.Read();
        if (name.Kind != TapeTokenKind.Setting || !Enum.TryParse<TapeSetting>(name.Value, out _))
            Error(name, "TAPE005", $"Unknown setting: {name.Value}");
        Enum.TryParse<TapeSetting>(name.Value, out var setting);
        var operand = _reader.Current;
        string value;
        if (name.Kind == TapeTokenKind.Setting && setting == TapeSetting.WaitTimeout)
            value = ParseDuration().Value;
        else
        {
            value = _reader.Read().Value;
            if (name.Kind == TapeTokenKind.Setting)
            {
                switch (setting)
                {
                    case TapeSetting.TypingSpeed:
                        value += IsUnit("ms", "s") ? _reader.Read().Value : "s";
                        break;
                    case TapeSetting.LoopOffset:
                        value += "%";
                        if (_reader.Current.Kind == TapeTokenKind.Percent)
                            _reader.Read();
                        break;
                    case TapeSetting.CursorBlink when operand.Kind != TapeTokenKind.Boolean:
                        Error(operand, "TAPE006", "CursorBlink expects a bare boolean.");
                        break;
                    case TapeSetting.WindowBar when value is not ("" or "Colorful" or "ColorfulRight" or "Rings" or "RingsRight"):
                        Error(operand, "TAPE006", "Invalid window bar style.");
                        break;
                    case TapeSetting.MarginFill when value.StartsWith('#') &&
                        (value.Length != 7 || !value.Skip(1).All(char.IsAsciiHexDigit)):
                        Error(operand, "TAPE006", "MarginFill expects a six-digit hexadecimal color.");
                        break;
                    case TapeSetting.WaitPattern:
                        ValidateRegex(operand);
                        break;
                }
            }
        }
        var literalSpan = ThroughLast(operand.Span);
        return new(setting, new(value, Raw(literalSpan), operand.Kind, literalSpan), span);
    }

    private TapeWaitCommand ParseWait(TapeSourceSpan span)
    {
        var scope = TapeWaitScope.Line;
        if (_reader.Current.Kind == TapeTokenKind.Plus)
        {
            _reader.Read();
            var token = _reader.Current;
            if (token.Kind != TapeTokenKind.String || token.Value is not ("Line" or "Screen"))
            {
                Error(token, "TAPE007", "Wait+ expects Line or Screen.");
                return new(scope, null, null, span);
            }
            else
            {
                scope = token.Value == "Line" ? TapeWaitScope.Line : TapeWaitScope.Screen;
                _reader.Read();
            }
        }
        var timeout = ParseSpeed();
        if (timeout is not null && timeout.Value.Length != 0 && !IsPositiveGoDuration(timeout.Value))
        {
            Error(_reader.Current, "TAPE008", "Wait expects a positive duration within Go's duration range.");
            return new(scope, null, timeout, span);
        }
        string? pattern = null;
        if (_reader.Current.Kind == TapeTokenKind.Regex)
        {
            var regex = _reader.Read();
            pattern = regex.Value;
            ValidateRegex(regex);
        }
        return new(scope, pattern, timeout, span);
    }

    private TapeChordCommand ParseChord(TapeToken token)
    {
        var parts = new List<string>();
        var modifierChain = true;
        if (token.Value != "Ctrl")
        {
            if (_reader.Current.Kind == TapeTokenKind.Plus)
            {
                _reader.Read();
                var part = _reader.Current;
                if (part.Kind == TapeTokenKind.String || IsKeyword(part, "Enter", "Tab") ||
                    part.Kind is TapeTokenKind.LeftBracket or TapeTokenKind.RightBracket)
                    parts.Add(_reader.Read().Value);
            }
        }
        else
        {
            while (_reader.Current.Kind == TapeTokenKind.Plus)
            {
                var plus = _reader.Read();
                var part = _reader.Current;
                if (part.Value is "Alt" or "Shift")
                {
                    if (!modifierChain)
                    {
                        Error(plus, "TAPE009", "Modifiers must precede ordinary keys.");
                        parts.Clear();
                        continue;
                    }
                    parts.Add(_reader.Read().Value);
                    continue;
                }
                modifierChain = false;
                if (IsKeyword(part, "Enter", "Space", "Backspace", "Left", "Right", "Up", "Down") ||
                    part.Kind is TapeTokenKind.Minus or TapeTokenKind.At or TapeTokenKind.LeftBracket or
                    TapeTokenKind.RightBracket or TapeTokenKind.Caret or TapeTokenKind.Backslash ||
                    part.Kind == TapeTokenKind.String && Encoding.UTF8.GetByteCount(part.Value) == 1)
                    parts.Add(part.Value);
                else
                {
                    Error(plus, "TAPE009", "Not a valid modifier.");
                    Error(plus, "TAPE009", "Invalid control argument.");
                }
                _reader.Read();
            }
        }
        if (parts.Count == 0)
            Error(_reader.LastRead ?? token, "TAPE009", $"{token.Value} expects a '+' operand.");
        return new(token.Value, parts, token.Span);
    }

    private TapeDurationLiteral? ParseSpeed()
    {
        if (_reader.Current.Kind != TapeTokenKind.At)
            return null;
        _reader.Read();
        return ParseDuration();
    }

    private TapeDurationLiteral ParseDuration()
    {
        var number = _reader.Current;
        if (number.Kind != TapeTokenKind.Number)
        {
            Error(_reader.LastRead ?? number, "TAPE003", "Expected a numeric duration.");
            return new("", "", number.Span with { Length = 0 });
        }
        _reader.Read();
        var value = number.Value + (IsUnit("ms", "s", "m") ? _reader.Read().Value : "s");
        var span = ThroughLast(number.Span);
        return new(value, Raw(span), span);
    }

    private string ParseStrings()
    {
        if (_reader.Current.Kind != TapeTokenKind.String)
            Error(_reader.Current, "TAPE002", "Expected a string.");
        var strings = new List<string>();
        while (_reader.Current.Kind == TapeTokenKind.String)
            strings.Add(_reader.Read().Value);
        return string.Join(' ', strings);
    }

    private TapeToken ReadString()
    {
        var token = _reader.Read();
        if (token.Kind != TapeTokenKind.String)
            Error(token, "TAPE002", "Expected one string.");
        return token;
    }

    private void ValidateRegex(TapeToken token)
    {
        if (!TapeGoRegexValidator.TryValidate(token.Value, out var error))
            Error(token, "TAPE010", $"Invalid Go regular expression: {error}");
    }

    private void Error(TapeToken token, string code, string message) =>
        _diagnostics.Add(new(code, TapeDiagnosticSeverity.Error, TapeDiagnosticStage.Parsing, message, token.Span));

    private bool IsUnit(params string[] units) =>
        _reader.Current.Kind == TapeTokenKind.Unit && units.Contains(_reader.Current.Value);

    private static bool IsKeyword(TapeToken token, params string[] names) =>
        token.Kind == TapeTokenKind.Keyword && names.Contains(token.Value);

    private TapeSourceSpan ThroughLast(TapeSourceSpan start)
    {
        var end = _reader.LastRead?.Span;
        return start with { Length = Math.Max(start.Length, (end?.Offset + end?.Length ?? start.Offset) - start.Offset) };
    }

    private string Raw(TapeSourceSpan span) => text.Substring(span.Offset, span.Length);

    private static string FileExtension(string path)
    {
        // Go's Unix filepath.Ext treats a final dot as an extension and a leading dot as a filename extension.
        var slash = path.LastIndexOf('/');
        var dot = path.LastIndexOf('.');
        return dot > slash ? path[dot..] : "";
    }

    private static bool IsPositiveGoDuration(string value)
    {
        var unitLength = value.EndsWith("ms", StringComparison.Ordinal) ? 2 : 1;
        var unit = unitLength == 2 ? 1_000_000UL : value.EndsWith('m') ? 60_000_000_000UL : 1_000_000_000UL;
        var number = value.AsSpan(0, value.Length - unitLength);
        var position = 0;
        ulong integer = 0;
        const ulong limit = 1UL << 63;
        while (position < number.Length && char.IsAsciiDigit(number[position]))
        {
            var digit = (uint)(number[position++] - '0');
            if (integer > (limit - digit) / 10)
                return false;
            integer = integer * 10 + digit;
        }
        var integerDigits = position;
        ulong fraction = 0;
        var scale = 1d;
        var fractionalDigits = 0;
        var saturated = false;
        if (position < number.Length && number[position] == '.')
        {
            position++;
            while (position < number.Length && char.IsAsciiDigit(number[position]))
            {
                var digit = (uint)(number[position++] - '0');
                fractionalDigits++;
                if (saturated || fraction > (limit - 1 - digit) / 10)
                {
                    saturated = true;
                    continue;
                }
                fraction = fraction * 10 + digit;
                scale *= 10;
            }
        }
        if (position != number.Length || integerDigits + fractionalDigits == 0 || integer > limit / unit)
            return false;
        var nanoseconds = integer * unit + (ulong)(fraction * (unit / scale));
        return nanoseconds is > 0 and <= long.MaxValue;
    }
}
