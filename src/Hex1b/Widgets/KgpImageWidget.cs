using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that displays a KGP (Kitty Graphics Protocol) image if the terminal supports it,
/// otherwise falls back to rendering the fallback widget.
/// </summary>
/// <param name="ImageData">Raw pixel data (RGBA32 format) for the image.</param>
/// <param name="PixelWidth">Width of the image in pixels.</param>
/// <param name="PixelHeight">Height of the image in pixels.</param>
/// <param name="Fallback">A widget to display if KGP is not supported.</param>
/// <param name="Width">Optional width in character cells. If null, computed from pixel dimensions.</param>
/// <param name="Height">Optional height in character cells. If null, computed from pixel dimensions.</param>
/// <param name="ZOrder">Z-ordering relative to text. Default is <see cref="KgpZOrder.BelowText"/>.</param>
public sealed record KgpImageWidget(
    byte[] ImageData,
    int PixelWidth,
    int PixelHeight,
    Hex1bWidget Fallback,
    int? Width = null,
    int? Height = null,
    KgpZOrder ZOrder = KgpZOrder.BelowText) : Hex1bWidget
{
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as KgpImageNode ?? new KgpImageNode();
        node.ImageData = ImageData;
        node.PixelWidth = PixelWidth;
        node.PixelHeight = PixelHeight;
        node.RequestedWidth = Width;
        node.RequestedHeight = Height;
        node.ZOrder = ZOrder;
        node.Fallback = await context.ReconcileChildAsync(node.Fallback, Fallback, node);
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(KgpImageNode);
}
