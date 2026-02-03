using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for building TabPanel widgets.
/// </summary>
public static class TabPanelExtensions
{
    /// <summary>
    /// Creates a TabPanel widget using a builder pattern.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="builder">A function that builds the tabs using a TabPanelContext.</param>
    /// <returns>A TabPanelWidget.</returns>
    /// <example>
    /// <code>
    /// ctx.TabPanel(tp => [
    ///     tp.Tab("Overview", t => [
    ///         t.Text("Overview content")
    ///     ]),
    ///     tp.Tab("Settings", t => [
    ///         t.Text("Settings content")
    ///     ])
    /// ])
    /// </code>
    /// </example>
    public static TabPanelWidget TabPanel<TParent>(
        this WidgetContext<TParent> ctx,
        Func<TabPanelContext, IEnumerable<TabItemWidget>> builder)
        where TParent : Hex1bWidget
    {
        var tabPanelContext = new TabPanelContext();
        var tabs = builder(tabPanelContext).ToList();
        return new TabPanelWidget(tabs);
    }

    /// <summary>
    /// Creates a TabPanel widget with pre-built tabs.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="tabs">The tabs to display.</param>
    /// <returns>A TabPanelWidget.</returns>
    public static TabPanelWidget TabPanel<TParent>(
        this WidgetContext<TParent> ctx,
        IEnumerable<TabItemWidget> tabs)
        where TParent : Hex1bWidget
    {
        return new TabPanelWidget(tabs.ToList());
    }

    /// <summary>
    /// Sets the selected tab index.
    /// </summary>
    /// <param name="widget">The TabPanel widget.</param>
    /// <param name="index">The index of the tab to select.</param>
    /// <returns>A new TabPanelWidget with the selected index set.</returns>
    public static TabPanelWidget SelectedIndex(this TabPanelWidget widget, int index)
        => widget.WithSelectedIndex(index);
}
