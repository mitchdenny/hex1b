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

    protected override Size MeasureCore(Constraints constraints)
    {
        // ThemePanel doesn't add any size - child takes all available space
        return Child?.Measure(constraints) ?? constraints.Constrain(Size.Zero);
    }

    protected override void ArrangeCore(Rect bounds)
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
        
        // Fill our entire bounds with the global background color (if set)
        // This ensures empty areas within the themed region have the correct background
        var bg = context.Theme.GetGlobalBackground();
        if (!bg.IsDefault && Bounds.Width > 0 && Bounds.Height > 0)
        {
            var bgCode = bg.ToBackgroundAnsi();
            var resetCode = context.Theme.GetResetToGlobalCodes();
            var spaces = new string(' ', Bounds.Width);
            
            for (int y = Bounds.Y; y < Bounds.Y + Bounds.Height; y++)
            {
                context.SetCursorPosition(Bounds.X, y);
                context.Write($"{bgCode}{spaces}{resetCode}");
            }
        }
        
        // Use RenderChild for automatic caching support
        context.RenderChild(Child);
        
        // Restore original theme
        context.Theme = originalTheme;
        
        // Reset the render context's style state to match the restored theme
        // This ensures subsequent nodes don't inherit our mutated colors
        var originalResetCode = originalTheme.GetResetToGlobalCodes();
        context.Write(originalResetCode);
    }

    /// <summary>
    /// Gets the direct children of this container for input routing.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Child != null) yield return Child;
    }
}
