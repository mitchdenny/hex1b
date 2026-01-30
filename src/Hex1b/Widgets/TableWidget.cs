using Hex1b.Data;
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
    /// Async data source for virtualized tables. When set, Data is ignored.
    /// </summary>
    public ITableDataSource<TRow>? DataSource { get; init; }

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
        
        // Handle DataSource (takes priority over Data)
        if (DataSource is not null)
        {
            if (!ReferenceEquals(node.DataSource, DataSource))
            {
                node.DataSource = DataSource;
                needsDirty = true;
                // Only clear Data when DataSource changes (not on every reconcile)
                // The cached data will be set via LoadDataAsync
            }
        }
        else if (!ReferenceEquals(node.Data, Data))
        {
            // Check content equality for collections
            if (node.Data is null || Data is null || !node.Data.SequenceEqual(Data))
            {
                needsDirty = true;
            }
            node.Data = Data;
            node.DataSource = null; // Clear DataSource when using Data
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
        
        // Get effective data (either from Data property or loaded from DataSource)
        IReadOnlyList<TRow>? effectiveData = null;
        int totalCount = 0;
        
        if (DataSource is not null)
        {
            // Check if we already have cached data (synchronous path)
            effectiveData = node.GetEffectiveData();
            totalCount = node.GetEffectiveItemCount();
            
            // Only load asynchronously if we don't have data yet
            if (effectiveData is null || effectiveData.Count == 0)
            {
                // First load - get count and initial data
                await node.LoadDataAsync(0, 50, context.CancellationToken);
                effectiveData = node.GetEffectiveData();
                totalCount = node.GetEffectiveItemCount();
            }
            else if (totalCount > 0)
            {
                // We have data, but check if we need to load a different range
                var (startRow, endRow) = node.GetVisibleRowRange(totalCount);
                int rangeCount = Math.Max(50, endRow - startRow);
                
                // LoadDataAsync will return early if range is already cached
                await node.LoadDataAsync(startRow, rangeCount, context.CancellationToken);
                effectiveData = node.GetEffectiveData();
            }
        }
        else if (Data is not null)
        {
            effectiveData = Data;
            totalCount = Data.Count;
        }
        
        // Reconcile data rows (with lazy virtualization)
        if (effectiveData != null && effectiveData.Count > 0 && RowBuilder != null)
        {
            var rowContext = new TableRowContext();
            node.DataRowNodes ??= [];
            
            // Resize to match total count (slots can be null for non-materialized rows)
            while (node.DataRowNodes.Count > totalCount)
            {
                node.DataRowNodes.RemoveAt(node.DataRowNodes.Count - 1);
            }
            while (node.DataRowNodes.Count < totalCount)
            {
                node.DataRowNodes.Add(null!);
            }
            
            // Get the range of rows to materialize (visible + buffer)
            var (startRow, endRow) = node.GetVisibleRowRange(totalCount);
            
            // Calculate max rows that could possibly fit based on available space
            // This prevents building 10k rows on first render when ViewportRowCount is 0
            int maxRowsToBuild;
            if (node.ViewportRowCount > 0)
            {
                // Use actual viewport if already calculated
                maxRowsToBuild = node.ViewportRowCount + 10; // +10 buffer
            }
            else
            {
                // First render: estimate from previous bounds or use sensible default
                // Terminal windows are typically 24-80 rows, so 50 is a safe upper bound
                int estimatedHeight = node.Bounds.Height > 0 ? node.Bounds.Height : 50;
                int headerLines = 3;  // top border + header + separator
                int footerLines = FooterBuilder != null ? 2 : 1;  // footer + bottom border, or just bottom border
                int rowHeight = node.RenderMode == TableRenderMode.Full ? 2 : 1;  // Full mode has separators
                int availableForRows = estimatedHeight - headerLines - footerLines;
                int maxVisibleRows = Math.Max(1, availableForRows / rowHeight);
                maxRowsToBuild = maxVisibleRows + 5;  // +5 buffer for scroll
            }
            
            // For small datasets, build all rows (simpler and avoids edge cases)
            bool buildAllRows = totalCount <= 50;
            
            // Calculate effective range to build
            // On first render (endRow == 0), start from 0 and build up to maxRowsToBuild
            int effectiveStartRow = buildAllRows ? 0 : (endRow > 0 ? startRow : 0);
            int effectiveEndRow = buildAllRows ? totalCount : (endRow > 0 ? Math.Min(endRow, effectiveStartRow + maxRowsToBuild) : Math.Min(totalCount, maxRowsToBuild));
            
            // When using DataSource, only build rows we have data for
            int dataOffset = DataSource is not null ? startRow : 0;
            
            for (int i = 0; i < totalCount; i++)
            {
                // Skip rows outside the effective range
                if (i < effectiveStartRow || i >= effectiveEndRow)
                {
                    // Keep existing node if present, otherwise leave as null
                    continue;
                }
                
                // Get the data item (adjust index for DataSource which loads a range)
                int dataIndex = DataSource is not null ? (i - dataOffset) : i;
                if (dataIndex < 0 || dataIndex >= effectiveData.Count)
                {
                    // Data not loaded for this row yet
                    continue;
                }
                
                var rowData = effectiveData[dataIndex];
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
                    IsLast = i == totalCount - 1
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
