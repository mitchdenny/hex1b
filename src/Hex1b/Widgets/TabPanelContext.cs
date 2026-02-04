namespace Hex1b.Widgets;

/// <summary>
/// Provides a fluent API context for building TabPanel structures.
/// This context exposes tab-creation methods (Tab) to guide developers toward the correct API usage.
/// </summary>
public readonly struct TabPanelContext
{
    /// <summary>
    /// Creates a tab with the specified title and content builder.
    /// </summary>
    /// <param name="title">The title displayed in the tab header.</param>
    /// <param name="contentBuilder">A function that builds the tab content widgets.</param>
    /// <returns>A TabItemWidget configured with the specified title and content.</returns>
    public TabItemWidget Tab(
        string title,
        Func<WidgetContext<VStackWidget>, IEnumerable<Hex1bWidget>> contentBuilder)
    {
        return new TabItemWidget(title, contentBuilder);
    }

    /// <summary>
    /// Creates a tab with the specified title, icon, and content builder.
    /// </summary>
    /// <param name="title">The title displayed in the tab header.</param>
    /// <param name="icon">The icon displayed before the title.</param>
    /// <param name="contentBuilder">A function that builds the tab content widgets.</param>
    /// <returns>A TabItemWidget configured with the specified title, icon, and content.</returns>
    public TabItemWidget Tab(
        string title,
        string icon,
        Func<WidgetContext<VStackWidget>, IEnumerable<Hex1bWidget>> contentBuilder)
    {
        return new TabItemWidget(title, contentBuilder) { Icon = icon };
    }
}
