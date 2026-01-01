using Hex1b.Events;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A menu item that can be selected to trigger an action.
/// </summary>
/// <param name="Label">The display label for the item.</param>
public sealed record MenuItemWidget(string Label) : Hex1bWidget, IMenuChild
{
    /// <summary>
    /// The handler called when the item is selected.
    /// </summary>
    internal Func<MenuItemSelectedEventArgs, Task>? SelectHandler { get; init; }
    
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
    /// Sets a synchronous handler called when the item is selected.
    /// </summary>
    public MenuItemWidget OnSelect(Action<MenuItemSelectedEventArgs> handler)
        => this with { SelectHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler called when the item is selected.
    /// </summary>
    public MenuItemWidget OnSelect(Func<MenuItemSelectedEventArgs, Task> handler)
        => this with { SelectHandler = handler };

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
        if (SelectHandler != null)
        {
            node.SelectAction = async ctx =>
            {
                var args = new MenuItemSelectedEventArgs(this, node, ctx);
                await SelectHandler(args);
            };
        }
        else
        {
            node.SelectAction = null;
        }
        
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(MenuItemNode);
}
