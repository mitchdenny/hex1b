namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating DrawerWidget.
/// </summary>
public static class DrawerExtensions
{
    /// <summary>
    /// Creates a DrawerWidget with the specified expansion state, header, and content.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="isExpanded">Whether the drawer is currently expanded.</param>
    /// <param name="onToggle">Handler called when the drawer is toggled.</param>
    /// <param name="header">The widget to display in the header row.</param>
    /// <param name="content">The widget to display when expanded.</param>
    public static DrawerWidget Drawer<TParent>(
        this WidgetContext<TParent> ctx,
        bool isExpanded,
        Action<bool> onToggle,
        Hex1bWidget header,
        Hex1bWidget content)
        where TParent : Hex1bWidget
        => new DrawerWidget(isExpanded, header, content).OnToggle(onToggle);

    /// <summary>
    /// Creates a DrawerWidget with a VStack content builder.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="isExpanded">Whether the drawer is currently expanded.</param>
    /// <param name="onToggle">Handler called when the drawer is toggled.</param>
    /// <param name="header">The widget to display in the header row.</param>
    /// <param name="contentBuilder">Builder function for the content (returns array of widgets for VStack).</param>
    public static DrawerWidget Drawer<TParent>(
        this WidgetContext<TParent> ctx,
        bool isExpanded,
        Action<bool> onToggle,
        Hex1bWidget header,
        Func<WidgetContext<VStackWidget>, Hex1bWidget[]> contentBuilder)
        where TParent : Hex1bWidget
    {
        var contentCtx = new WidgetContext<VStackWidget>();
        var children = contentBuilder(contentCtx);
        var content = new VStackWidget(children);
        return new DrawerWidget(isExpanded, header, content).OnToggle(onToggle);
    }

    /// <summary>
    /// Creates a DrawerWidget with position parameter.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="isExpanded">Whether the drawer is currently expanded.</param>
    /// <param name="onToggle">Handler called when the drawer is toggled.</param>
    /// <param name="header">The widget to display in the header row.</param>
    /// <param name="content">The widget to display when expanded.</param>
    /// <param name="position">The position of the drawer (which edge it anchors to).</param>
    public static DrawerWidget Drawer<TParent>(
        this WidgetContext<TParent> ctx,
        bool isExpanded,
        Action<bool> onToggle,
        Hex1bWidget header,
        Hex1bWidget content,
        DrawerPosition position)
        where TParent : Hex1bWidget
        => new DrawerWidget(isExpanded, header, content)
            .OnToggle(onToggle)
            .WithPosition(position);

    /// <summary>
    /// Creates a DrawerWidget with position and mode parameters.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="isExpanded">Whether the drawer is currently expanded.</param>
    /// <param name="onToggle">Handler called when the drawer is toggled.</param>
    /// <param name="header">The widget to display in the header row.</param>
    /// <param name="content">The widget to display when expanded.</param>
    /// <param name="position">The position of the drawer (which edge it anchors to).</param>
    /// <param name="mode">The display mode of the drawer.</param>
    public static DrawerWidget Drawer<TParent>(
        this WidgetContext<TParent> ctx,
        bool isExpanded,
        Action<bool> onToggle,
        Hex1bWidget header,
        Hex1bWidget content,
        DrawerPosition position,
        DrawerMode mode)
        where TParent : Hex1bWidget
        => new DrawerWidget(isExpanded, header, content)
            .OnToggle(onToggle)
            .WithPosition(position)
            .WithMode(mode);

    /// <summary>
    /// Creates a DrawerWidget with expanded size parameter.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="isExpanded">Whether the drawer is currently expanded.</param>
    /// <param name="onToggle">Handler called when the drawer is toggled.</param>
    /// <param name="header">The widget to display in the header row.</param>
    /// <param name="content">The widget to display when expanded.</param>
    /// <param name="expandedSize">The fixed size when expanded (width for left/right, height for top/bottom).</param>
    public static DrawerWidget Drawer<TParent>(
        this WidgetContext<TParent> ctx,
        bool isExpanded,
        Action<bool> onToggle,
        Hex1bWidget header,
        Hex1bWidget content,
        int expandedSize)
        where TParent : Hex1bWidget
        => new DrawerWidget(isExpanded, header, content)
            .OnToggle(onToggle)
            .WithExpandedSize(expandedSize);

    /// <summary>
    /// Creates a DrawerWidget with VStack content builder and position parameter.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="isExpanded">Whether the drawer is currently expanded.</param>
    /// <param name="onToggle">Handler called when the drawer is toggled.</param>
    /// <param name="header">The widget to display in the header row.</param>
    /// <param name="contentBuilder">Builder function for the content (returns array of widgets for VStack).</param>
    /// <param name="position">The position of the drawer (which edge it anchors to).</param>
    public static DrawerWidget Drawer<TParent>(
        this WidgetContext<TParent> ctx,
        bool isExpanded,
        Action<bool> onToggle,
        Hex1bWidget header,
        Func<WidgetContext<VStackWidget>, Hex1bWidget[]> contentBuilder,
        DrawerPosition position)
        where TParent : Hex1bWidget
    {
        var contentCtx = new WidgetContext<VStackWidget>();
        var children = contentBuilder(contentCtx);
        var content = new VStackWidget(children);
        return new DrawerWidget(isExpanded, header, content)
            .OnToggle(onToggle)
            .WithPosition(position);
    }
}
