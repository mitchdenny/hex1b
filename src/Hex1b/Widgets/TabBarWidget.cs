using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Event arguments for tab selection changes.
/// </summary>
public sealed class TabSelectionChangedEventArgs : EventArgs
{
    /// <summary>
    /// The index of the newly selected tab.
    /// </summary>
    public int SelectedIndex { get; init; }

    /// <summary>
    /// The index of the previously selected tab, or -1 if none.
    /// </summary>
    public int PreviousIndex { get; init; }

    /// <summary>
    /// The title of the newly selected tab.
    /// </summary>
    public string SelectedTitle { get; init; } = "";
}

/// <summary>
/// A horizontal tab bar widget that displays tabs with optional overflow navigation.
/// Can be used standalone or as part of a TabPanel.
/// </summary>
/// <param name="Tabs">The list of tabs to display.</param>
/// <example>
/// <code>
/// ctx.TabBar(tb => [
///     tb.Tab("Overview").Selected(),
///     tb.Tab("Settings"),
///     tb.Tab("Advanced")
/// ])
/// </code>
/// </example>
public sealed record TabBarWidget(IReadOnlyList<TabItemWidget> Tabs) : Hex1bWidget
{
    /// <summary>
    /// Handler called when the selected tab changes.
    /// </summary>
    internal Func<TabSelectionChangedEventArgs, Task>? SelectionChangedHandler { get; init; }

    /// <summary>
    /// The position of tabs (top or bottom). Used for rendering style.
    /// </summary>
    public TabPosition Position { get; init; } = TabPosition.Auto;

    /// <summary>
    /// The rendering mode for the tab bar.
    /// </summary>
    public TabBarRenderMode RenderMode { get; init; } = TabBarRenderMode.Full;

    /// <summary>
    /// Whether to show paging arrows when tabs overflow.
    /// </summary>
    public bool ShowPaging { get; init; } = true;

    /// <summary>
    /// Whether to show the dropdown selector for quick tab navigation.
    /// </summary>
    public bool ShowSelector { get; init; } = false;

    /// <summary>
    /// Sets the handler for selection changes.
    /// </summary>
    /// <param name="handler">The handler to call when selection changes.</param>
    public TabBarWidget OnSelectionChanged(Action<TabSelectionChangedEventArgs> handler)
        => this with { SelectionChangedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets the async handler for selection changes.
    /// </summary>
    /// <param name="handler">The async handler to call when selection changes.</param>
    public TabBarWidget OnSelectionChanged(Func<TabSelectionChangedEventArgs, Task> handler)
        => this with { SelectionChangedHandler = handler };

    /// <summary>
    /// Sets the tab position to top.
    /// </summary>
    public TabBarWidget TabsOnTop()
        => this with { Position = TabPosition.Top };

    /// <summary>
    /// Sets the tab position to bottom.
    /// </summary>
    public TabBarWidget TabsOnBottom()
        => this with { Position = TabPosition.Bottom };

    /// <summary>
    /// Sets the rendering mode to full (with visual separators).
    /// </summary>
    public TabBarWidget Full()
        => this with { RenderMode = TabBarRenderMode.Full };

    /// <summary>
    /// Sets the rendering mode to compact (just the tab row).
    /// </summary>
    public TabBarWidget Compact()
        => this with { RenderMode = TabBarRenderMode.Compact };

    /// <summary>
    /// Enables or disables paging arrows for tab overflow navigation.
    /// </summary>
    public TabBarWidget Paging(bool enabled = true)
        => this with { ShowPaging = enabled };

    /// <summary>
    /// Enables or disables the dropdown selector for quick tab navigation.
    /// </summary>
    public TabBarWidget Selector(bool enabled = true)
        => this with { ShowSelector = enabled };

    /// <summary>
    /// Gets the index of the selected tab based on IsSelected flags.
    /// Returns the index of the first tab with IsSelected=true, or 0 if none.
    /// </summary>
    private int GetSelectedIndex()
    {
        for (int i = 0; i < Tabs.Count; i++)
        {
            if (Tabs[i].IsSelected)
                return i;
        }
        return 0; // Default to first tab
    }

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as TabBarNode ?? new TabBarNode();

        // Determine selected index from tab widgets
        var selectedIndex = GetSelectedIndex();
        
        // Check if any tab has explicit selection - if so, this is a controlled component
        var hasExplicitSelection = Tabs.Any(t => t.IsSelected);
        if (hasExplicitSelection)
        {
            node.SelectedIndex = selectedIndex;
        }

        // Ensure selected index is valid
        if (node.SelectedIndex >= Tabs.Count)
        {
            node.SelectedIndex = Math.Max(0, Tabs.Count - 1);
        }

        // Store tab info (include IsSelected for rendering)
        node.Tabs = Tabs.Select((t, i) => new TabBarNode.TabInfo(
            t.Title, 
            t.Icon, 
            t.IsDisabled,
            i == node.SelectedIndex,
            t.LeftActions,
            t.RightActions)).ToList();
        node.SelectionChangedHandler = SelectionChangedHandler;

        // Detect position from context
        node.Position = DetectPosition(context);
        node.RenderMode = RenderMode;
        node.ShowPaging = ShowPaging;
        node.ShowSelector = ShowSelector;

        return Task.FromResult<Hex1bNode>(node);
    }

    private TabPosition DetectPosition(ReconcileContext context)
    {
        if (Position != TabPosition.Auto)
            return Position;

        // Auto-detect based on position in VStack
        var isVertical = context.LayoutAxis == LayoutAxis.Vertical;
        var isFirst = context.ChildIndex == 0;
        var isLast = context.ChildIndex.HasValue && context.ChildCount.HasValue
                     && context.ChildIndex.Value == context.ChildCount.Value - 1;

        if (isVertical)
        {
            // In VStack: first = top, last = bottom
            return isLast && !isFirst ? TabPosition.Bottom : TabPosition.Top;
        }

        // Default to top if not in VStack
        return TabPosition.Top;
    }

    internal override Type GetExpectedNodeType() => typeof(TabBarNode);
}
