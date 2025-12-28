namespace Hex1b;

using Hex1b.Theming;
using Hex1b.Widgets;

/// <summary>
/// Extension methods for building ThemePanelWidget.
/// </summary>
public static class ThemePanelExtensions
{
    /// <summary>
    /// Creates a ThemePanel that applies theme customizations to a child widget tree.
    /// The theme mutator function receives the current theme and returns the theme to use.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="themeMutator">
    /// A function that receives a clone of the current theme and returns the theme to use.
    /// The theme is already cloned, so you can safely call Set() directly.
    /// </param>
    /// <param name="child">The child widget to render with the customized theme.</param>
    /// <returns>A ThemePanelWidget.</returns>
    /// <example>
    /// <code>
    /// ctx.ThemePanel(
    ///     theme => theme.Set(ButtonTheme.BackgroundColor, Hex1bColor.Blue),
    ///     ctx.Button("Blue Button")
    /// )
    /// </code>
    /// </example>
    public static ThemePanelWidget ThemePanel<TParent>(
        this WidgetContext<TParent> ctx,
        Func<Hex1bTheme, Hex1bTheme> themeMutator,
        Hex1bWidget child)
        where TParent : Hex1bWidget
        => new(themeMutator, child);

    /// <summary>
    /// Creates a ThemePanel with a VStack child that applies theme customizations.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="themeMutator">
    /// A function that receives a clone of the current theme and returns the theme to use.
    /// The theme is already cloned, so you can safely call Set() directly.
    /// </param>
    /// <param name="builder">A builder function that creates the VStack children.</param>
    /// <returns>A ThemePanelWidget containing a VStack.</returns>
    /// <example>
    /// <code>
    /// ctx.ThemePanel(
    ///     theme => theme
    ///         .Set(TextTheme.ForegroundColor, Hex1bColor.Green)
    ///         .Set(ButtonTheme.ForegroundColor, Hex1bColor.Yellow),
    ///     v => [
    ///         v.Text("Green text"),
    ///         v.Button("Yellow button")
    ///     ]
    /// )
    /// </code>
    /// </example>
    public static ThemePanelWidget ThemePanel<TParent>(
        this WidgetContext<TParent> ctx,
        Func<Hex1bTheme, Hex1bTheme> themeMutator,
        Func<WidgetContext<VStackWidget>, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<VStackWidget>();
        var children = builder(childCtx);
        return new ThemePanelWidget(themeMutator, new VStackWidget(children));
    }
}
