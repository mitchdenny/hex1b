using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

public sealed class ListNode : Hex1bNode
{
    /// <summary>
    /// The source widget that was reconciled into this node.
    /// </summary>
    public ListWidget? SourceWidget { get; set; }

    private IReadOnlyList<string> _items = [];
    /// <summary>
    /// The list items to display.
    /// </summary>
    public IReadOnlyList<string> Items 
    { 
        get => _items; 
        set 
        {
            if (!ReferenceEquals(_items, value))
            {
                _items = value;
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// The currently selected index. This is preserved across reconciliation.
    /// </summary>
    private int _selectedIndex = 0;
    public int SelectedIndex 
    { 
        get => _selectedIndex; 
        set 
        {
            if (_selectedIndex != value)
            {
                _selectedIndex = value;
                EnsureSelectionVisible();
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// The scroll offset (index of the first visible item).
    /// This is preserved across reconciliation.
    /// </summary>
    private int _scrollOffset = 0;
    public int ScrollOffset 
    { 
        get => _scrollOffset; 
        set 
        {
            var clamped = Math.Clamp(value, 0, MaxScrollOffset);
            if (_scrollOffset != clamped)
            {
                _scrollOffset = clamped;
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// The number of visible rows in the viewport (set during Arrange).
    /// </summary>
    private int _viewportHeight = 0;
    public int ViewportHeight => _viewportHeight;

    /// <summary>
    /// The maximum scroll offset based on item count and viewport height.
    /// </summary>
    public int MaxScrollOffset => Math.Max(0, Items.Count - _viewportHeight);

    /// <summary>
    /// Whether the list needs scrolling (more items than viewport height).
    /// </summary>
    public bool IsScrollable => Items.Count > _viewportHeight && _viewportHeight > 0;

    /// <summary>
    /// The text of the currently selected item, or null if the list is empty.
    /// </summary>
    public string? SelectedText => SelectedIndex >= 0 && SelectedIndex < Items.Count 
        ? Items[SelectedIndex] 
        : null;

    /// <summary>
    /// Internal action invoked when selection changes.
    /// </summary>
    internal Func<InputBindingActionContext, Task>? SelectionChangedAction { get; set; }

    /// <summary>
    /// Internal action invoked when an item is activated.
    /// </summary>
    internal Func<InputBindingActionContext, Task>? ItemActivatedAction { get; set; }
    
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

    private bool _isHovered;
    public override bool IsHovered 
    { 
        get => _isHovered; 
        set 
        {
            if (_isHovered != value)
            {
                _isHovered = value;
                MarkDirty();
            }
        }
    }

    public override bool IsFocusable => true;

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Navigation with selection changed events
        bindings.Key(Hex1bKey.UpArrow).Action(MoveUpWithEvent, "Move up");
        bindings.Key(Hex1bKey.DownArrow).Action(MoveDownWithEvent, "Move down");
        
        // Activation - always bind Enter/Space so focused lists consume these keys
        bindings.Key(Hex1bKey.Enter).Action(ActivateItemWithEvent, "Activate item");
        bindings.Key(Hex1bKey.Spacebar).Action(ActivateItemWithEvent, "Activate item");
        
        // Mouse click to select and activate
        bindings.Mouse(MouseButton.Left).Action(MouseSelectAndActivate, "Select and activate item");
        
        // Mouse wheel scrolling - navigates selection like arrow keys (ignores cursor position)
        bindings.Mouse(MouseButton.ScrollUp).Action(MoveUpWithEvent, "Scroll up");
        bindings.Mouse(MouseButton.ScrollDown).Action(MoveDownWithEvent, "Scroll down");
    }

    private async Task MouseSelectAndActivate(InputBindingActionContext ctx)
    {
        // The mouse position is available in the context, calculate local Y to determine which item was clicked
        var localY = ctx.MouseY - Bounds.Y;
        
        // Convert local Y to item index (accounting for scroll offset)
        var itemIndex = localY + _scrollOffset;
        
        if (itemIndex >= 0 && itemIndex < Items.Count)
        {
            var previousIndex = SelectedIndex;
            SetSelection(itemIndex);
            
            // Fire selection changed if the selection actually changed
            if (previousIndex != SelectedIndex && SelectionChangedAction != null)
            {
                await SelectionChangedAction(ctx);
            }
            
            if (ItemActivatedAction != null)
            {
                await ItemActivatedAction(ctx);
            }
        }
    }

    private async Task ActivateItemWithEvent(InputBindingActionContext ctx)
    {
        if (ItemActivatedAction != null)
        {
            await ItemActivatedAction(ctx);
        }
    }

    private async Task MoveUpWithEvent(InputBindingActionContext ctx)
    {
        MoveUp();
        if (SelectionChangedAction != null)
        {
            await SelectionChangedAction(ctx);
        }
    }

    private async Task MoveDownWithEvent(InputBindingActionContext ctx)
    {
        MoveDown();
        if (SelectionChangedAction != null)
        {
            await SelectionChangedAction(ctx);
        }
    }

    /// <summary>
    /// Moves selection up (with wrap-around).
    /// </summary>
    internal void MoveUp()
    {
        if (Items.Count == 0) return;
        SelectedIndex = SelectedIndex <= 0 ? Items.Count - 1 : SelectedIndex - 1;
    }

    /// <summary>
    /// Moves selection down (with wrap-around).
    /// </summary>
    internal void MoveDown()
    {
        if (Items.Count == 0) return;
        SelectedIndex = (SelectedIndex + 1) % Items.Count;
    }

    /// <summary>
    /// Sets the selection to a specific index.
    /// </summary>
    internal void SetSelection(int index)
    {
        if (Items.Count == 0 || index < 0 || index >= Items.Count) return;
        SelectedIndex = index;
    }

    /// <summary>
    /// Ensures the currently selected item is visible in the viewport.
    /// Scrolls the viewport if necessary.
    /// </summary>
    private void EnsureSelectionVisible()
    {
        if (_viewportHeight <= 0 || Items.Count == 0) return;

        // If selection is above the viewport, scroll up
        if (SelectedIndex < _scrollOffset)
        {
            _scrollOffset = SelectedIndex;
        }
        // If selection is below the viewport, scroll down
        else if (SelectedIndex >= _scrollOffset + _viewportHeight)
        {
            _scrollOffset = SelectedIndex - _viewportHeight + 1;
        }
        
        // Clamp scroll offset to valid range
        _scrollOffset = Math.Clamp(_scrollOffset, 0, MaxScrollOffset);
    }

    /// <summary>
    /// Handles mouse click by selecting the item at the clicked row.
    /// </summary>
    public override InputResult HandleMouseClick(int localX, int localY, Hex1bMouseEvent mouseEvent)
    {
        // Convert local Y to item index (accounting for scroll offset)
        var itemIndex = localY + _scrollOffset;
        if (itemIndex >= 0 && itemIndex < Items.Count)
        {
            SetSelection(itemIndex);
            return InputResult.Handled;
        }
        return InputResult.NotHandled;
    }

    public override Size Measure(Constraints constraints)
    {
        // List: width is max item length + indicator (2 chars), height is item count
        var maxWidth = 0;
        foreach (var item in Items)
        {
            maxWidth = Math.Max(maxWidth, item.Length + 2); // "> " indicator
        }
        var height = Math.Max(Items.Count, 1);
        var constrainedSize = constraints.Constrain(new Size(maxWidth, height));
        
        // Store the viewport height from constraints (may be less than item count)
        _viewportHeight = constrainedSize.Height;
        
        return constrainedSize;
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);
        
        // Update viewport height based on actual arranged bounds
        _viewportHeight = bounds.Height;
        
        // Clamp scroll offset to valid range after viewport size change
        _scrollOffset = Math.Clamp(_scrollOffset, 0, MaxScrollOffset);
        
        // Ensure selection is still visible after arrange
        EnsureSelectionVisible();
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var selectedIndicator = theme.Get(ListTheme.SelectedIndicator);
        var unselectedIndicator = theme.Get(ListTheme.UnselectedIndicator);
        var selectedFg = theme.Get(ListTheme.SelectedForegroundColor);
        var selectedBg = theme.Get(ListTheme.SelectedBackgroundColor);
        
        // Get global colors for non-selected items
        var globalColors = theme.GetGlobalColorCodes();
        var resetToGlobal = theme.GetResetToGlobalCodes();
        
        // Calculate which items are visible in the viewport
        var visibleStart = _scrollOffset;
        var visibleEnd = Math.Min(_scrollOffset + _viewportHeight, Items.Count);
        
        for (int i = visibleStart; i < visibleEnd; i++)
        {
            var item = Items[i];
            var isSelected = i == SelectedIndex;

            var x = Bounds.X;
            var y = Bounds.Y + (i - _scrollOffset);  // Adjust y position for scroll offset
            
            string text;
            if (isSelected && IsFocused)
            {
                // Focused and selected: use theme colors
                text = $"{selectedFg.ToForegroundAnsi()}{selectedBg.ToBackgroundAnsi()}{selectedIndicator}{item}{resetToGlobal}";
            }
            else if (isSelected)
            {
                // Selected but not focused: just show indicator with global colors
                text = $"{globalColors}{selectedIndicator}{item}{resetToGlobal}";
            }
            else
            {
                // Not selected: use global colors
                text = $"{globalColors}{unselectedIndicator}{item}{resetToGlobal}";
            }

            // Use clipped rendering when a layout provider is active
            if (context.CurrentLayoutProvider != null)
            {
                context.WriteClipped(x, y, text);
            }
            else
            {
                context.SetCursorPosition(x, y);
                context.Write(text);
            }
        }
    }
}
