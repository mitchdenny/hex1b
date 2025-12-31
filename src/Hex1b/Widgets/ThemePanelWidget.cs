using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that applies theme customizations to its child widget tree.
/// Use this to override theme elements (colors, characters, etc.) for a specific
/// portion of your UI without affecting the rest of the application.
/// </summary>
/// <param name="ThemeMutator">
/// A function that receives a clone of the current theme and returns a (possibly modified) theme.
/// The returned theme is used for rendering the child subtree.
/// The theme is already cloned, so you can safely call Set() directly.
/// </param>
/// <param name="Child">The child widget to render with the customized theme.</param>
/// <example>
/// <code>
/// // Override button colors for this section only
/// ctx.ThemePanel(
///     theme => theme
///         .Set(ButtonTheme.ForegroundColor, Hex1bColor.White)
///         .Set(ButtonTheme.BackgroundColor, Hex1bColor.Blue),
///     ctx.VStack(v => [
///         v.Button("Primary"),
///         v.Button("Secondary")
///     ])
/// )
/// </code>
/// </example>
public sealed record ThemePanelWidget(Func<Hex1bTheme, Hex1bTheme> ThemeMutator, Hex1bWidget Child) : Hex1bWidget
{
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ThemePanelNode ?? new ThemePanelNode();
        
        // Mark dirty if theme mutator changed (we can't easily compare delegates, so always mark)
        if (existingNode is ThemePanelNode existing && existing.ThemeMutator != ThemeMutator)
        {
            node.MarkDirty();
        }
        
        node.ThemeMutator = ThemeMutator;
        node.Child = await context.ReconcileChildAsync(node.Child, Child, node);
        
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

    internal override Type GetExpectedNodeType() => typeof(ThemePanelNode);
}
