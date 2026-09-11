using System.Text;

namespace Hex1b.Automation;

internal sealed class TapeLexer(string text, string? sourceName, CancellationToken cancellationToken = default)
{
    private int _offset;
    private int _line = 1;
    private int _column = 1;

    internal static TapeTokenKind Classify(string value) => value switch
    {
        "true" or "false" => TapeTokenKind.Boolean,
        "ms" or "s" or "m" or "em" or "px" => TapeTokenKind.Unit,
        "Shell" or "FontFamily" or "FontSize" or "LetterSpacing" or "LineHeight" or
        "Framerate" or "TypingSpeed" or "Theme" or "PlaybackSpeed" or "Height" or
        "Width" or "Padding" or "LoopOffset" or "MarginFill" or "Margin" or
        "WindowBar" or "WindowBarSize" or "BorderRadius" or "CursorBlink" or
        "WaitTimeout" or "WaitPattern" => TapeTokenKind.Setting,
        "Set" or "Sleep" or "Type" or "Enter" or "Space" or "Backspace" or "Delete" or
        "Insert" or "Ctrl" or "Alt" or "Shift" or "Down" or "Left" or "Right" or "Up" or
        "PageUp" or "PageDown" or "ScrollUp" or "ScrollDown" or "Tab" or "Escape" or
        "End" or "Hide" or "Require" or "Show" or "Output" or "Wait" or "Source" or
        "Screenshot" or "Copy" or "Paste" or "Env" => TapeTokenKind.Keyword,
        _ => TapeTokenKind.String
    };

    internal TapeToken Next()
    {
        cancellationToken.ThrowIfCancellationRequested();
        while (Current is ' ' or '\t' or '\r' or '\n')
        {
            if (Current == '\n')
            {
                _line++;
                _column = 0;
            }
            Advance();
        }
        var start = _offset;
        var line = _line;
        var column = _column;
        var character = Current;
        var kind = character switch
        {
            '\0' => TapeTokenKind.EndOfFile,
            '@' => TapeTokenKind.At,
            '+' => TapeTokenKind.Plus,
            '-' => TapeTokenKind.Minus,
            '=' => TapeTokenKind.Equal,
            '%' => TapeTokenKind.Percent,
            '[' => TapeTokenKind.LeftBracket,
            ']' => TapeTokenKind.RightBracket,
            '^' => TapeTokenKind.Caret,
            '\\' => TapeTokenKind.Backslash,
            '#' => TapeTokenKind.Comment,
            '{' => TapeTokenKind.Json,
            '"' or '\'' or '`' => TapeTokenKind.String,
            '/' => TapeTokenKind.Regex,
            _ => TapeTokenKind.Illegal
        };
        string value;
        if (kind == TapeTokenKind.EndOfFile)
            value = "\0";
        else if (kind is TapeTokenKind.String or TapeTokenKind.Regex or TapeTokenKind.Comment or TapeTokenKind.Json)
        {
            Advance();
            var contentStart = _offset;
            var backslashes = 0;
            while (Current != '\0')
            {
                if (kind == TapeTokenKind.Json ? Current == '}' :
                    Current is '\n' or '\r' && (kind != TapeTokenKind.Regex || backslashes == 0) ||
                    kind == TapeTokenKind.String && Current == character ||
                    kind == TapeTokenKind.Regex && Current == '/' && backslashes % 2 == 0)
                    break;
                backslashes = Current == '\\' ? backslashes + 1 : 0;
                Advance();
            }
            value = text[contentStart.._offset];
            if (kind == TapeTokenKind.Json)
                value = "{" + value + "}";
            // Upstream accepts newline/EOF-terminated strings and regexes.
            if (kind != TapeTokenKind.Comment)
                Advance();
        }
        else if (char.IsAsciiDigit(character) || character == '.' && char.IsAsciiDigit(PeekCharacter()))
        {
            do { Advance(); } while (char.IsAsciiDigit(Current) || Current == '.');
            value = text[start.._offset];
            kind = TapeTokenKind.Number;
        }
        else if (char.IsAsciiLetter(character) || character == '.')
        {
            do { Advance(); } while (char.IsAsciiLetterOrDigit(Current) || Current is '.' or '-' or '_' or '/' or '%');
            value = text[start.._offset];
            kind = Classify(value);
        }
        else
        {
            Advance();
            value = text[start.._offset];
        }
        var span = new TapeSourceSpan(sourceName, start, _offset - start, line, column);
        return new(kind, value, text[start.._offset], span);
    }

    private char Current => _offset < text.Length ? text[_offset] : '\0';
    private char PeekCharacter() => _offset + 1 < text.Length ? text[_offset + 1] : '\0';

    private void Advance()
    {
        if ((_offset & 1023) == 0)
            cancellationToken.ThrowIfCancellationRequested();
        if (_offset >= text.Length)
        {
            _column++;
            return;
        }
        var length = char.IsHighSurrogate(Current) && char.IsLowSurrogate(PeekCharacter()) ? 2 : 1;
        _column += Encoding.UTF8.GetByteCount(text.AsSpan(_offset, length));
        _offset += length;
    }
}
