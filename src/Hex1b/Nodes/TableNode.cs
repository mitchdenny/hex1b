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
public class TableNode<TRow> : Hex1bNode, ILayoutProvider
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
    
    // Scrollbar constants
    private const int ScrollbarWidth = 1;

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
    public IReadOnlyList<TRow>? Data { get; set; }

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
    /// The render mode for the table (Compact or Full).
    /// </summary>
    public TableRenderMode RenderMode { get; set; } = TableRenderMode.Compact;

    // Scroll state
    private int _scrollOffset;
    private int _contentRowCount;
    private int _viewportRowCount;
    private Rect _contentViewport;
    
    /// <summary>
    /// The current scroll offset (first visible row index).
    /// </summary>
    public int ScrollOffset => _scrollOffset;
    
    /// <summary>
    /// Whether the table content is scrollable (more rows than viewport).
    /// </summary>
    public bool IsScrollable => _contentRowCount > _viewportRowCount;
    
    /// <summary>
    /// The maximum scroll offset.
    /// </summary>
    public int MaxScrollOffset => Math.Max(0, _contentRowCount - _viewportRowCount);

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
        FocusedKey = key;
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

        for (int i = 0; i < Data.Count; i++)
        {
            var key = GetRowKey(Data[i], i);
            if (Equals(key, FocusedKey))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Sets focus to the row at the given index and ensures it's visible.
    /// </summary>
    private async Task SetFocusedRowIndexAsync(int index)
    {
        if (Data == null || Data.Count == 0)
            return;

        // Clamp index to valid range
        index = Math.Clamp(index, 0, Data.Count - 1);
        
        var row = Data[index];
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
        var maxIndex = (Data?.Count ?? 0) - 1;
        
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
        if (Data != null && Data.Count > 0)
        {
            _ = SetFocusedRowIndexAsync(0);
        }
    }

    private void MoveFocusToLast(InputBindingActionContext ctx)
    {
        if (Data != null && Data.Count > 0)
        {
            _ = SetFocusedRowIndexAsync(Data.Count - 1);
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
        var scrollbarX = Bounds.Width - ScrollbarWidth;
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
        
        if (trackHeight <= 2) return new DragHandler(); // Too small for meaningful scroll
        
        // Calculate thumb position and size
        var thumbSize = Math.Max(1, (int)Math.Ceiling((double)_viewportRowCount / _contentRowCount * (trackHeight - 2)));
        var scrollRange = trackHeight - 2 - thumbSize;
        var thumbPosition = scrollRange > 0 
            ? (int)Math.Round((double)_scrollOffset / MaxScrollOffset * scrollRange) 
            : 0;
        
        var trackY = localY - trackStart;
        
        if (trackY == 0)
        {
            // Up arrow clicked
            ScrollByAmount(-1);
            return new DragHandler();
        }
        else if (trackY == trackHeight - 1)
        {
            // Down arrow clicked
            ScrollByAmount(1);
            return new DragHandler();
        }
        else if (trackY > 0 && trackY < trackHeight - 1)
        {
            var thumbTrackY = trackY - 1;
            
            if (thumbTrackY >= thumbPosition && thumbTrackY < thumbPosition + thumbSize)
            {
                // Clicked on thumb - start drag
                var startOffset = _scrollOffset;
                var contentPerPixel = MaxScrollOffset > 0 && trackHeight - 2 > thumbSize
                    ? (double)MaxScrollOffset / (trackHeight - 2 - thumbSize)
                    : 0;
                
                return new DragHandler(
                    onMove: (deltaX, deltaY) =>
                    {
                        if (contentPerPixel > 0)
                        {
                            var newOffset = (int)Math.Round(startOffset + deltaY * contentPerPixel);
                            SetScrollOffset(newOffset);
                        }
                    }
                );
            }
            else if (thumbTrackY < thumbPosition)
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
    /// </summary>
    private bool IsRowFocused(object key)
    {
        return FocusedKey is not null && Equals(FocusedKey, key);
    }

    /// <summary>
    /// Checks if a row is selected using the selector.
    /// </summary>
    private bool IsRowSelected(TRow row)
    {
        return IsSelectedSelector?.Invoke(row) ?? false;
    }
    
    /// <summary>
    /// Checks if a row at the given index is currently selected (for rendering).
    /// </summary>
    private bool IsRowSelectedForRender(int rowIndex)
    {
        if (Data == null || rowIndex < 0 || rowIndex >= Data.Count)
            return false;
        return IsSelectedSelector?.Invoke(Data[rowIndex]) ?? false;
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
            for (int i = 0; i < rowCount; i++)
            {
                var row = Data[i];
                var rowKey = GetRowKey(row, i);
                
                var state = new TableRowState
                {
                    RowIndex = i,
                    RowKey = rowKey,
                    IsFocused = IsRowFocused(rowKey),
                    IsSelected = IsRowSelected(row),
                    IsFirst = i == 0,
                    IsLast = i == rowCount - 1
                };
                
                var cells = RowBuilder(rowContext, row, state);
                if (cells.Count != _columnCount)
                {
                    throw new InvalidOperationException(
                        $"Table column count mismatch: header has {_columnCount} columns, " +
                        $"but row {i} has {cells.Count} columns.");
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
        
        // Reserve space for scrollbar if needed
        int scrollbarSpace = reserveScrollbar ? ScrollbarWidth : 0;
        
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
        // Both null data and empty data show the empty state
        if (Data is null || Data.Count == 0)
        {
            _contentRowCount = 1; // Empty state (message row)
        }
        else
        {
            _contentRowCount = _rowCells?.Count ?? 0;
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
        
        int scrollbarSpace = IsScrollable ? ScrollbarWidth : 0;
        
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
        int width = Bounds.Width;
        
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
                SetRowNodeColumnWidths(_dataRowNodes[i]);
                _dataRowNodes[i].Arrange(new Rect(Bounds.X, Bounds.Y + y, width, 1));
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

        // Top border
        RenderHorizontalBorder(context, y, TopLeft, TeeDown, TopRight);
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
                RenderHorizontalBorder(context, y, TeeRight, Cross, TeeLeft);
            }
            else
            {
                // No column breaks in separator when empty - columns "close off"
                // But selection column still needs a cross since it's still visible
                RenderHorizontalBorder(context, y, TeeRight, TeeUp, TeeLeft, selectionColumnMiddle: Cross);
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
            for (int i = _scrollOffset; i < endRow && y < Bounds.Height - (_footerRowNode is not null ? 3 : 1); i++)
            {
                var state = _rowStates[i];
                // Use runtime selection check instead of cached state
                bool isSelected = IsRowSelectedForRender(i);
                
                // Update row node highlight/selection state for rendering
                _dataRowNodes[i].IsHighlighted = state.IsFocused;
                _dataRowNodes[i].IsSelected = isSelected;
                
                context.RenderChild(_dataRowNodes[i]);
                y++;
                rowsRendered++;
                
                // In Full mode, render separator between rows (but not after the last visible row)
                if (RenderMode == TableRenderMode.Full && i < endRow - 1 && y < Bounds.Height - (_footerRowNode is not null ? 3 : 1))
                {
                    RenderHorizontalBorder(context, y, TeeRight, Cross, TeeLeft);
                    y++;
                }
            }
        }

        // Footer
        if (_footerRowNode is not null)
        {
            if (hasColumnStructure)
            {
                RenderHorizontalBorder(context, y, TeeRight, Cross, TeeLeft);
            }
            else
            {
                // Transition from empty to footer - columns "open up" again
                // Selection column still needs a cross since it's still visible
                RenderHorizontalBorder(context, y, TeeRight, TeeDown, TeeLeft, selectionColumnMiddle: Cross);
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
                RenderHorizontalBorder(context, y, BottomLeft, TeeUp, BottomRight);
            }
            else
            {
                // Empty state with no footer - solid bottom border
                RenderSolidHorizontalBorder(context, y, BottomLeft, BottomRight);
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
        
        // Use border characters: thin (│) for track, thick (┃) for thumb
        const char trackChar = '│';
        const char thumbChar = '┃';
        
        // Scrollbar is in the rightmost column, aligned with the content viewport
        var scrollbarX = Bounds.X + Bounds.Width - ScrollbarWidth;
        var scrollbarY = _contentViewport.Y;
        var scrollbarHeight = _contentViewport.Height;
        
        if (scrollbarHeight <= 0) return;
        
        // Calculate thumb position and size (no arrows, use full height)
        var thumbSize = Math.Max(1, (int)Math.Ceiling((double)_viewportRowCount / _contentRowCount * scrollbarHeight));
        var scrollRange = scrollbarHeight - thumbSize;
        var thumbPosition = scrollRange > 0 && MaxScrollOffset > 0
            ? (int)Math.Round((double)_scrollOffset / MaxScrollOffset * scrollRange) 
            : 0;
        
        // Render scrollbar
        for (int row = 0; row < scrollbarHeight; row++)
        {
            context.SetCursorPosition(scrollbarX, scrollbarY + row);
            
            if (row >= thumbPosition && row < thumbPosition + thumbSize)
            {
                // Thumb - use thick line with focused color
                context.Write($"{focusedBorderColor.ToForegroundAnsi()}{thumbChar}\x1b[0m");
            }
            else
            {
                // Track - use thin line with border color
                context.Write($"{borderColor.ToForegroundAnsi()}{trackChar}\x1b[0m");
            }
        }
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
        int selectionWidth = ShowSelectionColumn ? SelectionColumnWidth + 1 : 0; // +1 for separator
        
        // In Full mode, each column has 1 char padding on left and right
        int paddingTotal = RenderMode == TableRenderMode.Full ? _columnCount * 2 : 0;
        
        // Total content width = sum of column widths + (columnCount - 1) separators + selection column + padding
        int contentWidth = _columnWidths.Sum() + (_columnCount - 1) + selectionWidth + paddingTotal;
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
}
