using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// A passthrough widget that fills its bounds with a background color
/// before rendering its child. All layout, focus, and input is delegated
/// to the child unchanged.
/// </summary>
/// <param name="Color">The background color to fill.</param>
/// <param name="Child">The child widget to render on top of the background.</param>
public sealed record BackgroundPanelWidget(Hex1bColor Color, Hex1bWidget Child) : Hex1bWidget
{
    /// <summary>
    /// Optional theme element to read the background color from at render time.
    /// When set, the color is resolved from the theme during rendering.
    /// </summary>
    internal Hex1bThemeElement<Hex1bColor>? ThemeElement { get; init; }

    /// <summary>
    /// Creates a BackgroundPanelWidget that reads its color from a theme element.
    /// </summary>
    internal BackgroundPanelWidget(Hex1bThemeElement<Hex1bColor> themeElement, Hex1bWidget child)
        : this(Hex1bColor.Default, child)
    {
        ThemeElement = themeElement;
    }

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as BackgroundPanelNode ?? new BackgroundPanelNode();

        if (!node.Color.Equals(Color))
        {
            node.Color = Color;
            node.MarkDirty();
        }
        node.ThemeElement = ThemeElement;

        node.Child = await context.ReconcileChildAsync(node.Child, Child, node);
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(BackgroundPanelNode);
}
