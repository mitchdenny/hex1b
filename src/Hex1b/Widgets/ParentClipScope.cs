using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Clips to the parent's bounds.
/// </summary>
internal sealed record ParentClipScope : ClipScope
{
    internal override Rect Resolve(
        Rect zstackBounds,
        Rect? parentClipRect,
        Rect screenBounds,
        Func<Hex1bWidget, Hex1bNode?>? widgetNodeResolver)
    {
        // Use parent's clip rect if available, otherwise use our own bounds
        return parentClipRect ?? zstackBounds;
    }
}
