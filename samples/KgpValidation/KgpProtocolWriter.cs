using System.Text;
using Hex1b;

namespace KgpValidation;

/// <summary>
/// Builds the raw ANSI and Kitty graphics byte stream consumed by
/// <see cref="Hex1bTerminal"/>.
/// </summary>
/// <remarks>
/// This deliberately exposes control strings at scenario call sites. The sample
/// is a protocol debugging aid, so an agent should be able to see the exact KGP
/// keys involved without stepping through a higher-level graphics abstraction.
/// </remarks>
internal sealed class KgpProtocolWriter
{
    private readonly StringBuilder _buffer = new();

    public void Raw(string value) => _buffer.Append(value);

    public void MoveTo(int row, int column)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(row, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(column, 1);
        _buffer.Append("\x1b[")
            .Append(row)
            .Append(';')
            .Append(column)
            .Append('H');
    }

    public void TextAt(
        int row,
        int column,
        string value,
        int maximumLength = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(value);
        MoveTo(row, column);

        var sanitized = value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\x1b', '?');
        _buffer.Append(sanitized.AsSpan(
            0,
            Math.Min(sanitized.Length, maximumLength)));
    }

    public void Kgp(string controlData)
        => Kgp(controlData, ReadOnlySpan<byte>.Empty);

    public void Kgp(string controlData, ReadOnlySpan<byte> payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(controlData);
        if (controlData.Contains(';') || controlData.Contains('\x1b'))
        {
            throw new ArgumentException(
                "KGP control data cannot contain payload or escape delimiters.",
                nameof(controlData));
        }

        _buffer.Append("\x1b_G").Append(controlData);
        if (!payload.IsEmpty)
        {
            _buffer.Append(';');
            _buffer.Append(Convert.ToBase64String(payload));
        }
        _buffer.Append("\x1b\\");
    }

    public void ChunkedTransmit(
        uint imageId,
        int width,
        int height,
        KgpFormat format,
        ReadOnlySpan<byte> payload,
        int rawChunkSize = 3072)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rawChunkSize, 3);
        if (rawChunkSize % 3 != 0)
        {
            throw new ArgumentException(
                "Every non-final Base64 chunk must contain a multiple of three bytes.",
                nameof(rawChunkSize));
        }

        var offset = 0;
        while (offset < payload.Length)
        {
            var count = Math.Min(rawChunkSize, payload.Length - offset);
            var isFinal = offset + count == payload.Length;
            var controls = offset == 0
                ? $"a=t,f={(int)format},s={width},v={height},i={imageId},m={(isFinal ? 0 : 1)},q=2"
                : $"m={(isFinal ? 0 : 1)},q=2";
            Kgp(controls, payload.Slice(offset, count));
            offset += count;
        }
    }

    public byte[] ToUtf8Bytes() => Encoding.UTF8.GetBytes(_buffer.ToString());
}
