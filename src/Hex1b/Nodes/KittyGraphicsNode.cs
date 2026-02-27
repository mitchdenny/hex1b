using System.Security.Cryptography;
using System.Text;
using Hex1b.Kgp;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Render node for displaying a raster image via the Kitty Graphics Protocol.
/// Created by reconciling a <see cref="KittyGraphicsWidget"/>.
/// </summary>
/// <remarks>
/// <para>
/// This node handles measuring, arranging, and rendering a KGP image.
/// During render, it builds a KGP transmit+display (a=T) escape sequence
/// and places it on the surface cell at the node's position. The surface
/// diff system then emits the sequence only when the image changes.
/// </para>
/// </remarks>
/// <seealso cref="KittyGraphicsWidget"/>
public sealed class KittyGraphicsNode : Hex1bNode
{
    // Fallback ID counter used when no KgpImageCache is available (e.g., in tests)
    private static uint _fallbackNextImageId = 1;

    /// <summary>Raw pixel data.</summary>
    public byte[] PixelData { get; set; } = [];

    /// <summary>Image width in pixels.</summary>
    public uint PixelWidth { get; set; }

    /// <summary>Image height in pixels.</summary>
    public uint PixelHeight { get; set; }

    /// <summary>Pixel format.</summary>
    public KgpFormat Format { get; set; } = KgpFormat.Rgba32;

    /// <summary>Display width in terminal columns. 0 = auto.</summary>
    public uint DisplayColumns { get; set; }

    /// <summary>Display height in terminal rows. 0 = auto.</summary>
    public uint DisplayRows { get; set; }

    /// <summary>
    /// Measures the size needed to display the image.
    /// </summary>
    protected override Size MeasureCore(Constraints constraints)
    {
        var cols = DisplayColumns > 0 ? (int)DisplayColumns : EstimateCellColumns();
        var rows = DisplayRows > 0 ? (int)DisplayRows : EstimateCellRows();

        return constraints.Constrain(new Size(
            Math.Max(1, cols),
            Math.Max(1, rows)));
    }

    /// <summary>
    /// Renders the image by placing KGP data on the surface cell at the node's position.
    /// </summary>
    public override void Render(Hex1bRenderContext context)
    {
        if (PixelData.Length == 0 || PixelWidth == 0 || PixelHeight == 0)
            return;

        if (!context.Capabilities.SupportsKgp)
            return;

        var contentHash = SHA256.HashData(PixelData);
        var cache = context.KgpCache;

        // Check the shared cache first — another node may have already transmitted this image
        bool needsTransmit;
        uint imageId;
        if (cache != null)
        {
            if (cache.TryGetImageId(contentHash, out imageId))
            {
                needsTransmit = false;
            }
            else
            {
                imageId = cache.AllocateImageId();
                needsTransmit = true;
            }
        }
        else
        {
            // No cache available (e.g., tests without host widget) — fallback to static counter
            imageId = _fallbackNextImageId++;
            needsTransmit = true;
        }

        var cols = DisplayColumns > 0 ? DisplayColumns : (uint)EstimateCellColumns();
        var rows = DisplayRows > 0 ? DisplayRows : (uint)EstimateCellRows();

        // Build the KGP escape sequence
        string payload;
        if (needsTransmit)
        {
            payload = BuildTransmitPayload(imageId, cols, rows);
            cache?.RegisterTransmission(contentHash, imageId);
        }
        else
        {
            payload = $"\x1b_Ga=p,i={imageId},c={cols},r={rows},C=1,q=2\x1b\\";
        }

        var kgpData = new KgpCellData(payload, (int)cols, (int)rows);

        // Place KGP data on the anchor cell via the surface
        if (context is SurfaceRenderContext surfaceContext)
        {
            var surface = surfaceContext.Surface;
            var x = Bounds.X - surfaceContext.OffsetX;
            var y = Bounds.Y - surfaceContext.OffsetY;
            if (x >= 0 && x < surface.Width && y >= 0 && y < surface.Height)
            {
                surface[x, y] = new SurfaceCell(" ", null, null, KgpData: kgpData);
            }
        }
        else
        {
            context.SetCursorPosition(Bounds.X, Bounds.Y);
            context.Write(payload);
        }
    }

    private string BuildTransmitPayload(uint imageId, uint cols, uint rows)
    {
        var base64 = Convert.ToBase64String(PixelData);
        const int maxChunkSize = 4096;

        if (base64.Length <= maxChunkSize)
        {
            var sb = new StringBuilder();
            sb.Append("\x1b_G");
            sb.Append($"a=T,f={(int)Format},s={PixelWidth},v={PixelHeight}");
            sb.Append($",i={imageId}");
            sb.Append($",c={cols},r={rows}");
            sb.Append(",C=1,q=2");
            sb.Append(';');
            sb.Append(base64);
            sb.Append("\x1b\\");
            return sb.ToString();
        }

        // Multi-chunk transmission
        var msb = new StringBuilder();
        int offset = 0;
        bool first = true;

        while (offset < base64.Length)
        {
            var remaining = base64.Length - offset;
            var chunkSize = Math.Min(maxChunkSize, remaining);
            if (remaining > maxChunkSize && chunkSize % 4 != 0)
                chunkSize -= chunkSize % 4;
            var isLast = offset + chunkSize >= base64.Length;

            msb.Append("\x1b_G");
            if (first)
            {
                msb.Append($"a=T,f={(int)Format},s={PixelWidth},v={PixelHeight}");
                msb.Append($",i={imageId}");
                msb.Append($",c={cols},r={rows}");
                msb.Append(",C=1,q=2");
                msb.Append(isLast ? ",m=0" : ",m=1");
                first = false;
            }
            else
            {
                msb.Append(isLast ? "m=0" : "m=1");
            }
            msb.Append(';');
            msb.Append(base64.AsSpan(offset, chunkSize));
            msb.Append("\x1b\\");

            offset += chunkSize;
        }

        return msb.ToString();
    }

    private int EstimateCellColumns()
    {
        return Math.Max(1, (int)Math.Ceiling(PixelWidth / 8.0));
    }

    private int EstimateCellRows()
    {
        return Math.Max(1, (int)Math.Ceiling(PixelHeight / 16.0));
    }
}
