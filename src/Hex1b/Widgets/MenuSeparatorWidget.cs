using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A visual separator between menu items.
/// Non-focusable and non-interactive.
/// </summary>
public sealed record MenuSeparatorWidget() : Hex1bWidget, IMenuChild
{
    /// <summary>Rebindable action: Close the parent menu.</summary>
    public static readonly ActionId Close = new(nameof(Close));
    /// <summary>Rebindable action: Navigate to previous menu.</summary>
    public static readonly ActionId PreviousMenu = new(nameof(PreviousMenu));
    /// <summary>Rebindable action: Navigate to next menu.</summary>
    public static readonly ActionId NextMenu = new(nameof(NextMenu));

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as MenuSeparatorNode ?? new MenuSeparatorNode();
        node.SourceWidget = this;
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(MenuSeparatorNode);
}
