using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for building DragBarPanel widgets.
/// </summary>
public static class DragBarPanelExtensions
{
    /// <summary>
    /// Creates a DragBarPanel that wraps content built from a callback.
    /// </summary>
    public static DragBarPanelWidget DragBarPanel<TParent>(
        this WidgetContext<TParent> ctx,
        Func<WidgetContext<DragBarPanelWidget>, Hex1bWidget> contentBuilder)
        where TParent : Hex1bWidget
    {
        var contentCtx = new WidgetContext<DragBarPanelWidget>();
        var content = contentBuilder(contentCtx);
        return new DragBarPanelWidget { Content = content };
    }
    
    /// <summary>
    /// Creates a DragBarPanel that wraps a widget directly.
    /// </summary>
    public static DragBarPanelWidget DragBarPanel<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bWidget content)
        where TParent : Hex1bWidget
        => new DragBarPanelWidget { Content = content };
    
    /// <summary>
    /// Sets the initial size of the panel in characters (width or height depending on edge).
    /// </summary>
    public static DragBarPanelWidget InitialSize(this DragBarPanelWidget widget, int size)
        => widget with { InitialSize = size };
    
    /// <summary>
    /// Sets the minimum allowed size in characters.
    /// </summary>
    public static DragBarPanelWidget MinSize(this DragBarPanelWidget widget, int size)
        => widget with { MinimumSize = size };
    
    /// <summary>
    /// Sets the maximum allowed size in characters.
    /// </summary>
    public static DragBarPanelWidget MaxSize(this DragBarPanelWidget widget, int size)
        => widget with { MaximumSize = size };
    
    /// <summary>
    /// Explicitly sets which edge the resize handle appears on,
    /// overriding auto-detection from layout context.
    /// </summary>
    public static DragBarPanelWidget HandleEdge(this DragBarPanelWidget widget, DragBarEdge edge)
        => widget with { Edge = edge };
    
}
