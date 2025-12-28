using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b.Nodes;

/// <summary>
/// A node that applies theme customizations to its child subtree.
/// ThemePanel is not focusable - focus passes through to the child.
/// </summary>
public sealed class ThemePanelNode : Hex1bNode
{
    /// <summary>
    /// The function that transforms the theme for this panel's subtree.
    /// Receives the current theme and returns the theme to use for children.
    /// </summary>
    public Func<Hex1bTheme, Hex1bTheme>? ThemeMutator { get; set; }
    
    /// <summary>
    /// The child node to render with the customized theme.
    /// </summary>
    public Hex1bNode? Child { get; set; }

    public override Size Measure(Constraints constraints)
    {
        // ThemePanel doesn't add any size - child takes all available space
        return Child?.Measure(constraints) ?? constraints.Constrain(Size.Zero);
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);

        // Child gets the full bounds
        Child?.Arrange(bounds);
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (Child != null)
        {
            foreach (var focusable in Child.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (Child == null) return;
        
        // Save the original theme
        var originalTheme = context.Theme;
        
        // Apply the theme mutator if we have one
        // We clone first so the mutator can safely modify without affecting the original
        if (ThemeMutator != null)
        {
            context.Theme = ThemeMutator(originalTheme.Clone());
        }
        
        // Render child content with the (possibly mutated) theme
        Child.Render(context);
        
        // Restore original theme
        context.Theme = originalTheme;
    }

    /// <summary>
    /// Gets the direct children of this container for input routing.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Child != null) yield return Child;
    }
}
