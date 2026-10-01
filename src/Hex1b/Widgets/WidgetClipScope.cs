using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Clips to a specific widget's bounds.
/// </summary>
internal sealed record WidgetClipScope(Hex1bWidget TargetWidget) : ClipScope
{
    internal override Rect Resolve(
        Rect zstackBounds,
        Rect? parentClipRect,
        Rect screenBounds,
        Func<Hex1bWidget, Hex1bNode?>? widgetNodeResolver)
    {
        if (widgetNodeResolver == null)
        {
            // No resolver available - fall back to own bounds
            return zstackBounds;
        }
        
        var targetNode = widgetNodeResolver(TargetWidget);
        if (targetNode == null)
        {
            // Target widget not found - fall back to own bounds
            return zstackBounds;
        }
        
        return targetNode.Bounds;
    }
}
