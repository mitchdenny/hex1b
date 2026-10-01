using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Defines how a ZStack's content is clipped.
/// </summary>
public abstract record ClipScope
{
    /// <summary>
    /// Content is clipped to the parent's bounds. This is the default.
    /// </summary>
    public static ClipScope Parent { get; } = new ParentClipScope();
    
    /// <summary>
    /// Content is not clipped - can render to the full screen/terminal bounds.
    /// </summary>
    public static ClipScope Screen { get; } = new ScreenClipScope();
    
    /// <summary>
    /// Content is clipped to a specific widget's bounds.
    /// </summary>
    /// <param name="widget">The widget whose bounds define the clip region.</param>
    public static ClipScope Widget(Hex1bWidget widget) => new WidgetClipScope(widget);
    
    /// <summary>
    /// Resolves this clip scope to actual bounds during arrange.
    /// </summary>
    /// <param name="zstackBounds">The ZStack's own bounds.</param>
    /// <param name="parentClipRect">The parent's clip rectangle.</param>
    /// <param name="screenBounds">The full screen bounds.</param>
    /// <param name="widgetNodeResolver">A function to resolve widgets to their nodes.</param>
    internal abstract Rect Resolve(
        Rect zstackBounds,
        Rect? parentClipRect,
        Rect screenBounds,
        Func<Hex1bWidget, Hex1bNode?>? widgetNodeResolver);
}
