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
        // Scroll navigation
        bindings.Key(Hex1bKey.UpArrow).Action(ScrollUp, "Scroll up");
        bindings.Key(Hex1bKey.DownArrow).Action(ScrollDown, "Scroll down");
        bindings.Key(Hex1bKey.PageUp).Action(PageUp, "Page up");
        bindings.Key(Hex1bKey.PageDown).Action(PageDown, "Page down");
        bindings.Key(Hex1bKey.Home).Action(ScrollToStart, "Scroll to start");
        bindings.Key(Hex1bKey.End).Action(ScrollToEnd, "Scroll to end");
        
        // Mouse wheel scrolling
        bindings.Mouse(MouseButton.ScrollUp).Action(_ => ScrollByAmount(-3), "Scroll up");
        bindings.Mouse(MouseButton.ScrollDown).Action(_ => ScrollByAmount(3), "Scroll down");
        
        // Scrollbar drag
        bindings.Drag(MouseButton.Left).Action(HandleScrollbarDrag, "Drag scrollbar");
    }

    private void ScrollUp(InputBindingActionContext ctx)
    {
        ScrollByAmount(-1);
    }

    private void ScrollDown(InputBindingActionContext ctx)
    {
        ScrollByAmount(1);
    }

    private void PageUp(InputBindingActionContext ctx)
    {
        ScrollByAmount(-Math.Max(1, _viewportRowCount - 1));
    }

    private void PageDown(InputBindingActionContext ctx)
    {
        ScrollByAmount(Math.Max(1, _viewportRowCount - 1));
    }

    private void ScrollToStart(InputBindingActionContext ctx)
    {
        SetScrollOffset(0);
    }

    private void ScrollToEnd(InputBindingActionContext ctx)
    {
        SetScrollOffset(MaxScrollOffset);
    }

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
    /// Checks if a row key is selected.
    /// </summary>
    private bool IsRowSelected(object key)
    {
        return SelectedKeys?.Contains(key) == true;
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
    /// Calculates column widths based on content (equal distribution for Phase 1).
    /// </summary>
    private void CalculateColumnWidths(int availableWidth, bool reserveScrollbar)
    {
        if (_columnCount == 0) return;

        // Account for borders: | col1 | col2 | col3 | = columnCount + 1 vertical bars
        int borderWidth = _columnCount + 1;
        
        // Reserve space for scrollbar if needed
        int scrollbarSpace = reserveScrollbar ? ScrollbarWidth : 0;
        int contentWidth = availableWidth - borderWidth - scrollbarSpace;

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
        int availableHeight = constraints.MaxHeight > 0 ? constraints.MaxHeight : 24;
        _viewportRowCount = Math.Max(1, availableHeight - fixedHeight);
        
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

        // Clamp scroll offset
        if (_scrollOffset > MaxScrollOffset)
        {
            _scrollOffset = MaxScrollOffset;
        }

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
            RenderRowWithNodes(context, y, _headerNodes);
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
                    RenderRowDirect(context, y, loadingCells, isHeader: false);
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
                bool isHighlighted = state.IsFocused || state.IsSelected;
                RenderRowWithNodes(context, y, _rowNodes[i], isHighlighted: isHighlighted);
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
    private void RenderRowWithNodes(Hex1bRenderContext context, int y, List<TextBlockNode> nodes, bool isHighlighted = false)
    {
        // Render borders
        var sb = new System.Text.StringBuilder();
        
        // Apply reverse video for highlighted rows
        if (isHighlighted)
        {
            sb.Append("\x1b[7m"); // Reverse video on
        }
        
        sb.Append(Vertical);
        
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
        
        // Now render each cell node on top
        foreach (var node in nodes)
        {
            context.RenderChild(node);
        }
    }

    /// <summary>
    /// Renders a row directly from cells (used for loading state).
    /// </summary>
    private void RenderRowDirect(Hex1bRenderContext context, int y, IReadOnlyList<TableCell> cells, 
        bool isHeader, bool isSelected = false, bool isFooter = false)
    {
        var sb = new System.Text.StringBuilder();
        
        // Apply reverse video for selected rows
        if (isSelected)
        {
            sb.Append("\x1b[7m"); // Reverse video on
        }
        
        sb.Append(Vertical);

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

        // Total content width = sum of column widths + (columnCount - 1) separators
        int contentWidth = _columnWidths.Sum() + (_columnCount - 1);
        sb.Append(new string(Horizontal, contentWidth));

        sb.Append(right);
        context.WriteClipped(Bounds.X, Bounds.Y + y, sb.ToString());
    }

    private void RenderEmptyRow(Hex1bRenderContext context, int y, int totalWidth)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(Vertical);

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
