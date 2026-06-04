using Hex1b.Composition;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Render node for <see cref="ListWidget{T}"/>. Supports two render modes:
/// <list type="bullet">
///   <item>
///     <description>
///       <b>Default mode</b> — no <c>ItemTemplate</c> set: each row is rendered
///       as a single line of <c>item?.ToString()</c> with the themed selection
///       indicator and selected/hover background, identical to the original
///       <see cref="ListNode"/> visuals.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Template mode</b> — <c>ItemTemplate</c> set: each row is reconciled
///       into a child widget tree the template returns. The template owns all
///       visual chrome and styles itself from
///       <see cref="ListItemContext{T}"/>.
///     </description>
///   </item>
/// </list>
/// </summary>
/// <typeparam name="T">The item type of the list.</typeparam>
public class TypedListNode<T> : Hex1bNode, ILayoutProvider
{
    /// <summary>
    /// The source widget that was reconciled into this node.
    /// </summary>
    public ListWidget<T>? SourceWidget { get; set; }

    private IReadOnlyList<T> _items = [];
    /// <summary>
    /// The list items to display.
    /// </summary>
    public IReadOnlyList<T> Items
    {
        get => _items;
        set
        {
            if (!ItemsEqual(_items, value))
            {
                _items = value;
                MarkDirty();
            }
        }
    }

    private static bool ItemsEqual(IReadOnlyList<T> a, IReadOnlyList<T> b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a.Count != b.Count) return false;
        var comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < a.Count; i++)
        {
            if (!comparer.Equals(a[i], b[i])) return false;
        }
        return true;
    }

    private int _itemHeight = 1;
    /// <summary>
    /// The fixed row height in terminal rows. Defaults to 1. Always at least 1.
    /// </summary>
    public int ItemHeight
    {
        get => _itemHeight;
        set
        {
            var clamped = Math.Max(1, value);
            if (_itemHeight != clamped)
            {
                _itemHeight = clamped;
                MarkDirty();
            }
        }
    }

    private int _selectedIndex = 0;
    /// <summary>
    /// The currently selected index. Preserved across reconciliation.
    /// </summary>
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

    private int _scrollOffset = 0;
    /// <summary>
    /// The scroll offset (index of the first visible item). Preserved across reconciliation.
    /// </summary>
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
    /// The viewport height in terminal rows (set during Arrange).
    /// </summary>
    private int _viewportHeight = 0;
    public int ViewportHeight => _viewportHeight;

    /// <summary>
    /// The number of items that fit in the viewport at the current <see cref="ItemHeight"/>.
    /// </summary>
    public int VisibleItemCount => Math.Max(0, _viewportHeight / Math.Max(1, _itemHeight));

    /// <summary>
    /// The maximum scroll offset based on item count and visible item count.
    /// </summary>
    public int MaxScrollOffset => Math.Max(0, Items.Count - VisibleItemCount);

    /// <summary>
    /// Whether the list needs scrolling (more items than fit in the viewport).
    /// </summary>
    public bool IsScrollable => Items.Count > VisibleItemCount && VisibleItemCount > 0;

    /// <summary>
    /// The currently selected item, or <c>default</c> if the list is empty.
    /// </summary>
    public T? SelectedItem => SelectedIndex >= 0 && SelectedIndex < Items.Count
        ? Items[SelectedIndex]
        : default;

    /// <summary>
    /// The text of the currently selected item (via <see cref="object.ToString"/>),
    /// or <c>null</c> if the list is empty.
    /// </summary>
    public string? SelectedText
    {
        get
        {
            if (SelectedIndex < 0 || SelectedIndex >= Items.Count) return null;
            return Items[SelectedIndex]?.ToString() ?? string.Empty;
        }
    }

    private int _hoveredItemIndex = -1;
    /// <summary>
    /// The index of the row currently under the mouse cursor, or -1 if the list
    /// isn't hovered or the cursor sits past the last item. Updated by
    /// <see cref="OnHoverMove"/> and reset to -1 when <see cref="IsHovered"/>
    /// flips to false.
    /// </summary>
    public int HoveredItemIndex
    {
        get => _hoveredItemIndex;
        private set
        {
            if (_hoveredItemIndex != value)
            {
                _hoveredItemIndex = value;
                MarkDirty();
            }
        }
    }

    internal Func<ListItemContext<T>, Hex1bWidget>? ItemTemplate { get; set; }
    internal Func<T, object>? ItemKeySelector { get; set; }

    /// <summary>
    /// Reconciled per-row child nodes. Populated when <see cref="ItemTemplate"/>
    /// is set; otherwise empty.
    /// </summary>
    internal List<Hex1bNode> ItemNodes { get; set; } = new();

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
                if (!value)
                {
                    // Mouse left the list — clear the per-row hover too.
                    HoveredItemIndex = -1;
                }
                MarkDirty();
            }
        }
    }

    public override bool IsFocusable => true;

    public override void OnHoverMove(int mouseX, int mouseY)
    {
        if (Items.Count == 0 || _itemHeight <= 0)
        {
            HoveredItemIndex = -1;
            return;
        }

        var localY = mouseY - Bounds.Y;
        if (localY < 0 || localY >= _viewportHeight)
        {
            HoveredItemIndex = -1;
            return;
        }

        var idx = (localY / _itemHeight) + _scrollOffset;
        HoveredItemIndex = idx >= 0 && idx < Items.Count ? idx : -1;
    }

    #region ILayoutProvider

    public Rect ClipRect => Bounds;
    public ClipMode ClipMode => ClipMode.Clip;
    public ILayoutProvider? ParentLayoutProvider { get; set; }

    public bool ShouldRenderAt(int x, int y) => LayoutProviderHelper.ShouldRenderAt(this, x, y);

    public (int adjustedX, string clippedText) ClipString(int x, int y, string text)
        => LayoutProviderHelper.ClipString(this, x, y, text);

    #endregion

    /// <summary>
    /// Default binding configuration uses <see cref="ListWidget{T}"/>'s
    /// rebindable actions. The <see cref="ListNode"/> override redirects to the
    /// legacy <see cref="ListWidget"/> action ids so existing rebind code keeps
    /// working.
    /// </summary>
    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        ConfigureDefaultBindings(
            bindings,
            ListWidget<T>.MoveUp,
            ListWidget<T>.MoveDown,
            ListWidget<T>.Activate,
            ListWidget<T>.ScrollUp,
            ListWidget<T>.ScrollDown);
    }

    internal void ConfigureDefaultBindings(
        InputBindingsBuilder bindings,
        ActionId moveUp,
        ActionId moveDown,
        ActionId activate,
        ActionId scrollUp,
        ActionId scrollDown)
    {
        bindings.Key(Hex1bKey.UpArrow).Triggers(moveUp, MoveUpWithEvent, "Move up");
        bindings.Key(Hex1bKey.DownArrow).Triggers(moveDown, MoveDownWithEvent, "Move down");

        bindings.Key(Hex1bKey.Enter).Triggers(activate, ActivateItemWithEvent, "Activate item");
        bindings.Key(Hex1bKey.Spacebar).Triggers(activate, ActivateItemWithEvent, "Activate item");

        bindings.Mouse(MouseButton.Left).Triggers(activate, MouseSelectAndActivate, "Select and activate item");

        bindings.Mouse(MouseButton.ScrollUp).Triggers(scrollUp, MoveUpWithEvent, "Scroll up");
        bindings.Mouse(MouseButton.ScrollDown).Triggers(scrollDown, MoveDownWithEvent, "Scroll down");
    }

    private async Task MouseSelectAndActivate(InputBindingActionContext ctx)
    {
        var localY = ctx.MouseY - Bounds.Y;
        var itemIndex = (localY / Math.Max(1, _itemHeight)) + _scrollOffset;

        if (itemIndex >= 0 && itemIndex < Items.Count)
        {
            var previousIndex = SelectedIndex;
            SetSelection(itemIndex);

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

    internal void MoveUp()
    {
        if (Items.Count == 0) return;
        SelectedIndex = SelectedIndex <= 0 ? Items.Count - 1 : SelectedIndex - 1;
    }

    internal void MoveDown()
    {
        if (Items.Count == 0) return;
        SelectedIndex = (SelectedIndex + 1) % Items.Count;
    }

    internal void SetSelection(int index)
    {
        if (Items.Count == 0 || index < 0 || index >= Items.Count) return;
        SelectedIndex = index;
    }

    private void EnsureSelectionVisible()
    {
        var visible = VisibleItemCount;
        if (visible <= 0 || Items.Count == 0) return;

        if (SelectedIndex < _scrollOffset)
        {
            _scrollOffset = SelectedIndex;
        }
        else if (SelectedIndex >= _scrollOffset + visible)
        {
            _scrollOffset = SelectedIndex - visible + 1;
        }

        _scrollOffset = Math.Clamp(_scrollOffset, 0, MaxScrollOffset);
    }

    public override InputResult HandleMouseClick(int localX, int localY, Hex1bMouseEvent mouseEvent)
    {
        var itemIndex = (localY / Math.Max(1, _itemHeight)) + _scrollOffset;
        if (itemIndex >= 0 && itemIndex < Items.Count)
        {
            SetSelection(itemIndex);
            return InputResult.Handled;
        }
        return InputResult.NotHandled;
    }

    protected override Size MeasureCore(Constraints constraints)
    {
        // Width: longest item label + indicator (default path) OR template's natural width.
        // For simplicity (and parity with the previous behavior), use the string-projected
        // width even when a template is present — this is just the "preferred" size; the
        // outer layout typically constrains via FillHeight/FillWidth or a fixed cell.
        var maxWidth = 0;
        for (int i = 0; i < Items.Count; i++)
        {
            var len = (Items[i]?.ToString()?.Length ?? 0) + 2; // "> " indicator
            if (len > maxWidth) maxWidth = len;
        }

        var height = Math.Max(Items.Count * Math.Max(1, _itemHeight), 1);
        var constrainedSize = constraints.Constrain(new Size(maxWidth, height));
        _viewportHeight = constrainedSize.Height;
        return constrainedSize;
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.ArrangeCore(bounds);

        _viewportHeight = bounds.Height;
        _scrollOffset = Math.Clamp(_scrollOffset, 0, MaxScrollOffset);
        EnsureSelectionVisible();

        // Arrange visible item nodes into their row sub-rects.
        if (ItemNodes.Count > 0)
        {
            var visible = VisibleItemCount;
            var rowHeight = Math.Max(1, _itemHeight);
            for (int i = 0; i < ItemNodes.Count; i++)
            {
                if (i < _scrollOffset || i >= _scrollOffset + visible)
                {
                    // Park offscreen children at a degenerate rect so they don't
                    // intercept hit tests but still get their bounds updated.
                    ItemNodes[i].Arrange(new Rect(bounds.X, bounds.Y, 0, 0));
                    continue;
                }

                var rowY = bounds.Y + (i - _scrollOffset) * rowHeight;
                var rowRect = new Rect(bounds.X, rowY, bounds.Width, rowHeight);
                // Measure to give the template a chance to size itself within the row.
                ItemNodes[i].Measure(new Constraints(0, bounds.Width, 0, rowHeight));
                ItemNodes[i].Arrange(rowRect);
            }
        }
    }

    public override IEnumerable<Hex1bNode> GetChildren() => ItemNodes;

    /// <summary>
    /// Item child nodes are render-only in v1 — the list itself is the only
    /// focusable surface. Interactive widgets inside an item template will not
    /// receive focus.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (IsFocusable) yield return this;
    }

    public override void Render(Hex1bRenderContext context)
    {
        var previousLayout = context.CurrentLayoutProvider;
        ParentLayoutProvider = previousLayout;
        context.CurrentLayoutProvider = this;

        if (ItemTemplate != null && ItemNodes.Count > 0)
        {
            RenderTemplated(context);
        }
        else
        {
            ListRenderCore.RenderDefault(this, context);
        }

        context.CurrentLayoutProvider = previousLayout;
        ParentLayoutProvider = null;
    }

    private void RenderTemplated(Hex1bRenderContext context)
    {
        var visibleStart = _scrollOffset;
        var visibleEnd = Math.Min(_scrollOffset + VisibleItemCount, ItemNodes.Count);

        for (int i = visibleStart; i < visibleEnd; i++)
        {
            context.RenderChild(ItemNodes[i]);
        }
    }
}

/// <summary>
/// Shared default (no-template) renderer for both <see cref="ListNode"/> and
/// templateless <see cref="TypedListNode{T}"/>. Keeps the visual contract of the
/// original list — themed selection indicator, selected background, hover
/// background — in one place.
/// </summary>
internal static class ListRenderCore
{
    public static void RenderDefault<T>(TypedListNode<T> node, Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var selectedIndicator = theme.Get(ListTheme.SelectedIndicator);
        var unselectedIndicator = theme.Get(ListTheme.UnselectedIndicator);
        var selectedFg = theme.Get(ListTheme.SelectedForegroundColor);
        var selectedBg = theme.Get(ListTheme.SelectedBackgroundColor);
        var hoveredFg = theme.Get(ListTheme.HoveredForegroundColor);
        var hoveredBg = theme.Get(ListTheme.HoveredBackgroundColor);

        var globalColors = theme.GetGlobalColorCodes();
        var resetToGlobal = theme.GetResetToGlobalCodes();

        var hoveredItemIndex = node.IsHovered ? node.HoveredItemIndex : -1;

        var visibleStart = node.ScrollOffset;
        var visibleEnd = Math.Min(node.ScrollOffset + node.VisibleItemCount, node.Items.Count);

        for (int i = visibleStart; i < visibleEnd; i++)
        {
            var item = node.Items[i]?.ToString() ?? string.Empty;
            var isSelected = i == node.SelectedIndex;
            var isHoveredItem = i == hoveredItemIndex;

            var x = node.Bounds.X;
            var y = node.Bounds.Y + (i - node.ScrollOffset);

            string text;
            if (isSelected && node.IsFocused)
            {
                text = $"{selectedFg.ToForegroundAnsi()}{selectedBg.ToBackgroundAnsi()}{selectedIndicator}{item}{resetToGlobal}";
            }
            else if (isHoveredItem && !isSelected)
            {
                text = $"{hoveredFg.ToForegroundAnsi()}{hoveredBg.ToBackgroundAnsi()}{unselectedIndicator}{item}{resetToGlobal}";
            }
            else if (isSelected)
            {
                text = $"{globalColors}{selectedIndicator}{item}{resetToGlobal}";
            }
            else
            {
                text = $"{globalColors}{unselectedIndicator}{item}{resetToGlobal}";
            }

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
