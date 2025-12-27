using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that scopes theme changes to its child content.
/// The theme builder callback receives a clone of the current theme and returns the modified theme.
/// </summary>
public sealed record ThemingPanelWidget(Func<Hex1bTheme, Hex1bTheme> ThemeBuilder, Hex1bWidget Child) : Hex1bWidget
{
    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ThemingPanelNode ?? new ThemingPanelNode();
        node.ThemeBuilder = ThemeBuilder;
        node.Child = context.ReconcileChild(node.Child, Child, node);
        
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

    internal override Type GetExpectedNodeType() => typeof(ThemingPanelNode);
}
