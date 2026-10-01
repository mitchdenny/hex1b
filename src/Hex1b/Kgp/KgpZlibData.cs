using System.IO.Compression;
using System.Security.Cryptography;

namespace Hex1b;

internal static class KgpZlibData
{
    internal readonly record struct Validation(
        int Length, uint Width, uint Height, byte[] Hash);

    internal static bool TryValidate(
        byte[] encoded,
        KgpFormat format,
        uint width,
        uint height,
        long maximumPixels,
        long maximumDecodedBytes,
        out Validation validation,
        out string error)
    {
        validation = default;
        error = "";
        var limit = Math.Min(Array.MaxLength, maximumDecodedBytes);
        long expectedLength = 0;
        if (format != KgpFormat.Png)
        {
            if (!ValidDimensions(width, height, maximumPixels))
            {
                error = "EFBIG:Image dimensions exceed the raster limit";
                return false;
            }
            expectedLength = checked((long)width * height *
                (format == KgpFormat.Rgb24 ? 3 : 4));
            if (expectedLength > limit)
            {
                error = "ENOSPC:Decoded image exceeds storage capacity";
                return false;
            }
            limit = expectedLength;
        }

        try
        {
            using var input = new CompleteInputStream(encoded);
            using var inflater = new ZLibStream(input, CompressionMode.Decompress);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            Span<byte> scratch = stackalloc byte[8192];
            Span<byte> pngHeader = stackalloc byte[33];
            var headerLength = 0;
            long length = 0;
            while (true)
            {
                // Read one byte beyond the bound, including after the expected
                // output, so completion and the zlib checksum are validated.
                var requested = (int)Math.Min(scratch.Length, Math.Max(1, limit - length + 1));
                var count = inflater.Read(scratch[..requested]);
                if (count == 0)
                    break;
                length = checked(length + count);
                if (length > limit)
                {
                    error = "EFBIG:Too much decoded image data";
                    return false;
                }
                hash.AppendData(scratch[..count]);
                if (format == KgpFormat.Png && headerLength < pngHeader.Length)
                {
                    var copied = Math.Min(count, pngHeader.Length - headerLength);
                    scratch[..copied].CopyTo(pngHeader[headerLength..]);
                    headerLength += copied;
                    if (headerLength == pngHeader.Length &&
                        (!KgpPngMetadata.TryReadDimensions(pngHeader, out width, out height) ||
                         !ValidDimensions(width, height, maximumPixels)))
                    {
                        error = "EINVAL:Invalid or oversized PNG dimensions";
                        return false;
                    }
                }
            }

            if (format == KgpFormat.Png ? headerLength < pngHeader.Length : length != expectedLength)
            {
                error = "ENODATA:Insufficient decoded image data";
                return false;
            }
            validation = new Validation(checked((int)length), width, height, hash.GetHashAndReset());
            return true;
        }
        catch (InvalidDataException)
        {
            error = "EINVAL:Invalid or incomplete zlib image data";
            return false;
        }
        catch (IOException)
        {
            // The in-memory inflater reports unsupported preset dictionaries as
            // an internal ZLibException (IOException), not InvalidDataException.
            error = "EINVAL:Invalid or unsupported zlib image data";
            return false;
        }
        catch (OutOfMemoryException)
        {
            error = "ENOMEM:Out of memory";
            return false;
        }
    }

    internal static bool ValidDimensions(uint width, uint height, long maximumPixels)
        => width > 0 && height > 0 &&
           (ulong)width * height <= (ulong)maximumPixels;

    internal static byte[] Materialize(byte[] encoded, int length)
    {
        var result = new byte[length];
        using var input = new CompleteInputStream(encoded);
        using var inflater = new ZLibStream(input, CompressionMode.Decompress);
        inflater.ReadExactly(result);
        return result;
    }

    // ZLibStream may accept a truncated checksum when its source returns EOF.
    // Completed uploads must instead fail if the inflater needs missing input.
    // A completed first member may leave bounded trailing bytes uninterpreted.
    private sealed class CompleteInputStream(byte[] data) : Stream
    {
        private int _position;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => data.Length;
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }
        public override int Read(byte[] buffer, int offset, int count)
            => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            if (buffer.IsEmpty)
                return 0;
            if (_position == data.Length)
                throw new InvalidDataException("Incomplete zlib member.");
            var count = Math.Min(buffer.Length, data.Length - _position);
            data.AsSpan(_position, count).CopyTo(buffer);
            _position += count;
            return count;
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
