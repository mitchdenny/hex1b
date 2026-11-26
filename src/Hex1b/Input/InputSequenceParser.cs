using Hex1b.Tokens;

namespace Hex1b.Input;

// Input framing is separate from output parsing: paste contents are opaque and
// an unfinished host response must not retain an unbounded amount of input.
internal sealed class InputSequenceParser
{
    // Matches the Kitty codec's inclusive bound. An unterminated prefix of this length
    // can only complete beyond the bound, so it starts discarding instead of buffering.
    internal const int MaximumSequenceLength = 4096;
    private string _pending = "";
    private bool _inPaste;
    private Discard _discard;
    internal int PendingLength => _pending.Length;
    internal bool InPaste => _inPaste;
    internal bool IsDiscarding => _discard != Discard.None;
    // Discarding never needs a timer: it ends only at a terminator or resync boundary.
    internal bool NeedsEscapeTimeout => !_inPaste && _discard == Discard.None && _pending.Length > 0;

    // Leading tokens of the last Parse result that began in input carried over from an
    // earlier call. Callers stamp those with the older arrival epoch.
    internal int CarriedTokenCount { get; private set; }

    // True when the retained pending prefix began in carried input.
    internal bool PendingIsCarried { get; private set; }

    // An oversized or timed-out sequence is dropped without retaining its text, but the
    // framer remembers it is inside that sequence so its remainder is not replayed as
    // keys, even across escape timeouts. CSI discard ends at its final byte or any other
    // non-parameter character. Control-string discard ends at ST, BEL (OSC only), CAN, or
    // SUB, or resynchronizes at ESC, which is then parsed as the start of new input, as
    // VT-family parsers do. Only a lone ESC is retained while discarding.
    private enum Discard { None, Csi, String, StringOrBell }

    // Unparsed text following a keyboard flags report when Parse was asked to stop there.
    // A confirming report changes how the rest of the same read must be decoded, so the
    // caller observes it and then parses this remainder as new input in the updated mode.
    internal string? Remainder { get; private set; }

    internal static bool IsKeyboardFlagsReport(string sequence)
        => sequence.Length > 3 && sequence.StartsWith("\x1b[?", StringComparison.Ordinal) && sequence[^1] == 'u';

    internal IReadOnlyList<AnsiToken> Parse(
        string text, bool enhanced, bool flush = false, bool stopAfterKeyboardReport = false)
    {
        var carried = _pending.Length;
        text = _pending + text;
        _pending = "";
        var tokens = new List<AnsiToken>();
        CarriedTokenCount = -1;
        PendingIsCarried = false;
        Remainder = null;
        var offset = 0;
        while (offset < text.Length)
        {
            if (CarriedTokenCount < 0 && offset >= carried)
                CarriedTokenCount = tokens.Count;

            if (_discard != Discard.None)
            {
                if (!ContinueDiscard(text, ref offset, carried))
                    break;
                continue;
            }

            if (_inPaste)
            {
                const string endMarker = "\x1b[201~";
                var end = text.IndexOf(endMarker, offset, StringComparison.Ordinal);
                if (end < 0)
                {
                    var keep = Math.Min(endMarker.Length - 1, text.Length - offset);
                    while (keep > 0 && !endMarker.StartsWith(text[^keep..], StringComparison.Ordinal))
                        keep--;
                    if (text.Length - keep > offset)
                        tokens.Add(new TextToken(text[offset..(text.Length - keep)]));
                    Retain(text, text.Length - keep, carried);
                    break;
                }
                if (end > offset)
                    tokens.Add(new TextToken(text[offset..end]));
                tokens.Add(new SpecialKeyToken(201));
                _inPaste = false;
                offset = end + endMarker.Length;
                continue;
            }

            var start = offset;
            if (text[offset] == '\x1b' && offset + 1 < text.Length && text[offset + 1] == '[')
            {
                offset += 2;
                while (offset < text.Length && text[offset] is >= '\x20' and <= '\x3f')
                    offset++;
                if (offset < text.Length && text[offset] is not (>= '\x40' and <= '\x7e'))
                    continue; // Invalid CSI; resume at the interrupting character.
                if (offset == text.Length)
                {
                    if (!flush && offset - start < MaximumSequenceLength)
                    {
                        Retain(text, start, carried);
                        break;
                    }
                    // A bare CSI introducer times out as legacy Alt+[; anything longer
                    // keeps discarding so its late remainder cannot leak as keys.
                    if (!flush || offset - start > 2)
                    {
                        _discard = Discard.Csi;
                        continue;
                    }
                }
                else
                    offset++;
            }
            else if (IsIntroducer(text[offset]))
            {
                if (Hex1bTerminal.TryFindEscapeSequenceEnd(text, offset, out var end))
                    offset = end;
                else if (ControlStringDiscard(text, start) is { } discard &&
                    (!flush || text.Length - start > IntroducerLength(text, start)) &&
                    (flush || text.Length - start >= MaximumSequenceLength))
                {
                    _discard = discard;
                    offset = start + IntroducerLength(text, start);
                    continue;
                }
                else if (flush)
                    offset = text.Length;
                else
                {
                    Retain(text, start, carried);
                    break;
                }
            }
            else
            {
                while (offset < text.Length && !IsIntroducer(text[offset]))
                    offset++;
                tokens.AddRange(AnsiTokenizer.Tokenize(text[start..offset]));
                continue;
            }

            if (offset - start > MaximumSequenceLength)
                continue;
            var sequence = text[start..offset];
            if (sequence == "\x1b[200~")
                _inPaste = true;
            // Preserve CPR's original spelling until dispatch: an effective-flags
            // report earlier in this same read can change its F3 ambiguity.
            if (KittyKeyboardInput.IsCursorPositionReport(sequence))
                tokens.Add(new UnrecognizedSequenceToken(sequence));
            else if (IsKeyboardFlagsReport(sequence))
            {
                tokens.Add(new UnrecognizedSequenceToken(sequence));
                if (stopAfterKeyboardReport && offset < text.Length)
                {
                    Remainder = text[offset..];
                    break;
                }
            }
            else
                tokens.AddRange(enhanced
                    ? AnsiTokenizer.TokenizeEnhancedInput(sequence)
                    : AnsiTokenizer.Tokenize(sequence));
        }
        if (CarriedTokenCount < 0)
            CarriedTokenCount = tokens.Count;
        return tokens;
    }

    private void Retain(string text, int start, int carried)
    {
        _pending = text[start..];
        PendingIsCarried = start < carried;
    }

    // C1 OSC, APC, and DCS are control-string introducers on input, as ESC is.
    private static bool IsIntroducer(char c) => c is '\x1b' or '\x9d' or '\x9f' or '\x90';

    private static int IntroducerLength(string text, int start) => text[start] == '\x1b' ? 2 : 1;

    private static Discard? ControlStringDiscard(string text, int start) => text[start] switch
    {
        '\x9d' => Discard.StringOrBell,
        '\x90' or '\x9f' => Discard.String,
        '\x1b' when start + 1 < text.Length => text[start + 1] switch
        {
            ']' => Discard.StringOrBell,
            'P' or '_' => Discard.String,
            _ => null
        },
        _ => null
    };

    // Returns false when the remaining text was consumed while still discarding.
    private bool ContinueDiscard(string text, ref int offset, int carried)
    {
        if (_discard == Discard.Csi)
        {
            while (offset < text.Length && text[offset] is >= '\x20' and <= '\x3f')
                offset++;
            if (offset == text.Length)
                return false;
            if (text[offset] is >= '\x40' and <= '\x7e')
                offset++;
            _discard = Discard.None;
            return true;
        }

        while (offset < text.Length)
        {
            var c = text[offset];
            if (c is '\x9c' or '\x18' or '\x1a' || c == '\x07' && _discard == Discard.StringOrBell)
            {
                offset++;
                _discard = Discard.None;
                return true;
            }
            if (c == '\x1b')
            {
                if (offset + 1 == text.Length)
                {
                    // Possibly the first half of ESC \; keep it across timeouts.
                    Retain(text, offset, carried);
                    offset = text.Length;
                    return false;
                }
                _discard = Discard.None;
                if (text[offset + 1] == '\\')
                    offset += 2;
                return true;
            }
            offset++;
        }
        return false;
    }
}