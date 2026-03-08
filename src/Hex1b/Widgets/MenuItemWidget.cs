using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A menu item that can be activated to trigger an action.
/// </summary>
/// <param name="Label">The display label for the item.</param>
public sealed record MenuItemWidget(string Label) : Hex1bWidget, IMenuChild
{
    /// <summary>Rebindable action: Move to next item.</summary>
    public static readonly ActionId MoveDown = new("MenuItem.MoveDown");
    /// <summary>Rebindable action: Move to previous item.</summary>
    public static readonly ActionId MoveUp = new("MenuItem.MoveUp");
    /// <summary>Rebindable action: Close the menu.</summary>
    public static readonly ActionId Close = new("MenuItem.Close");
    /// <summary>Rebindable action: Navigate to previous menu.</summary>
    public static readonly ActionId NavigateLeft = new("MenuItem.NavigateLeft");
    /// <summary>Rebindable action: Navigate to next menu.</summary>
    public static readonly ActionId NavigateRight = new("MenuItem.NavigateRight");
    /// <summary>Rebindable action: Activate the menu item.</summary>
    public static readonly ActionId Activate = new("MenuItem.Activate");

    /// <summary>
    /// The handler called when the item is activated (user triggers the action).
    /// </summary>
    internal Func<MenuItemActivatedEventArgs, Task>? ActivatedHandler { get; init; }
    
    /// <summary>
    /// Whether the item is disabled (grayed out and non-interactive).
    /// </summary>
    internal bool IsDisabled { get; init; }
    
    /// <summary>
    /// The explicitly specified accelerator character (from &amp; syntax).
    /// </summary>
    internal char? ExplicitAccelerator { get; init; }
    
    /// <summary>
    /// The index of the accelerator character in the display label.
    /// </summary>
    internal int AcceleratorIndex { get; init; } = -1;
    
    /// <summary>
    /// Whether to disable automatic accelerator assignment.
    /// </summary>
    internal bool DisableAccelerator { get; init; }

    /// <summary>
    /// Sets a synchronous handler called when the item is activated.
    /// </summary>
    public MenuItemWidget OnActivated(Action<MenuItemActivatedEventArgs> handler)
        => this with { ActivatedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler called when the item is activated.
    /// </summary>
    public MenuItemWidget OnActivated(Func<MenuItemActivatedEventArgs, Task> handler)
        => this with { ActivatedHandler = handler };

    /// <summary>
    /// Sets whether the item is disabled.
    /// </summary>
    /// <param name="disabled">True to disable the item.</param>
    public MenuItemWidget Disabled(bool disabled = true)
        => this with { IsDisabled = disabled };

    /// <summary>
    /// Disables automatic accelerator assignment for this item.
    /// </summary>
    public MenuItemWidget NoAccelerator()
        => this with { DisableAccelerator = true };

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as MenuItemNode ?? new MenuItemNode();
        
        // Mark dirty if properties changed
        if (node.Label != Label || node.IsDisabled != IsDisabled)
        {
            node.MarkDirty();
        }
        
        node.Label = Label;
        node.IsDisabled = IsDisabled;
        node.SourceWidget = this;
        
        // Convert the typed event handler to the internal handler
        // Menu items always close all menus after activation
        if (ActivatedHandler != null)
        {
            node.ActivatedAction = async ctx =>
            {
                var args = new MenuItemActivatedEventArgs(this, node, ctx);
                await ActivatedHandler(args);
                // Auto-close all menus after activation
                ctx.Popups.Clear();
            };
        }
        else
        {
            // Even without a handler, activation closes the menu
            node.ActivatedAction = ctx =>
            {
                ctx.Popups.Clear();
                return Task.CompletedTask;
            };
        }
        
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(MenuItemNode);
}
