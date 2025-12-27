namespace Hex1b;

using Hex1b.Theming;
using Hex1b.Widgets;

/// <summary>
/// Extension methods for building ThemingPanelWidget.
/// </summary>
public static class ThemingPanelExtensions
{
    /// <summary>
    /// Creates a ThemingPanel that scopes theme changes to its child.
    /// The theme builder receives a clone of the current theme and should return the modified theme.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="themeBuilder">A callback that receives a cloned theme and returns the modified theme for children.</param>
    /// <param name="child">The child widget to render with the scoped theme.</param>
    public static ThemingPanelWidget ThemingPanel<TParent>(
        this WidgetContext<TParent> ctx,
        Func<Hex1bTheme, Hex1bTheme> themeBuilder,
        Hex1bWidget child)
        where TParent : Hex1bWidget
        => new(themeBuilder, child);

    /// <summary>
    /// Creates a ThemingPanel that scopes theme changes to a VStack of children.
    /// The theme builder receives a clone of the current theme and should return the modified theme.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="themeBuilder">A callback that receives a cloned theme and returns the modified theme for children.</param>
    /// <param name="builder">A builder function that creates the child widgets.</param>
    public static ThemingPanelWidget ThemingPanel<TParent>(
        this WidgetContext<TParent> ctx,
        Func<Hex1bTheme, Hex1bTheme> themeBuilder,
        Func<WidgetContext<VStackWidget>, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<VStackWidget>();
        var children = builder(childCtx);
        return new ThemingPanelWidget(themeBuilder, new VStackWidget(children));
    }
}
