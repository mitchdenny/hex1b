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
public sealed record TabBarWidget(IReadOnlyList<TabItemWidget> Tabs) : Hex1bWidget
{
    /// <summary>
    /// The currently selected tab index. When null, the node manages its own state.
    /// </summary>
    public int? SelectedIndex { get; init; }

    /// <summary>
    /// Handler called when the selected tab changes.
    /// </summary>
    internal Func<TabSelectionChangedEventArgs, Task>? SelectionChangedHandler { get; init; }

    /// <summary>
    /// The position of tabs (top or bottom). Used for rendering style.
    /// </summary>
    public TabPosition Position { get; init; } = TabPosition.Auto;

    /// <summary>
    /// Sets the selected tab index.
    /// </summary>
    /// <param name="index">The index of the tab to select.</param>
    public TabBarWidget WithSelectedIndex(int index)
        => this with { SelectedIndex = index };

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

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as TabBarNode ?? new TabBarNode();

        // Sync state from widget if user is controlling it
        if (SelectedIndex.HasValue)
        {
            node.SelectedIndex = SelectedIndex.Value;
        }

        // Ensure selected index is valid
        if (node.SelectedIndex >= Tabs.Count)
        {
            node.SelectedIndex = Math.Max(0, Tabs.Count - 1);
        }

        // Store tab info
        node.Tabs = Tabs.Select(t => new TabBarNode.TabInfo(t.Title, t.Icon, t.IsDisabled)).ToList();
        node.SelectionChangedHandler = SelectionChangedHandler;

        // Detect position from context
        node.Position = DetectPosition(context);

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

    internal override Type GetExpectedNodeType() => typeof(TabBarNode);
}
