using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for building TabBar widgets.
/// </summary>
public static class TabBarExtensions
{
    /// <summary>
    /// Creates a TabBar widget using a builder pattern.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="builder">A function that builds the tabs using a TabPanelContext.</param>
    /// <returns>A TabBarWidget.</returns>
    /// <example>
    /// <code>
    /// ctx.TabBar(tp => [
    ///     tp.Tab("Tab 1", null).Selected(),
    ///     tp.Tab("Tab 2", null),
    ///     tp.Tab("Tab 3", null)
    /// ])
    /// </code>
    /// </example>
    public static TabBarWidget TabBar<TParent>(
        this WidgetContext<TParent> ctx,
        Func<TabPanelContext, IEnumerable<TabItemWidget>> builder)
        where TParent : Hex1bWidget
    {
        var tabPanelContext = new TabPanelContext();
        var tabs = builder(tabPanelContext).ToList();
        return new TabBarWidget(tabs);
    }

    /// <summary>
    /// Creates a TabBar widget with simple string titles.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="titles">The tab titles.</param>
    /// <returns>A TabBarWidget.</returns>
    public static TabBarWidget TabBar<TParent>(
        this WidgetContext<TParent> ctx,
        params string[] titles)
        where TParent : Hex1bWidget
    {
        var tabs = titles.Select(t => new TabItemWidget(t, null)).ToList();
        return new TabBarWidget(tabs);
    }
}
