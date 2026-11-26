using System.Buffers;
using System.Text;
using Hex1b.Input;
using Hex1b.Tokens;

namespace Hex1b;

// The remote stack is virtual. Only one entry at a time belongs to this viewer
// on the physical host, and it is popped before switching screens or detaching.
internal sealed class Hmp1KeyboardPresentation
{
    private readonly KittyKeyboardMode _main = new();
    private readonly KittyKeyboardMode _alternate = new();
    private readonly Hmp1OutputProjection _projection;
    private bool _inAlternate;
    private bool _ownsHostEntry;

    internal Hmp1KeyboardPresentation()
    {
        _projection = new Hmp1OutputProjection(ProjectCsi, projectOwnedProtocols: false, ProjectEscape);
    }

    internal ReadOnlyMemory<byte> Process(ReadOnlySpan<byte> bytes, bool stateSync)
    {
        var output = new ArrayBufferWriter<byte>();
        if (stateSync)
        {
            _projection.Reset();
            _main.Reset();
            _alternate.Reset();
            if (_ownsHostEntry)
                output.Write("\x1b[=0u"u8);
        }
        output.Write(_projection.Process(bytes).Span);
        return output.WrittenMemory;
    }

    internal ReadOnlyMemory<byte> Complete()
    {
        var pending = _projection.Complete();
        if (!_ownsHostEntry)
            return pending;
        _ownsHostEntry = false;
        return (byte[])[.. pending.Span, .. "\x1b[<u"u8];
    }

    internal static ReadOnlyMemory<byte>? StripKeyboardCommand(ReadOnlyMemory<byte> sequence)
    {
        var mode = new KittyKeyboardMode();
        return mode.Apply(Encoding.ASCII.GetString(sequence.Span), out var query) && !query
            ? ReadOnlyMemory<byte>.Empty
            : default(ReadOnlyMemory<byte>?);
    }

    private ReadOnlyMemory<byte>? ProjectCsi(ReadOnlyMemory<byte> bytes)
    {
        var sequence = Encoding.ASCII.GetString(bytes.Span);
        var mode = _inAlternate ? _alternate : _main;
        if (mode.Apply(sequence, out var query) && !query)
        {
            var operation = _ownsHostEntry ? '=' : '>';
            _ownsHostEntry = true;
            return Encoding.ASCII.GetBytes($"\x1b[{operation}{mode.Flags}u");
        }

        if (!sequence.StartsWith("\x1b[?", StringComparison.Ordinal) ||
            sequence[^1] is not ('h' or 'l'))
            return default(ReadOnlyMemory<byte>?);
        var screen = _inAlternate;
        var switchesScreen = false;
        foreach (var token in AnsiTokenizer.Tokenize(sequence))
        {
            if (token is PrivateModeToken { Mode: 47 or 1047 or 1049 } privateMode)
            {
                screen = privateMode.Enable;
                switchesScreen = true;
            }
        }
        if (!switchesScreen)
            return default(ReadOnlyMemory<byte>?);

        _inAlternate = screen;
        if (!_ownsHostEntry)
            return default(ReadOnlyMemory<byte>?);
        var flags = (_inAlternate ? _alternate : _main).Flags;
        return Encoding.ASCII.GetBytes($"\x1b[<u{sequence}\x1b[>{flags}u");
    }

    private ReadOnlyMemory<byte>? ProjectEscape(ReadOnlyMemory<byte> bytes)
    {
        _main.Reset();
        _alternate.Reset();
        _inAlternate = false;
        return _ownsHostEntry
            ? "\u001bc\u001b[>0u"u8.ToArray()
            : default(ReadOnlyMemory<byte>?);
    }
}
