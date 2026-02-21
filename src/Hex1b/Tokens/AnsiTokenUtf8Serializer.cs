using System.Buffers;
using System.Buffers.Text;
using System.Text;
using Hex1b.Input;

namespace Hex1b.Tokens;

/// <summary>
/// Serializes ANSI tokens directly to UTF-8 bytes to avoid allocating intermediate strings.
/// </summary>
public static class AnsiTokenUtf8Serializer
{
    private static readonly Encoding Utf8 = Encoding.UTF8;

    /// <summary>
    /// Serializes a list of tokens into a UTF-8 byte buffer.
    /// </summary>
    public static ReadOnlyMemory<byte> Serialize(IEnumerable<AnsiToken> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        var initialCapacity = tokens is IReadOnlyCollection<AnsiToken> c
            ? Math.Clamp(c.Count * 4, 256, 128 * 1024)
            : 1024;

        var writer = new ArrayBufferWriter<byte>(initialCapacity);
        foreach (var token in tokens)
        {
            WriteToken(writer, token);
        }
        return writer.WrittenMemory;
    }

    /// <summary>
    /// Serializes a single token into a UTF-8 byte buffer.
    /// </summary>
    public static ReadOnlyMemory<byte> Serialize(AnsiToken token)
    {
        ArgumentNullException.ThrowIfNull(token);

        var writer = new ArrayBufferWriter<byte>(64);
        WriteToken(writer, token);
        return writer.WrittenMemory;
    }

    private static void WriteToken(ArrayBufferWriter<byte> writer, AnsiToken token)
    {
        switch (token)
        {
            case TextToken t:
                WriteUtf8(writer, t.Text);
                return;

            case ControlCharacterToken c:
                WriteControlCharacter(writer, c);
                return;

            case SgrToken sgr:
                WriteEscLeftBracket(writer);
                WriteUtf8(writer, sgr.Parameters);
                WriteByte(writer, (byte)'m');
                return;

            case CursorPositionToken pos:
                WriteCursorPosition(writer, pos);
                return;

            case CursorShapeToken shape:
                WriteEscLeftBracket(writer);
                WriteInt(writer, shape.Shape);
                WriteByte(writer, (byte)' ');
                WriteByte(writer, (byte)'q');
                return;

            case CursorMoveToken move:
                WriteCursorMove(writer, move);
                return;

            case CursorColumnToken col:
                WriteEscLeftBracket(writer);
                if (col.Column != 1)
                    WriteInt(writer, col.Column);
                WriteByte(writer, (byte)'G');
                return;

            case CursorRowToken row:
                WriteEscLeftBracket(writer);
                if (row.Row != 1)
                    WriteInt(writer, row.Row);
                WriteByte(writer, (byte)'d');
                return;

            case ClearScreenToken clear:
            {
                WriteEscLeftBracket(writer);
                var clearCode = (int)clear.Mode;
                if (clearCode != 0)
                    WriteInt(writer, clearCode);
                WriteByte(writer, (byte)'J');
                return;
            }

            case ClearLineToken clear:
            {
                WriteEscLeftBracket(writer);
                var clearCode = (int)clear.Mode;
                if (clearCode != 0)
                    WriteInt(writer, clearCode);
                WriteByte(writer, (byte)'K');
                return;
            }

            case ScrollRegionToken scroll:
                WriteEscLeftBracket(writer);
                if (!(scroll.Top == 1 && scroll.Bottom == 0))
                {
                    WriteInt(writer, scroll.Top);
                    WriteByte(writer, (byte)';');
                    WriteInt(writer, scroll.Bottom);
                }
                WriteByte(writer, (byte)'r');
                return;

            case ScrollUpToken su:
                WriteEscLeftBracket(writer);
                if (su.Count != 1)
                    WriteInt(writer, su.Count);
                WriteByte(writer, (byte)'S');
                return;

            case ScrollDownToken sd:
                WriteEscLeftBracket(writer);
                if (sd.Count != 1)
                    WriteInt(writer, sd.Count);
                WriteByte(writer, (byte)'T');
                return;

            case InsertLinesToken il:
                WriteEscLeftBracket(writer);
                if (il.Count != 1)
                    WriteInt(writer, il.Count);
                WriteByte(writer, (byte)'L');
                return;

            case DeleteLinesToken dl:
                WriteEscLeftBracket(writer);
                if (dl.Count != 1)
                    WriteInt(writer, dl.Count);
                WriteByte(writer, (byte)'M');
                return;

            case InsertCharacterToken ich:
                WriteEscLeftBracket(writer);
                if (ich.Count != 1)
                    WriteInt(writer, ich.Count);
                WriteByte(writer, (byte)'@');
                return;

            case DeleteCharacterToken dch:
                WriteEscLeftBracket(writer);
                if (dch.Count != 1)
                    WriteInt(writer, dch.Count);
                WriteByte(writer, (byte)'P');
                return;

            case EraseCharacterToken ech:
                WriteEscLeftBracket(writer);
                if (ech.Count != 1)
                    WriteInt(writer, ech.Count);
                WriteByte(writer, (byte)'X');
                return;

            case RepeatCharacterToken rep:
                WriteEscLeftBracket(writer);
                if (rep.Count != 1)
                    WriteInt(writer, rep.Count);
                WriteByte(writer, (byte)'b');
                return;

            case IndexToken:
                WriteByte(writer, 0x1b);
                WriteByte(writer, (byte)'D');
                return;

            case ReverseIndexToken:
                WriteByte(writer, 0x1b);
                WriteByte(writer, (byte)'M');
                return;

            case CharacterSetToken cs:
                WriteByte(writer, 0x1b);
                WriteByte(writer, (byte)(cs.Target == 0 ? '(' : ')'));
                WriteByte(writer, (byte)cs.Charset);
                return;

            case KeypadModeToken kp:
                WriteByte(writer, 0x1b);
                WriteByte(writer, (byte)(kp.Application ? '=' : '>'));
                return;

            case LeftRightMarginToken lrm:
                WriteEscLeftBracket(writer);
                WriteInt(writer, lrm.Left);
                WriteByte(writer, (byte)';');
                WriteInt(writer, lrm.Right);
                WriteByte(writer, (byte)'s');
                return;

            case SaveCursorToken save:
                WriteByte(writer, 0x1b);
                if (save.UseDec)
                {
                    WriteByte(writer, (byte)'7');
                }
                else
                {
                    WriteByte(writer, (byte)'[');
                    WriteByte(writer, (byte)'s');
                }
                return;

            case RestoreCursorToken restore:
                WriteByte(writer, 0x1b);
                if (restore.UseDec)
                {
                    WriteByte(writer, (byte)'8');
                }
                else
                {
                    WriteByte(writer, (byte)'[');
                    WriteByte(writer, (byte)'u');
                }
                return;

            case PrivateModeToken pm:
                WriteEscLeftBracket(writer);
                WriteByte(writer, (byte)'?');
                WriteInt(writer, pm.Mode);
                WriteByte(writer, (byte)(pm.Enable ? 'h' : 'l'));
                return;

            case OscToken osc:
                WriteOsc(writer, osc);
                return;

            case DcsToken dcs:
                WriteByte(writer, 0x1b);
                WriteByte(writer, (byte)'P');
                WriteUtf8(writer, dcs.Payload);
                WriteByte(writer, 0x1b);
                WriteByte(writer, (byte)'\\');
                return;

            case Ss3Token ss3:
                WriteByte(writer, 0x1b);
                WriteByte(writer, (byte)'O');
                WriteByte(writer, (byte)ss3.Character);
                return;

            case SgrMouseToken mouse:
                WriteEscLeftBracket(writer);
                WriteByte(writer, (byte)'<');
                WriteInt(writer, mouse.RawButtonCode);
                WriteByte(writer, (byte)';');
                WriteInt(writer, mouse.X + 1);
                WriteByte(writer, (byte)';');
                WriteInt(writer, mouse.Y + 1);
                WriteByte(writer, (byte)(mouse.Action == MouseAction.Up ? 'm' : 'M'));
                return;

            case SpecialKeyToken special:
                WriteEscLeftBracket(writer);
                WriteInt(writer, special.KeyCode);
                if (special.Modifiers != 1)
                {
                    WriteByte(writer, (byte)';');
                    WriteInt(writer, special.Modifiers);
                }
                WriteByte(writer, (byte)'~');
                return;

            case UnrecognizedSequenceToken unrec:
                WriteUtf8(writer, unrec.Sequence);
                return;

            case DeviceStatusReportToken dsr:
                WriteEscLeftBracket(writer);
                WriteInt(writer, dsr.Type);
                WriteByte(writer, (byte)'n');
                return;

            default:
                throw new ArgumentException($"Unknown token type: {token.GetType().Name}", nameof(token));
        }
    }

    private static void WriteCursorPosition(ArrayBufferWriter<byte> writer, CursorPositionToken token)
    {
        // Mirror AnsiTokenSerializer.SerializeCursorPosition() exactly, including OriginalParams.
        if (token.OriginalParams is not null)
        {
            WriteEscLeftBracket(writer);
            if (string.IsNullOrEmpty(token.OriginalParams))
            {
                WriteByte(writer, (byte)'H');
                return;
            }

            WriteUtf8(writer, token.OriginalParams);
            WriteByte(writer, (byte)'H');
            return;
        }

        WriteEscLeftBracket(writer);

        if (token.Row == 1 && token.Column == 1)
        {
            WriteByte(writer, (byte)'H');
            return;
        }

        WriteInt(writer, token.Row);

        if (token.Column != 1)
        {
            WriteByte(writer, (byte)';');
            WriteInt(writer, token.Column);
        }

        WriteByte(writer, (byte)'H');
    }

    private static void WriteCursorMove(ArrayBufferWriter<byte> writer, CursorMoveToken token)
    {
        WriteEscLeftBracket(writer);

        var command = token.Direction switch
        {
            CursorMoveDirection.Up => (byte)'A',
            CursorMoveDirection.Down => (byte)'B',
            CursorMoveDirection.Forward => (byte)'C',
            CursorMoveDirection.Back => (byte)'D',
            CursorMoveDirection.NextLine => (byte)'E',
            CursorMoveDirection.PreviousLine => (byte)'F',
            _ => (byte)'A'
        };

        if (token.Count != 1)
            WriteInt(writer, token.Count);

        WriteByte(writer, command);
    }

    private static void WriteOsc(ArrayBufferWriter<byte> writer, OscToken token)
    {
        // Preserve original terminator style (ESC \ vs BEL)
        // Mirror AnsiTokenSerializer.SerializeOsc() exactly.
        WriteByte(writer, 0x1b);
        WriteByte(writer, (byte)']');

        WriteUtf8(writer, token.Command);
        WriteByte(writer, (byte)';');

        if (token.Command == "8")
        {
            WriteUtf8(writer, token.Parameters);
            WriteByte(writer, (byte)';');
            WriteUtf8(writer, token.Payload);
            WriteOscTerminator(writer, token.UseEscBackslash);
            return;
        }

        if (!string.IsNullOrEmpty(token.Parameters))
        {
            WriteUtf8(writer, token.Parameters);
            WriteByte(writer, (byte)';');
            WriteUtf8(writer, token.Payload);
            WriteOscTerminator(writer, token.UseEscBackslash);
            return;
        }

        WriteUtf8(writer, token.Payload);
        WriteOscTerminator(writer, token.UseEscBackslash);
    }

    private static void WriteOscTerminator(ArrayBufferWriter<byte> writer, bool useEscBackslash)
    {
        if (useEscBackslash)
        {
            WriteByte(writer, 0x1b);
            WriteByte(writer, (byte)'\\');
        }
        else
        {
            WriteByte(writer, 0x07);
        }
    }

    private static void WriteControlCharacter(ArrayBufferWriter<byte> writer, ControlCharacterToken token)
    {
        switch (token.Character)
        {
            case '\n':
            case '\r':
            case '\t':
                WriteByte(writer, (byte)token.Character);
                return;
            default:
                WriteCharUtf8(writer, token.Character);
                return;
        }
    }

    private static void WriteEscLeftBracket(ArrayBufferWriter<byte> writer)
    {
        var span = writer.GetSpan(2);
        span[0] = 0x1b;
        span[1] = (byte)'[';
        writer.Advance(2);
    }

    private static void WriteByte(ArrayBufferWriter<byte> writer, byte value)
    {
        var span = writer.GetSpan(1);
        span[0] = value;
        writer.Advance(1);
    }

    private static void WriteInt(ArrayBufferWriter<byte> writer, int value)
    {
        var span = writer.GetSpan(11); // int32 max length
        if (!Utf8Formatter.TryFormat(value, span, out var written))
            throw new InvalidOperationException("Failed to format integer.");
        writer.Advance(written);
    }

    private static void WriteUtf8(ArrayBufferWriter<byte> writer, string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        // Common case in terminal output: ASCII-only text (escape sequences, digits, most UI chars).
        // Copy directly to avoid UTF-8 transcoding overhead per token.
        var dest = writer.GetSpan(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch > 0x7F)
            {
                // Commit the ASCII prefix, then encode the remainder properly.
                writer.Advance(i);
                WriteUtf8Slow(writer, text.AsSpan(i));
                return;
            }
            dest[i] = (byte)ch;
        }

        writer.Advance(text.Length);
    }

    private static void WriteCharUtf8(ArrayBufferWriter<byte> writer, char ch)
    {
        Span<char> chars = stackalloc char[1];
        chars[0] = ch;
        var byteCount = Utf8.GetByteCount(chars);
        var span = writer.GetSpan(byteCount);
        var written = Utf8.GetBytes(chars, span);
        writer.Advance(written);
    }

    private static void WriteUtf8Slow(ArrayBufferWriter<byte> writer, ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
            return;

        var byteCount = Utf8.GetByteCount(text);
        var span = writer.GetSpan(byteCount);
        var written = Utf8.GetBytes(text, span);
        writer.Advance(written);
    }
}
