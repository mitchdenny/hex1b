namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating DrawerWidget.
/// </summary>
public static class DrawerExtensions
{
    /// <summary>
    /// Creates a new DrawerWidget.
    /// </summary>
    /// <remarks>
    /// Use <see cref="DrawerWidget.CollapsedContent"/> and <see cref="DrawerWidget.ExpandedContent"/>
    /// to define the drawer's content for each state.
    /// </remarks>
    public static DrawerWidget Drawer<TParent>(this WidgetContext<TParent> ctx)
        where TParent : Hex1bWidget
        => new();
}
