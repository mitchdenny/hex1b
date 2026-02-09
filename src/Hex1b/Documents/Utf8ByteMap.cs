using System.Text;

namespace Hex1b.Documents;

/// <summary>
/// Bidirectional mapping between UTF-8 byte offsets and character indices.
/// Built from a string; immutable after construction. Rebuild when the
/// document text changes.
/// </summary>
public sealed class Utf8ByteMap
{
    // Per-character: the byte offset where that character's UTF-8 encoding starts.
    private readonly int[] _charToByteStart;
    // Per-character: the number of UTF-8 bytes for that character.
    private readonly byte[] _charByteLength;

    /// <summary>Total number of UTF-8 bytes in the mapped text.</summary>
    public int TotalBytes { get; }

    /// <summary>Total number of characters in the mapped text.</summary>
    public int CharCount => _charToByteStart.Length;

    /// <summary>
    /// Builds a byte↔char map from the given text.
    /// </summary>
    public Utf8ByteMap(string text)
    {
        var len = text.Length;
        _charToByteStart = new int[len];
        _charByteLength = new byte[len];

        var byteOffset = 0;
        for (var i = 0; i < len; i++)
        {
            _charToByteStart[i] = byteOffset;
            var byteLen = Encoding.UTF8.GetByteCount(text.AsSpan(i, 1));
            _charByteLength[i] = (byte)byteLen;
            byteOffset += byteLen;
        }

        TotalBytes = byteOffset;
    }

    /// <summary>
    /// Maps a byte offset to the character that contains it.
    /// Returns the character index and the byte's position within
    /// that character's UTF-8 sequence (0 for the first byte, etc.).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="byteOffset"/> is negative or ≥ <see cref="TotalBytes"/>.
    /// </exception>
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
    /// Returns the number of UTF-8 bytes that encode the character at <paramref name="charIndex"/>.
    /// </summary>
    public int CharByteLength(int charIndex)
    {
        if (charIndex < 0 || charIndex >= _charByteLength.Length)
            throw new ArgumentOutOfRangeException(nameof(charIndex));
        return _charByteLength[charIndex];
    }

    /// <summary>
    /// Returns the full UTF-8 byte sequence for the character at <paramref name="charIndex"/>,
    /// extracted from the provided byte array of the full document.
    /// </summary>
    public ReadOnlySpan<byte> GetCharBytes(int charIndex, ReadOnlySpan<byte> allBytes)
    {
        var start = CharToByteStart(charIndex);
        var len = CharByteLength(charIndex);
        return allBytes.Slice(start, len);
    }
}
