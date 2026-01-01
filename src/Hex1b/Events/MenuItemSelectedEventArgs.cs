using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for when a menu item is selected.
/// </summary>
public sealed class MenuItemSelectedEventArgs
{
    /// <summary>
    /// The widget that raised the event.
    /// </summary>
    public MenuItemWidget Widget { get; }
    
    /// <summary>
    /// The node that raised the event.
    /// </summary>
    public MenuItemNode Node { get; }
    
    /// <summary>
    /// The input binding context.
    /// </summary>
    public InputBindingActionContext Context { get; }
    
    /// <summary>
    /// The popup stack for closing menus.
    /// </summary>
    public PopupStack Popups => Context.Popups;

    internal MenuItemSelectedEventArgs(MenuItemWidget widget, MenuItemNode node, InputBindingActionContext context)
    {
        Widget = widget;
        Node = node;
        Context = context;
    }
    
    /// <summary>
    /// Convenience method to close all menus.
    /// </summary>
    public void CloseMenu() => Popups.Clear();
}
