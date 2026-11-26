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
    internal bool NeedsEscapeTimeout => !_inPaste && (_pending.Length > 0 || _discard != Discard.None);

    // An oversized sequence is dropped without retaining its text, but the framer
    // still remembers it is inside that sequence so its remainder is not replayed
    // as keys. Discard ends at the sequence terminator, a safe interruption, or flush.
    private enum Discard { None, Csi, String, StringOrBell }

    internal IReadOnlyList<AnsiToken> Parse(string text, bool enhanced, bool flush = false)
    {
        text = _pending + text;
        _pending = "";
        var tokens = new List<AnsiToken>();
        var offset = 0;
        while (offset < text.Length)
        {
            if (_discard != Discard.None)
            {
                if (!ContinueDiscard(text, ref offset, flush))
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
                    _pending = text[(text.Length - keep)..];
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
                if (offset == text.Length && !flush)
                {
                    if (offset - start < MaximumSequenceLength)
                        _pending = text[start..];
                    else
                        _discard = Discard.Csi;
                    break;
                }
                if (offset < text.Length)
                    offset++;
            }
            else if (IsIntroducer(text[offset]))
            {
                if (Hex1bTerminal.TryFindEscapeSequenceEnd(text, offset, out var end))
                    offset = end;
                else if (flush)
                    offset = text.Length;
                else if (text.Length - start < MaximumSequenceLength)
                {
                    _pending = text[start..];
                    break;
                }
                else
                {
                    // Only control strings can outgrow the bound while unterminated.
                    _discard = text[start] == '\x9d' || text[start] == '\x1b' && text[start + 1] == ']'
                        ? Discard.StringOrBell : Discard.String;
                    offset = start + (text[start] == '\x1b' ? 2 : 1);
                    continue;
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
            else
                tokens.AddRange(enhanced
                    ? AnsiTokenizer.TokenizeEnhancedInput(sequence)
                    : AnsiTokenizer.Tokenize(sequence));
        }
        if (flush)
            _discard = Discard.None;
        return tokens;
    }

    // C1 OSC, APC, and DCS are control-string introducers on input, as ESC is.
    private static bool IsIntroducer(char c) => c is '\x1b' or '\x9d' or '\x9f' or '\x90';

    // Returns false when the remaining text was consumed while still discarding.
    private bool ContinueDiscard(string text, ref int offset, bool flush)
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
            if (c == '\x9c' || c == '\x07' && _discard == Discard.StringOrBell)
            {
                offset++;
                _discard = Discard.None;
                return true;
            }
            if (c == '\x1b')
            {
                if (offset + 1 == text.Length)
                {
                    if (!flush)
                        _pending = "\x1b";
                    offset = text.Length;
                    return false;
                }
                if (text[offset + 1] == '\\')
                {
                    offset += 2;
                    _discard = Discard.None;
                    return true;
                }
            }
            offset++;
        }
        return false;
    }
}