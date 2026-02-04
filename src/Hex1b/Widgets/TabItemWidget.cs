using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Represents a single tab within a TabPanel or TabBar.
/// Contains the tab title, optional icon, and content builder.
/// </summary>
/// <param name="Title">The title displayed in the tab header.</param>
/// <param name="ContentBuilder">A function that builds the tab content widgets.</param>
public sealed record TabItemWidget(
    string Title,
    Func<WidgetContext<VStackWidget>, IEnumerable<Hex1bWidget>>? ContentBuilder) : Hex1bWidget
{
    /// <summary>
    /// Optional icon displayed before the tab title.
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Whether this tab is disabled (cannot be selected).
    /// </summary>
    public bool IsDisabled { get; init; }

    /// <summary>
    /// Whether this tab is selected.
    /// When multiple tabs have IsSelected=true, the first one wins.
    /// </summary>
    public bool IsSelected { get; init; }

    /// <summary>
    /// Action icons displayed on the left side of the tab, in order.
    /// These are composable IconWidgets that can have click handlers.
    /// </summary>
    public IReadOnlyList<IconWidget> LeftActions { get; init; } = [];

    /// <summary>
    /// Action icons displayed on the right side of the tab, in order.
    /// These are composable IconWidgets that can have click handlers.
    /// </summary>
    public IReadOnlyList<IconWidget> RightActions { get; init; } = [];

    /// <summary>
    /// Sets the icon for this tab.
    /// </summary>
    /// <param name="icon">The icon string (emoji or text).</param>
    public TabItemWidget WithIcon(string icon)
        => this with { Icon = icon };

    /// <summary>
    /// Sets whether this tab is disabled.
    /// </summary>
    /// <param name="disabled">True to disable the tab.</param>
    public TabItemWidget Disabled(bool disabled = true)
        => this with { IsDisabled = disabled };

    /// <summary>
    /// Marks this tab as selected.
    /// When multiple tabs are marked as selected, the first one wins.
    /// </summary>
    /// <param name="selected">True to select this tab, false otherwise.</param>
    /// <example>
    /// <code>
    /// // Static selection
    /// tp.Tab("Settings", t => [...]).Selected()
    /// 
    /// // Conditional selection based on state
    /// tp.Tab(doc.Name, t => [...]).Selected(doc.Id == currentDocId)
    /// </code>
    /// </example>
    public TabItemWidget Selected(bool selected = true)
        => this with { IsSelected = selected };

    /// <summary>
    /// Adds action icons to the left side of the tab using a builder.
    /// Actions are displayed in the order they are added.
    /// </summary>
    /// <param name="builder">A function that returns the icons to add.</param>
    /// <example>
    /// <code>
    /// tp.Tab("Document", t => [...])
    ///   .WithLeftActions(a => [a.Icon("📌").OnClick(_ => Pin())])
    /// </code>
    /// </example>
    public TabItemWidget WithLeftActions(Func<WidgetContext<TabItemWidget>, IEnumerable<IconWidget>> builder)
    {
        var ctx = new WidgetContext<TabItemWidget>();
        var icons = builder(ctx).ToList();
        return this with { LeftActions = icons };
    }

    /// <summary>
    /// Adds action icons to the right side of the tab using a builder.
    /// Actions are displayed in the order they are added.
    /// </summary>
    /// <param name="builder">A function that returns the icons to add.</param>
    /// <example>
    /// <code>
    /// tp.Tab("Document.cs", t => [...])
    ///   .WithRightActions(a => [a.Icon("×").OnClick(_ => Close())])
    /// </code>
    /// </example>
    public TabItemWidget WithRightActions(Func<WidgetContext<TabItemWidget>, IEnumerable<IconWidget>> builder)
    {
        var ctx = new WidgetContext<TabItemWidget>();
        var icons = builder(ctx).ToList();
        return this with { RightActions = icons };
    }

    /// <summary>
    /// Builds the content widget tree for this tab.
    /// </summary>
    internal Hex1bWidget? BuildContent()
    {
        if (ContentBuilder == null)
            return null;

        var ctx = new WidgetContext<VStackWidget>();
        var children = ContentBuilder(ctx).ToList();
        return new VStackWidget(children);
    }

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        // TabItemWidget is not directly reconciled - it's used by TabBarNode/TabPanelNode
        // to build the tab header and content. This method should not be called directly.
        throw new InvalidOperationException(
            "TabItemWidget should not be reconciled directly. Use TabPanel or TabBar instead.");
    }

    internal override Type GetExpectedNodeType() => typeof(TabItemNode);
}
