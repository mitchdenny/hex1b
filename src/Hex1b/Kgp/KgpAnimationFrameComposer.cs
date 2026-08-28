namespace Hex1b;

internal static class KgpAnimationFrameComposer
{
    internal static bool TryGetRgbaBufferLength(
        uint width,
        uint height,
        out int length)
    {
        var pixels = (ulong)width * height;
        if (pixels > (ulong)Array.MaxLength / 4)
        {
            length = 0;
            return false;
        }

        length = checked((int)(pixels * 4));
        return true;
    }

    internal static byte[] CreateRgbaCanvas(
        uint width,
        uint height,
        uint backgroundColor)
    {
        if (!TryGetRgbaBufferLength(width, height, out var length))
            throw new ArgumentOutOfRangeException(nameof(width));

        var canvas = new byte[length];
        if (backgroundColor == 0)
            return canvas;

        var red = (byte)(backgroundColor >> 24);
        var green = (byte)(backgroundColor >> 16);
        var blue = (byte)(backgroundColor >> 8);
        var alpha = (byte)backgroundColor;
        for (var offset = 0; offset < canvas.Length; offset += 4)
        {
            canvas[offset] = red;
            canvas[offset + 1] = green;
            canvas[offset + 2] = blue;
            canvas[offset + 3] = alpha;
        }

        return canvas;
    }

    internal static byte[] ConvertToRgba(
        ReadOnlySpan<byte> data,
        uint width,
        uint height,
        KgpFormat format)
    {
        if (!TryGetRgbaBufferLength(width, height, out var length))
            throw new ArgumentOutOfRangeException(nameof(width));

        if (format == KgpFormat.Rgba32)
        {
            if (data.Length != length)
                throw new ArgumentException("RGBA pixel data has an invalid length.", nameof(data));
            return data.ToArray();
        }

        if (format != KgpFormat.Rgb24)
            throw new ArgumentOutOfRangeException(nameof(format));

        var pixelCount = length / 4;
        if (data.Length != checked(pixelCount * 3))
            throw new ArgumentException("RGB pixel data has an invalid length.", nameof(data));

        var rgba = new byte[length];
        var sourceOffset = 0;
        var destinationOffset = 0;
        while (sourceOffset < data.Length)
        {
            rgba[destinationOffset] = data[sourceOffset];
            rgba[destinationOffset + 1] = data[sourceOffset + 1];
            rgba[destinationOffset + 2] = data[sourceOffset + 2];
            rgba[destinationOffset + 3] = byte.MaxValue;
            sourceOffset += 3;
            destinationOffset += 4;
        }

        return rgba;
    }

    internal static void Compose(
        Span<byte> destination,
        uint destinationWidth,
        uint destinationHeight,
        ReadOnlySpan<byte> source,
        uint sourceWidth,
        uint sourceHeight,
        KgpFormat sourceFormat,
        uint destinationX,
        uint destinationY,
        KgpParsedCommand.CompositionMode composition)
    {
        if (!TryGetRgbaBufferLength(
                destinationWidth,
                destinationHeight,
                out var destinationLength) ||
            destination.Length != destinationLength)
        {
            throw new ArgumentException(
                "The destination is not a complete RGBA canvas.",
                nameof(destination));
        }

        var sourceBytesPerPixel = sourceFormat switch
        {
            KgpFormat.Rgb24 => 3,
            KgpFormat.Rgba32 => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(sourceFormat)),
        };
        var sourceLength = checked((ulong)sourceWidth * sourceHeight *
            (uint)sourceBytesPerPixel);
        if (sourceLength > int.MaxValue || source.Length != (int)sourceLength)
        {
            throw new ArgumentException(
                "The source does not match its dimensions and format.",
                nameof(source));
        }

        if (destinationX >= destinationWidth ||
            destinationY >= destinationHeight ||
            sourceWidth == 0 ||
            sourceHeight == 0)
        {
            return;
        }

        var copyWidth = Math.Min(sourceWidth, destinationWidth - destinationX);
        var copyHeight = Math.Min(sourceHeight, destinationHeight - destinationY);
        var destinationStride = checked((int)destinationWidth * 4);
        var sourceStride = checked((int)sourceWidth * sourceBytesPerPixel);
        var destinationColumnOffset = checked((int)destinationX * 4);

        for (var row = 0; row < copyHeight; row++)
        {
            var destinationOffset = checked(
                ((int)destinationY + (int)row) * destinationStride +
                destinationColumnOffset);
            var sourceOffset = checked((int)row * sourceStride);
            for (var column = 0; column < copyWidth; column++)
            {
                var destinationPixel = destination.Slice(destinationOffset, 4);
                var sourcePixel = source.Slice(sourceOffset, sourceBytesPerPixel);
                if (composition == KgpParsedCommand.CompositionMode.Overwrite ||
                    sourceBytesPerPixel == 3)
                {
                    destinationPixel[0] = sourcePixel[0];
                    destinationPixel[1] = sourcePixel[1];
                    destinationPixel[2] = sourcePixel[2];
                    destinationPixel[3] = sourceBytesPerPixel == 4
                        ? sourcePixel[3]
                        : byte.MaxValue;
                }
                else
                {
                    AlphaBlend(destinationPixel, sourcePixel);
                }

                destinationOffset += 4;
                sourceOffset += sourceBytesPerPixel;
            }
        }
    }

    private static void AlphaBlend(
        Span<byte> destination,
        ReadOnlySpan<byte> source)
    {
        if (source[3] == 0)
            return;

        var destinationAlpha = destination[3] / 255f;
        var sourceAlpha = source[3] / 255f;
        var alpha = sourceAlpha + destinationAlpha * (1f - sourceAlpha);
        destination[3] = (byte)(255f * alpha);
        if (destination[3] == 0)
        {
            destination[0] = 0;
            destination[1] = 0;
            destination[2] = 0;
            return;
        }

        for (var channel = 0; channel < 3; channel++)
        {
            destination[channel] = (byte)(
                (source[channel] * sourceAlpha +
                 destination[channel] * destinationAlpha * (1f - sourceAlpha)) /
                alpha);
        }
    }
}
