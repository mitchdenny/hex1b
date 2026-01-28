using System.Security.Cryptography;
using Hex1b.Surfaces;

namespace Hex1b;

/// <summary>
/// Immutable data containing Sixel graphics information.
/// </summary>
/// <remarks>
/// <para>
/// Sixel data is content-addressable: identical payloads share the same
/// <see cref="SixelData"/> instance. This deduplicates memory when the same
/// image appears in multiple cells or is re-rendered.
/// </para>
/// <para>
/// The raw DCS sequence is stored so it can be re-emitted during rendering.
/// Pixel dimensions are parsed from the raster attributes in the payload.
/// </para>
/// </remarks>
public sealed class SixelData
{
    private SixelPixelBuffer? _decodedPixels;

    /// <summary>
    /// Gets the raw Sixel DCS sequence (ESC P ... ESC \).
    /// </summary>
    public string Payload { get; }

    /// <summary>
    /// Gets the width of the Sixel image in pixels.
    /// </summary>
    public int PixelWidth { get; }

    /// <summary>
    /// Gets the height of the Sixel image in pixels.
    /// </summary>
    public int PixelHeight { get; }

    /// <summary>
    /// Gets the width of the Sixel image in cells.
    /// </summary>
    public int WidthInCells { get; }

    /// <summary>
    /// Gets the height of the Sixel image in cells.
    /// </summary>
    public int HeightInCells { get; }

    /// <summary>
    /// Gets the content hash used for deduplication.
    /// </summary>
    internal byte[] ContentHash { get; }

    internal SixelData(
        string payload,
        int widthInCells,
        int heightInCells,
        byte[] contentHash)
        : this(payload, widthInCells, heightInCells, contentHash, 0, 0)
    {
    }

    internal SixelData(
        string payload,
        int widthInCells,
        int heightInCells,
        byte[] contentHash,
        int pixelWidth,
        int pixelHeight)
    {
        Payload = payload;
        WidthInCells = widthInCells;
        HeightInCells = heightInCells;
        ContentHash = contentHash;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
    }

    /// <summary>
    /// Gets the cell span for this sixel using the specified cell metrics.
    /// </summary>
    /// <param name="metrics">The cell metrics to use for conversion.</param>
    /// <returns>The width and height in cells.</returns>
    public (int Width, int Height) GetCellSpan(CellMetrics metrics)
    {
        if (PixelWidth > 0 && PixelHeight > 0)
        {
            return metrics.PixelToCellSpan(PixelWidth, PixelHeight);
        }
        // Fall back to stored cell dimensions
        return (WidthInCells, HeightInCells);
    }

    /// <summary>
    /// Decodes the sixel payload to a pixel buffer.
    /// The result is cached for subsequent calls.
    /// </summary>
    /// <returns>The decoded pixel buffer, or null if decoding fails.</returns>
    public SixelPixelBuffer? GetPixels()
    {
        if (_decodedPixels is not null)
            return _decodedPixels;

        var decoded = Automation.SixelDecoder.Decode(Payload);
        if (decoded is null)
            return null;

        // Convert SixelImage to SixelPixelBuffer
        var buffer = new SixelPixelBuffer(decoded.Width, decoded.Height);
        for (var y = 0; y < decoded.Height; y++)
        {
            for (var x = 0; x < decoded.Width; x++)
            {
                var idx = (y * decoded.Width + x) * 4;
                buffer[x, y] = new Rgba32(
                    decoded.Pixels[idx],
                    decoded.Pixels[idx + 1],
                    decoded.Pixels[idx + 2],
                    decoded.Pixels[idx + 3]);
            }
        }

        _decodedPixels = buffer;
        return buffer;
    }

    /// <summary>
    /// Computes a content hash for a Sixel payload.
    /// </summary>
    internal static byte[] ComputeHash(string payload)
    {
        // Use SHA256 for robust deduplication
        return SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload));
    }

    /// <summary>
    /// Checks if two content hashes are equal.
    /// </summary>
    internal static bool HashEquals(byte[] a, byte[] b)
    {
        return a.AsSpan().SequenceEqual(b.AsSpan());
    }
}
