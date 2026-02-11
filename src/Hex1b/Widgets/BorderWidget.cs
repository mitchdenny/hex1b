using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that draws a box border around its child content.
/// Use the fluent <see cref="Title"/> method to set an optional title.
/// </summary>
public sealed record BorderWidget(Hex1bWidget Child) : Hex1bWidget
{
    /// <summary>
    /// The optional title displayed centered on the top border.
    /// </summary>
    internal string? TitleText { get; init; }

    /// <summary>
    /// Sets the title displayed centered on the top border.
    /// </summary>
    public BorderWidget Title(string title) => this with { TitleText = title };

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as BorderNode ?? new BorderNode();
        node.Child = await context.ReconcileChildAsync(node.Child, Child, node);
        node.Title = TitleText;
        
        // Set initial focus only if this is a new node AND we're at the root or parent doesn't manage focus
        if (context.IsNew && !context.ParentManagesFocus())
        {
            var focusables = node.GetFocusableNodes().ToList();
            if (focusables.Count > 0)
            {
                ReconcileContext.SetNodeFocus(focusables[0], true);
            }
        }
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(BorderNode);
}
