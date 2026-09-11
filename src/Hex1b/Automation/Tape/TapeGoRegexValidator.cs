using System.Globalization;

namespace Hex1b.Automation;

// This validates syntax, not matching semantics. In particular, using System.Text.RegularExpressions
// here would both admit unsupported constructs and reject valid Go Unicode scripts and flags.
internal sealed class TapeGoRegexValidator(string pattern)
{
    private int _position;
    private string? _error;

    internal static bool TryValidate(string pattern, out string? error)
    {
        var parser = new TapeGoRegexValidator(pattern);
        parser.ParseExpression(false, 0);
        error = parser._error;
        return error is null;
    }

    private int ParseExpression(bool grouped, int depth)
    {
        if (depth >= 1000)
        {
            Fail("Expression nesting exceeds Go's limit.");
            return 1;
        }
        var hasAtom = false;
        var repeated = false;
        var atomRepeat = 1;
        var largestRepeat = 1;
        while (_position < pattern.Length && _error is null)
        {
            var character = pattern[_position];
            if (character == ')')
            {
                _position++;
                if (!grouped)
                    Fail("Unexpected closing parenthesis.");
                return Math.Max(largestRepeat, atomRepeat);
            }
            if (character == '|')
            {
                _position++;
                largestRepeat = Math.Max(largestRepeat, atomRepeat);
                atomRepeat = 1;
                hasAtom = repeated = false;
                continue;
            }
            var quantifier = character is '*' or '+' or '?';
            var count = 1;
            var countEnd = _position;
            if (character == '{')
                quantifier = TryRepeat(out count, out countEnd);
            if (quantifier)
            {
                if (!hasAtom || repeated)
                {
                    Fail(!hasAtom ? "Repetition has no operand." : "Invalid nested repetition operator.");
                    break;
                }
                _position = character == '{' ? countEnd : _position + 1;
                if (_position < pattern.Length && pattern[_position] == '?')
                    _position++;
                if (character == '{')
                {
                    if (count > 1000 || atomRepeat > 1000 / Math.Max(1, count))
                        Fail("Counted repetition exceeds Go's limit of 1000.");
                    atomRepeat *= Math.Max(1, count);
                }
                repeated = true;
                continue;
            }
            largestRepeat = Math.Max(largestRepeat, atomRepeat);
            if (character == '(')
            {
                _position++;
                if (_position < pattern.Length && pattern[_position] == '?' && ParseGroupPrefix())
                    continue;
                atomRepeat = ParseExpression(true, depth + 1);
            }
            else if (character == '[')
            {
                ParseClass();
                atomRepeat = 1;
            }
            else if (character == '\\')
            {
                if (_position + 1 < pattern.Length && pattern[_position + 1] == 'Q')
                {
                    _position += 2;
                    var end = pattern.IndexOf("\\E", _position, StringComparison.Ordinal);
                    var quotedLength = (end < 0 ? pattern.Length : end) - _position;
                    _position = end < 0 ? pattern.Length : end + 2;
                    if (quotedLength == 0)
                        continue;
                }
                else
                    ParseEscape(false);
                atomRepeat = 1;
            }
            else
            {
                ReadRune();
                atomRepeat = 1;
            }
            hasAtom = true;
            repeated = false;
        }
        if (grouped && _error is null)
            Fail("Unclosed parenthesis.");
        return Math.Max(largestRepeat, atomRepeat);
    }

    // Returns true for a flags-only group, which contributes no atom.
    private bool ParseGroupPrefix()
    {
        _position++;
        if (At("P<") || At("<"))
        {
            _position += At("P<") ? 2 : 1;
            var start = _position;
            while (_position < pattern.Length && pattern[_position] != '>')
                _position++;
            var name = pattern.AsSpan(start, _position - start);
            if (_position == pattern.Length || name.Length == 0 || !IsCaptureName(name))
                Fail("Invalid named capture.");
            else
                _position++;
            return false;
        }
        var sawFlag = false;
        var subtracting = false;
        var flagsAfterMinus = false;
        while (_position < pattern.Length)
        {
            var character = pattern[_position++];
            if (character is 'i' or 'm' or 's' or 'U')
            {
                sawFlag = true;
                flagsAfterMinus |= subtracting;
            }
            else if (character == '-' && !subtracting)
                subtracting = true;
            else if (character is ':' or ')')
            {
                if (subtracting && !flagsAfterMinus || character == ')' && !sawFlag)
                    Fail("Invalid inline flags.");
                return character == ')';
            }
            else
            {
                Fail("Unsupported group construct or inline flag.");
                return false;
            }
        }
        Fail("Unclosed group prefix.");
        return false;
    }

    private void ParseClass()
    {
        _position++;
        if (_position < pattern.Length && pattern[_position] == '^')
            _position++;
        var first = true;
        while (_position < pattern.Length && _error is null)
        {
            if (pattern[_position] == ']' && !first)
            {
                _position++;
                return;
            }
            first = false;
            if (At("[:"))
            {
                var end = pattern.IndexOf(":]", _position + 2, StringComparison.Ordinal);
                if (end >= 0)
                {
                    var name = pattern[(_position + 2)..end].TrimStart('^');
                    if (name is not ("alnum" or "alpha" or "ascii" or "blank" or "cntrl" or "digit" or
                        "graph" or "lower" or "print" or "punct" or "space" or "upper" or "word" or "xdigit") ||
                        pattern.AsSpan(_position + 2, end - _position - 2).StartsWith("^^", StringComparison.Ordinal))
                        Fail("Unknown POSIX character class.");
                    _position = end + 2;
                    continue;
                }
            }
            var start = ParseClassCharacter();
            if (start is null)
                continue;
            if (_position + 1 < pattern.Length && pattern[_position] == '-' && pattern[_position + 1] != ']')
            {
                _position++;
                var end = ParseClassCharacter();
                if (end is null || start > end)
                    Fail("Invalid character class range.");
            }
        }
        if (_error is null)
            Fail("Unclosed character class.");
    }

    private int? ParseClassCharacter() =>
        _position < pattern.Length && pattern[_position] == '\\' ? ParseEscape(true) : ReadRune();

    private int? ParseEscape(bool inClass)
    {
        _position++;
        if (_position == pattern.Length)
        {
            Fail("Trailing backslash.");
            return null;
        }
        var character = pattern[_position++];
        if (character is 'd' or 'D' or 's' or 'S' or 'w' or 'W')
            return null;
        if (!inClass && character is 'A' or 'z' or 'b' or 'B')
            return null;
        if (character is 'p' or 'P')
        {
            string name;
            if (_position < pattern.Length && pattern[_position] == '{')
            {
                var end = pattern.IndexOf('}', ++_position);
                if (end < 0)
                {
                    Fail("Unclosed Unicode class.");
                    return null;
                }
                name = pattern[_position..end];
                _position = end + 1;
                if (name.StartsWith('^'))
                    name = name[1..];
            }
            else
                name = _position < pattern.Length ? pattern[_position++].ToString() : "";
            if (!IsUnicodeClass(name))
                Fail("Unknown Unicode character class.");
            return null;
        }
        if (character is >= '0' and <= '7')
        {
            if (character != '0' && (_position == pattern.Length || pattern[_position] is < '0' or > '7'))
            {
                Fail("Backreferences are not supported.");
                return null;
            }
            var value = character - '0';
            for (var digits = 1; digits < 3 && _position < pattern.Length && pattern[_position] is >= '0' and <= '7'; digits++)
                value = value * 8 + pattern[_position++] - '0';
            return value;
        }
        if (character == 'x')
        {
            var braced = _position < pattern.Length && pattern[_position] == '{';
            if (braced)
                _position++;
            var start = _position;
            var value = 0;
            while (_position < pattern.Length && char.IsAsciiHexDigit(pattern[_position]) &&
                (braced || _position - start < 2))
            {
                var digit = int.Parse(pattern.AsSpan(_position++, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                if (value > 0x10ffff / 16)
                {
                    Fail("Hexadecimal escape exceeds the Unicode range.");
                    return null;
                }
                value = value * 16 + digit;
            }
            if (value > 0x10ffff || _position == start || !braced && _position - start != 2 ||
                braced && (_position == pattern.Length || pattern[_position++] != '}'))
                Fail("Invalid hexadecimal escape.");
            return value;
        }
        var escaped = character switch
        {
            'a' => '\a', 'f' => '\f', 't' => '\t', 'n' => '\n', 'r' => '\r', 'v' => '\v',
            _ => character
        };
        if (escaped == character && (character > 127 || char.IsAsciiLetterOrDigit(character)))
            Fail($"Invalid escape '\\{character}'.");
        return escaped;
    }

    private bool TryRepeat(out int maximum, out int end)
    {
        maximum = 1;
        end = _position;
        var cursor = _position + 1;
        if (!ReadCount(ref cursor, out var minimum))
            return false;
        maximum = minimum;
        if (cursor < pattern.Length && pattern[cursor] == ',')
        {
            cursor++;
            if (cursor < pattern.Length && pattern[cursor] == '}')
                maximum = minimum;
            else if (!ReadCount(ref cursor, out maximum))
                return false;
        }
        if (cursor == pattern.Length || pattern[cursor] != '}')
            return false;
        end = cursor + 1;
        if (minimum > maximum)
            Fail("Repetition minimum exceeds maximum.");
        if (minimum > 1000 || maximum > 1000)
            Fail("Counted repetition exceeds Go's limit of 1000.");
        return true;
    }

    private bool ReadCount(ref int cursor, out int value)
    {
        value = 0;
        var start = cursor;
        while (cursor < pattern.Length && char.IsAsciiDigit(pattern[cursor]))
        {
            value = Math.Min(1001, value * 10 + pattern[cursor++] - '0');
        }
        return cursor > start && (cursor - start == 1 || pattern[start] != '0');
    }

    private int ReadRune()
    {
        if (_position == pattern.Length)
        {
            Fail("Missing character.");
            return 0;
        }
        var character = pattern[_position++];
        if (char.IsHighSurrogate(character) && _position < pattern.Length && char.IsLowSurrogate(pattern[_position]))
            return char.ConvertToUtf32(character, pattern[_position++]);
        return character;
    }

    private bool At(string value) => pattern.AsSpan(_position).StartsWith(value, StringComparison.Ordinal);
    private void Fail(string message) => _error ??= message;

    private static bool IsCaptureName(ReadOnlySpan<char> name)
    {
        foreach (var character in name)
            if (!char.IsAsciiLetterOrDigit(character) && character != '_')
                return false;
        return true;
    }

    private static bool IsUnicodeClass(string name)
    {
        if (name.Length == 0)
            return false;
        const string names =
            " Any C Cc Cf Co Cs L Ll Lm Lo Lt Lu M Mc Me Mn N Nd Nl No P Pc Pd Pe Pf Pi Po Ps S Sc Sk Sm So Z Zl Zp Zs " +
            "Adlam Ahom Anatolian_Hieroglyphs Arabic Armenian Avestan Balinese Bamum Bassa_Vah Batak Bengali Bhaiksuki Bopomofo Brahmi Braille Buginese Buhid " +
            "Canadian_Aboriginal Carian Caucasian_Albanian Chakma Cham Cherokee Chorasmian Common Coptic Cuneiform Cypriot Cypro_Minoan Cyrillic Deseret Devanagari " +
            "Dives_Akuru Dogra Duployan Egyptian_Hieroglyphs Elbasan Elymaic Ethiopic Georgian Glagolitic Gothic Grantha Greek Gujarati Gunjala_Gondi Gurmukhi Han " +
            "Hangul Hanifi_Rohingya Hanunoo Hatran Hebrew Hiragana Imperial_Aramaic Inherited Inscriptional_Pahlavi Inscriptional_Parthian Javanese Kaithi Kannada " +
            "Katakana Kawi Kayah_Li Kharoshthi Khitan_Small_Script Khmer Khojki Khudawadi Lao Latin Lepcha Limbu Linear_A Linear_B Lisu Lycian Lydian Mahajani Makasar " +
            "Malayalam Mandaic Manichaean Marchen Masaram_Gondi Medefaidrin Meetei_Mayek Mende_Kikakui Meroitic_Cursive Meroitic_Hieroglyphs Miao Modi Mongolian Mro " +
            "Multani Myanmar Nabataean Nag_Mundari Nandinagari New_Tai_Lue Newa Nko Nushu Nyiakeng_Puachue_Hmong Ogham Ol_Chiki Old_Hungarian Old_Italic " +
            "Old_North_Arabian Old_Permic Old_Persian Old_Sogdian Old_South_Arabian Old_Turkic Old_Uyghur Oriya Osage Osmanya Pahawh_Hmong Palmyrene Pau_Cin_Hau " +
            "Phags_Pa Phoenician Psalter_Pahlavi Rejang Runic Samaritan Saurashtra Sharada Shavian Siddham SignWriting Sinhala Sogdian Sora_Sompeng Soyombo Sundanese " +
            "Syloti_Nagri Syriac Tagalog Tagbanwa Tai_Le Tai_Tham Tai_Viet Takri Tamil Tangsa Tangut Telugu Thaana Thai Tibetan Tifinagh Tirhuta Toto Ugaritic Vai " +
            "Vithkuqi Wancho Warang_Citi Yezidi Yi Zanabazar_Square ";
        return names.Contains(" " + name + " ", StringComparison.Ordinal);
    }
}
