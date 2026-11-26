namespace Hex1b;

/// <summary>
/// Splits prefetched console input into top-level control frames so capability probe
/// replies are recognised only at frame boundaries. Bracketed paste and string controls
/// (OSC, DCS, APC, SOS, PM) are opaque: reply-shaped bytes inside them are never reported
/// as separate frames.
/// </summary>
/// <remarks>
/// String controls may be introduced and terminated by 7-bit escapes (<c>ESC ]</c>,
/// <c>ESC \</c>), by UTF-8 encoded C1 controls (for example <c>C2 9D</c> and <c>C2 9C</c>),
/// or by raw 8-bit C1 bytes that are not part of a UTF-8 character. OSC may also end with
/// BEL. Scanning stops at the first incomplete frame so later bytes wait for more input.
/// C1 controls are recognised only for UTF-8 and single-byte input encodings: in other
/// multi-byte code pages (for example GBK or Shift-JIS) bytes 0x80–0x9F are parts of
/// characters, so only ESC-introduced frames are recognised there.
/// </remarks>
internal static class ConsoleProbeFrameScanner
{
    private const byte Esc = 0x1b;
    private const byte C1StringTerminator = 0x9c;

    /// <summary>How bytes 0x80 and above are interpreted.</summary>
    internal enum ByteMode
    {
        /// <summary>UTF-8: C1 controls are <c>C2 80</c>–<c>C2 9F</c> or stray 8-bit bytes.</summary>
        Utf8,

        /// <summary>A single-byte code page: bytes 0x80–0x9F are C1 controls.</summary>
        SingleByte,

        /// <summary>Another multi-byte code page: no C1 controls are recognised.</summary>
        MultiByte,
    }

    internal enum FrameKind
    {
        Csi,
        Osc,
        Dcs,
        Apc,
        Sos,
        Pm,
        BracketedPaste,
    }

    /// <summary>A complete top-level frame.</summary>
    /// <param name="Kind">The frame kind.</param>
    /// <param name="Start">Index of the first introducer byte.</param>
    /// <param name="End">Index one past the last terminator byte.</param>
    /// <param name="ContentStart">Index of the first byte after the introducer.</param>
    /// <param name="ContentEnd">
    /// Index one past the content: the terminator for string controls and paste, or the
    /// final byte for CSI.
    /// </param>
    /// <param name="SevenBit">Whether the frame was introduced by <c>ESC</c> rather than a C1 control.</param>
    internal readonly record struct Frame(FrameKind Kind, int Start, int End, int ContentStart, int ContentEnd, bool SevenBit);

    public static List<Frame> Scan(ReadOnlySpan<byte> span, ByteMode mode = ByteMode.Utf8)
    {
        ReadOnlySpan<byte> pasteStart = "\x1b[200~"u8;
        ReadOnlySpan<byte> pasteEnd = "\x1b[201~"u8;
        var frames = new List<Frame>();
        var i = 0;
        while (i < span.Length)
        {
            if (span[i] == Esc)
            {
                if (i + 1 >= span.Length)
                    break;

                var next = span[i + 1];
                if (next == (byte)'[')
                {
                    var j = i + 2;
                    while (j < span.Length && span[j] is >= 0x20 and <= 0x3f)
                        j++;
                    if (j >= span.Length)
                        break;
                    if (span[j] is not (>= 0x40 and <= 0x7e))
                    {
                        i = j;
                        continue;
                    }

                    var end = j + 1;
                    if (span[i..end].SequenceEqual(pasteStart))
                    {
                        var close = span[end..].IndexOf(pasteEnd);
                        if (close < 0)
                            break;
                        var closeStart = end + close;
                        frames.Add(new Frame(FrameKind.BracketedPaste, i, closeStart + pasteEnd.Length, end, closeStart, true));
                        i = closeStart + pasteEnd.Length;
                        continue;
                    }

                    frames.Add(new Frame(FrameKind.Csi, i, end, i + 2, j, true));
                    i = end;
                }
                else if (TryGetStringKind(next, out var kind))
                {
                    if (!TryFindStringEnd(span, i + 2, kind == FrameKind.Osc, mode, out var contentEnd, out var end))
                        break;
                    frames.Add(new Frame(kind, i, end, i + 2, contentEnd, true));
                    i = end;
                }
                else
                {
                    // A lone ESC (an Escape key press) may directly precede another sequence or
                    // a multi-byte character; otherwise ESC plus one byte is an Alt-modified key.
                    i += next == Esc || next >= 0x80 ? 1 : 2;
                }

                continue;
            }

            if (TryReadC1(span, i, mode, out var c1, out var c1Length, out var incomplete))
            {
                if (TryGetStringKind((byte)(c1 - 0x40), out var kind))
                {
                    if (!TryFindStringEnd(span, i + c1Length, kind == FrameKind.Osc, mode, out var contentEnd, out var end))
                        break;
                    frames.Add(new Frame(kind, i, end, i + c1Length, contentEnd, false));
                    i = end;
                }
                else
                {
                    i += c1Length;
                }

                continue;
            }

            if (incomplete)
                break;

            i += CharLength(span, i, mode);
        }

        return frames;
    }

    private static bool TryGetStringKind(byte introducer, out FrameKind kind)
    {
        kind = introducer switch
        {
            (byte)']' => FrameKind.Osc,
            (byte)'P' => FrameKind.Dcs,
            (byte)'_' => FrameKind.Apc,
            (byte)'X' => FrameKind.Sos,
            (byte)'^' => FrameKind.Pm,
            _ => FrameKind.Csi,
        };
        return kind != FrameKind.Csi;
    }

    private static bool TryFindStringEnd(
        ReadOnlySpan<byte> span, int start, bool allowBell, ByteMode mode, out int contentEnd, out int end)
    {
        var j = start;
        while (j < span.Length)
        {
            var b = span[j];
            if (allowBell && b == 0x07)
            {
                contentEnd = j;
                end = j + 1;
                return true;
            }

            if (b == Esc)
            {
                if (j + 1 >= span.Length)
                    break;
                if (span[j + 1] == (byte)'\\')
                {
                    contentEnd = j;
                    end = j + 2;
                    return true;
                }

                j++;
                continue;
            }

            if (TryReadC1(span, j, mode, out var c1, out var c1Length, out var incomplete))
            {
                if (c1 == C1StringTerminator)
                {
                    contentEnd = j;
                    end = j + c1Length;
                    return true;
                }

                j += c1Length;
                continue;
            }

            if (incomplete)
                break;

            j += CharLength(span, j, mode);
        }

        contentEnd = end = span.Length;
        return false;
    }

    /// <summary>
    /// Reads a C1 control at <paramref name="index"/>: UTF-8 encoded (<c>C2 80</c>–<c>C2 9F</c>)
    /// or a raw 8-bit byte that is not part of a character (UTF-8), or any byte 0x80–0x9F
    /// (single-byte code pages).
    /// </summary>
    private static bool TryReadC1(
        ReadOnlySpan<byte> span, int index, ByteMode mode, out byte code, out int length, out bool incomplete)
    {
        code = 0;
        length = 0;
        incomplete = false;
        if (mode == ByteMode.MultiByte)
            return false;

        var b = span[index];
        if (mode == ByteMode.Utf8 && b == 0xc2)
        {
            if (index + 1 >= span.Length)
            {
                incomplete = true;
                return false;
            }
            if (span[index + 1] is >= 0x80 and <= 0x9f)
            {
                code = span[index + 1];
                length = 2;
                return true;
            }
            return false;
        }

        if (b is >= 0x80 and <= 0x9f)
        {
            code = b;
            length = 1;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns how far to advance past the character at <paramref name="index"/>. For UTF-8
    /// this is the lead byte plus the continuation bytes actually present, so stray bytes
    /// advance one at a time. Other encodings advance one byte, which is safe because ESC is
    /// never a trail byte in the supported multi-byte code pages.
    /// </summary>
    private static int CharLength(ReadOnlySpan<byte> span, int index, ByteMode mode)
    {
        if (mode != ByteMode.Utf8)
            return 1;

        var expected = span[index] switch
        {
            >= 0xc2 and <= 0xdf => 2,
            >= 0xe0 and <= 0xef => 3,
            >= 0xf0 and <= 0xf4 => 4,
            _ => 1,
        };

        var length = 1;
        while (length < expected && index + length < span.Length && span[index + length] is >= 0x80 and <= 0xbf)
            length++;
        return length;
    }
}
