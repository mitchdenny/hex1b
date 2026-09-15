using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using Hex1b.Tokens;

namespace Hex1b;

// HMP viewers render the producer's state; they must not repeat its protocol replies.
internal sealed class Hmp1OutputProjection
{
    internal const int MaximumHeaderBytes = 64 * 1024;

    private enum State { Ground, Escape, Utf8Control, Csi, ApcStart, KgpHeader, String, StringEscape }
    private enum StringKind { Osc, Dcs, Apc, Other }

    private State _state;
    private StringKind _stringKind;
    private readonly List<byte> _pending = [];
    private bool _discardString;
    private int _kgpControlOffset;
    private int _utf8Remaining;
    private byte _previous;

    internal ReadOnlyMemory<byte> Process(ReadOnlySpan<byte> bytes)
    {
        var output = new ArrayBufferWriter<byte>(Math.Max(bytes.Length, 1));
        foreach (var value in bytes)
        {
            var continuation = _utf8Remaining > 0 && value is >= 0x80 and <= 0xbf;
            var stringTerminator = value == 0x9c && (!continuation || _previous == 0xc2);
            ProcessByte(value, continuation, stringTerminator, output);
            _utf8Remaining = continuation ? _utf8Remaining - 1 : value switch
            {
                >= 0xc2 and <= 0xdf => 1,
                >= 0xe0 and <= 0xef => 2,
                >= 0xf0 and <= 0xf4 => 3,
                _ => 0,
            };
            _previous = value;
        }
        return output.WrittenMemory;
    }

    internal void Reset()
    {
        _state = State.Ground;
        _pending.Clear();
        _discardString = false;
        _utf8Remaining = 0;
        _previous = 0;
    }

    // Only bytes already emitted by the projection belong in a late viewer's seed.
    internal static ReadOnlyMemory<byte> ProjectContinuation(ReadOnlySpan<byte> pending) =>
        new Hmp1OutputProjection().Process(pending);

    private void ProcessByte(byte value, bool continuation, bool stringTerminator, ArrayBufferWriter<byte> output)
    {
        switch (_state)
        {
            case State.Ground:
                if (value == 0x1b)
                {
                    _pending.Add(value);
                    _state = State.Escape;
                }
                else if (value == 0xc2)
                {
                    _pending.Add(value);
                    _state = State.Utf8Control;
                }
                else
                {
                    WriteByte(output, value);
                    if (!continuation && value is 0x90 or 0x9d or 0x9f or 0x98 or 0x9e)
                        StartString(value);
                }
                break;

            case State.Utf8Control:
                if (value == 0x9f)
                {
                    _pending.Add(value);
                    _state = State.ApcStart;
                }
                else
                {
                    FlushPending(output);
                    if (value is 0x90 or 0x9d or 0x98 or 0x9e)
                    {
                        WriteByte(output, value);
                        StartString(value);
                    }
                    else
                    {
                        _state = State.Ground;
                        ProcessByte(value, continuation, stringTerminator, output);
                    }
                }
                break;

            case State.Escape:
                if (value == '[')
                {
                    _pending.Add(value);
                    _state = State.Csi;
                }
                else if (value == '_')
                {
                    _pending.Add(value);
                    _state = State.ApcStart;
                }
                else
                {
                    FlushPending(output);
                    _state = State.Ground;
                    if (value is (byte)']' or (byte)'P' or (byte)'X' or (byte)'^')
                    {
                        WriteByte(output, value);
                        StartString(value);
                    }
                    else
                        ProcessByte(value, continuation, stringTerminator, output);
                }
                break;

            case State.Csi:
                if (value == 0x1b || value is 0x18 or 0x1a)
                {
                    FlushPending(output);
                    _state = State.Ground;
                    ProcessByte(value, continuation, stringTerminator, output);
                    break;
                }
                AppendHeader(value);
                if (value is >= 0x40 and <= 0x7e)
                {
                    var query = false;
                    if (value is (byte)'c' or (byte)'t' or (byte)'n')
                    {
                        var sequence = Encoding.UTF8.GetString(CollectionsMarshal.AsSpan(_pending));
                        var tokens = AnsiTokenizer.Tokenize(sequence);
                        query = tokens.Count == 1 && tokens[0] is
                            DeviceAttributesQueryToken or
                            WindowOperationToken or
                            DeviceStatusReportToken { Type: DeviceStatusReportToken.StatusReport or DeviceStatusReportToken.CursorPositionReport };
                    }
                    if (!query)
                        FlushPending(output);
                    else
                        _pending.Clear();
                    _state = State.Ground;
                }
                break;

            case State.ApcStart:
                if (value == 'G')
                {
                    _pending.Add(value);
                    _kgpControlOffset = _pending.Count;
                    _state = State.KgpHeader;
                }
                else
                {
                    FlushPending(output);
                    _stringKind = StringKind.Apc;
                    _state = State.String;
                    ProcessByte(value, continuation, stringTerminator, output);
                }
                break;

            case State.KgpHeader:
                if (value == ';')
                {
                    EmitKgpHeader(output);
                    if (!_discardString)
                        WriteByte(output, value);
                    _state = State.String;
                }
                else if (stringTerminator || (_previous == 0x1b && value == '\\'))
                {
                    var prefixLength = _previous is 0xc2 or 0x1b ? 1 : 0;
                    if (prefixLength != 0)
                        _pending.RemoveAt(_pending.Count - 1);
                    EmitKgpHeader(output);
                    if (!_discardString)
                    {
                        if (prefixLength != 0)
                            WriteByte(output, _previous);
                        WriteByte(output, value);
                    }
                    _discardString = false;
                    _state = State.Ground;
                }
                else
                    AppendHeader(value);
                break;

            case State.String:
            case State.StringEscape:
                if (!_discardString)
                    WriteByte(output, value);
                if (stringTerminator || (_state == State.StringEscape && value == '\\') ||
                    (_stringKind == StringKind.Osc && value == 7) ||
                    (_stringKind == StringKind.Dcs && value is 0x9c or 0x18 or 0x1a))
                {
                    _state = State.Ground;
                    _discardString = false;
                }
                else
                    _state = value == 0x1b ? State.StringEscape : State.String;
                break;
        }
    }

    private void EmitKgpHeader(ArrayBufferWriter<byte> output)
    {
        _stringKind = StringKind.Apc;
        var header = CollectionsMarshal.AsSpan(_pending);
        var controls = Encoding.UTF8.GetString(header[_kgpControlOffset..]);
        if (!KgpCommandParser.TryParse(controls, out var command, out _))
        {
            FlushPending(output);
            return;
        }
        if (command is KgpParsedCommand.Query)
        {
            _discardString = true;
            _pending.Clear();
            return;
        }
        if (command.Quiet == KgpParsedCommand.QuietMode.SuppressAll)
        {
            FlushPending(output);
            return;
        }

        output.Write(header[.._kgpControlOffset]);
        var quietControls = string.Join(',', controls.Split(',').Where(pair =>
            pair.Length != 0 && !pair.StartsWith("q=", StringComparison.Ordinal)));
        if (quietControls.Length != 0)
            quietControls += ",";
        output.Write(Encoding.UTF8.GetBytes(quietControls + "q=2"));
        _pending.Clear();
    }

    private void StartString(byte introducer)
    {
        _stringKind = introducer switch
        {
            (byte)']' or 0x9d => StringKind.Osc,
            (byte)'P' or 0x90 => StringKind.Dcs,
            0x9f => StringKind.Apc,
            _ => StringKind.Other,
        };
        _state = State.String;
    }

    private void AppendHeader(byte value)
    {
        if (_pending.Count >= MaximumHeaderBytes)
            throw new InvalidDataException($"HMP1 output control header exceeds {MaximumHeaderBytes} bytes.");
        _pending.Add(value);
    }

    private void FlushPending(ArrayBufferWriter<byte> output)
    {
        output.Write(CollectionsMarshal.AsSpan(_pending));
        _pending.Clear();
    }

    private static void WriteByte(ArrayBufferWriter<byte> output, byte value)
    {
        output.GetSpan(1)[0] = value;
        output.Advance(1);
    }
}
