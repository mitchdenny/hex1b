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
    /// Whether to show a selection column with checkboxes.
    /// </summary>
    public bool ShowSelectionColumn { get; init; }

    /// <summary>
    /// Selector function to determine if a row is selected.
    /// Called during render to determine checkbox state.
    /// </summary>
    internal Func<TRow, bool>? IsSelectedSelector { get; init; }

    /// <summary>
    /// Callback invoked when a row's selection state changes.
    /// Receives the row and the new selection state (true = selected, false = deselected).
    /// </summary>
    internal Action<TRow, bool>? SelectionChangedCallback { get; init; }

    /// <summary>
    /// Callback invoked when "select all" is triggered from the header checkbox.
    /// </summary>
    internal Action? SelectAllCallback { get; init; }

    /// <summary>
    /// Callback invoked when "deselect all" is triggered from the header checkbox.
    /// </summary>
    internal Action? DeselectAllCallback { get; init; }

    /// <summary>
    /// The render mode for the table (Compact or Full).
    /// </summary>
    public TableRenderMode RenderMode { get; init; } = TableRenderMode.Compact;

    /// <summary>
    /// Handler called when focus changes.
    /// </summary>
    internal Func<object?, Task>? FocusChangedHandler { get; init; }

    /// <summary>
    /// Handler called when a row is activated (Enter key or double-click).
    /// </summary>
    internal Func<object, TRow, Task>? RowActivatedHandler { get; init; }

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
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

        if (node.ShowSelectionColumn != ShowSelectionColumn)
        {
            node.ShowSelectionColumn = ShowSelectionColumn;
            needsDirty = true;
        }

        // Selection callbacks (always update, no dirty check needed)
        node.IsSelectedSelector = IsSelectedSelector;
        node.SelectionChangedCallback = SelectionChangedCallback;
        node.SelectAllCallback = SelectAllCallback;
        node.DeselectAllCallback = DeselectAllCallback;
        
        // Pass invalidate callback for INotifyCollectionChanged support
        node.InvalidateCallback = context.InvalidateCallback;

        if (node.RenderMode != RenderMode)
        {
            node.RenderMode = RenderMode;
            needsDirty = true;
        }

        node.FocusChangedHandler = FocusChangedHandler;
        node.RowActivatedHandler = RowActivatedHandler;

        // Build row widgets and reconcile them
        await ReconcileRowWidgets(node, context);

        if (needsDirty)
        {
            node.MarkDirty();
        }

        return node;
    }

    /// <summary>
    /// Builds row widgets from cell data and reconciles them as children.
    /// </summary>
    private async Task ReconcileRowWidgets(TableNode<TRow> node, ReconcileContext context)
    {
        // Build header cells and column definitions
        var headerCells = HeaderBuilder?.Invoke(new TableHeaderContext());
        var columnDefs = BuildColumnDefs(headerCells);
        node.SetColumnDefs(columnDefs);
        
        // Reconcile header row
        if (headerCells != null && headerCells.Count > 0)
        {
            var headerWidget = new TableRowWidget(
                headerCells, 
                columnDefs, 
                IsHeader: true,
                ShowSelectionColumn: ShowSelectionColumn,
                RenderMode: RenderMode
            );
            node.HeaderRowNode = await context.ReconcileChildAsync(
                node.HeaderRowNode, 
                headerWidget, 
                node
            ) as TableRowNode;
        }
        else
        {
            node.HeaderRowNode = null;
        }
        
        // Reconcile data rows
        if (Data != null && Data.Count > 0 && RowBuilder != null)
        {
            var rowContext = new TableRowContext();
            node.DataRowNodes ??= [];
            
            // Resize to match data count
            while (node.DataRowNodes.Count > Data.Count)
            {
                node.DataRowNodes.RemoveAt(node.DataRowNodes.Count - 1);
            }
            while (node.DataRowNodes.Count < Data.Count)
            {
                node.DataRowNodes.Add(null!);
            }
            
            for (int i = 0; i < Data.Count; i++)
            {
                var rowData = Data[i];
                var rowKey = RowKeySelector?.Invoke(rowData) ?? i;
                var isFocused = Equals(rowKey, FocusedKey ?? node.GetInternalFocusedKey());
                var isSelected = IsSelectedSelector?.Invoke(rowData) ?? false;
                
                var rowState = new TableRowState 
                { 
                    RowIndex = i, 
                    IsFocused = isFocused, 
                    IsSelected = isSelected,
                    RowKey = rowKey,
                    IsFirst = i == 0,
                    IsLast = i == Data.Count - 1
                };
                var rowCells = RowBuilder(rowContext, rowData, rowState);
                
                var rowWidget = new TableRowWidget(
                    rowCells, 
                    columnDefs, 
                    IsHighlighted: isFocused,
                    IsSelected: isSelected,
                    ShowSelectionColumn: ShowSelectionColumn,
                    RenderMode: RenderMode
                );
                
                node.DataRowNodes[i] = await context.ReconcileChildAsync(
                    node.DataRowNodes[i], 
                    rowWidget, 
                    node
                ) as TableRowNode ?? new TableRowNode();
            }
        }
        else
        {
            node.DataRowNodes = null;
        }
        
        // Reconcile footer row
        var footerCells = FooterBuilder?.Invoke(new TableFooterContext());
        if (footerCells != null && footerCells.Count > 0)
        {
            var footerWidget = new TableRowWidget(
                footerCells, 
                columnDefs,
                ShowSelectionColumn: ShowSelectionColumn,
                IsFooter: true,
                RenderMode: RenderMode
            );
            node.FooterRowNode = await context.ReconcileChildAsync(
                node.FooterRowNode, 
                footerWidget, 
                node
            ) as TableRowNode;
        }
        else
        {
            node.FooterRowNode = null;
        }
    }

    /// <summary>
    /// Builds column definitions from header cells.
    /// </summary>
    private static List<TableColumnDef> BuildColumnDefs(IReadOnlyList<TableCell>? headerCells)
    {
        var defs = new List<TableColumnDef>();
        
        if (headerCells == null) return defs;
        
        foreach (var cell in headerCells)
        {
            defs.Add(new TableColumnDef(
                cell.Width ?? Layout.SizeHint.Fill,
                cell.Alignment
            ));
        }
        
        return defs;
    }

    internal override Type GetExpectedNodeType() => typeof(TableNode<TRow>);
}
