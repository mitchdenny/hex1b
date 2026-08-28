using System.Buffers.Binary;

namespace Hex1b;

internal static class KgpPngMetadata
{
    private static ReadOnlySpan<byte> Signature =>
    [
        0x89, (byte)'P', (byte)'N', (byte)'G',
        0x0D, 0x0A, 0x1A, 0x0A,
    ];

    internal static bool TryReadDimensions(
        ReadOnlySpan<byte> data,
        out uint width,
        out uint height)
    {
        width = 0;
        height = 0;
        if (data.Length < 33 ||
            !data[..8].SequenceEqual(Signature) ||
            BinaryPrimitives.ReadUInt32BigEndian(data[8..12]) != 13 ||
            !data[12..16].SequenceEqual("IHDR"u8) ||
            BinaryPrimitives.ReadUInt32BigEndian(data[29..33]) !=
                ComputeCrc32(data[12..29]))
        {
            return false;
        }

        width = BinaryPrimitives.ReadUInt32BigEndian(data[16..20]);
        height = BinaryPrimitives.ReadUInt32BigEndian(data[20..24]);
        if (width == 0 ||
            height == 0 ||
            width > int.MaxValue ||
            height > int.MaxValue)
        {
            width = 0;
            height = 0;
            return false;
        }

        return true;
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0
                    ? (crc >> 1) ^ 0xEDB88320u
                    : crc >> 1;
            }
        }
        return ~crc;
    }
}
