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
    /// </summary>
    internal Func<TableRowContext, TRow, IReadOnlyList<TableCell>>? RowBuilder { get; init; }

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
    /// The currently selected row index, or null for no selection.
    /// </summary>
    public int? SelectedIndex { get; init; }

    /// <summary>
    /// Handler called when selection changes.
    /// </summary>
    internal Func<int, Task>? SelectionChangedHandler { get; init; }

    /// <summary>
    /// Handler called when a row is activated (Enter key or double-click).
    /// </summary>
    internal Func<int, TRow, Task>? RowActivatedHandler { get; init; }

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

        if (node.SelectedIndex != SelectedIndex)
        {
            node.SelectedIndex = SelectedIndex;
            needsDirty = true;
        }

        node.SelectionChangedHandler = SelectionChangedHandler;
        node.RowActivatedHandler = RowActivatedHandler;

        if (needsDirty)
        {
            node.MarkDirty();
        }

        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(TableNode<TRow>);
}
