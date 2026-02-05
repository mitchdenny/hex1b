namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating <see cref="WindowPanelWidget"/>.
/// </summary>
public static class WindowPanelExtensions
{
    /// <summary>
    /// Creates a WindowPanel that can host floating windows.
    /// Windows are rendered on top of the main content.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="content">The main content widget displayed behind windows.</param>
    /// <returns>A new WindowPanelWidget.</returns>
    /// <example>
    /// <code>
    /// ctx.WindowPanel(
    ///     ctx.VStack(v => [
    ///         v.Text("Main content"),
    ///         v.Button("Open Window").OnClick(e => {
    ///             e.Context.Windows.Open(
    ///                 id: "settings",
    ///                 title: "Settings",
    ///                 content: () => e.Context.VStack(v => [
    ///                     v.Text("Window content")
    ///                 ])
    ///             );
    ///         })
    ///     ])
    /// )
    /// </code>
    /// </example>
    public static WindowPanelWidget WindowPanel<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bWidget content)
        where TParent : Hex1bWidget
    {
        return new WindowPanelWidget(content);
    }

    /// <summary>
    /// Creates a WindowPanel with content built from a context.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="contentBuilder">A function that builds the content widget.</param>
    /// <returns>A new WindowPanelWidget.</returns>
    public static WindowPanelWidget WindowPanel<TParent>(
        this WidgetContext<TParent> ctx,
        Func<WidgetContext<WindowPanelWidget>, Hex1bWidget> contentBuilder)
        where TParent : Hex1bWidget
    {
        var childContext = new WidgetContext<WindowPanelWidget>();
        var content = contentBuilder(childContext);
        return new WindowPanelWidget(content);
    }

    /// <summary>
    /// Creates a named WindowPanel that can host floating windows.
    /// Use named panels when you have multiple WindowPanels in your app.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="name">The unique name for this panel.</param>
    /// <param name="contentBuilder">A function that builds the content widget.</param>
    /// <returns>A new WindowPanelWidget.</returns>
    /// <example>
    /// <code>
    /// // Create named panels
    /// ctx.WindowPanel("editor", w => w.Text("Editor area"));
    /// ctx.WindowPanel("preview", w => w.Text("Preview area"));
    /// 
    /// // Access specific panel from event handler
    /// e.Windows["editor"].Open(...);
    /// </code>
    /// </example>
    public static WindowPanelWidget WindowPanel<TParent>(
        this WidgetContext<TParent> ctx,
        string name,
        Func<WidgetContext<WindowPanelWidget>, Hex1bWidget> contentBuilder)
        where TParent : Hex1bWidget
    {
        var childContext = new WidgetContext<WindowPanelWidget>();
        var content = contentBuilder(childContext);
        return new WindowPanelWidget(content, name);
    }

    /// <summary>
    /// Allows windows to be dragged outside the panel bounds.
    /// Scrollbars appear when windows extend beyond the visible area.
    /// </summary>
    /// <param name="widget">The WindowPanelWidget to configure.</param>
    /// <returns>A new WindowPanelWidget with AllowOutOfBounds enabled.</returns>
    public static WindowPanelWidget Unbounded(this WindowPanelWidget widget)
        => widget with { AllowOutOfBounds = true };
}
