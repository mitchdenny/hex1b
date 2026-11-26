namespace Hex1b;

/// <summary>
/// Fragment-safe classifier for the 7-bit controls that affect Kitty keyboard
/// presentation: keyboard stack controls, alternate-screen switches, and RIS.
/// </summary>
/// <remarks>
/// Incomplete escape and CSI sequences are held across calls. String controls
/// (OSC, DCS, APC, PM, SOS) are passed through without classifying their
/// contents. CSI sequences longer than <see cref="MaxCsiLength"/> are passed
/// through unclassified. 8-bit C1 introducers are not recognized.
/// </remarks>
internal sealed class Hmp1KeyboardControlScanner
{
    internal const int MaxCsiLength = 256;

    private const byte Esc = 0x1B;
    private const byte Can = 0x18;
    private const byte Sub = 0x1A;

    private enum State { Ground, Escape, Csi, CsiOverflow, String, StringEscape }

    private readonly List<byte> _pending = [];
    private State _state;
    private bool _osc;
    private bool _stringC2;

    /// <summary>Whether bytes are currently held back awaiting completion.</summary>
    public bool HasPending => _pending.Count > 0;

    /// <summary>Discards held bytes and returns to ground state.</summary>
    public void Reset()
    {
        _pending.Clear();
        _state = State.Ground;
        _osc = false;
        _stringC2 = false;
    }

    public void Process(ReadOnlySpan<byte> data, IHmp1KeyboardControlSink sink)
    {
        var run = 0;
        for (var i = 0; i < data.Length; i++)
        {
            var b = data[i];
            switch (_state)
            {
                case State.Ground:
                    if (b == Esc)
                    {
                        Flush(data, run, i, sink);
                        BeginEscape();
                    }
                    break;

                case State.Escape:
                    if (b == (byte)'[')
                    {
                        _pending.Add(b);
                        _state = State.Csi;
                    }
                    else if (b is (byte)']' or (byte)'P' or (byte)'_' or (byte)'^' or (byte)'X')
                    {
                        _pending.Add(b);
                        EmitPendingText(sink);
                        _state = State.String;
                        _osc = b == (byte)']';
                        _stringC2 = false;
                        run = i + 1;
                    }
                    else if (b == (byte)'c')
                    {
                        _pending.Add(b);
                        sink.FullReset(PendingSpan());
                        _pending.Clear();
                        _state = State.Ground;
                        run = i + 1;
                    }
                    else if (b == Esc)
                    {
                        EmitPendingText(sink);
                        BeginEscape();
                    }
                    else
                    {
                        _pending.Add(b);
                        EmitPendingText(sink);
                        _state = State.Ground;
                        run = i + 1;
                    }
                    break;

                case State.Csi:
                    if (b == Esc)
                    {
                        EmitPendingText(sink);
                        BeginEscape();
                    }
                    else if (b is Can or Sub)
                    {
                        _pending.Add(b);
                        EmitPendingText(sink);
                        _state = State.Ground;
                        run = i + 1;
                    }
                    else if (b is >= 0x40 and <= 0x7E)
                    {
                        _pending.Add(b);
                        Classify(sink);
                        _pending.Clear();
                        _state = State.Ground;
                        run = i + 1;
                    }
                    else
                    {
                        _pending.Add(b);
                        if (_pending.Count > MaxCsiLength)
                        {
                            EmitPendingText(sink);
                            _state = State.CsiOverflow;
                            run = i + 1;
                        }
                    }
                    break;

                case State.CsiOverflow:
                    if (b == Esc)
                    {
                        Flush(data, run, i, sink);
                        BeginEscape();
                    }
                    else if (b is Can or Sub or >= 0x40 and <= 0x7E)
                    {
                        _state = State.Ground;
                    }
                    break;

                case State.String:
                    if (b == Esc)
                    {
                        Flush(data, run, i, sink);
                        _pending.Clear();
                        _pending.Add(Esc);
                        _state = State.StringEscape;
                    }
                    else if ((b == 0x07 && _osc) || b is Can or Sub || (_stringC2 && b == 0x9C))
                    {
                        _state = State.Ground;
                    }
                    _stringC2 = b == 0xC2;
                    break;

                case State.StringEscape:
                    if (b == (byte)'\\')
                    {
                        _pending.Add(b);
                        EmitPendingText(sink);
                        _state = State.Ground;
                        run = i + 1;
                    }
                    else
                    {
                        // ESC inside a string aborts it and begins a new escape.
                        _state = State.Escape;
                        i--;
                    }
                    break;
            }
        }

        if (_state is State.Ground or State.String or State.CsiOverflow)
            Flush(data, run, data.Length, sink);
    }

    private void BeginEscape()
    {
        _pending.Clear();
        _pending.Add(Esc);
        _state = State.Escape;
    }

    private static void Flush(ReadOnlySpan<byte> data, int start, int end, IHmp1KeyboardControlSink sink)
    {
        if (end > start)
            sink.Text(data[start..end]);
    }

    private ReadOnlySpan<byte> PendingSpan()
        => System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_pending);

    private void EmitPendingText(IHmp1KeyboardControlSink sink)
    {
        if (_pending.Count > 0)
            sink.Text(PendingSpan());
        _pending.Clear();
    }

    private void Classify(IHmp1KeyboardControlSink sink)
    {
        var seq = PendingSpan();
        var body = seq[2..^1];
        var final = seq[^1];
        if (IsKeyboardControl(body, final))
            sink.Keyboard(seq);
        else if (IsScreenSwitch(body, final))
            sink.ScreenMode(seq);
        else
            sink.Text(seq);
    }

    internal static bool IsKeyboardControl(ReadOnlySpan<byte> body, byte final)
    {
        if (final != (byte)'u' || body.IsEmpty || body[0] is not ((byte)'?' or (byte)'=' or (byte)'>' or (byte)'<'))
            return false;
        foreach (var c in body[1..])
        {
            if (c is not ((>= (byte)'0' and <= (byte)'9') or (byte)';' or (byte)':'))
                return false;
        }
        return true;
    }

    private static bool IsScreenSwitch(ReadOnlySpan<byte> body, byte final)
    {
        if (final is not ((byte)'h' or (byte)'l') || body.IsEmpty || body[0] != (byte)'?')
            return false;
        var rest = body[1..];
        while (!rest.IsEmpty)
        {
            var sep = rest.IndexOf((byte)';');
            var param = sep < 0 ? rest : rest[..sep];
            if (param.SequenceEqual("47"u8) || param.SequenceEqual("1047"u8) || param.SequenceEqual("1049"u8))
                return true;
            rest = sep < 0 ? default : rest[(sep + 1)..];
        }
        return false;
    }
}
