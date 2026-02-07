namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating <see cref="WindowPanelWidget"/>.
/// </summary>
public static class WindowPanelExtensions
{
    /// <summary>
    /// Creates a WindowPanel that can host floating windows.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <returns>A new WindowPanelWidget.</returns>
    /// <example>
    /// <code>
    /// ctx.WindowPanel()
    ///    .Background(b => b.Surface(...))
    ///    .Unbounded()
    ///    .Fill()
    /// </code>
    /// </example>
    public static WindowPanelWidget WindowPanel<TParent>(this WidgetContext<TParent> ctx)
        where TParent : Hex1bWidget
    {
        return new WindowPanelWidget();
    }

    /// <summary>
    /// Creates a named WindowPanel that can host floating windows.
    /// Use named panels when you have multiple WindowPanels in your app.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="name">The unique name for this panel.</param>
    /// <returns>A new WindowPanelWidget.</returns>
    /// <example>
    /// <code>
    /// // Create named panels
    /// ctx.WindowPanel("editor");
    /// ctx.WindowPanel("preview");
    /// 
    /// // Access specific panel from event handler
    /// e.Windows["editor"].Open(...);
    /// </code>
    /// </example>
    public static WindowPanelWidget WindowPanel<TParent>(
        this WidgetContext<TParent> ctx,
        string name)
        where TParent : Hex1bWidget
    {
        return new WindowPanelWidget(null, name);
    }

    /// <summary>
    /// Creates a WindowPanel with content displayed behind windows.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="content">The content widget displayed behind windows.</param>
    /// <returns>A new WindowPanelWidget.</returns>
    public static WindowPanelWidget WindowPanel<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bWidget content)
        where TParent : Hex1bWidget
    {
        return new WindowPanelWidget(content);
    }

    /// <summary>
    /// Allows windows to be dragged outside the panel bounds.
    /// Scrollbars appear when windows extend beyond the visible area.
    /// </summary>
    /// <param name="widget">The WindowPanelWidget to configure.</param>
    /// <returns>A new WindowPanelWidget with AllowOutOfBounds enabled.</returns>
    public static WindowPanelWidget Unbounded(this WindowPanelWidget widget)
        => widget with { AllowOutOfBounds = true };

    /// <summary>
    /// Sets a decorative background widget that renders behind all content and windows.
    /// The background does not receive focus or input.
    /// </summary>
    /// <param name="widget">The WindowPanelWidget to configure.</param>
    /// <param name="backgroundBuilder">A function that builds the background widget using a context.</param>
    /// <returns>A new WindowPanelWidget with the background configured.</returns>
    /// <example>
    /// <code>
    /// ctx.WindowPanel()
    ///    .Background(b => b.Surface(s => [s.Layer(GradientBackground)]))
    /// </code>
    /// </example>
    public static WindowPanelWidget Background(
        this WindowPanelWidget widget, 
        Func<WidgetContext<Hex1bWidget>, Hex1bWidget> backgroundBuilder)
    {
        var bgContext = new WidgetContext<Hex1bWidget>();
        var background = backgroundBuilder(bgContext);
        return widget with { Background = background };
    }
}
