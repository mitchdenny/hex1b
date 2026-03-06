using System.Security.Cryptography;

namespace Hex1b;

/// <summary>
/// Stores transmitted image data for the Kitty Graphics Protocol.
/// Images are identified by ID and may be displayed via multiple placements.
/// </summary>
public sealed class KgpImageData
{
    /// <summary>
    /// The image ID assigned by the client or allocated by the terminal.
    /// </summary>
    public uint ImageId { get; }

    /// <summary>
    /// The image number (I key), if specified. 0 means unspecified.
    /// </summary>
    public uint ImageNumber { get; }

    /// <summary>
    /// The raw decoded pixel data (after base64 decoding, before decompression).
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Image width in pixels.
    /// </summary>
    public uint Width { get; }

    /// <summary>
    /// Image height in pixels.
    /// </summary>
    public uint Height { get; }

    /// <summary>
    /// The pixel format of the stored data.
    /// </summary>
    public KgpFormat Format { get; }

    /// <summary>
    /// SHA256 hash of the data for content-addressable deduplication.
    /// </summary>
    public byte[] ContentHash { get; }

    public KgpImageData(uint imageId, uint imageNumber, byte[] data, uint width, uint height, KgpFormat format)
    {
        ImageId = imageId;
        ImageNumber = imageNumber;
        Data = data;
        Width = width;
        Height = height;
        Format = format;
        ContentHash = SHA256.HashData(data);
    }

    /// <summary>
    /// Validates that the data size matches the expected size for the given format and dimensions.
    /// </summary>
    public bool IsDataSizeValid()
    {
        if (Format == KgpFormat.Png)
            return Data.Length > 0;

        var bytesPerPixel = Format == KgpFormat.Rgb24 ? 3 : 4;
        var expectedSize = (long)Width * Height * bytesPerPixel;
        return Data.Length == expectedSize;
    }

    /// <summary>
    /// Whether pixels are 4-byte aligned (RGBA or PNG).
    /// </summary>
    public bool Is4ByteAligned => Format != KgpFormat.Rgb24;
}
