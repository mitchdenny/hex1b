using Hex1b.Tokens;

namespace Hex1b.Input;

// Input framing is separate from output parsing: paste contents are opaque and
// an unfinished host response must not retain an unbounded amount of input.
internal sealed class InputSequenceParser
{
    internal const int MaximumSequenceLength = 4096;
    private string _pending = "";
    private bool _inPaste;
    internal int PendingLength => _pending.Length;
    internal bool InPaste => _inPaste;
    internal bool NeedsEscapeTimeout => !_inPaste && _pending.Length > 0;

    internal IReadOnlyList<AnsiToken> Parse(string text, bool enhanced, bool flush = false)
    {
        text = _pending + text;
        _pending = "";
        var tokens = new List<AnsiToken>();
        var offset = 0;
        while (offset < text.Length)
        {
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
            if (text[offset] == '\x1b')
            {
                if (offset + 1 < text.Length && text[offset + 1] == '[')
                {
                    offset += 2;
                    while (offset < text.Length && text[offset] is >= '\x20' and <= '\x3f')
                        offset++;
                    if (offset < text.Length && text[offset] is not (>= '\x40' and <= '\x7e'))
                        continue; // Invalid CSI; resume at the interrupting character.
                    if (offset == text.Length && !flush &&
                        offset - start < MaximumSequenceLength)
                    {
                        _pending = text[start..];
                        break;
                    }
                    if (offset < text.Length)
                        offset++;
                    if (offset - start >= MaximumSequenceLength)
                        continue;
                }
                else if (Hex1bTerminal.TryFindEscapeSequenceEnd(text, offset, out var end))
                    offset = end;
                else if (!flush && text.Length - start < MaximumSequenceLength)
                {
                    _pending = text[start..];
                    break;
                }
                else
                    offset = text.Length;

                if (offset - start >= MaximumSequenceLength)
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
            else
            {
                while (offset < text.Length && text[offset] != '\x1b')
                    offset++;
                tokens.AddRange(AnsiTokenizer.Tokenize(text[start..offset]));
            }
        }
        return tokens;
    }
}
