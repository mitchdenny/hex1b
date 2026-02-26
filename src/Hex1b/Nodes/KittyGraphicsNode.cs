using System.Security.Cryptography;
using System.Text;
using Hex1b.Kgp;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Render node for displaying a raster image via the Kitty Graphics Protocol.
/// Created by reconciling a <see cref="KittyGraphicsWidget"/>.
/// </summary>
/// <remarks>
/// <para>
/// This node handles measuring, arranging, and rendering a KGP image.
/// During render, it emits a KGP transmit+display (a=T) escape sequence
/// that sends the pixel data and places the image at the node's position.
/// </para>
/// <para>
/// The node tracks a content hash to detect when pixel data changes,
/// and manages an internal image ID for the KGP protocol.
/// </para>
/// </remarks>
/// <seealso cref="KittyGraphicsWidget"/>
public sealed class KittyGraphicsNode : Hex1bNode
{
    private static uint _nextImageId = 1;

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

    private byte[]? _lastContentHash;
    private uint _assignedImageId;

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
    /// Renders the image by emitting a KGP transmit+display escape sequence.
    /// </summary>
    public override void Render(Hex1bRenderContext context)
    {
        if (PixelData.Length == 0 || PixelWidth == 0 || PixelHeight == 0)
            return;

        if (!context.Capabilities.SupportsKgp)
            return;

        var contentHash = SHA256.HashData(PixelData);
        var needsTransmit = _lastContentHash is null ||
                            !contentHash.AsSpan().SequenceEqual(_lastContentHash);

        if (needsTransmit)
        {
            _assignedImageId = _nextImageId++;
            _lastContentHash = contentHash;
        }

        var cols = DisplayColumns > 0 ? DisplayColumns : (uint)EstimateCellColumns();
        var rows = DisplayRows > 0 ? DisplayRows : (uint)EstimateCellRows();

        // Position cursor at the node's top-left corner
        context.SetCursorPosition(Bounds.X, Bounds.Y);

        if (needsTransmit)
        {
            // Transmit and display in one shot
            var payload = Convert.ToBase64String(PixelData);
            var sb = new StringBuilder();
            sb.Append("\x1b_G");
            sb.Append($"a=T,f={(int)Format},s={PixelWidth},v={PixelHeight}");
            sb.Append($",i={_assignedImageId}");
            sb.Append($",c={cols},r={rows}");
            sb.Append(",C=1"); // Do not move cursor
            sb.Append(",q=2"); // Suppress responses
            sb.Append(';');
            sb.Append(payload);
            sb.Append("\x1b\\");

            context.Write(sb.ToString());
        }
        else
        {
            // Image already transmitted — just place it again
            var sb = new StringBuilder();
            sb.Append("\x1b_G");
            sb.Append($"a=p,i={_assignedImageId}");
            sb.Append($",c={cols},r={rows}");
            sb.Append(",C=1");
            sb.Append(",q=2");
            sb.Append("\x1b\\");

            context.Write(sb.ToString());
        }
    }

    private int EstimateCellColumns()
    {
        // Default: ~8 pixels per cell column (typical monospace)
        return Math.Max(1, (int)Math.Ceiling(PixelWidth / 8.0));
    }

    private int EstimateCellRows()
    {
        // Default: ~16 pixels per cell row (typical monospace)
        return Math.Max(1, (int)Math.Ceiling(PixelHeight / 16.0));
    }
}
