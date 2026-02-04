using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A complete tabbed panel widget with a tab bar and content area.
/// Supports composable tab content with automatic tab switching.
/// </summary>
/// <param name="Tabs">The list of tabs to display.</param>
/// <example>
/// <code>
/// ctx.TabPanel(tp => [
///     tp.Tab("Overview", t => [t.Text("Overview content")]),
///     tp.Tab("Settings", t => [t.Text("Settings content")]).Selected(),
///     tp.Tab("Advanced", t => [t.Text("Advanced content")])
/// ])
/// </code>
/// </example>
public sealed record TabPanelWidget(IReadOnlyList<TabItemWidget> Tabs) : Hex1bWidget
{
    /// <summary>
    /// Handler called when the selected tab changes.
    /// </summary>
    internal Func<TabSelectionChangedEventArgs, Task>? SelectionChangedHandler { get; init; }

    /// <summary>
    /// The position of tabs (top or bottom).
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
    public TabPanelWidget OnSelectionChanged(Action<TabSelectionChangedEventArgs> handler)
        => this with { SelectionChangedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets the async handler for selection changes.
    /// </summary>
    /// <param name="handler">The async handler to call when selection changes.</param>
    public TabPanelWidget OnSelectionChanged(Func<TabSelectionChangedEventArgs, Task> handler)
        => this with { SelectionChangedHandler = handler };

    /// <summary>
    /// Sets the tab position to top.
    /// </summary>
    public TabPanelWidget TabsOnTop()
        => this with { Position = TabPosition.Top };

    /// <summary>
    /// Sets the tab position to bottom.
    /// </summary>
    public TabPanelWidget TabsOnBottom()
        => this with { Position = TabPosition.Bottom };

    /// <summary>
    /// Sets the rendering mode to full (with visual separators).
    /// </summary>
    public TabPanelWidget Full()
        => this with { RenderMode = TabBarRenderMode.Full };

    /// <summary>
    /// Sets the rendering mode to compact (just the tab row).
    /// </summary>
    public TabPanelWidget Compact()
        => this with { RenderMode = TabBarRenderMode.Compact };

    /// <summary>
    /// Enables or disables paging arrows for tab overflow navigation.
    /// </summary>
    public TabPanelWidget Paging(bool enabled = true)
        => this with { ShowPaging = enabled };

    /// <summary>
    /// Enables or disables the dropdown selector for quick tab navigation.
    /// </summary>
    public TabPanelWidget Selector(bool enabled = true)
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

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as TabPanelNode ?? new TabPanelNode();

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

        // Store handlers and settings
        node.SelectionChangedHandler = SelectionChangedHandler;
        node.Position = DetectPosition(context);
        node.RenderMode = RenderMode;
        node.ShowPaging = ShowPaging;
        node.ShowSelector = ShowSelector;
        node.TabCount = Tabs.Count;

        // Store tab info for the tab bar (include IsSelected for rendering)
        node.Tabs = Tabs.Select((t, i) => new TabBarNode.TabInfo(
            t.Title, 
            t.Icon, 
            t.IsDisabled,
            i == node.SelectedIndex,
            t.LeftIcons,
            t.RightIcons)).ToList();

        // Build and reconcile the selected tab's content
        if (Tabs.Count > 0 && node.SelectedIndex >= 0 && node.SelectedIndex < Tabs.Count)
        {
            var selectedTab = Tabs[node.SelectedIndex];
            var contentWidget = selectedTab.BuildContent();
            node.Content = await context.ReconcileChildAsync(node.Content, contentWidget, node);
        }
        else
        {
            node.Content = null;
        }

        return node;
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

    internal override Type GetExpectedNodeType() => typeof(TabPanelNode);
}
