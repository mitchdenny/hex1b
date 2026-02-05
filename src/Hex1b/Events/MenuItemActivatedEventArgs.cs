using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for when a menu item is activated (user triggers the action).
/// Note: All menus are automatically closed after the activation handler completes.
/// </summary>
public sealed class MenuItemActivatedEventArgs
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

    /// <summary>
    /// The window manager for managing floating windows.
    /// </summary>
    public WindowManager Windows => Context.Windows;

    internal MenuItemActivatedEventArgs(MenuItemWidget widget, MenuItemNode node, InputBindingActionContext context)
    {
        Widget = widget;
        Node = node;
        Context = context;
    }
    
    /// <summary>
    /// Manually closes all menus. This is called automatically after the handler completes,
    /// so you typically don't need to call it. However, if you need to close menus
    /// before an async operation completes, you can call this explicitly.
    /// </summary>
    public void CloseMenu() => Popups.Clear();
}
