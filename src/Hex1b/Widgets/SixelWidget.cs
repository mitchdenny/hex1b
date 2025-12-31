using System.Diagnostics.CodeAnalysis;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that displays a Sixel image if the terminal supports it,
/// otherwise falls back to rendering the fallback widget.
/// </summary>
/// <param name="ImageData">The raw Sixel-encoded image data (as a string in Sixel format).</param>
/// <param name="Fallback">A widget to display if Sixel is not supported.</param>
/// <param name="Width">The width in character cells for the image. If null, uses the image's natural width.</param>
/// <param name="Height">The height in character cells for the image. If null, uses the image's natural height.</param>
[Experimental("HEX1B_SIXEL", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/sixel.md")]
public sealed record SixelWidget(
    string ImageData, 
    Hex1bWidget Fallback,
    int? Width = null,
    int? Height = null) : Hex1bWidget
{
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as SixelNode ?? new SixelNode();
        node.ImageData = ImageData;
        node.RequestedWidth = Width;
        node.RequestedHeight = Height;
        node.Fallback = await context.ReconcileChildAsync(node.Fallback, Fallback, node);
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(SixelNode);
}
