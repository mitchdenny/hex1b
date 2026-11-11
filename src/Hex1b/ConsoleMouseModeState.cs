using System.Text;
using Hex1b.Input;
using Hex1b.Tokens;

namespace Hex1b;

/// <summary>
/// Observes output mouse modes without interpreting control-string payloads.
/// </summary>
internal sealed class ConsoleMouseModeState
{
    private enum State { Ground, Escape, Csi, String, StringEscape, Osc, OscEscape, CsiIgnore }

    private State _state;
    private readonly StringBuilder _csi = new();
    private bool _x10;
    private bool _normal;
    private bool _button;
    private bool _any;
    private bool _sgr;
    private bool _utf8;
    private bool _urxvt;
    private bool _trackingChanged;

    internal TerminalMouseTracking Tracking => TerminalInputEncoder.MouseTracking(_x10, _normal, _button, _any);

    internal TerminalInputModes InputModes(int width, int height) => new(
        false, false, false, Tracking,
        _sgr ? TerminalMouseEncoding.Sgr :
            _urxvt ? TerminalMouseEncoding.Urxvt :
            _utf8 ? TerminalMouseEncoding.Utf8 : TerminalMouseEncoding.Legacy,
        width, height);

    internal bool Observe(ReadOnlySpan<byte> data)
    {
        _trackingChanged = false;
        foreach (var value in data)
        {
            if (value is 0x18 or 0x1a)
            {
                _state = State.Ground;
                _csi.Clear();
                continue;
            }
            switch (_state)
            {
                case State.String:
                case State.Osc:
                    if (value == 0x1b)
                        _state = _state == State.Osc ? State.OscEscape : State.StringEscape;
                    else if (value == 0x07 && _state == State.Osc)
                        _state = State.Ground;
                    break;
                case State.StringEscape:
                case State.OscEscape:
                    if (value == (byte)'\\')
                        _state = State.Ground;
                    else if (value != 0x1b)
                        _state = _state == State.OscEscape ? State.Osc : State.String;
                    break;
                case State.Escape:
                    _state = value switch
                    {
                        (byte)'[' => State.Csi,
                        (byte)']' => State.Osc,
                        (byte)'P' or (byte)'_' or (byte)'^' or (byte)'X' => State.String,
                        0x1b => State.Escape,
                        _ => State.Ground
                    };
                    _csi.Clear();
                    if (value == (byte)'c')
                        Reset();
                    break;
                case State.Csi:
                case State.CsiIgnore:
                    if (value == 0x1b)
                    {
                        _state = State.Escape;
                        _csi.Clear();
                        break;
                    }
                    if (_state == State.Csi)
                    {
                        if (_csi.Length < 128)
                            _csi.Append((char)value);
                        else
                            _state = State.CsiIgnore;
                    }
                    if (value is >= 0x40 and <= 0x7e)
                    {
                        if (_state == State.Csi)
                            ApplyCsi();
                        _state = State.Ground;
                        _csi.Clear();
                    }
                    break;
                default:
                    if (value == 0x1b)
                        _state = State.Escape;
                    break;
            }
        }
        return _trackingChanged;
    }

    private void ApplyCsi()
    {
        if (_csi.Length == 2 && _csi[0] == '!' && _csi[1] == 'p')
        {
            Reset();
            return;
        }
        if (_csi.Length < 3 || _csi[0] != '?' || _csi[^1] is not ('h' or 'l'))
            return;

        foreach (var token in AnsiTokenizer.Tokenize("\x1b[" + _csi))
        {
            if (token is not PrivateModeToken mode)
                continue;

            var previousTracking = Tracking;
            switch (mode.Mode)
            {
                case 9: _x10 = mode.Enable; break;
                case 1000: _normal = mode.Enable; break;
                case 1002: _button = mode.Enable; break;
                case 1003: _any = mode.Enable; break;
                case 1005: _utf8 = mode.Enable; break;
                case 1006: _sgr = mode.Enable; break;
                case 1015: _urxvt = mode.Enable; break;
            }
            _trackingChanged |= previousTracking != Tracking;
        }
    }

    internal void Reset()
    {
        _trackingChanged |= Tracking != TerminalMouseTracking.None;
        _state = State.Ground;
        _csi.Clear();
        _x10 = _normal = _button = _any = _sgr = _utf8 = _urxvt = false;
    }
}
