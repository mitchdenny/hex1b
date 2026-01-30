using System.Collections.Specialized;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Node for rendering a table with columns, rows, and optional header/footer.
/// Implements ILayoutProvider to clip scrollable content area.
/// </summary>
/// <typeparam name="TRow">The type of data for each row.</typeparam>
public class TableNode<TRow> : Hex1bNode, ILayoutProvider, IDisposable
{
    // Border characters (single line box drawing)
    private const char TopLeft = '┌';
    private const char TopRight = '┐';
    private const char BottomLeft = '└';
    private const char BottomRight = '┘';
    private const char Horizontal = '─';
    private const char Vertical = '│';
    private const char TeeDown = '┬';
    private const char TeeUp = '┴';
    private const char TeeRight = '├';
    private const char TeeLeft = '┤';
    private const char Cross = '┼';
    
    // Scrollbar column constants
    // Scrollbar column overlays the table's right border
    // Layout: [table right border becomes scrollbar left border] track rightBorder = 2 chars extra
    private const int ScrollbarColumnWidth = 2;
    private const char ScrollbarTrack = '│';      // Thin vertical for track
    private const char ScrollbarThumb = '▉';      // 7/8 block character for thumb (U+2589)

    // INotifyCollectionChanged subscription
    private INotifyCollectionChanged? _subscribedCollection;

    /// <summary>
    /// Pads a string to a specified display width using spaces.
    /// </summary>
    private static string PadRightByDisplayWidth(string text, int targetWidth)
    {
        var currentWidth = DisplayWidth.GetStringWidth(text);
        if (currentWidth >= targetWidth)
            return text;
        return text + new string(' ', targetWidth - currentWidth);
    }

    /// <summary>
    /// The data source for table rows.
    /// </summary>
    public IReadOnlyList<TRow>? Data
    {
        get => _data;
        set
        {
            if (ReferenceEquals(_data, value)) return;
            
            // Unsubscribe from old collection
            UnsubscribeFromCollectionChanged();
            
            _data = value;
            
            // Subscribe to new collection if it supports INotifyCollectionChanged
            SubscribeToCollectionChanged();
        }
    }
    private IReadOnlyList<TRow>? _data;
    
    /// <summary>
    /// Async data source for virtualized tables. When set, Data property is ignored.
    /// </summary>
    public Data.ITableDataSource<TRow>? DataSource
    {
        get => _dataSource;
        set
        {
            if (ReferenceEquals(_dataSource, value)) return;
            
            // Unsubscribe from old data source
            UnsubscribeFromDataSource();
            
            _dataSource = value;
            
            // Subscribe to new data source
            SubscribeToDataSource();
            
            // Clear cached data when data source changes
            _cachedItems = null;
            _cachedItemCount = null;
        }
    }
    private Data.ITableDataSource<TRow>? _dataSource;
    private INotifyCollectionChanged? _subscribedDataSource;
    
    // Cached data from async data source
    private IReadOnlyList<TRow>? _cachedItems;
    private int? _cachedItemCount;
    private (int Start, int End)? _cachedRange;

    /// <summary>
    /// Builder for header cells.
    /// </summary>
    public Func<TableHeaderContext, IReadOnlyList<TableCell>>? HeaderBuilder { get; set; }

    /// <summary>
    /// Builder for row cells. Receives row context, row data, and row state.
    /// </summary>
    public Func<TableRowContext, TRow, TableRowState, IReadOnlyList<TableCell>>? RowBuilder { get; set; }

    /// <summary>
    /// Builder for footer cells.
    /// </summary>
    public Func<TableFooterContext, IReadOnlyList<TableCell>>? FooterBuilder { get; set; }

    /// <summary>
    /// Builder for empty state widget.
    /// </summary>
    public Func<RootContext, Hex1bWidget>? EmptyBuilder { get; set; }

    /// <summary>
    /// Selector function to extract a unique key from each row.
    /// </summary>
    public Func<TRow, object>? RowKeySelector { get; set; }

    /// <summary>
    /// The key of the currently focused row.
    /// </summary>
    public object? FocusedKey { get; set; }

    /// <summary>
    /// Handler for focus changes.
    /// </summary>
    public Func<object?, Task>? FocusChangedHandler { get; set; }

    /// <summary>
    /// Handler for row activation.
    /// </summary>
    public Func<object, TRow, Task>? RowActivatedHandler { get; set; }

    /// <summary>
    /// Whether to show a selection column with checkboxes.
    /// </summary>
    public bool ShowSelectionColumn { get; set; }

    /// <summary>
    /// Selector to determine if a row is selected (reads from view model).
    /// </summary>
    public Func<TRow, bool>? IsSelectedSelector { get; set; }

    /// <summary>
    /// Callback invoked when a row's selection state changes.
    /// </summary>
    public Action<TRow, bool>? SelectionChangedCallback { get; set; }

    /// <summary>
    /// Callback invoked when select all is triggered.
    /// </summary>
    public Action? SelectAllCallback { get; set; }

    /// <summary>
    /// Callback invoked when deselect all is triggered.
    /// </summary>
    public Action? DeselectAllCallback { get; set; }

    /// <summary>
    /// Callback to invalidate the app and trigger a re-render.
    /// Used when the data source changes via INotifyCollectionChanged.
    /// </summary>
    internal Action? InvalidateCallback { get; set; }

    /// <summary>
    /// The render mode for the table (Compact or Full).
    /// </summary>
    public TableRenderMode RenderMode { get; set; } = TableRenderMode.Compact;

    // Scroll state
    private int _scrollOffset;
    private int _contentRowCount;
    private int _viewportRowCount;
    private Rect _contentViewport;
    
    // Virtualization buffer (rows above/below visible area to pre-render)
    private const int VirtualizationBuffer = 5;
    
    /// <summary>
    /// The current scroll offset (first visible row index).
    /// </summary>
    public int ScrollOffset => _scrollOffset;
    
    /// <summary>
    /// The number of rows visible in the viewport.
    /// </summary>
    public int ViewportRowCount => _viewportRowCount;
    
    /// <summary>
    /// Whether the table content is scrollable (more rows than viewport).
    /// </summary>
    public bool IsScrollable => _contentRowCount > _viewportRowCount;
    
    /// <summary>
    /// The maximum scroll offset.
    /// </summary>
    public int MaxScrollOffset => Math.Max(0, _contentRowCount - _viewportRowCount);
    
    /// <summary>
    /// Gets the range of rows that should be materialized (visible + buffer).
    /// </summary>
    /// <param name="totalRows">Total number of data rows.</param>
    /// <returns>Tuple of (startIndex, endIndex) for rows to materialize.</returns>
    public (int Start, int End) GetVisibleRowRange(int totalRows)
    {
        if (totalRows == 0 || _viewportRowCount == 0)
            return (0, 0);
            
        int start = Math.Max(0, _scrollOffset - VirtualizationBuffer);
        int end = Math.Min(totalRows, _scrollOffset + _viewportRowCount + VirtualizationBuffer);
        return (start, end);
    }

    // Focus state
    private bool _isFocused;
    public override bool IsFocused
    {
        get => _isFocused;
        set
        {
            if (_isFocused != value)
            {
                _isFocused = value;
                MarkDirty();
            }
        }
    }

    public override bool IsFocusable => true;

    /// <summary>
    /// Returns focusable nodes including this table and any focusable children in row cells.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        // Return ourselves first (table handles its own focus for row navigation)
        yield return this;
        
        // Also return focusable children from row nodes (e.g., Picker buttons in cells)
        if (_headerRowNode is not null)
        {
            foreach (var focusable in _headerRowNode.GetFocusableNodes())
                yield return focusable;
        }
        
        if (_dataRowNodes is not null)
        {
            foreach (var row in _dataRowNodes)
            {
                if (row is null) continue; // Skip unmaterialized rows
                foreach (var focusable in row.GetFocusableNodes())
                    yield return focusable;
            }
        }
        
        if (_footerRowNode is not null)
        {
            foreach (var focusable in _footerRowNode.GetFocusableNodes())
                yield return focusable;
        }
    }

    // Computed layout data
    private int _columnCount;
    private int[] _columnWidths = [];
    private IReadOnlyList<TableCell>? _headerCells;
    private List<IReadOnlyList<TableCell>>? _rowCells;
    private List<TableRowState>? _rowStates;
    private IReadOnlyList<TableCell>? _footerCells;
    
    // Column definitions derived from header cells
    private List<TableColumnDef>? _columnDefs;
    
    // Child nodes for row rendering (using TableRowNode now)
    private TableRowNode? _headerRowNode;
    private List<TableRowNode>? _dataRowNodes;
    private TableRowNode? _footerRowNode;
    
    /// <summary>
    /// Sets column definitions from the widget reconciliation.
    /// </summary>
    internal void SetColumnDefs(List<TableColumnDef> defs)
    {
        _columnDefs = defs;
        _columnCount = defs.Count;
    }
    
    /// <summary>
    /// Gets/sets the header row node.
    /// </summary>
    internal TableRowNode? HeaderRowNode
    {
        get => _headerRowNode;
        set => _headerRowNode = value;
    }
    
    /// <summary>
    /// Gets/sets the data row nodes.
    /// </summary>
    internal List<TableRowNode>? DataRowNodes
    {
        get => _dataRowNodes;
        set => _dataRowNodes = value;
    }
    
    /// <summary>
    /// Gets/sets the footer row node.
    /// </summary>
    internal TableRowNode? FooterRowNode
    {
        get => _footerRowNode;
        set => _footerRowNode = value;
    }
    
    /// <summary>
    /// Gets the internal focused key for widget reconciliation.
    /// </summary>
    internal object? GetInternalFocusedKey() => FocusedKey;

    #region ILayoutProvider Implementation
    
    /// <summary>
    /// The clip rectangle for the scrollable content area.
    /// </summary>
    public Rect ClipRect => _contentViewport;
    
    /// <summary>
    /// The clip mode for this layout region.
    /// </summary>
    public ClipMode ClipMode => ClipMode.Clip;
    
    /// <summary>
    /// Parent layout provider for nested clipping.
    /// </summary>
    public ILayoutProvider? ParentLayoutProvider { get; set; }

    /// <summary>
    /// Determines if a character at the given absolute position should be rendered.
    /// </summary>
    public bool ShouldRenderAt(int x, int y) => LayoutProviderHelper.ShouldRenderAt(this, x, y);

    /// <summary>
    /// Clips a string that starts at the given position, returning only the visible portion.
    /// </summary>
    public (int adjustedX, string clippedText) ClipString(int x, int y, string text) 
        => LayoutProviderHelper.ClipString(this, x, y, text);

    #endregion

    #region Input Bindings

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Row navigation (moves focus between rows, auto-scrolls to keep focus visible)
        bindings.Key(Hex1bKey.UpArrow).Action(MoveFocusUp, "Previous row");
        bindings.Key(Hex1bKey.DownArrow).Action(MoveFocusDown, "Next row");
        bindings.Key(Hex1bKey.Home).Action(MoveFocusToFirst, "First row");
        bindings.Key(Hex1bKey.End).Action(MoveFocusToLast, "Last row");
        
        // Shift+Arrow for range selection
        bindings.Shift().Key(Hex1bKey.UpArrow).Action(ExtendSelectionUp, "Extend selection up");
        bindings.Shift().Key(Hex1bKey.DownArrow).Action(ExtendSelectionDown, "Extend selection down");
        bindings.Shift().Key(Hex1bKey.Home).Action(ExtendSelectionToFirst, "Select to first");
        bindings.Shift().Key(Hex1bKey.End).Action(ExtendSelectionToLast, "Select to last");
        
        // Selection commands
        bindings.Key(Hex1bKey.Spacebar).Action(ToggleSelection, "Toggle selection");
        bindings.Ctrl().Key(Hex1bKey.A).Action(SelectAll, "Select all");
        
        // Page scrolling (scrolls viewport, keeps focus if possible)
        bindings.Key(Hex1bKey.PageUp).Action(PageUp, "Page up");
        bindings.Key(Hex1bKey.PageDown).Action(PageDown, "Page down");
        
        // Mouse wheel scrolling
        bindings.Mouse(MouseButton.ScrollUp).Action(_ => ScrollByAmount(-3), "Scroll up");
        bindings.Mouse(MouseButton.ScrollDown).Action(_ => ScrollByAmount(3), "Scroll down");
        
        // Scrollbar drag
        bindings.Drag(MouseButton.Left).Action(HandleScrollbarDrag, "Drag scrollbar");
        
        // Mouse click on rows
        bindings.Mouse(MouseButton.Left).Action(HandleRowClick, "Click row");
    }

    /// <summary>
    /// Handles mouse click on a table row or header.
    /// </summary>
    private async Task HandleRowClick(InputBindingActionContext ctx)
    {
        var mouseY = ctx.MouseY;
        var mouseX = ctx.MouseX;
        
        // Check if click is on header checkbox
        if (ShowSelectionColumn && _headerRowNode is not null)
        {
            int headerY = Bounds.Y + 1; // After top border
            int checkboxEndX = Bounds.X + 1 + SelectionColumnWidth;
            
            if (mouseY == headerY && mouseX >= Bounds.X + 1 && mouseX < checkboxEndX)
            {
                // Toggle select all/none
                ToggleSelectAll();
                return;
            }
        }
        
        if (Data is null || Data.Count == 0) return;
        
        // Calculate which row was clicked
        int headerHeight = _headerRowNode is not null ? 2 : 0; // Header row + separator
        int dataStartY = Bounds.Y + 1 + headerHeight; // After top border + header
        
        // In Full mode, each row takes 2 lines (content + separator)
        int rowHeight = RenderMode == TableRenderMode.Full ? 2 : 1;
        int clickedRowIndex = (mouseY - dataStartY) / rowHeight + _scrollOffset;
        
        if (clickedRowIndex < 0 || clickedRowIndex >= Data.Count) 
            return;
        
        // Check if click is on the selection column (checkbox area)
        bool clickedCheckbox = false;
        if (ShowSelectionColumn)
        {
            int checkboxEndX = Bounds.X + 1 + SelectionColumnWidth; // After left border + checkbox width
            clickedCheckbox = mouseX >= Bounds.X + 1 && mouseX < checkboxEndX;
        }
        
        // Update focus to clicked row
        var key = RowKeySelector?.Invoke(Data[clickedRowIndex]) ?? clickedRowIndex;
        if (!Equals(FocusedKey, key))
        {
            FocusedKey = key;
            MarkDirty(); // Trigger visual update for focus change
        }
        if (FocusChangedHandler != null)
        {
            await FocusChangedHandler(key);
        }
        
        // If clicked on checkbox, toggle selection
        if (clickedCheckbox)
        {
            ToggleSelectionForRow(Data[clickedRowIndex]);
        }
    }
    
    /// <summary>
    /// Toggles selection for a specific row by invoking the callback.
    /// </summary>
    private void ToggleSelectionForRow(TRow row)
    {
        if (SelectionChangedCallback is null) return;
        
        var currentlySelected = IsSelectedSelector?.Invoke(row) ?? false;
        SelectionChangedCallback(row, !currentlySelected);
        MarkDirty();
    }
    
    /// <summary>
    /// Toggles between select all and deselect all.
    /// </summary>
    private void ToggleSelectAll()
    {
        if (Data is null || Data.Count == 0) return;
        
        // Count how many are currently selected
        int selectedCount = 0;
        if (IsSelectedSelector != null)
        {
            selectedCount = Data.Count(IsSelectedSelector);
        }
        
        // If all selected, deselect all; otherwise select all
        bool allSelected = selectedCount == Data.Count;
        
        if (allSelected)
        {
            DeselectAllCallback?.Invoke();
        }
        else
        {
            SelectAllCallback?.Invoke();
        }
        
        MarkDirty();
    }

    // Selection anchor for range selection (the row where Shift-selection started)
    private int _selectionAnchorIndex = -1;

    /// <summary>
    /// Gets the index of the currently focused row, or -1 if no row is focused.
    /// </summary>
    private int GetFocusedRowIndex()
    {
        if (FocusedKey == null || Data == null || Data.Count == 0)
            return -1;

        // For async data sources, we need to add the data offset to get absolute index
        int dataOffset = _dataSource is not null && _cachedRange.HasValue ? _cachedRange.Value.Start : 0;

        for (int i = 0; i < Data.Count; i++)
        {
            var key = GetRowKey(Data[i], dataOffset + i);
            if (Equals(key, FocusedKey))
                return dataOffset + i; // Return absolute index
        }
        
        // If focus key is an integer (index-based key from async navigation), return it directly
        if (FocusedKey is int focusedIndex)
            return focusedIndex;
            
        return -1;
    }

    /// <summary>
    /// Sets focus to the row at the given index and ensures it's visible.
    /// </summary>
    private async Task SetFocusedRowIndexAsync(int index)
    {
        int totalCount = GetEffectiveItemCount();
        if (totalCount == 0)
            return;

        // Clamp index to valid range (use total count, not just cached data)
        index = Math.Clamp(index, 0, totalCount - 1);
        
        // When using DataSource, the row might not be in memory yet
        // We set the focus by index-based key; actual row will be loaded during reconciliation
        if (_dataSource is not null)
        {
            // Use index-based key when row isn't loaded
            var cachedStart = _cachedRange?.Start ?? 0;
            var cachedEnd = _cachedRange?.End ?? 0;
            
            if (index >= cachedStart && index < cachedEnd && _cachedItems is not null)
            {
                // Row is in cache
                var row = _cachedItems[index - cachedStart];
                var newKey = GetRowKey(row, index);
                if (!Equals(newKey, FocusedKey))
                {
                    FocusedKey = newKey;
                    if (FocusChangedHandler != null)
                        await FocusChangedHandler(newKey);
                    MarkDirty();
                }
            }
            else
            {
                // Row not in cache - set focus by index, will resolve after data load
                // Use index as the key temporarily
                object newKey = index;
                if (!Equals(newKey, FocusedKey))
                {
                    FocusedKey = newKey;
                    if (FocusChangedHandler != null)
                        await FocusChangedHandler(newKey);
                    MarkDirty();
                }
            }
        }
        else if (_data is not null && index < _data.Count)
        {
            var row = _data[index];
            var newKey = GetRowKey(row, index);
            
            if (!Equals(newKey, FocusedKey))
            {
                FocusedKey = newKey;
                
                // Notify handler if present
                if (FocusChangedHandler != null)
                {
                    await FocusChangedHandler(newKey);
                }
                
                MarkDirty();
            }
        }

        // Ensure the focused row is visible (auto-scroll if needed)
        EnsureRowVisible(index);
    }

    /// <summary>
    /// Scrolls the viewport if necessary to make the given row index visible.
    /// </summary>
    private void EnsureRowVisible(int rowIndex)
    {
        if (rowIndex < 0 || _viewportRowCount <= 0)
            return;

        // If row is above the visible area, scroll up
        if (rowIndex < _scrollOffset)
        {
            SetScrollOffset(rowIndex);
        }
        // If row is below the visible area, scroll down
        else if (rowIndex >= _scrollOffset + _viewportRowCount)
        {
            SetScrollOffset(rowIndex - _viewportRowCount + 1);
        }
    }

    private void MoveFocusUp(InputBindingActionContext ctx)
    {
        var currentIndex = GetFocusedRowIndex();
        if (currentIndex < 0)
        {
            // No focus - focus the first visible row
            _ = SetFocusedRowIndexAsync(_scrollOffset);
        }
        else if (currentIndex > 0)
        {
            _ = SetFocusedRowIndexAsync(currentIndex - 1);
        }
    }

    private void MoveFocusDown(InputBindingActionContext ctx)
    {
        var currentIndex = GetFocusedRowIndex();
        var maxIndex = GetEffectiveItemCount() - 1;
        
        if (currentIndex < 0)
        {
            // No focus - focus the first visible row
            _ = SetFocusedRowIndexAsync(_scrollOffset);
        }
        else if (currentIndex < maxIndex)
        {
            _ = SetFocusedRowIndexAsync(currentIndex + 1);
        }
    }

    private void MoveFocusToFirst(InputBindingActionContext ctx)
    {
        int totalCount = GetEffectiveItemCount();
        if (totalCount > 0)
        {
            _ = SetFocusedRowIndexAsync(0);
        }
    }

    private void MoveFocusToLast(InputBindingActionContext ctx)
    {
        int totalCount = GetEffectiveItemCount();
        if (totalCount > 0)
        {
            _ = SetFocusedRowIndexAsync(totalCount - 1);
        }
    }

    private void PageUp(InputBindingActionContext ctx)
    {
        var pageSize = Math.Max(1, _viewportRowCount - 1);
        var currentIndex = GetFocusedRowIndex();
        
        if (currentIndex >= 0)
        {
            // Move focus up by a page
            _ = SetFocusedRowIndexAsync(currentIndex - pageSize);
        }
        else
        {
            // Just scroll the viewport
            ScrollByAmount(-pageSize);
        }
    }

    private void PageDown(InputBindingActionContext ctx)
    {
        var pageSize = Math.Max(1, _viewportRowCount - 1);
        var currentIndex = GetFocusedRowIndex();
        
        if (currentIndex >= 0)
        {
            // Move focus down by a page
            _ = SetFocusedRowIndexAsync(currentIndex + pageSize);
        }
        else
        {
            // Just scroll the viewport
            ScrollByAmount(pageSize);
        }
    }

    #region Selection Actions

    /// <summary>
    /// Toggles selection on the currently focused row.
    /// </summary>
    private void ToggleSelection(InputBindingActionContext ctx)
    {
        var currentIndex = GetFocusedRowIndex();
        if (currentIndex < 0 || Data == null)
            return;

        ToggleSelectionForRow(Data[currentIndex]);
    }

    /// <summary>
    /// Selects all rows.
    /// </summary>
    private void SelectAll(InputBindingActionContext ctx)
    {
        if (Data == null || Data.Count == 0)
            return;

        SelectAllCallback?.Invoke();
        MarkDirty();
    }

    /// <summary>
    /// Extends selection upward from anchor to current focus.
    /// </summary>
    private void ExtendSelectionUp(InputBindingActionContext ctx)
    {
        var currentIndex = GetFocusedRowIndex();
        if (currentIndex <= 0)
            return;

        // Set anchor if not set
        if (_selectionAnchorIndex < 0)
            _selectionAnchorIndex = currentIndex;

        // Move focus up
        _ = SetFocusedRowIndexAsync(currentIndex - 1);
        
        // Select range from anchor to new focus
        SelectRange(_selectionAnchorIndex, currentIndex - 1);
    }

    /// <summary>
    /// Extends selection downward from anchor to current focus.
    /// </summary>
    private void ExtendSelectionDown(InputBindingActionContext ctx)
    {
        var currentIndex = GetFocusedRowIndex();
        var maxIndex = (Data?.Count ?? 0) - 1;
        
        if (currentIndex < 0 || currentIndex >= maxIndex)
            return;

        // Set anchor if not set
        if (_selectionAnchorIndex < 0)
            _selectionAnchorIndex = currentIndex;

        // Move focus down
        _ = SetFocusedRowIndexAsync(currentIndex + 1);
        
        // Select range from anchor to new focus
        SelectRange(_selectionAnchorIndex, currentIndex + 1);
    }

    /// <summary>
    /// Extends selection from anchor to first row.
    /// </summary>
    private void ExtendSelectionToFirst(InputBindingActionContext ctx)
    {
        var currentIndex = GetFocusedRowIndex();
        if (currentIndex < 0)
            return;

        // Set anchor if not set
        if (_selectionAnchorIndex < 0)
            _selectionAnchorIndex = currentIndex;

        // Move focus to first
        _ = SetFocusedRowIndexAsync(0);
        
        // Select range from anchor to first
        SelectRange(_selectionAnchorIndex, 0);
    }

    /// <summary>
    /// Extends selection from anchor to last row.
    /// </summary>
    private void ExtendSelectionToLast(InputBindingActionContext ctx)
    {
        var currentIndex = GetFocusedRowIndex();
        var maxIndex = (Data?.Count ?? 0) - 1;
        
        if (currentIndex < 0 || maxIndex < 0)
            return;

        // Set anchor if not set
        if (_selectionAnchorIndex < 0)
            _selectionAnchorIndex = currentIndex;

        // Move focus to last
        _ = SetFocusedRowIndexAsync(maxIndex);
        
        // Select range from anchor to last
        SelectRange(_selectionAnchorIndex, maxIndex);
    }

    /// <summary>
    /// Selects all rows in the range from startIndex to endIndex (inclusive).
    /// </summary>
    private void SelectRange(int startIndex, int endIndex)
    {
        if (Data == null || SelectionChangedCallback == null)
            return;

        var minIndex = Math.Min(startIndex, endIndex);
        var maxIndex = Math.Max(startIndex, endIndex);
        
        // Clamp to valid range
        minIndex = Math.Max(0, minIndex);
        maxIndex = Math.Min(Data.Count - 1, maxIndex);

        // Select all rows in range
        for (int i = minIndex; i <= maxIndex; i++)
        {
            var row = Data[i];
            var isSelected = IsSelectedSelector?.Invoke(row) ?? false;
            if (!isSelected)
            {
                SelectionChangedCallback(row, true);
            }
        }

        MarkDirty();
    }

    #endregion

    private void ScrollByAmount(int amount)
    {
        SetScrollOffset(_scrollOffset + amount);
    }

    private void SetScrollOffset(int offset)
    {
        var clamped = Math.Clamp(offset, 0, MaxScrollOffset);
        if (clamped != _scrollOffset)
        {
            _scrollOffset = clamped;
            MarkDirty();
        }
    }

    private DragHandler HandleScrollbarDrag(int localX, int localY)
    {
        if (!IsScrollable) return new DragHandler();
        
        // Check if click is on the scrollbar (rightmost column within bounds)
        var scrollbarX = Bounds.Width - ScrollbarColumnWidth;
        if (localX < scrollbarX || localX >= Bounds.Width)
        {
            return new DragHandler(); // Click not on scrollbar
        }
        
        // Calculate scrollbar track area (excluding header/footer borders)
        var headerHeight = _headerRowNode is not null ? 2 : 0; // Header row + separator
        var footerHeight = _footerRowNode is not null ? 2 : 0; // Separator + footer row
        var trackStart = 1 + headerHeight; // After top border and header
        var trackEnd = Bounds.Height - 1 - footerHeight; // Before bottom border and footer
        var trackHeight = trackEnd - trackStart;
        
        if (trackHeight <= 0) return new DragHandler(); // Too small for meaningful scroll
        
        // Calculate thumb position and size (uses full track, no arrow buttons)
        var thumbSize = Math.Max(1, (int)Math.Ceiling((double)_viewportRowCount / _contentRowCount * trackHeight));
        var scrollRange = trackHeight - thumbSize;
        var thumbPosition = scrollRange > 0 
            ? (int)Math.Round((double)_scrollOffset / MaxScrollOffset * scrollRange) 
            : 0;
        
        var trackY = localY - trackStart;
        
        if (trackY >= 0 && trackY < trackHeight)
        {
            if (trackY >= thumbPosition && trackY < thumbPosition + thumbSize)
            {
                // Clicked on thumb - start drag
                var startOffset = _scrollOffset;
                var contentPerPixel = MaxScrollOffset > 0 && scrollRange > 0
                    ? (double)MaxScrollOffset / scrollRange
                    : 0;
                
                if (contentPerPixel > 0)
                {
                    return new DragHandler(
                        onMove: (deltaX, deltaY) =>
                        {
                            var newOffset = (int)Math.Round(startOffset + deltaY * contentPerPixel);
                            SetScrollOffset(newOffset);
                        }
                    );
                }
                else
                {
                    // Thumb fills entire track (1-cell thumb) - use click position to scroll proportionally
                    return HandleProportionalScroll(trackY, trackHeight);
                }
            }
            else if (trackY < thumbPosition)
            {
                // Clicked above thumb - page up
                ScrollByAmount(-Math.Max(1, _viewportRowCount - 1));
                return new DragHandler();
            }
            else
            {
                // Clicked below thumb - page down
                ScrollByAmount(Math.Max(1, _viewportRowCount - 1));
                return new DragHandler();
            }
        }
        
        return new DragHandler();
    }
    
    /// <summary>
    /// Handles proportional scrolling when thumb can't be dragged (fills entire track).
    /// </summary>
    private DragHandler HandleProportionalScroll(int clickY, int trackHeight)
    {
        // When thumb fills entire track, use click position to scroll proportionally
        var proportion = (double)clickY / Math.Max(1, trackHeight - 1);
        var targetOffset = (int)Math.Round(proportion * MaxScrollOffset);
        SetScrollOffset(targetOffset);
        
        return new DragHandler(
            onMove: (deltaX, deltaY) =>
            {
                // Allow dragging to scroll proportionally
                var newProportion = Math.Clamp((clickY + deltaY) / (double)Math.Max(1, trackHeight - 1), 0, 1);
                var newOffset = (int)Math.Round(newProportion * MaxScrollOffset);
                SetScrollOffset(newOffset);
            }
        );
    }

    #endregion

    /// <summary>
    /// Gets the row key for a given row and index.
    /// </summary>
    private object GetRowKey(TRow row, int index)
    {
        return RowKeySelector?.Invoke(row) ?? index;
    }

    /// <summary>
    /// Checks if a row key is focused.
    /// Also handles index-based focus keys used during async navigation.
    /// </summary>
    private bool IsRowFocused(object key, int absoluteIndex)
    {
        if (FocusedKey is null)
            return false;
        
        // Direct key match
        if (Equals(FocusedKey, key))
            return true;
        
        // If FocusedKey is an integer index (from async navigation), compare by index
        if (FocusedKey is int focusedIndex && focusedIndex == absoluteIndex)
            return true;
        
        return false;
    }

    /// <summary>
    /// Checks if a row is selected using the selector.
    /// </summary>
    private bool IsRowSelected(TRow row)
    {
        return IsSelectedSelector?.Invoke(row) ?? false;
    }
    
    /// <summary>
    /// Checks if a row at the given absolute index is currently selected (for rendering).
    /// </summary>
    private bool IsRowSelectedForRender(int absoluteRowIndex)
    {
        if (Data == null)
            return false;
        
        // For async data sources, convert absolute index to relative index
        int dataOffset = _dataSource is not null && _cachedRange.HasValue ? _cachedRange.Value.Start : 0;
        int relativeIndex = absoluteRowIndex - dataOffset;
        
        if (relativeIndex < 0 || relativeIndex >= Data.Count)
            return false;
        return IsSelectedSelector?.Invoke(Data[relativeIndex]) ?? false;
    }

    /// <summary>
    /// Builds cell data from builders and validates column counts.
    /// </summary>
    private void BuildCellData()
    {
        var headerContext = new TableHeaderContext();
        var rowContext = new TableRowContext();
        var footerContext = new TableFooterContext();

        // Build header
        _headerCells = HeaderBuilder?.Invoke(headerContext);
        _columnCount = _headerCells?.Count ?? 0;

        if (_columnCount == 0)
        {
            throw new InvalidOperationException("Table must have at least one column. Header builder returned no cells.");
        }

        // Build rows
        _rowCells = [];
        _rowStates = [];
        if (Data is not null && RowBuilder is not null)
        {
            int rowCount = Data.Count;
            // For async data sources, we need the offset to calculate absolute indices
            int dataOffset = _dataSource is not null && _cachedRange.HasValue ? _cachedRange.Value.Start : 0;
            
            for (int i = 0; i < rowCount; i++)
            {
                var row = Data[i];
                int absoluteIndex = dataOffset + i;
                var rowKey = GetRowKey(row, absoluteIndex);
                
                var state = new TableRowState
                {
                    RowIndex = absoluteIndex,
                    RowKey = rowKey,
                    IsFocused = IsRowFocused(rowKey, absoluteIndex),
                    IsSelected = IsRowSelected(row),
                    IsFirst = absoluteIndex == 0,
                    IsLast = absoluteIndex == GetEffectiveItemCount() - 1
                };
                
                var cells = RowBuilder(rowContext, row, state);
                if (cells.Count != _columnCount)
                {
                    throw new InvalidOperationException(
                        $"Table column count mismatch: header has {_columnCount} columns, " +
                        $"but row {absoluteIndex} has {cells.Count} columns.");
                }
                _rowCells.Add(cells);
                _rowStates.Add(state);
            }
        }

        // Build footer
        _footerCells = FooterBuilder?.Invoke(footerContext);
        if (_footerCells is not null && _footerCells.Count != _columnCount)
        {
            throw new InvalidOperationException(
                $"Table column count mismatch: header has {_columnCount} columns, " +
                $"but footer has {_footerCells.Count} columns.");
        }
        
        // Build row nodes directly if not already built by widget reconciliation
        // This supports standalone usage and testing
        if (_headerRowNode == null && _headerCells != null)
        {
            BuildRowNodesDirect();
        }
    }

    /// <summary>
    /// Builds row nodes directly without widget reconciliation.
    /// Used for standalone usage and testing when TableNode is created directly.
    /// </summary>
    private void BuildRowNodesDirect()
    {
        // Build column definitions
        if (_columnDefs == null && _headerCells != null)
        {
            _columnDefs = [];
            foreach (var cell in _headerCells)
            {
                _columnDefs.Add(new TableColumnDef(
                    cell.Width ?? SizeHint.Fill,
                    cell.Alignment
                ));
            }
        }
        
        _columnDefs ??= [];
        
        // Build header row node
        if (_headerCells != null && _headerCells.Count > 0)
        {
            var headerRowWidget = new TableRowWidget(
                _headerCells, 
                _columnDefs, 
                IsHeader: true,
                ShowSelectionColumn: ShowSelectionColumn
            );
            _headerRowNode = new TableRowNode();
            BuildRowNodeChildren(_headerRowNode, _headerCells, isHeader: true);
        }
        
        // Build data row nodes
        if (_rowCells != null && _rowCells.Count > 0)
        {
            _dataRowNodes = [];
            for (int i = 0; i < _rowCells.Count; i++)
            {
                var rowNode = new TableRowNode();
                var isSelected = _rowStates != null && i < _rowStates.Count && _rowStates[i].IsSelected;
                var isFocused = _rowStates != null && i < _rowStates.Count && _rowStates[i].IsFocused;
                rowNode.IsHighlighted = isFocused;
                rowNode.IsSelected = isSelected;
                BuildRowNodeChildren(rowNode, _rowCells[i], isSelected: isSelected);
                _dataRowNodes.Add(rowNode);
            }
        }
        
        // Build footer row node
        if (_footerCells != null && _footerCells.Count > 0)
        {
            _footerRowNode = new TableRowNode();
            BuildRowNodeChildren(_footerRowNode, _footerCells);
        }
    }

    /// <summary>
    /// Builds children for a row node from cells.
    /// </summary>
    private void BuildRowNodeChildren(TableRowNode rowNode, IReadOnlyList<TableCell> cells, bool isHeader = false, bool isSelected = false)
    {
        rowNode.Children.Clear();
        
        // Left border
        rowNode.Children.Add(new TextBlockNode { Text = Vertical.ToString() });
        
        // Selection column (if enabled)
        if (ShowSelectionColumn)
        {
            var checkText = isSelected ? "[x]" : "[ ]";
            var selNode = new TextBlockNode { Text = checkText };
            selNode.WidthHint = SizeHint.Fixed(3);
            rowNode.Children.Add(selNode);
            rowNode.Children.Add(new TextBlockNode { Text = Vertical.ToString() });
        }
        
        // Cell widgets with borders between them
        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            var alignment = _columnDefs != null && i < _columnDefs.Count ? _columnDefs[i].Alignment : Alignment.Left;
            var widthHint = _columnDefs != null && i < _columnDefs.Count ? _columnDefs[i].Width : SizeHint.Fill;
            
            Hex1bNode cellNode;
            var textNode = new TextBlockNode { Text = cell.Text ?? "", Overflow = TextOverflow.Ellipsis };
            
            // Wrap in AlignNode if not left-aligned
            if (alignment != Alignment.Left && alignment != Alignment.None)
            {
                var alignNode = new AlignNode { Child = textNode, Alignment = alignment };
                alignNode.WidthHint = widthHint;
                cellNode = alignNode;
            }
            else
            {
                textNode.WidthHint = widthHint;
                cellNode = textNode;
            }
            
            rowNode.Children.Add(cellNode);
            
            // Separator between cells (not after last cell)
            if (i < cells.Count - 1)
            {
                rowNode.Children.Add(new TextBlockNode { Text = Vertical.ToString() });
            }
        }
        
        // Right border
        rowNode.Children.Add(new TextBlockNode { Text = Vertical.ToString() });
    }


    /// <summary>
    /// Gets the selection column width from theme when selection column is enabled.
    /// Uses the theme's default value for layout calculations (theme may be overridden at render time).
    /// </summary>
    private int SelectionColumnWidth => ShowSelectionColumn ? TableTheme.SelectionColumnWidth.DefaultValue() : 0;

    /// <summary>
    /// Calculates column widths based on column definitions (Fixed, Content, Fill hints).
    /// </summary>
    private void CalculateColumnWidths(int availableWidth, bool reserveScrollbar)
    {
        if (_columnCount == 0) return;

        // Account for borders: | col1 | col2 | col3 | = columnCount + 1 vertical bars
        int borderWidth = _columnCount + 1;
        
        // Reserve space for scrollbar column if needed (includes its own border)
        // Scrollbar column: │ track │ = 3 chars total
        int scrollbarSpace = reserveScrollbar ? ScrollbarColumnWidth : 0;
        
        // Reserve space for selection column if enabled
        int selectionSpace = ShowSelectionColumn ? SelectionColumnWidth + 1 : 0; // +1 for separator
        
        // Reserve space for cell padding in Full mode (1 char left + 1 char right per cell)
        int paddingSpace = RenderMode == TableRenderMode.Full ? _columnCount * 2 : 0;
        
        int contentWidth = availableWidth - borderWidth - scrollbarSpace - selectionSpace - paddingSpace;

        if (contentWidth < _columnCount)
        {
            // Minimum 1 char per column
            contentWidth = _columnCount;
        }

        _columnWidths = new int[_columnCount];
        
        // If we have column definitions, use them
        if (_columnDefs != null && _columnDefs.Count > 0)
        {
            // First pass: allocate fixed widths and measure content widths
            int usedWidth = 0;
            int fillWeightTotal = 0;
            var fillColumns = new List<int>();
            
            for (int i = 0; i < _columnCount; i++)
            {
                var hint = i < _columnDefs.Count ? _columnDefs[i].Width : SizeHint.Fill;
                
                if (hint.IsFixed)
                {
                    _columnWidths[i] = Math.Max(1, hint.FixedValue);
                    usedWidth += _columnWidths[i];
                }
                else if (hint.IsContent)
                {
                    // Measure max content width for this column
                    int maxWidth = MeasureColumnContentWidth(i);
                    _columnWidths[i] = Math.Max(1, maxWidth);
                    usedWidth += _columnWidths[i];
                }
                else // Fill
                {
                    fillColumns.Add(i);
                    fillWeightTotal += hint.IsFill ? hint.FillWeight : 1;
                }
            }
            
            // Second pass: distribute remaining width to fill columns
            int remainingWidth = contentWidth - usedWidth;
            if (fillColumns.Count > 0 && remainingWidth > 0)
            {
                int distributed = 0;
                for (int j = 0; j < fillColumns.Count; j++)
                {
                    int i = fillColumns[j];
                    var hint = i < _columnDefs.Count ? _columnDefs[i].Width : SizeHint.Fill;
                    int weight = hint.IsFill ? hint.FillWeight : 1;
                    
                    if (j == fillColumns.Count - 1)
                    {
                        _columnWidths[i] = Math.Max(1, remainingWidth - distributed);
                    }
                    else
                    {
                        int share = (int)Math.Floor((double)remainingWidth * weight / fillWeightTotal);
                        _columnWidths[i] = Math.Max(1, share);
                        distributed += _columnWidths[i];
                    }
                }
            }
            else if (fillColumns.Count > 0)
            {
                // No remaining width - give fill columns minimum of 1
                foreach (int i in fillColumns)
                {
                    _columnWidths[i] = 1;
                }
            }
        }
        else
        {
            // Fallback: equal distribution
            int baseWidth = contentWidth / _columnCount;
            int remainder = contentWidth % _columnCount;
            for (int i = 0; i < _columnCount; i++)
            {
                _columnWidths[i] = baseWidth + (i < remainder ? 1 : 0);
            }
        }
    }
    
    /// <summary>
    /// Measures the maximum content width for a column across all rows.
    /// Uses both text cells and reconciled widget nodes for measurement.
    /// </summary>
    private int MeasureColumnContentWidth(int columnIndex)
    {
        int maxWidth = 0;
        
        // Measure header - try both node and text, take max
        if (_headerRowNode != null)
        {
            maxWidth = Math.Max(maxWidth, MeasureRowNodeColumnWidth(_headerRowNode, columnIndex));
        }
        if (_headerCells != null && columnIndex < _headerCells.Count)
        {
            var text = _headerCells[columnIndex].Text ?? "";
            maxWidth = Math.Max(maxWidth, DisplayWidth.GetStringWidth(text));
        }
        
        // Measure data rows - try both nodes and text cells, take max
        if (_dataRowNodes != null)
        {
            foreach (var rowNode in _dataRowNodes)
            {
                if (rowNode is null) continue; // Skip unmaterialized rows
                maxWidth = Math.Max(maxWidth, MeasureRowNodeColumnWidth(rowNode, columnIndex));
            }
        }
        if (_rowCells != null)
        {
            foreach (var row in _rowCells)
            {
                if (columnIndex < row.Count)
                {
                    var cell = row[columnIndex];
                    if (cell.Text != null)
                    {
                        maxWidth = Math.Max(maxWidth, DisplayWidth.GetStringWidth(cell.Text));
                    }
                    // For widget cells, we can't easily measure without reconciliation,
                    // so rely on the node measurement above
                }
            }
        }
        
        // Measure footer - try both node and text, take max
        if (_footerRowNode != null)
        {
            maxWidth = Math.Max(maxWidth, MeasureRowNodeColumnWidth(_footerRowNode, columnIndex));
        }
        if (_footerCells != null && columnIndex < _footerCells.Count)
        {
            var text = _footerCells[columnIndex].Text ?? "";
            maxWidth = Math.Max(maxWidth, DisplayWidth.GetStringWidth(text));
        }
        
        return maxWidth;
    }
    
    /// <summary>
    /// Measures the width of a specific column cell in a row node.
    /// </summary>
    private int MeasureRowNodeColumnWidth(TableRowNode rowNode, int columnIndex)
    {
        // Row structure depends on render mode:
        // Compact: │ [sel] │ cell0 │ cell1 │ ... │ cellN │
        // Full:    │ [sel] │ pad cell0 pad │ pad cell1 pad │ ... │ pad cellN pad │
        
        // Note: HasSelectionColumn and HasCellPadding might not be set yet during Measure,
        // so use the node's properties directly from reconciliation
        bool hasCellPadding = rowNode.HasCellPadding;
        bool hasSelectionColumn = rowNode.HasSelectionColumn;
        
        // Fallback: check if this table has selection column (node might not have it set yet)
        if (!hasSelectionColumn && ShowSelectionColumn)
        {
            hasSelectionColumn = true;
        }
        
        // Fallback: check if this is Full mode (node might not have it set yet)
        if (!hasCellPadding && RenderMode == TableRenderMode.Full)
        {
            hasCellPadding = true;
        }
        
        int childIndex = 1; // Start after left border
        
        // Skip selection column if present
        if (hasSelectionColumn)
        {
            childIndex += 2; // selection cell + border
        }
        
        // Navigate to the correct column
        // In Full mode (with padding), each column has: pad + cell + pad + border = 4 widgets
        // In Compact mode, each column has: cell + border = 2 widgets
        int widgetsPerColumn = hasCellPadding ? 4 : 2;
        childIndex += columnIndex * widgetsPerColumn;
        
        // In Full mode, skip the left padding to get to the cell
        if (hasCellPadding)
        {
            childIndex += 1; // Skip left padding
        }
        
        if (childIndex >= rowNode.Children.Count)
        {
            return 0;
        }
        
        var cellNode = rowNode.Children[childIndex];
        
        // Measure the cell node with loose constraints
        var size = cellNode.Measure(Constraints.Unbounded);
        return size.Width;
    }

    public override Size Measure(Constraints constraints)
    {
        BuildCellData();

        // Determine total content row count
        // When using DataSource, use cached item count; otherwise use Data.Count
        // Both null data and empty data show the empty state
        int totalItemCount = GetEffectiveItemCount();
        if (totalItemCount == 0)
        {
            _contentRowCount = 1; // Empty state (message row)
        }
        else
        {
            _contentRowCount = totalItemCount;
        }
        
        // Calculate fixed heights (header, footer, borders)
        int fixedHeight = 2; // Top and bottom borders
        if (_headerCells is not null)
        {
            fixedHeight += 2; // Header row + separator
        }
        if (_footerCells is not null)
        {
            fixedHeight += 2; // Separator + footer row
        }
        
        // Calculate available viewport height for data rows
        // If height is unbounded (int.MaxValue), use content row count - no scrolling needed
        int maxHeight = constraints.MaxHeight;
        if (maxHeight <= 0 || maxHeight >= int.MaxValue - 1000)
        {
            // Unbounded or very large - use content size (no scrolling in unbounded context)
            int dataHeight = _contentRowCount;
            if (RenderMode == TableRenderMode.Full && _contentRowCount > 0)
            {
                dataHeight += _contentRowCount - 1; // Add separators between rows
            }
            maxHeight = fixedHeight + dataHeight + 10; // Add some padding
        }
        
        // In Full mode, each row takes 2 lines (row + separator) except the last
        // So viewport capacity is: (availableHeight + 1) / 2 for Full mode
        int availableForData = Math.Max(1, maxHeight - fixedHeight);
        if (RenderMode == TableRenderMode.Full)
        {
            _viewportRowCount = Math.Max(1, (availableForData + 1) / 2);
        }
        else
        {
            _viewportRowCount = availableForData;
        }
        
        // Determine if scrollbar is needed
        bool needsScrollbar = _contentRowCount > _viewportRowCount;
        
        // Calculate column widths (reserve space for scrollbar if needed)
        int width = constraints.MaxWidth > 0 ? constraints.MaxWidth : 80;
        CalculateColumnWidths(width, needsScrollbar);

        // Calculate total height (capped by viewport)
        int visibleRows = Math.Min(_contentRowCount, _viewportRowCount);
        int dataRowsHeight;
        if (Data is not null && Data.Count == 0)
        {
            dataRowsHeight = 1; // Empty message
        }
        else if (RenderMode == TableRenderMode.Full && visibleRows > 0)
        {
            dataRowsHeight = visibleRows + (visibleRows - 1); // Rows + separators between them
        }
        else
        {
            dataRowsHeight = visibleRows;
        }
        int height = fixedHeight + dataRowsHeight;

        // NOTE: Do NOT clamp scroll offset here in Measure!
        // Measure may receive unbounded constraints from VStack, which would incorrectly reset scroll.
        // Scroll clamping happens in Arrange when we know the real viewport size.

        // Measure all child nodes with their column widths
        MeasureChildNodes();

        return constraints.Constrain(new Size(width, height));
    }

    /// <summary>
    /// Measures all child row nodes with appropriate constraints.
    /// </summary>
    private void MeasureChildNodes()
    {
        int width = Bounds.Width > 0 ? Bounds.Width : (_columnWidths.Sum() + _columnCount + 1);
        
        // Measure header row node
        if (_headerRowNode is not null)
        {
            _headerRowNode.Measure(new Constraints(0, width, 0, 1));
        }
        
        // Measure data row nodes
        if (_dataRowNodes is not null)
        {
            foreach (var rowNode in _dataRowNodes)
            {
                if (rowNode is null) continue; // Skip unmaterialized rows
                rowNode.Measure(new Constraints(0, width, 0, 1));
            }
        }
        
        // Measure footer row node
        if (_footerRowNode is not null)
        {
            _footerRowNode.Measure(new Constraints(0, width, 0, 1));
        }
    }

    public override void Arrange(Rect rect)
    {
        Bounds = rect;
        
        // Calculate content viewport for clipping
        int headerHeight = _headerRowNode is not null ? 2 : 0; // Header row + separator
        int footerHeight = _footerRowNode is not null ? 2 : 0; // Separator + footer row
        
        // Recalculate viewport row count based on actual arranged height
        // This is critical for scrolling to work correctly since Measure may receive unbounded constraints
        int fixedHeight = 2 + headerHeight + footerHeight; // Top/bottom borders + header/footer
        int availableForData = Math.Max(1, rect.Height - fixedHeight);
        
        // In Full mode, each row takes 2 lines (row + separator) except the last
        if (RenderMode == TableRenderMode.Full)
        {
            _viewportRowCount = Math.Max(1, (availableForData + 1) / 2);
        }
        else
        {
            _viewportRowCount = availableForData;
        }
        
        // Clamp scroll offset now that we know the real viewport size
        if (_scrollOffset > MaxScrollOffset)
        {
            _scrollOffset = MaxScrollOffset;
        }
        
        // Recalculate column widths if scrollbar state changed after Arrange's viewport recalculation
        // This happens when Measure receives unbounded height but Arrange has bounded height
        bool needsScrollbar = IsScrollable;
        CalculateColumnWidths(rect.Width, needsScrollbar);
        
        int scrollbarSpace = needsScrollbar ? ScrollbarColumnWidth : 0;
        
        int viewportY = rect.Y + 1 + headerHeight; // After top border and header
        int viewportHeight = rect.Height - 2 - headerHeight - footerHeight; // Between borders and header/footer
        int viewportWidth = rect.Width - scrollbarSpace;
        
        _contentViewport = new Rect(rect.X, viewportY, viewportWidth, viewportHeight);
        
        ArrangeChildNodes();
    }

    /// <summary>
    /// Arranges all child row nodes within their bounds.
    /// </summary>
    private void ArrangeChildNodes()
    {
        int y = 0;
        // Width should exclude scrollbar if present
        int scrollbarSpace = IsScrollable ? ScrollbarColumnWidth : 0;
        int width = Bounds.Width - scrollbarSpace;
        
        // Skip top border
        y++;
        
        // Arrange header row
        if (_headerRowNode is not null)
        {
            SetRowNodeColumnWidths(_headerRowNode);
            _headerRowNode.Arrange(new Rect(Bounds.X, Bounds.Y + y, width, 1));
            y += 2; // Header row + separator
        }
        
        // Arrange only visible data row nodes (within viewport, offset by scroll)
        if (_dataRowNodes is not null && _dataRowNodes.Count > 0)
        {
            int endRow = Math.Min(_scrollOffset + _viewportRowCount, _dataRowNodes.Count);
            for (int i = _scrollOffset; i < endRow; i++)
            {
                var rowNode = _dataRowNodes[i];
                if (rowNode is not null)
                {
                    SetRowNodeColumnWidths(rowNode);
                    rowNode.Arrange(new Rect(Bounds.X, Bounds.Y + y, width, 1));
                }
                y++;
                
                // In Full mode, account for separator between rows
                if (RenderMode == TableRenderMode.Full && i < endRow - 1)
                {
                    y++; // Skip separator line
                }
            }
        }
        else if (Data is null || Data.Count == 0)
        {
            y++; // Empty message row
        }
        
        // Arrange footer row (skip separator)
        if (_footerRowNode is not null)
        {
            y++; // Skip footer separator
            SetRowNodeColumnWidths(_footerRowNode);
            _footerRowNode.Arrange(new Rect(Bounds.X, Bounds.Y + y, width, 1));
        }
    }
    
    /// <summary>
    /// Sets the column widths on a row node so it uses the same widths as border rendering.
    /// </summary>
    private void SetRowNodeColumnWidths(TableRowNode rowNode)
    {
        rowNode.ColumnWidths = _columnWidths;
        rowNode.HasSelectionColumn = ShowSelectionColumn;
        rowNode.SelectionColumnWidth = SelectionColumnWidth;
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (_columnCount == 0 || _columnWidths.Length == 0) return;

        int y = 0;
        int totalWidth = _columnWidths.Sum() + _columnCount + 1;
        bool hasDataRows = Data is not null && Data.Count > 0;
        bool isLoading = Data is null;
        bool hasColumnStructure = hasDataRows || isLoading; // Loading rows also have column structure
        
        // When scrollbar is present, use connecting characters for the right edge
        char topRightCorner = IsScrollable ? TeeDown : TopRight;
        char rightTee = TeeLeft; // Always TeeLeft - scrollbar track is separate column
        char bottomRightCorner = IsScrollable ? TeeUp : BottomRight;

        // Top border
        RenderHorizontalBorder(context, y, TopLeft, TeeDown, topRightCorner);
        y++;

        // Header
        if (_headerRowNode is not null)
        {
            // Render the header row node (it handles its own children)
            context.RenderChild(_headerRowNode);
            y++;
            
            // Use different separator when transitioning to empty state vs data/loading rows
            if (hasColumnStructure)
            {
                // Header separator uses Cross when scrollable to connect to scrollbar track
                char headerRightTee = IsScrollable ? Cross : TeeLeft;
                RenderHorizontalBorder(context, y, TeeRight, Cross, headerRightTee);
            }
            else
            {
                // No column breaks in separator when empty - columns "close off"
                // But selection column still needs a cross since it's still visible
                char emptyRightTee = IsScrollable ? Cross : TeeLeft;
                RenderHorizontalBorder(context, y, TeeRight, TeeUp, emptyRightTee, selectionColumnMiddle: Cross);
            }
            y++;
        }

        // Data rows or empty state
        if (Data is null || Data.Count == 0)
        {
            // Empty state - render with just left/right borders, no column separators
            RenderEmptyRow(context, y, totalWidth);
            y++;
        }
        else if (_dataRowNodes is not null && _rowStates is not null)
        {
            // Render only visible data rows (using scroll offset)
            int endRow = Math.Min(_scrollOffset + _viewportRowCount, _dataRowNodes.Count);
            int rowsRendered = 0;
            
            // For async data sources, we need to adjust indices since _rowStates uses relative indices
            int dataOffset = _dataSource is not null && _cachedRange.HasValue ? _cachedRange.Value.Start : 0;
            
            for (int i = _scrollOffset; i < endRow && y < Bounds.Height - (_footerRowNode is not null ? 3 : 1); i++)
            {
                var rowNode = _dataRowNodes[i];
                
                // Skip null nodes (not yet materialized due to virtualization)
                // This shouldn't happen for visible rows, but handle gracefully
                if (rowNode is null)
                {
                    y++;
                    rowsRendered++;
                    if (RenderMode == TableRenderMode.Full && i < endRow - 1)
                        y++;
                    continue;
                }
                
                // Convert absolute row index to relative index for state lookup
                int relativeIndex = i - dataOffset;
                if (relativeIndex < 0 || relativeIndex >= _rowStates.Count)
                {
                    // State not available for this row, but check for index-based focus
                    bool isFocused = FocusedKey is int focusedIndex && focusedIndex == i;
                    rowNode.IsHighlighted = isFocused;
                    rowNode.IsSelected = false;
                }
                else
                {
                    var state = _rowStates[relativeIndex];
                    bool isSelected = IsRowSelectedForRender(i);
                    
                    rowNode.IsHighlighted = state.IsFocused;
                    rowNode.IsSelected = isSelected;
                }
                
                context.RenderChild(rowNode);
                y++;
                rowsRendered++;
                
                // In Full mode, render separator between rows (but not after the last visible row)
                if (RenderMode == TableRenderMode.Full && i < endRow - 1 && y < Bounds.Height - (_footerRowNode is not null ? 3 : 1))
                {
                    RenderHorizontalBorder(context, y, TeeRight, Cross, rightTee);
                    y++;
                }
            }
        }

        // Footer
        if (_footerRowNode is not null)
        {
            if (hasColumnStructure)
            {
                RenderHorizontalBorder(context, y, TeeRight, Cross, rightTee);
            }
            else
            {
                // Transition from empty to footer - columns "open up" again
                // Selection column still needs a cross since it's still visible
                char emptyRightTee = IsScrollable ? Cross : TeeLeft;
                RenderHorizontalBorder(context, y, TeeRight, TeeDown, emptyRightTee, selectionColumnMiddle: Cross);
            }
            y++;
            context.RenderChild(_footerRowNode);
            y++;
        }

        // Bottom border
        if (y < Bounds.Height)
        {
            // Use TeeUp for column positions when we have data/loading/footer, just horizontal when empty with no footer
            bool showColumnTees = hasColumnStructure || _footerRowNode is not null;
            if (showColumnTees)
            {
                RenderHorizontalBorder(context, y, BottomLeft, TeeUp, bottomRightCorner);
            }
            else
            {
                // Empty state with no footer - solid bottom border
                char emptyBottomRight = IsScrollable ? TeeUp : BottomRight;
                RenderSolidHorizontalBorder(context, y, BottomLeft, emptyBottomRight);
            }
        }
        
        // Render scrollbar if needed
        if (IsScrollable)
        {
            RenderScrollbar(context);
        }
    }

    private void RenderScrollbar(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var borderColor = theme.Get(TableTheme.BorderColor);
        var focusedBorderColor = theme.Get(TableTheme.FocusedBorderColor);
        
        // Scrollbar column starts after the table's right border
        var scrollbarColumnX = Bounds.X + Bounds.Width - ScrollbarColumnWidth;
        
        // Calculate the actual content height in screen rows
        // In Full mode, we have data rows + separators between them
        // In other modes, just data rows
        int visibleDataRows = Math.Min(_viewportRowCount, _contentRowCount);
        int scrollbarHeight;
        if (RenderMode == TableRenderMode.Full)
        {
            // data rows + separators between them = visibleDataRows + (visibleDataRows - 1) = 2*visibleDataRows - 1
            // But if no data, just 1 row for empty state
            scrollbarHeight = visibleDataRows > 0 ? (2 * visibleDataRows - 1) : 1;
        }
        else
        {
            scrollbarHeight = Math.Max(1, visibleDataRows);
        }
        
        if (scrollbarHeight <= 0) return;
        
        // Calculate thumb size and position based on screen rows
        // Thumb size is proportional to viewport/content ratio
        var thumbSize = Math.Max(1, (int)Math.Ceiling((double)_viewportRowCount / _contentRowCount * scrollbarHeight));
        var scrollRange = scrollbarHeight - thumbSize;
        var thumbPosition = scrollRange > 0 && MaxScrollOffset > 0
            ? (int)Math.Round((double)_scrollOffset / MaxScrollOffset * scrollRange) 
            : 0;
        
        // Render top border of scrollbar column
        int y = 0;
        context.SetCursorPosition(scrollbarColumnX, Bounds.Y + y);
        context.Write($"{borderColor.ToForegroundAnsi()}{Horizontal}{TopRight}\x1b[0m");
        y++;
        
        // Render header row(s) - empty scrollbar area with track
        if (_headerRowNode is not null)
        {
            // Header row - empty cell + border (no track in header area)
            context.SetCursorPosition(scrollbarColumnX, Bounds.Y + y);
            context.Write($"{borderColor.ToForegroundAnsi()} {Vertical}\x1b[0m");
            y++;
            
            // Header separator - connects to table's horizontal line
            context.SetCursorPosition(scrollbarColumnX, Bounds.Y + y);
            context.Write($"{borderColor.ToForegroundAnsi()}{Horizontal}{TeeLeft}\x1b[0m");
            y++;
        }
        
        // Render content rows with continuous scrollbar track/thumb
        // The track runs continuously - no breaks for row separators in Full mode
        for (int row = 0; row < scrollbarHeight; row++)
        {
            context.SetCursorPosition(scrollbarColumnX, Bounds.Y + y);
            
            bool isThumb = row >= thumbPosition && row < thumbPosition + thumbSize;
            char trackChar = isThumb ? ScrollbarThumb : ScrollbarTrack;
            var trackColor = isThumb ? focusedBorderColor : borderColor;
            
            // Always use vertical border - continuous clean track
            context.Write($"{trackColor.ToForegroundAnsi()}{trackChar}{borderColor.ToForegroundAnsi()}{Vertical}\x1b[0m");
            y++;
        }
        
        // Render footer row(s) if present
        if (_footerRowNode is not null)
        {
            // Footer separator - connects to table's horizontal line
            context.SetCursorPosition(scrollbarColumnX, Bounds.Y + y);
            context.Write($"{borderColor.ToForegroundAnsi()}{Horizontal}{TeeLeft}\x1b[0m");
            y++;
            
            // Footer row - track + border
            context.SetCursorPosition(scrollbarColumnX, Bounds.Y + y);
            context.Write($"{borderColor.ToForegroundAnsi()}{ScrollbarTrack}{Vertical}\x1b[0m");
            y++;
        }
        
        // Render bottom border
        context.SetCursorPosition(scrollbarColumnX, Bounds.Y + y);
        context.Write($"{borderColor.ToForegroundAnsi()}{Horizontal}{BottomRight}\x1b[0m");
    }

    private void RenderHorizontalBorder(Hex1bRenderContext context, int y, char left, char middle, char right, char? selectionColumnMiddle = null)
    {
        var sb = new System.Text.StringBuilder();
        
        // Apply border color from theme
        var borderColor = context.Theme.Get(TableTheme.BorderColor);
        sb.Append(borderColor.ToForegroundAnsi());
        
        sb.Append(left);

        // Selection column border (if enabled)
        if (ShowSelectionColumn)
        {
            sb.Append(new string(Horizontal, SelectionColumnWidth));
            // Use specific character for selection column separator if provided, otherwise use middle
            sb.Append(selectionColumnMiddle ?? middle);
        }

        // In Full mode, each column has 1 char padding on left and right
        int paddingPerColumn = RenderMode == TableRenderMode.Full ? 2 : 0;

        for (int col = 0; col < _columnCount; col++)
        {
            sb.Append(new string(Horizontal, _columnWidths[col] + paddingPerColumn));
            if (col < _columnCount - 1)
            {
                sb.Append(middle);
            }
        }

        sb.Append(right);
        sb.Append("\x1b[0m"); // Reset
        context.WriteClipped(Bounds.X, Bounds.Y + y, sb.ToString());
    }

    /// <summary>
    /// Renders a row using child TextBlockNodes.
    /// </summary>
    private void RenderRowWithNodes(Hex1bRenderContext context, int y, List<TextBlockNode> nodes, bool isHighlighted = false, bool? isSelected = null, bool isHeader = false)
    {
        // Render borders
        var sb = new System.Text.StringBuilder();
        var theme = context.Theme;
        
        // Apply reverse video for highlighted rows
        if (isHighlighted)
        {
            sb.Append("\x1b[7m"); // Reverse video on
        }
        
        sb.Append(Vertical);
        
        // Selection column (if enabled)
        if (ShowSelectionColumn)
        {
            var selWidth = SelectionColumnWidth;
            if (isHeader)
            {
                // Header checkbox - only show when there's data to select
                if (Data != null && Data.Count > 0)
                {
                    var selectedCount = IsSelectedSelector != null ? Data.Count(IsSelectedSelector) : 0;
                    var allSelected = selectedCount == Data.Count;
                    var someSelected = selectedCount > 0 && !allSelected;
                    var checkChar = allSelected ? theme.Get(TableTheme.CheckboxChecked) 
                        : someSelected ? theme.Get(TableTheme.CheckboxIndeterminate)
                        : theme.Get(TableTheme.CheckboxUnchecked);
                    var checkColor = allSelected ? theme.Get(TableTheme.CheckboxCheckedForeground)
                        : theme.Get(TableTheme.CheckboxUncheckedForeground);
                    sb.Append(checkColor.ToForegroundAnsi());
                    sb.Append(PadRightByDisplayWidth(checkChar, selWidth));
                    sb.Append(Hex1bColor.Default.ToForegroundAnsi());
                }
                else
                {
                    // No data (loading or empty) - show empty space
                    sb.Append(new string(' ', selWidth));
                }
            }
            else if (isSelected.HasValue)
            {
                // Data row checkbox
                var checkChar = isSelected.Value ? theme.Get(TableTheme.CheckboxChecked) : theme.Get(TableTheme.CheckboxUnchecked);
                var checkColor = isSelected.Value ? theme.Get(TableTheme.CheckboxCheckedForeground) : theme.Get(TableTheme.CheckboxUncheckedForeground);
                sb.Append(checkColor.ToForegroundAnsi());
                sb.Append(PadRightByDisplayWidth(checkChar, selWidth));
                sb.Append(Hex1bColor.Default.ToForegroundAnsi());
            }
            else
            {
                // Footer or other - just empty
                sb.Append(new string(' ', selWidth));
            }
            sb.Append(Vertical);
        }
        
        int x = 1; // Start after left border
        for (int col = 0; col < _columnCount && col < nodes.Count; col++)
        {
            // Add padding (cell content will be rendered by child node)
            sb.Append(new string(' ', _columnWidths[col]));
            
            if (col < _columnCount - 1)
            {
                sb.Append(Vertical);
            }
            x += _columnWidths[col] + 1;
        }
        
        sb.Append(Vertical);
        
        if (isHighlighted)
        {
            sb.Append("\x1b[27m"); // Reverse video off
        }
        
        // Write borders (with spaces for cell content)
        context.WriteClipped(Bounds.X, Bounds.Y + y, sb.ToString());
        
        // Now render each cell node on top with proper clipping
        var previousLayout = context.CurrentLayoutProvider;
        foreach (var node in nodes)
        {
            // Create a layout provider to clip each cell to its bounds
            var cellProvider = new RectLayoutProvider(node.Bounds);
            cellProvider.ParentLayoutProvider = previousLayout;
            context.CurrentLayoutProvider = cellProvider;
            
            context.RenderChild(node);
            
            context.CurrentLayoutProvider = previousLayout;
        }
    }

    /// <summary>
    /// Renders a row directly from cells (used for loading state).
    /// </summary>
    private void RenderRowDirect(Hex1bRenderContext context, int y, IReadOnlyList<TableCell> cells, 
        bool isHeader, bool isSelected = false, bool isFooter = false, bool isLoading = false)
    {
        var sb = new System.Text.StringBuilder();
        var theme = context.Theme;
        
        // Apply reverse video for selected rows
        if (isSelected)
        {
            sb.Append("\x1b[7m"); // Reverse video on
        }
        
        sb.Append(Vertical);

        // Selection column (if enabled)
        if (ShowSelectionColumn)
        {
            var selWidth = SelectionColumnWidth;
            if (isHeader)
            {
                // Header checkbox - only show when there's data to select
                if (Data != null && Data.Count > 0)
                {
                    var selectedCount = IsSelectedSelector != null ? Data.Count(IsSelectedSelector) : 0;
                    var allSelected = selectedCount == Data.Count;
                    var someSelected = selectedCount > 0 && !allSelected;
                    var checkChar = allSelected ? theme.Get(TableTheme.CheckboxChecked) 
                        : someSelected ? theme.Get(TableTheme.CheckboxIndeterminate)
                        : theme.Get(TableTheme.CheckboxUnchecked);
                    sb.Append(PadRightByDisplayWidth(checkChar, selWidth));
                }
                else
                {
                    // No data (loading or empty) - show empty space
                    sb.Append(new string(' ', selWidth));
                }
            }
            else if (!isFooter && !isLoading)
            {
                // Data row checkbox
                var checkChar = isSelected ? theme.Get(TableTheme.CheckboxChecked) : theme.Get(TableTheme.CheckboxUnchecked);
                sb.Append(PadRightByDisplayWidth(checkChar, selWidth));
            }
            else
            {
                // Footer or loading row - just empty
                sb.Append(new string(' ', selWidth));
            }
            sb.Append(Vertical);
        }

        for (int col = 0; col < _columnCount && col < cells.Count; col++)
        {
            var cell = cells[col];
            var text = cell.Text ?? "";
            int width = _columnWidths[col];

            // Truncate or pad text to fit column width
            if (text.Length > width)
            {
                text = text[..(width - 1)] + "…";
            }
            else
            {
                text = text.PadRight(width);
            }

            sb.Append(text);

            if (col < _columnCount - 1)
            {
                sb.Append(Vertical);
            }
        }

        sb.Append(Vertical);
        
        if (isSelected)
        {
            sb.Append("\x1b[27m"); // Reverse video off
        }
        
        context.WriteClipped(Bounds.X, Bounds.Y + y, sb.ToString());
    }

    private void RenderSolidHorizontalBorder(Hex1bRenderContext context, int y, char left, char right)
    {
        var sb = new System.Text.StringBuilder();
        
        // Apply border color from theme
        var borderColor = context.Theme.Get(TableTheme.BorderColor);
        sb.Append(borderColor.ToForegroundAnsi());
        
        sb.Append(left);

        // Selection column (if enabled)
        if (ShowSelectionColumn)
        {
            sb.Append(new string(Horizontal, SelectionColumnWidth));
            sb.Append(TeeUp); // ┴ character where selection column ends
        }
        
        // In Full mode, each column has 1 char padding on left and right
        int paddingTotal = RenderMode == TableRenderMode.Full ? _columnCount * 2 : 0;
        
        // Total content width = sum of column widths + (columnCount - 1) separators + padding
        int contentWidth = _columnWidths.Sum() + (_columnCount - 1) + paddingTotal;
        sb.Append(new string(Horizontal, contentWidth));

        sb.Append(right);
        sb.Append("\x1b[0m"); // Reset
        context.WriteClipped(Bounds.X, Bounds.Y + y, sb.ToString());
    }

    private void RenderEmptyRow(Hex1bRenderContext context, int y, int totalWidth)
    {
        var sb = new System.Text.StringBuilder();
        
        // Apply border color from theme
        var borderColor = context.Theme.Get(TableTheme.BorderColor);
        sb.Append(borderColor.ToForegroundAnsi());
        
        sb.Append(Vertical);

        // Selection column (if enabled) - show empty (no checkbox for empty state)
        if (ShowSelectionColumn)
        {
            sb.Append("\x1b[0m"); // Reset for content
            sb.Append(new string(' ', SelectionColumnWidth));
            sb.Append(borderColor.ToForegroundAnsi());
            sb.Append(Vertical);
        }

        // In Full mode, account for padding
        int paddingTotal = RenderMode == TableRenderMode.Full ? _columnCount * 2 : 0;

        // Content area without column separators
        int contentWidth = _columnWidths.Sum() + (_columnCount - 1) + paddingTotal;
        
        // Center the "No data" message
        const string emptyMessage = "No data";
        int padding = (contentWidth - emptyMessage.Length) / 2;
        
        sb.Append("\x1b[0m"); // Reset for content
        if (padding > 0)
        {
            sb.Append(new string(' ', padding));
            sb.Append(emptyMessage);
            sb.Append(new string(' ', contentWidth - padding - emptyMessage.Length));
        }
        else
        {
            // If content area is too small, just show what fits
            sb.Append(emptyMessage.Length <= contentWidth 
                ? emptyMessage.PadRight(contentWidth) 
                : emptyMessage[..contentWidth]);
        }

        sb.Append(borderColor.ToForegroundAnsi());
        sb.Append(Vertical);
        sb.Append("\x1b[0m"); // Reset
        context.WriteClipped(Bounds.X, Bounds.Y + y, sb.ToString());
    }

    #region INotifyCollectionChanged Support

    /// <summary>
    /// Subscribes to CollectionChanged events if the data source supports it.
    /// </summary>
    private void SubscribeToCollectionChanged()
    {
        if (_data is INotifyCollectionChanged notifyCollection)
        {
            _subscribedCollection = notifyCollection;
            notifyCollection.CollectionChanged += OnCollectionChanged;
        }
    }

    /// <summary>
    /// Unsubscribes from CollectionChanged events.
    /// </summary>
    private void UnsubscribeFromCollectionChanged()
    {
        if (_subscribedCollection is not null)
        {
            _subscribedCollection.CollectionChanged -= OnCollectionChanged;
            _subscribedCollection = null;
        }
    }

    /// <summary>
    /// Handles CollectionChanged events from the data source.
    /// </summary>
    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Clear cached data on collection change
        _cachedItems = null;
        _cachedItemCount = null;
        _cachedRange = null;
        
        // Mark dirty and trigger app invalidation to re-render
        MarkDirty();
        InvalidateCallback?.Invoke();
    }

    #endregion
    
    #region ITableDataSource Support
    
    /// <summary>
    /// Subscribes to CollectionChanged events from the data source.
    /// </summary>
    private void SubscribeToDataSource()
    {
        if (_dataSource is INotifyCollectionChanged notifyCollection)
        {
            _subscribedDataSource = notifyCollection;
            notifyCollection.CollectionChanged += OnDataSourceCollectionChanged;
        }
    }
    
    /// <summary>
    /// Unsubscribes from data source events.
    /// </summary>
    private void UnsubscribeFromDataSource()
    {
        if (_subscribedDataSource is not null)
        {
            _subscribedDataSource.CollectionChanged -= OnDataSourceCollectionChanged;
            _subscribedDataSource = null;
        }
    }
    
    /// <summary>
    /// Handles CollectionChanged events from the data source.
    /// </summary>
    private void OnDataSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Clear cached data
        _cachedItems = null;
        _cachedItemCount = null;
        _cachedRange = null;
        
        // Mark dirty and trigger app invalidation to re-render
        MarkDirty();
        InvalidateCallback?.Invoke();
    }
    
    /// <summary>
    /// Gets the effective data to use, either from Data property or cached from DataSource.
    /// </summary>
    public IReadOnlyList<TRow>? GetEffectiveData() => _dataSource is not null ? _cachedItems : _data;
    
    /// <summary>
    /// Gets the total item count, either from Data or cached from DataSource.
    /// </summary>
    public int GetEffectiveItemCount() => _dataSource is not null ? (_cachedItemCount ?? 0) : (_data?.Count ?? 0);
    
    /// <summary>
    /// Loads data from the async data source for the visible range.
    /// Called during reconciliation.
    /// </summary>
    public async ValueTask LoadDataAsync(int startIndex, int count, CancellationToken cancellationToken = default)
    {
        if (_dataSource is null) return;
        
        // Load item count if not cached
        if (!_cachedItemCount.HasValue)
        {
            _cachedItemCount = await _dataSource.GetItemCountAsync(cancellationToken);
        }
        
        // Check if we already have this range cached
        if (_cachedRange.HasValue && _cachedRange.Value.Start <= startIndex && 
            _cachedRange.Value.End >= startIndex + count && _cachedItems is not null)
        {
            return; // Already have the data
        }
        
        // Load the requested range
        _cachedItems = await _dataSource.GetItemsAsync(startIndex, count, cancellationToken);
        _cachedRange = (startIndex, startIndex + count);
        
        // Set Data to cached items so rendering code can use it
        // Note: This bypasses the setter to avoid re-subscribing
        _data = _cachedItems;
        
        // Resolve integer-based focus key to actual row key if the focused row is now loaded
        ResolveFocusKeyIfNeeded();
        
        // Set initial focus if no focus is set and we have data
        SetInitialFocusIfNeeded();
    }
    
    /// <summary>
    /// Sets initial focus to the first row if no focus is currently set.
    /// Called after data is first loaded for async data sources.
    /// </summary>
    private void SetInitialFocusIfNeeded()
    {
        if (FocusedKey is not null)
            return;
            
        if (_cachedItems is null || _cachedItems.Count == 0)
            return;
            
        // Set focus to first row
        var firstRow = _cachedItems[0];
        int absoluteIndex = _cachedRange?.Start ?? 0;
        var key = GetRowKey(firstRow, absoluteIndex);
        FocusedKey = key;
        
        // Notify handler
        if (FocusChangedHandler != null)
        {
            _ = FocusChangedHandler(key);
        }
    }
    
    /// <summary>
    /// If FocusedKey is an integer index and that row is now in the cache, 
    /// update FocusedKey to the actual row key for consistent focus tracking.
    /// Also notifies the focus changed handler if the key was resolved.
    /// </summary>
    private void ResolveFocusKeyIfNeeded()
    {
        if (FocusedKey is not int focusedIndex)
            return;
            
        if (_cachedItems is null || !_cachedRange.HasValue)
            return;
            
        var (start, end) = _cachedRange.Value;
        if (focusedIndex >= start && focusedIndex < end)
        {
            int relativeIndex = focusedIndex - start;
            if (relativeIndex >= 0 && relativeIndex < _cachedItems.Count)
            {
                var row = _cachedItems[relativeIndex];
                var actualKey = GetRowKey(row, focusedIndex);
                FocusedKey = actualKey;
                
                // Notify handler with the resolved key
                // Note: Fire-and-forget since we're in a sync context
                if (FocusChangedHandler != null)
                {
                    _ = FocusChangedHandler(actualKey);
                }
            }
        }
    }

    /// <summary>
    /// Disposes resources and unsubscribes from events.
    /// </summary>
    public void Dispose()
    {
        UnsubscribeFromCollectionChanged();
        UnsubscribeFromDataSource();
        GC.SuppressFinalize(this);
    }

    #endregion
}
