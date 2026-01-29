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
    /// Builder for loading row cells.
    /// </summary>
    public Func<TableLoadingContext, int, IReadOnlyList<TableCell>>? LoadingRowBuilder { get; set; }

    /// <summary>
    /// Number of loading placeholder rows.
    /// </summary>
    public int LoadingRowCount { get; set; } = 3;

    /// <summary>
    /// Selector function to extract a unique key from each row.
    /// </summary>
    public Func<TRow, object>? RowKeySelector { get; set; }

    /// <summary>
    /// The key of the currently focused row.
    /// </summary>
    public object? FocusedKey { get; set; }

    /// <summary>
    /// The set of keys for selected rows.
    /// </summary>
    public IReadOnlySet<object>? SelectedKeys { get; set; }

    /// <summary>
    /// Handler for focus changes.
    /// </summary>
    public Func<object?, Task>? FocusChangedHandler { get; set; }

    /// <summary>
    /// Handler for selection changes.
    /// </summary>
    public Func<IReadOnlySet<object>, Task>? SelectionChangedHandler { get; set; }

    /// <summary>
    /// Handler for row activation.
    /// </summary>
    public Func<object, TRow, Task>? RowActivatedHandler { get; set; }

    /// <summary>
    /// Whether to show a selection column with checkboxes.
    /// </summary>
    public bool ShowSelectionColumn { get; set; }

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

    // Computed layout data
    private int _columnCount;
    private int[] _columnWidths = [];
    private IReadOnlyList<TableCell>? _headerCells;
    private List<IReadOnlyList<TableCell>>? _rowCells;
    private List<TableRowState>? _rowStates;
    private IReadOnlyList<TableCell>? _footerCells;
    
    // Child nodes for cell rendering
    private List<TextBlockNode>? _headerNodes;
    private List<List<TextBlockNode>>? _rowNodes;
    private List<TextBlockNode>? _footerNodes;

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
        if (ShowSelectionColumn && _headerNodes is not null)
        {
            int headerY = Bounds.Y + 1; // After top border
            int checkboxEndX = Bounds.X + 1 + SelectionColumnWidth;
            
            if (mouseY == headerY && mouseX >= Bounds.X + 1 && mouseX < checkboxEndX)
            {
                // Toggle select all/none
                await ToggleSelectAll();
                return;
            }
        }
        
        if (Data is null || Data.Count == 0) return;
        
        // Calculate which row was clicked
        int headerHeight = _headerNodes is not null ? 2 : 0; // Header row + separator
        int dataStartY = Bounds.Y + 1 + headerHeight; // After top border + header
        
        int clickedRowIndex = mouseY - dataStartY + _scrollOffset;
        
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
            await ToggleSelectionForKey(key);
        }
    }
    
    /// <summary>
    /// Toggles selection for a specific key.
    /// </summary>
    private async Task ToggleSelectionForKey(object key)
    {
        SyncInternalSelection();
        
        if (_internalSelectedKeys.Contains(key))
        {
            _internalSelectedKeys.Remove(key);
        }
        else
        {
            _internalSelectedKeys.Add(key);
        }
        
        await NotifySelectionChanged();
    }
    
    /// <summary>
    /// Toggles between select all and deselect all.
    /// </summary>
    private async Task ToggleSelectAll()
    {
        if (Data is null || Data.Count == 0) return;
        
        SyncInternalSelection();
        
        // If all selected, deselect all; otherwise select all
        bool allSelected = _internalSelectedKeys.Count == Data.Count;
        
        _internalSelectedKeys.Clear();
        
        if (!allSelected)
        {
            for (int i = 0; i < Data.Count; i++)
            {
                var key = RowKeySelector?.Invoke(Data[i]) ?? i;
                _internalSelectedKeys.Add(key);
            }
        }
        
        await NotifySelectionChanged();
    }

    // Selection anchor for range selection (the row where Shift-selection started)
    private int _selectionAnchorIndex = -1;
    
    // Internal mutable set for selection management
    private HashSet<object> _internalSelectedKeys = new();

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

        var key = GetRowKey(Data[currentIndex], currentIndex);
        
        // Toggle in internal set
        SyncInternalSelection();
        if (_internalSelectedKeys.Contains(key))
        {
            _internalSelectedKeys.Remove(key);
        }
        else
        {
            _internalSelectedKeys.Add(key);
        }
        
        // Update anchor to current position
        _selectionAnchorIndex = currentIndex;
        
        _ = NotifySelectionChanged();
    }

    /// <summary>
    /// Selects all rows.
    /// </summary>
    private void SelectAll(InputBindingActionContext ctx)
    {
        if (Data == null || Data.Count == 0)
            return;

        _internalSelectedKeys.Clear();
        for (int i = 0; i < Data.Count; i++)
        {
            _internalSelectedKeys.Add(GetRowKey(Data[i], i));
        }
        
        _ = NotifySelectionChanged();
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
        _ = SelectRangeAsync(_selectionAnchorIndex, currentIndex - 1);
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
        _ = SelectRangeAsync(_selectionAnchorIndex, currentIndex + 1);
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
        _ = SelectRangeAsync(_selectionAnchorIndex, 0);
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
        _ = SelectRangeAsync(_selectionAnchorIndex, maxIndex);
    }

    /// <summary>
    /// Selects all rows in the range from startIndex to endIndex (inclusive).
    /// </summary>
    private async Task SelectRangeAsync(int startIndex, int endIndex)
    {
        if (Data == null)
            return;

        var minIndex = Math.Min(startIndex, endIndex);
        var maxIndex = Math.Max(startIndex, endIndex);
        
        // Clamp to valid range
        minIndex = Math.Max(0, minIndex);
        maxIndex = Math.Min(Data.Count - 1, maxIndex);

        _internalSelectedKeys.Clear();
        for (int i = minIndex; i <= maxIndex; i++)
        {
            _internalSelectedKeys.Add(GetRowKey(Data[i], i));
        }

        await NotifySelectionChanged();
    }

    /// <summary>
    /// Syncs internal selection set with external SelectedKeys.
    /// </summary>
    private void SyncInternalSelection()
    {
        if (SelectedKeys != null)
        {
            _internalSelectedKeys = new HashSet<object>(SelectedKeys);
        }
    }

    /// <summary>
    /// Notifies the selection changed handler and updates SelectedKeys.
    /// </summary>
    private async Task NotifySelectionChanged()
    {
        SelectedKeys = _internalSelectedKeys.ToHashSet();
        MarkDirty();
        
        if (SelectionChangedHandler != null)
        {
            await SelectionChangedHandler(SelectedKeys);
        }
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
        var headerHeight = _headerNodes is not null ? 2 : 0; // Header row + separator
        var footerHeight = _footerNodes is not null ? 2 : 0; // Separator + footer row
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
    /// Checks if a row key is selected (uses internal selection for runtime updates).
    /// </summary>
    private bool IsRowSelected(object key)
    {
        // Check internal selection first (updated at runtime), fall back to external prop
        if (_internalSelectedKeys.Count > 0)
        {
            return _internalSelectedKeys.Contains(key);
        }
        return SelectedKeys?.Contains(key) == true;
    }
    
    /// <summary>
    /// Checks if a row key is currently selected (for rendering).
    /// This is the runtime-aware version that checks internal state.
    /// </summary>
    private bool IsRowSelectedForRender(object key)
    {
        return _internalSelectedKeys.Contains(key) || (SelectedKeys?.Contains(key) == true);
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
                    IsSelected = IsRowSelected(rowKey),
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
        
        // Create child nodes for cells
        BuildChildNodes();
    }

    /// <summary>
    /// Creates or updates TextBlockNode children for each cell.
    /// </summary>
    private void BuildChildNodes()
    {
        // Header nodes
        if (_headerCells is not null)
        {
            _headerNodes = CreateOrUpdateCellNodes(_headerNodes, _headerCells);
        }
        else
        {
            _headerNodes = null;
        }
        
        // Row nodes
        if (_rowCells is not null && _rowCells.Count > 0)
        {
            _rowNodes ??= [];
            
            // Resize the list
            while (_rowNodes.Count > _rowCells.Count)
            {
                _rowNodes.RemoveAt(_rowNodes.Count - 1);
            }
            while (_rowNodes.Count < _rowCells.Count)
            {
                _rowNodes.Add([]);
            }
            
            for (int i = 0; i < _rowCells.Count; i++)
            {
                _rowNodes[i] = CreateOrUpdateCellNodes(_rowNodes[i], _rowCells[i]);
            }
        }
        else
        {
            _rowNodes = null;
        }
        
        // Footer nodes
        if (_footerCells is not null)
        {
            _footerNodes = CreateOrUpdateCellNodes(_footerNodes, _footerCells);
        }
        else
        {
            _footerNodes = null;
        }
    }

    /// <summary>
    /// Creates or updates a list of TextBlockNodes from cells.
    /// </summary>
    private static List<TextBlockNode> CreateOrUpdateCellNodes(
        List<TextBlockNode>? existing, 
        IReadOnlyList<TableCell> cells)
    {
        existing ??= [];
        
        // Resize the list
        while (existing.Count > cells.Count)
        {
            existing.RemoveAt(existing.Count - 1);
        }
        while (existing.Count < cells.Count)
        {
            existing.Add(new TextBlockNode());
        }
        
        // Update text values
        for (int i = 0; i < cells.Count; i++)
        {
            existing[i].Text = cells[i].Text ?? "";
            existing[i].Overflow = TextOverflow.Ellipsis; // Use ellipsis for table cells
        }
        
        return existing;
    }

    /// <summary>
    /// Gets the selection column width from theme when selection column is enabled.
    /// Uses the theme's default value for layout calculations (theme may be overridden at render time).
    /// </summary>
    private int SelectionColumnWidth => ShowSelectionColumn ? TableTheme.SelectionColumnWidth.DefaultValue() : 0;

    /// <summary>
    /// Calculates column widths based on content (equal distribution for Phase 1).
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
        
        int contentWidth = availableWidth - borderWidth - scrollbarSpace - selectionSpace;

        if (contentWidth < _columnCount)
        {
            // Minimum 1 char per column
            contentWidth = _columnCount;
        }

        // Equal distribution for Phase 1
        int baseWidth = contentWidth / _columnCount;
        int remainder = contentWidth % _columnCount;

        _columnWidths = new int[_columnCount];
        for (int i = 0; i < _columnCount; i++)
        {
            _columnWidths[i] = baseWidth + (i < remainder ? 1 : 0);
        }
    }

    public override Size Measure(Constraints constraints)
    {
        BuildCellData();

        // Determine total content row count
        if (Data is null)
        {
            _contentRowCount = LoadingRowCount; // Loading state
        }
        else if (Data.Count == 0)
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
            maxHeight = fixedHeight + _contentRowCount + 10; // Add some padding
        }
        _viewportRowCount = Math.Max(1, maxHeight - fixedHeight);
        
        // Determine if scrollbar is needed
        bool needsScrollbar = _contentRowCount > _viewportRowCount;
        
        // Calculate column widths (reserve space for scrollbar if needed)
        int width = constraints.MaxWidth > 0 ? constraints.MaxWidth : 80;
        CalculateColumnWidths(width, needsScrollbar);

        // Calculate total height (capped by viewport)
        int dataRowsHeight = Math.Min(_contentRowCount, _viewportRowCount);
        if (Data is not null && Data.Count == 0)
        {
            dataRowsHeight = 1; // Empty message
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
    /// Measures all child nodes with appropriate constraints.
    /// </summary>
    private void MeasureChildNodes()
    {
        // Measure header nodes
        if (_headerNodes is not null)
        {
            for (int col = 0; col < _headerNodes.Count && col < _columnWidths.Length; col++)
            {
                var constraints = new Constraints(0, _columnWidths[col], 0, 1);
                _headerNodes[col].Measure(constraints);
            }
        }
        
        // Measure row nodes
        if (_rowNodes is not null)
        {
            foreach (var rowNodeList in _rowNodes)
            {
                for (int col = 0; col < rowNodeList.Count && col < _columnWidths.Length; col++)
                {
                    var constraints = new Constraints(0, _columnWidths[col], 0, 1);
                    rowNodeList[col].Measure(constraints);
                }
            }
        }
        
        // Measure footer nodes
        if (_footerNodes is not null)
        {
            for (int col = 0; col < _footerNodes.Count && col < _columnWidths.Length; col++)
            {
                var constraints = new Constraints(0, _columnWidths[col], 0, 1);
                _footerNodes[col].Measure(constraints);
            }
        }
    }

    public override void Arrange(Rect rect)
    {
        Bounds = rect;
        
        // Calculate content viewport for clipping
        int headerHeight = _headerNodes is not null ? 2 : 0; // Header row + separator
        int footerHeight = _footerNodes is not null ? 2 : 0; // Separator + footer row
        
        // Recalculate viewport row count based on actual arranged height
        // This is critical for scrolling to work correctly since Measure may receive unbounded constraints
        int fixedHeight = 2 + headerHeight + footerHeight; // Top/bottom borders + header/footer
        _viewportRowCount = Math.Max(1, rect.Height - fixedHeight);
        
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
    /// Arranges all child nodes within their cell bounds.
    /// </summary>
    private void ArrangeChildNodes()
    {
        int y = 0;
        
        // Skip top border
        y++;
        
        // Arrange header nodes
        if (_headerNodes is not null)
        {
            ArrangeRowNodes(_headerNodes, y);
            y += 2; // Header row + separator
        }
        
        // Arrange only visible row nodes (within viewport, offset by scroll)
        if (_rowNodes is not null && _rowNodes.Count > 0)
        {
            int endRow = Math.Min(_scrollOffset + _viewportRowCount, _rowNodes.Count);
            for (int i = _scrollOffset; i < endRow; i++)
            {
                ArrangeRowNodes(_rowNodes[i], y);
                y++;
            }
        }
        else if (Data is not null && Data.Count == 0)
        {
            y++; // Empty message row
        }
        else if (Data is null)
        {
            y += LoadingRowCount; // Loading rows
        }
        
        // Arrange footer nodes (skip separator)
        if (_footerNodes is not null)
        {
            y++; // Skip footer separator
            ArrangeRowNodes(_footerNodes, y);
        }
    }

    /// <summary>
    /// Arranges nodes for a single row.
    /// </summary>
    private void ArrangeRowNodes(List<TextBlockNode> nodes, int rowY)
    {
        int x = Bounds.X + 1; // Start after left border
        
        // Skip selection column if enabled
        if (ShowSelectionColumn)
        {
            x += SelectionColumnWidth + 1; // Skip selection column + separator
        }
        
        for (int col = 0; col < nodes.Count && col < _columnWidths.Length; col++)
        {
            var cellRect = new Rect(x, Bounds.Y + rowY, _columnWidths[col], 1);
            nodes[col].Arrange(cellRect);
            x += _columnWidths[col] + 1; // Move past column + separator
        }
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
        if (_headerNodes is not null)
        {
            RenderRowWithNodes(context, y, _headerNodes, isHeader: true);
            y++;
            
            // Use different separator when transitioning to empty state vs data/loading rows
            if (hasColumnStructure)
            {
                RenderHorizontalBorder(context, y, TeeRight, Cross, TeeLeft);
            }
            else
            {
                // No column breaks in separator when empty - columns "close off"
                RenderHorizontalBorder(context, y, TeeRight, TeeUp, TeeLeft);
            }
            y++;
        }

        // Data rows or loading/empty state
        if (Data is null)
        {
            // Loading state - still show column structure
            var loadingContext = new TableLoadingContext();
            for (int i = 0; i < LoadingRowCount && y < Bounds.Height - 1; i++)
            {
                var loadingCells = LoadingRowBuilder?.Invoke(loadingContext, i);
                if (loadingCells is not null)
                {
                    RenderRowDirect(context, y, loadingCells, isHeader: false, isLoading: true);
                }
                else
                {
                    // Default loading placeholder
                    RenderLoadingRow(context, y);
                }
                y++;
            }
        }
        else if (Data.Count == 0)
        {
            // Empty state - render with just left/right borders, no column separators
            RenderEmptyRow(context, y, totalWidth);
            y++;
        }
        else if (_rowNodes is not null && _rowStates is not null)
        {
            // Render only visible data rows (using scroll offset)
            int endRow = Math.Min(_scrollOffset + _viewportRowCount, _rowNodes.Count);
            int rowsRendered = 0;
            for (int i = _scrollOffset; i < endRow && y < Bounds.Height - (_footerNodes is not null ? 3 : 1); i++)
            {
                var state = _rowStates[i];
                // Use runtime selection check instead of cached state
                bool isSelected = IsRowSelectedForRender(state.RowKey);
                bool isHighlighted = state.IsFocused || isSelected;
                RenderRowWithNodes(context, y, _rowNodes[i], isHighlighted: isHighlighted, isSelected: isSelected);
                y++;
                rowsRendered++;
            }
        }

        // Footer
        if (_footerNodes is not null)
        {
            if (hasColumnStructure)
            {
                RenderHorizontalBorder(context, y, TeeRight, Cross, TeeLeft);
            }
            else
            {
                // Transition from empty to footer - columns "open up" again
                RenderHorizontalBorder(context, y, TeeRight, TeeDown, TeeLeft);
            }
            y++;
            RenderRowWithNodes(context, y, _footerNodes);
            y++;
        }

        // Bottom border
        if (y < Bounds.Height)
        {
            // Use TeeUp for column positions when we have data/loading/footer, just horizontal when empty with no footer
            bool showColumnTees = hasColumnStructure || _footerNodes is not null;
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
        var trackColor = theme.Get(ScrollTheme.TrackColor);
        var thumbColor = IsFocused 
            ? theme.Get(ScrollTheme.FocusedThumbColor) 
            : theme.Get(ScrollTheme.ThumbColor);
        
        var trackChar = theme.Get(ScrollTheme.VerticalTrackCharacter);
        var thumbChar = theme.Get(ScrollTheme.VerticalThumbCharacter);
        var upArrow = theme.Get(ScrollTheme.UpArrowCharacter);
        var downArrow = theme.Get(ScrollTheme.DownArrowCharacter);
        
        // Scrollbar is in the rightmost column, aligned with the content viewport
        var scrollbarX = Bounds.X + Bounds.Width - ScrollbarWidth;
        var scrollbarY = _contentViewport.Y;
        var scrollbarHeight = _contentViewport.Height;
        
        if (scrollbarHeight <= 2) return; // Too small
        
        // Calculate thumb position and size
        var thumbSize = Math.Max(1, (int)Math.Ceiling((double)_viewportRowCount / _contentRowCount * (scrollbarHeight - 2)));
        var scrollRange = scrollbarHeight - 2 - thumbSize;
        var thumbPosition = scrollRange > 0 && MaxScrollOffset > 0
            ? (int)Math.Round((double)_scrollOffset / MaxScrollOffset * scrollRange) 
            : 0;
        
        // Render scrollbar
        for (int row = 0; row < scrollbarHeight; row++)
        {
            context.SetCursorPosition(scrollbarX, scrollbarY + row);
            
            string charToRender;
            Hex1bColor color;
            
            if (row == 0)
            {
                charToRender = upArrow;
                color = thumbColor;
            }
            else if (row == scrollbarHeight - 1)
            {
                charToRender = downArrow;
                color = thumbColor;
            }
            else if (row - 1 >= thumbPosition && row - 1 < thumbPosition + thumbSize)
            {
                charToRender = thumbChar;
                color = thumbColor;
            }
            else
            {
                charToRender = trackChar;
                color = trackColor;
            }
            
            context.Write($"{color.ToForegroundAnsi()}{charToRender}\x1b[0m");
        }
    }

    private void RenderHorizontalBorder(Hex1bRenderContext context, int y, char left, char middle, char right)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(left);

        // Selection column border (if enabled)
        if (ShowSelectionColumn)
        {
            sb.Append(new string(Horizontal, SelectionColumnWidth));
            sb.Append(middle);
        }

        for (int col = 0; col < _columnCount; col++)
        {
            sb.Append(new string(Horizontal, _columnWidths[col]));
            if (col < _columnCount - 1)
            {
                sb.Append(middle);
            }
        }

        sb.Append(right);
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
                // Header checkbox - show based on selection state (use internal selection for runtime)
                var effectiveSelectedCount = _internalSelectedKeys.Count > 0 ? _internalSelectedKeys.Count : (SelectedKeys?.Count ?? 0);
                var allSelected = Data != null && Data.Count > 0 && effectiveSelectedCount == Data.Count;
                var someSelected = effectiveSelectedCount > 0 && !allSelected;
                var checkChar = allSelected ? theme.Get(TableTheme.CheckboxChecked) 
                    : someSelected ? theme.Get(TableTheme.CheckboxIndeterminate)
                    : theme.Get(TableTheme.CheckboxUnchecked);
                var checkColor = allSelected ? theme.Get(TableTheme.CheckboxCheckedForeground)
                    : theme.Get(TableTheme.CheckboxUncheckedForeground);
                sb.Append(checkColor.ToForegroundAnsi());
                sb.Append(PadRightByDisplayWidth(checkChar, selWidth));
                sb.Append(Hex1bColor.Default.ToForegroundAnsi());
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
                // Header checkbox - show based on selection state (use internal selection for runtime)
                var effectiveSelectedCount = _internalSelectedKeys.Count > 0 ? _internalSelectedKeys.Count : (SelectedKeys?.Count ?? 0);
                var allSelected = Data != null && Data.Count > 0 && effectiveSelectedCount == Data.Count;
                var someSelected = effectiveSelectedCount > 0 && !allSelected;
                var checkChar = allSelected ? theme.Get(TableTheme.CheckboxChecked) 
                    : someSelected ? theme.Get(TableTheme.CheckboxIndeterminate)
                    : theme.Get(TableTheme.CheckboxUnchecked);
                sb.Append(PadRightByDisplayWidth(checkChar, selWidth));
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

    private void RenderLoadingRow(Hex1bRenderContext context, int y)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(Vertical);

        // Selection column (if enabled) - show empty for loading rows (no data to select)
        if (ShowSelectionColumn)
        {
            sb.Append(new string(' ', SelectionColumnWidth));
            sb.Append(Vertical);
        }

        for (int col = 0; col < _columnCount; col++)
        {
            int width = _columnWidths[col];
            sb.Append(new string('░', width));
            if (col < _columnCount - 1)
            {
                sb.Append(Vertical);
            }
        }

        sb.Append(Vertical);
        context.WriteClipped(Bounds.X, Bounds.Y + y, sb.ToString());
    }

    private void RenderSolidHorizontalBorder(Hex1bRenderContext context, int y, char left, char right)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(left);

        // Selection column (if enabled)
        int selectionWidth = ShowSelectionColumn ? SelectionColumnWidth + 1 : 0; // +1 for separator
        
        // Total content width = sum of column widths + (columnCount - 1) separators + selection column
        int contentWidth = _columnWidths.Sum() + (_columnCount - 1) + selectionWidth;
        sb.Append(new string(Horizontal, contentWidth));

        sb.Append(right);
        context.WriteClipped(Bounds.X, Bounds.Y + y, sb.ToString());
    }

    private void RenderEmptyRow(Hex1bRenderContext context, int y, int totalWidth)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(Vertical);

        // Selection column (if enabled) - show unchecked
        if (ShowSelectionColumn)
        {
            var theme = context.Theme;
            var checkChar = theme.Get(TableTheme.CheckboxUnchecked);
            sb.Append(PadRightByDisplayWidth(checkChar, SelectionColumnWidth));
            sb.Append(Vertical);
        }

        // Content area without column separators
        int contentWidth = _columnWidths.Sum() + (_columnCount - 1);
        
        // Center the "No data" message
        const string emptyMessage = "No data";
        int padding = (contentWidth - emptyMessage.Length) / 2;
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

        sb.Append(Vertical);
        context.WriteClipped(Bounds.X, Bounds.Y + y, sb.ToString());
    }
}
