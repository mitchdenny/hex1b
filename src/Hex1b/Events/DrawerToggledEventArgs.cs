using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for drawer toggle events.
/// </summary>
public sealed class DrawerToggledEventArgs : WidgetEventArgs<DrawerWidget, DrawerNode>
{
    /// <summary>
    /// The new expanded state of the drawer.
    /// </summary>
    public bool IsExpanded { get; }

    internal DrawerToggledEventArgs(DrawerWidget widget, DrawerNode node, InputBindingActionContext context, bool isExpanded)
        : base(widget, node, context)
    {
        IsExpanded = isExpanded;
    }
}
