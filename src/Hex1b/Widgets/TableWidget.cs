using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A widget for displaying tabular data with columns, rows, and optional header/footer.
/// </summary>
/// <typeparam name="TRow">The type of data for each row.</typeparam>
public record TableWidget<TRow> : Hex1bWidget
{
    /// <summary>
    /// The data source for the table rows. When null, loading state is shown.
    /// When empty, empty state is shown.
    /// </summary>
    public IReadOnlyList<TRow>? Data { get; init; }

    /// <summary>
    /// Builder function for header cells. Defines column structure.
    /// </summary>
    internal Func<TableHeaderContext, IReadOnlyList<TableCell>>? HeaderBuilder { get; init; }

    /// <summary>
    /// Builder function for row cells. Called once per row in Data.
    /// Receives the row context, row data, and row state (focus, selection, index).
    /// </summary>
    internal Func<TableRowContext, TRow, TableRowState, IReadOnlyList<TableCell>>? RowBuilder { get; init; }

    /// <summary>
    /// Builder function for footer cells.
    /// </summary>
    internal Func<TableFooterContext, IReadOnlyList<TableCell>>? FooterBuilder { get; init; }

    /// <summary>
    /// Builder function for empty state widget, shown when Data is empty.
    /// </summary>
    internal Func<RootContext, Hex1bWidget>? EmptyBuilder { get; init; }

    /// <summary>
    /// Builder function for loading row cells, shown when Data is null.
    /// </summary>
    internal Func<TableLoadingContext, int, IReadOnlyList<TableCell>>? LoadingRowBuilder { get; init; }

    /// <summary>
    /// Number of loading placeholder rows to show.
    /// </summary>
    internal int LoadingRowCount { get; init; } = 3;

    /// <summary>
    /// Selector function to extract a unique key from each row.
    /// Used for stable selection tracking across data changes.
    /// If not specified, row index is used as the key.
    /// </summary>
    internal Func<TRow, object>? RowKeySelector { get; init; }

    /// <summary>
    /// The key of the currently focused row (keyboard navigation cursor).
    /// </summary>
    public object? FocusedKey { get; init; }

    /// <summary>
    /// The set of keys for selected rows.
    /// </summary>
    public IReadOnlySet<object>? SelectedKeys { get; init; }

    /// <summary>
    /// Handler called when focus changes.
    /// </summary>
    internal Func<object?, Task>? FocusChangedHandler { get; init; }

    /// <summary>
    /// Handler called when selection changes.
    /// </summary>
    internal Func<IReadOnlySet<object>, Task>? SelectionChangedHandler { get; init; }

    /// <summary>
    /// Handler called when a row is activated (Enter key or double-click).
    /// </summary>
    internal Func<object, TRow, Task>? RowActivatedHandler { get; init; }

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as TableNode<TRow> ?? new TableNode<TRow>();

        // Check if we need to mark dirty
        bool needsDirty = false;
        
        if (!ReferenceEquals(node.Data, Data))
        {
            // Check content equality for collections
            if (node.Data is null || Data is null || !node.Data.SequenceEqual(Data))
            {
                needsDirty = true;
            }
            node.Data = Data;
        }

        if (node.HeaderBuilder != HeaderBuilder)
        {
            node.HeaderBuilder = HeaderBuilder;
            needsDirty = true;
        }

        if (node.RowBuilder != RowBuilder)
        {
            node.RowBuilder = RowBuilder;
            needsDirty = true;
        }

        if (node.FooterBuilder != FooterBuilder)
        {
            node.FooterBuilder = FooterBuilder;
            needsDirty = true;
        }

        if (node.EmptyBuilder != EmptyBuilder)
        {
            node.EmptyBuilder = EmptyBuilder;
            needsDirty = true;
        }

        if (node.LoadingRowBuilder != LoadingRowBuilder)
        {
            node.LoadingRowBuilder = LoadingRowBuilder;
            needsDirty = true;
        }

        if (node.LoadingRowCount != LoadingRowCount)
        {
            node.LoadingRowCount = LoadingRowCount;
            needsDirty = true;
        }

        if (node.RowKeySelector != RowKeySelector)
        {
            node.RowKeySelector = RowKeySelector;
            needsDirty = true;
        }

        if (!Equals(node.FocusedKey, FocusedKey))
        {
            node.FocusedKey = FocusedKey;
            needsDirty = true;
        }

        if (!SetEquals(node.SelectedKeys, SelectedKeys))
        {
            node.SelectedKeys = SelectedKeys;
            needsDirty = true;
        }

        node.FocusChangedHandler = FocusChangedHandler;
        node.SelectionChangedHandler = SelectionChangedHandler;
        node.RowActivatedHandler = RowActivatedHandler;

        if (needsDirty)
        {
            node.MarkDirty();
        }

        return Task.FromResult<Hex1bNode>(node);
    }

    private static bool SetEquals(IReadOnlySet<object>? a, IReadOnlySet<object>? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return a.SetEquals(b);
    }

    internal override Type GetExpectedNodeType() => typeof(TableNode<TRow>);
}
