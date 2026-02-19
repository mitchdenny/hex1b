using System.Text;

namespace Hex1b.Documents;

/// <summary>
/// Bidirectional mapping between raw byte offsets and character indices.
/// Built from actual document bytes (not re-encoded text), so the mapping
/// is accurate even when the bytes contain invalid UTF-8 sequences.
/// </summary>
public sealed class Utf8ByteMap
{
    // Per-character: the byte offset in the source bytes where that character starts.
    private readonly int[] _charToByteStart;
    // Per-character: the number of source bytes that produced that character.
    private readonly byte[] _charByteLength;

    /// <summary>Total number of bytes in the source.</summary>
    public int TotalBytes { get; }

    /// <summary>Total number of characters produced by decoding.</summary>
    public int CharCount => _charToByteStart.Length;

    /// <summary>
    /// Builds a byte↔char map by decoding the raw bytes as UTF-8.
    /// Tracks how many source bytes each decoded character consumed.
    /// </summary>
    public Utf8ByteMap(ReadOnlySpan<byte> bytes)
    {
        TotalBytes = bytes.Length;
        if (bytes.Length == 0)
        {
            _charToByteStart = [];
            _charByteLength = [];
            return;
        }

        var starts = new List<int>(bytes.Length);
        var lengths = new List<byte>(bytes.Length);

        var i = 0;
        while (i < bytes.Length)
        {
            starts.Add(i);
            var b = bytes[i];

            if (b < 0x80)
            {
                // ASCII: 1 byte → 1 char
                lengths.Add(1);
                i += 1;
            }
            else if ((b & 0xE0) == 0xC0)
            {
                // 2-byte sequence start
                var seqLen = ValidateSequence(bytes, i, 2);
                lengths.Add((byte)seqLen);
                i += seqLen;
            }
            else if ((b & 0xF0) == 0xE0)
            {
                // 3-byte sequence start
                var seqLen = ValidateSequence(bytes, i, 3);
                lengths.Add((byte)seqLen);
                i += seqLen;
            }
            else if ((b & 0xF8) == 0xF0)
            {
                // 4-byte sequence start (produces surrogate pair = 2 chars)
                var seqLen = ValidateSequence(bytes, i, 4);
                if (seqLen == 4)
                {
                    // Valid 4-byte sequence → 2 chars (surrogate pair)
                    // First char (high surrogate) maps to all 4 bytes
                    lengths.Add(4);
                    // Second char (low surrogate) maps to same position with 0 length
                    starts.Add(i);
                    lengths.Add(0);
                }
                else
                {
                    // Invalid: each consumed byte → 1 replacement char
                    lengths.Add(1);
                    for (var j = 1; j < seqLen; j++)
                    {
                        starts.Add(i + j);
                        lengths.Add(1);
                    }
                }
                i += seqLen;
            }
            else
            {
                // Invalid byte (continuation without leader, or 0xFE/0xFF)
                // → 1 replacement char consuming 1 byte
                lengths.Add(1);
                i += 1;
            }
        }

        _charToByteStart = starts.ToArray();
        _charByteLength = lengths.ToArray();
    }

    /// <summary>
    /// Builds a byte↔char map from a string by encoding to UTF-8 first.
    /// Use this overload only when the string was decoded from valid UTF-8
    /// (no byte-level edits). Prefer the <c>ReadOnlySpan&lt;byte&gt;</c>
    /// overload when raw document bytes are available.
    /// </summary>
    public Utf8ByteMap(string text)
        : this(Encoding.UTF8.GetBytes(text).AsSpan())
    {
    }

    /// <summary>
    /// Validates a multi-byte UTF-8 sequence starting at <paramref name="offset"/>.
    /// Returns the number of bytes that form a valid sequence (or the number of
    /// bytes consumed before the sequence became invalid, minimum 1).
    /// </summary>
    private static int ValidateSequence(ReadOnlySpan<byte> bytes, int offset, int expectedLen)
    {
        if (offset + expectedLen > bytes.Length)
        {
            // Truncated: consume only the bytes that exist
            return bytes.Length - offset;
        }

        for (var i = 1; i < expectedLen; i++)
        {
            if ((bytes[offset + i] & 0xC0) != 0x80)
            {
                // Invalid continuation byte — only consume up to the bad byte
                return i;
            }
        }

        return expectedLen;
    }

    /// <summary>
    /// Maps a byte offset to the character that contains it.
    /// Returns the character index and the byte's position within
    /// that character's source byte range.
    /// </summary>
    public (int charIndex, int byteWithinChar) ByteToChar(int byteOffset)
    {
        if (byteOffset < 0 || byteOffset >= TotalBytes)
            throw new ArgumentOutOfRangeException(nameof(byteOffset),
                $"Byte offset {byteOffset} is out of range [0..{TotalBytes}).");

        // Binary search for the character whose byte range contains byteOffset.
        var lo = 0;
        var hi = _charToByteStart.Length - 1;
        while (lo < hi)
        {
            var mid = lo + (hi - lo + 1) / 2;
            if (_charToByteStart[mid] <= byteOffset)
                lo = mid;
            else
                hi = mid - 1;
        }

        return (lo, byteOffset - _charToByteStart[lo]);
    }

    /// <summary>
    /// Returns the starting byte offset for the character at <paramref name="charIndex"/>.
    /// </summary>
    public int CharToByteStart(int charIndex)
    {
        if (charIndex < 0 || charIndex >= _charToByteStart.Length)
            throw new ArgumentOutOfRangeException(nameof(charIndex));
        return _charToByteStart[charIndex];
    }

    /// <summary>
    /// Returns the number of source bytes that produced the character at <paramref name="charIndex"/>.
    /// </summary>
    public int CharByteLength(int charIndex)
    {
        if (charIndex < 0 || charIndex >= _charByteLength.Length)
            throw new ArgumentOutOfRangeException(nameof(charIndex));
        return _charByteLength[charIndex];
    }
}
