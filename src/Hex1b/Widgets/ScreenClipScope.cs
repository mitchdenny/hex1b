using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// No clipping - full screen bounds.
/// </summary>
internal sealed record ScreenClipScope : ClipScope
{
    internal override Rect Resolve(
        Rect zstackBounds,
        Rect? parentClipRect,
        Rect screenBounds,
        Func<Hex1bWidget, Hex1bNode?>? widgetNodeResolver)
    {
        return screenBounds;
    }
}
