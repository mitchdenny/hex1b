using System.Security.Cryptography;
using System.Text;

namespace Hex1b;

/// <summary>
/// Immutable data for a KGP (Kitty Graphics Protocol) image placement on a surface cell.
/// </summary>
/// <remarks>
/// <para>
/// Stores structured placement parameters so that clipping can adjust the source
/// rectangle without re-encoding the image data. The transmit payload (a=t) is
/// stored separately from the placement parameters so that the placement command
/// (a=p) can be regenerated with different source rectangles after compositing clips.
/// </para>
/// </remarks>
public sealed class KgpCellData
{
    /// <summary>
    /// Gets the transmit payload (a=t with image data), or null if the image
    /// was already transmitted (cache hit → use a=p only).
    /// </summary>
    public string? TransmitPayload { get; }

    /// <summary>
    /// Gets the KGP image ID for placement commands.
    /// </summary>
    public uint ImageId { get; }

    /// <summary>
    /// Gets the width of the placement in terminal columns.
    /// </summary>
    public int WidthInCells { get; }

    /// <summary>
    /// Gets the height of the placement in terminal rows.
    /// </summary>
    public int HeightInCells { get; }

    /// <summary>
    /// Gets the source image width in pixels (for computing clip ratios).
    /// </summary>
    public uint SourcePixelWidth { get; }

    /// <summary>
    /// Gets the source image height in pixels (for computing clip ratios).
    /// </summary>
    public uint SourcePixelHeight { get; }

    /// <summary>
    /// Gets the pixel X offset into the source image for clipping (0 = no clip).
    /// </summary>
    public int ClipX { get; }

    /// <summary>
    /// Gets the pixel Y offset into the source image for clipping (0 = no clip).
    /// </summary>
    public int ClipY { get; }

    /// <summary>
    /// Gets the pixel width of the visible source region (0 = full width).
    /// </summary>
    public int ClipW { get; }

    /// <summary>
    /// Gets the pixel height of the visible source region (0 = full height).
    /// </summary>
    public int ClipH { get; }

    /// <summary>
    /// Gets the content hash for deduplication.
    /// </summary>
    public byte[] ContentHash { get; }

    /// <summary>
    /// Gets the z-index for KGP placement stacking order relative to text.
    /// Negative values render behind text, positive values render on top of text.
    /// </summary>
    public int ZIndex { get; }

    /// <summary>
    /// Creates a new KGP cell data instance with structured placement data.
    /// </summary>
    public KgpCellData(
        string? transmitPayload,
        uint imageId,
        int widthInCells,
        int heightInCells,
        uint sourcePixelWidth,
        uint sourcePixelHeight,
        byte[] contentHash,
        int clipX = 0,
        int clipY = 0,
        int clipW = 0,
        int clipH = 0,
        int zIndex = -1)
    {
        TransmitPayload = transmitPayload;
        ImageId = imageId;
        WidthInCells = widthInCells;
        HeightInCells = heightInCells;
        SourcePixelWidth = sourcePixelWidth;
        SourcePixelHeight = sourcePixelHeight;
        ContentHash = contentHash;
        ClipX = clipX;
        ClipY = clipY;
        ClipW = clipW;
        ClipH = clipH;
        ZIndex = zIndex;
    }

    /// <summary>
    /// Builds the placement command (a=p) with current clip parameters.
    /// </summary>
    public string BuildPlacementPayload()
    {
        var sb = new StringBuilder();
        sb.Append("\x1b_G");
        sb.Append($"a=p,i={ImageId},c={WidthInCells},r={HeightInCells}");
        if (ClipX > 0) sb.Append($",x={ClipX}");
        if (ClipY > 0) sb.Append($",y={ClipY}");
        if (ClipW > 0) sb.Append($",w={ClipW}");
        if (ClipH > 0) sb.Append($",h={ClipH}");
        sb.Append($",q=2,z={ZIndex}");
        sb.Append("\x1b\\");
        return sb.ToString();
    }

    /// <summary>
    /// Creates a clipped version of this KGP data with a new source rectangle.
    /// The transmit payload is preserved (image only needs to be sent once).
    /// </summary>
    public KgpCellData WithClip(int clipX, int clipY, int clipW, int clipH, int newWidthInCells, int newHeightInCells)
    {
        return new KgpCellData(
            TransmitPayload,
            ImageId,
            newWidthInCells,
            newHeightInCells,
            SourcePixelWidth,
            SourcePixelHeight,
            ContentHash,
            clipX,
            clipY,
            clipW,
            clipH,
            ZIndex);
    }

    /// <summary>
    /// Gets whether this placement has a source rectangle clip applied.
    /// </summary>
    public bool IsClipped => ClipX > 0 || ClipY > 0 || ClipW > 0 || ClipH > 0;

    /// <summary>
    /// Maximum payload size per KGP APC chunk (KGP protocol limit).
    /// </summary>
    private const int MaxChunkSize = 4096;

    /// <summary>
    /// Builds chunked transmit sequences for the KGP image data.
    /// Payloads larger than 4096 bytes are split into multiple APC sequences
    /// using the m=1 (more data) / m=0 (final chunk) protocol.
    /// Returns an empty list if TransmitPayload is null.
    /// </summary>
    public List<string> BuildTransmitChunks()
    {
        var chunks = new List<string>();
        if (TransmitPayload == null)
            return chunks;

        // Legacy opaque payload (from FromPayload for tests) — emit as-is
        if (SourcePixelWidth == 0)
        {
            chunks.Add(TransmitPayload);
            return chunks;
        }

        // Extract base64 from the stored transmit payload (format: ESC_G{params};{base64}ESC\)
        var semiIdx = TransmitPayload.IndexOf(';');
        if (semiIdx < 0)
        {
            chunks.Add(TransmitPayload);
            return chunks;
        }

        // Get just the params part without ESC_G prefix and base64 without ESC\ suffix
        var paramsStart = TransmitPayload.IndexOf('G') + 1; // after 'G' in ESC_G
        var paramsStr = TransmitPayload.Substring(paramsStart, semiIdx - paramsStart);
        var base64 = TransmitPayload.Substring(semiIdx + 1, TransmitPayload.Length - semiIdx - 3); // strip ESC\

        if (base64.Length <= MaxChunkSize)
        {
            // Small enough for a single chunk
            chunks.Add($"\x1b_G{paramsStr};{base64}\x1b\\");
        }
        else
        {
            // Split into chunks
            var offset = 0;
            var isFirst = true;
            while (offset < base64.Length)
            {
                var remaining = base64.Length - offset;
                var chunkLen = Math.Min(remaining, MaxChunkSize);
                var isLast = (offset + chunkLen >= base64.Length);
                var chunk = base64.Substring(offset, chunkLen);

                if (isFirst)
                    chunks.Add($"\x1b_G{paramsStr},m=1;{chunk}\x1b\\");
                else if (isLast)
                    chunks.Add($"\x1b_Gm=0;{chunk}\x1b\\");
                else
                    chunks.Add($"\x1b_Gm=1;{chunk}\x1b\\");

                offset += chunkLen;
                isFirst = false;
            }
        }

        return chunks;
    }

    /// <summary>
    /// Gets the complete payload for emission: transmit (if needed) + placement.
    /// For legacy/opaque payloads (SourcePixelWidth == 0), returns TransmitPayload as-is.
    /// </summary>
    public string Payload
    {
        get
        {
            // Legacy opaque payload (created via FromPayload for tests)
            if (SourcePixelWidth == 0 && TransmitPayload != null)
                return TransmitPayload;
            
            if (TransmitPayload != null)
                return TransmitPayload + BuildPlacementPayload();
            return BuildPlacementPayload();
        }
    }

    /// <summary>
    /// Creates a KGP cell data from a pre-built payload string (for tests and backward compatibility).
    /// The payload is stored as-is and emitted directly.
    /// </summary>
    public static KgpCellData FromPayload(string payload, int widthInCells, int heightInCells)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload));
        return new KgpCellData(payload, 0, widthInCells, heightInCells, 0, 0, hash);
    }

    /// <summary>
    /// Compares content hashes for equality.
    /// </summary>
    public static bool HashEquals(byte[] a, byte[] b)
        => a.AsSpan().SequenceEqual(b);
}
