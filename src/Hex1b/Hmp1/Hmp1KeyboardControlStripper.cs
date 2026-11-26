using System.Buffers;

namespace Hex1b;

/// <summary>
/// Fragment-safe filter that removes Kitty keyboard stack controls
/// (<c>CSI [?=&gt;&lt;] … u</c>) from a byte stream, for peers or consumers
/// that did not negotiate Kitty keyboard presentation.
/// </summary>
internal sealed class Hmp1KeyboardControlStripper : IHmp1KeyboardControlSink
{
    private readonly Hmp1KeyboardControlScanner _scanner = new();
    private readonly ArrayBufferWriter<byte> _output = new();

    /// <summary>Whether bytes are held back awaiting completion.</summary>
    public bool HasPending => _scanner.HasPending;

    public void Reset() => _scanner.Reset();

    /// <summary>
    /// Filters <paramref name="data"/>. Returns the input instance unchanged
    /// when nothing was removed or held back.
    /// </summary>
    public ReadOnlyMemory<byte> Process(ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty)
            return data;
        _output.ResetWrittenCount();
        var hadPending = _scanner.HasPending;
        _scanner.Process(data.Span, this);
        if (!hadPending && !_scanner.HasPending && _output.WrittenCount == data.Length)
            return data;
        return _output.WrittenSpan.ToArray();
    }

    /// <summary>Filters a complete, self-contained buffer with a fresh scanner state.</summary>
    public static ReadOnlyMemory<byte> StripComplete(ReadOnlyMemory<byte> data)
    {
        var stripper = new Hmp1KeyboardControlStripper();
        return stripper.Process(data);
    }

    void IHmp1KeyboardControlSink.Text(ReadOnlySpan<byte> bytes) => _output.Write(bytes);
    void IHmp1KeyboardControlSink.Keyboard(ReadOnlySpan<byte> sequence) { }
    void IHmp1KeyboardControlSink.ScreenMode(ReadOnlySpan<byte> sequence) => _output.Write(sequence);
    void IHmp1KeyboardControlSink.FullReset(ReadOnlySpan<byte> sequence) => _output.Write(sequence);
}
