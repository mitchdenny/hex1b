using Hex1b.Kgp;

namespace Hex1b.Widgets;

/// <summary>
/// Widget for displaying a raster image using the Kitty Graphics Protocol (KGP).
/// </summary>
/// <remarks>
/// <para>
/// This widget transmits raw pixel data to the terminal and displays it at the
/// node's arranged position. The image is transmitted using the KGP transmit+display
/// command (a=T) which combines data transfer and placement in a single operation.
/// </para>
/// <para>
/// Pixel data must be provided as a byte array in either RGB24 (3 bytes/pixel) or
/// RGBA32 (4 bytes/pixel) format. The terminal must have <see cref="TerminalCapabilities.SupportsKgp"/>
/// enabled for the image to render.
/// </para>
/// </remarks>
/// <param name="PixelData">Raw pixel data in the specified format.</param>
/// <param name="PixelWidth">Width of the image in pixels.</param>
/// <param name="PixelHeight">Height of the image in pixels.</param>
/// <param name="Format">Pixel format (RGB24 or RGBA32).</param>
/// <param name="DisplayColumns">Number of terminal columns to display the image in. 0 = auto from pixel size.</param>
/// <param name="DisplayRows">Number of terminal rows to display the image in. 0 = auto from pixel size.</param>
/// <seealso cref="KittyGraphicsNode"/>
public sealed record KittyGraphicsWidget(
    byte[] PixelData,
    uint PixelWidth,
    uint PixelHeight,
    KgpFormat Format = KgpFormat.Rgba32,
    uint DisplayColumns = 0,
    uint DisplayRows = 0) : Hex1bWidget
{
    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as KittyGraphicsNode ?? new KittyGraphicsNode();

        if (!ReferenceEquals(node.PixelData, PixelData) ||
            node.PixelWidth != PixelWidth ||
            node.PixelHeight != PixelHeight ||
            node.Format != Format ||
            node.DisplayColumns != DisplayColumns ||
            node.DisplayRows != DisplayRows)
        {
            node.MarkDirty();
        }

        node.PixelData = PixelData;
        node.PixelWidth = PixelWidth;
        node.PixelHeight = PixelHeight;
        node.Format = Format;
        node.DisplayColumns = DisplayColumns;
        node.DisplayRows = DisplayRows;

        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(KittyGraphicsNode);
}
