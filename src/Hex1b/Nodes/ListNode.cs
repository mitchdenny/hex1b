using System.Collections.Specialized;
using Hex1b.Composition;
using Hex1b.Data;
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
public class ListNode<T> : Hex1bNode, ILayoutProvider
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
    public int MaxScrollOffset => Math.Max(0, EffectiveItemCount - VisibleItemCount);

    /// <summary>
    /// Whether the list needs scrolling (more items than fit in the viewport).
    /// </summary>
    public bool IsScrollable => EffectiveItemCount > VisibleItemCount && VisibleItemCount > 0;

    /// <summary>
    /// The currently selected item, or <c>default</c> if the list is empty or
    /// the selected row hasn't been loaded yet (virtualized mode).
    /// </summary>
    public T? SelectedItem
    {
        get
        {
            if (SelectedIndex < 0 || SelectedIndex >= EffectiveItemCount) return default;
            return TryGetEffectiveItem(SelectedIndex, out var item) ? item : default;
        }
    }

    /// <summary>
    /// The text of the currently selected item (via <see cref="object.ToString"/>),
    /// or <c>null</c> if the list is empty / not yet loaded.
    /// </summary>
    public string? SelectedText
    {
        get
        {
            if (SelectedIndex < 0 || SelectedIndex >= EffectiveItemCount) return null;
            return TryGetEffectiveItem(SelectedIndex, out var item)
                ? item?.ToString() ?? string.Empty
                : null;
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
    /// Reconciled per-row child nodes. In non-virtualized mode this holds one
    /// node per item (<c>ItemNodes[i]</c> corresponds to absolute index <c>i</c>).
    /// In virtualized mode this holds only the window
    /// <c>[ItemNodesWindowStart, ItemNodesWindowStart + ItemNodes.Count)</c>;
    /// callers must map absolute indices through
    /// <see cref="TryGetItemNode(int, out Hex1bNode)"/>.
    /// </summary>
    internal List<Hex1bNode> ItemNodes { get; set; } = new();

    /// <summary>
    /// Absolute item index that <c>ItemNodes[0]</c> corresponds to. Always 0 in
    /// non-virtualized mode.
    /// </summary>
    internal int ItemNodesWindowStart { get; set; } = 0;

    /// <summary>
    /// Attempts to resolve the templated child node for an absolute item index.
    /// Returns <c>false</c> for indices outside the currently materialized
    /// window (only possible under virtualization).
    /// </summary>
    internal bool TryGetItemNode(int absoluteIndex, out Hex1bNode node)
    {
        var offset = absoluteIndex - ItemNodesWindowStart;
        if (offset >= 0 && offset < ItemNodes.Count)
        {
            node = ItemNodes[offset];
            return true;
        }
        node = null!;
        return false;
    }

    #region Virtualized data source

    /// <summary>Buffer of rows above/below the visible viewport to pre-load.</summary>
    internal const int VirtualizationBuffer = 5;

    private IListDataSource<T>? _dataSource;
    private INotifyCollectionChanged? _subscribedDataSource;
    private IReadOnlyList<T>? _cachedItems;
    private int? _cachedItemCount;
    private (int Start, int End)? _cachedRange;
    private CancellationTokenSource? _loadCts;

    /// <summary>
    /// Optional virtualized data source. When set, <see cref="Items"/> is
    /// ignored and the node materialises only a window of items around the
    /// visible viewport on each frame. Subscribes to
    /// <see cref="INotifyCollectionChanged"/> if the source supports it.
    /// </summary>
    public IListDataSource<T>? DataSource
    {
        get => _dataSource;
        set
        {
            if (ReferenceEquals(_dataSource, value)) return;
            UnsubscribeFromDataSource();
            _dataSource = value;
            _cachedItems = null;
            _cachedItemCount = null;
            _cachedRange = null;
            if (_dataSource is not null) SubscribeToDataSource();
            MarkDirty();
        }
    }

    /// <summary>Invalidate callback wired from <c>ReconcileContext</c>.</summary>
    internal Action? InvalidateCallback { get; set; }

    /// <summary>Whether the node is operating in virtualized mode.</summary>
    public bool IsVirtualized => _dataSource is not null;

    /// <summary>
    /// The total number of items, either from the in-memory <see cref="Items"/>
    /// list or the cached count from <see cref="DataSource"/>. Returns 0 until
    /// the first async count completes.
    /// </summary>
    public int EffectiveItemCount => IsVirtualized ? (_cachedItemCount ?? 0) : Items.Count;

    /// <summary>
    /// Attempts to resolve an item by absolute index. Returns <c>false</c> for
    /// indices not currently cached (virtualized mode only). In non-virtualized
    /// mode this always succeeds when <paramref name="absoluteIndex"/> is in
    /// range.
    /// </summary>
    public bool TryGetEffectiveItem(int absoluteIndex, out T item)
    {
        if (IsVirtualized)
        {
            if (_cachedItems is null || !_cachedRange.HasValue)
            {
                item = default!;
                return false;
            }
            var (start, end) = _cachedRange.Value;
            var offset = absoluteIndex - start;
            if (offset < 0 || offset >= _cachedItems.Count || absoluteIndex >= end)
            {
                item = default!;
                return false;
            }
            item = _cachedItems[offset];
            return true;
        }

        if (absoluteIndex < 0 || absoluteIndex >= Items.Count)
        {
            item = default!;
            return false;
        }
        item = Items[absoluteIndex];
        return true;
    }

    /// <summary>
    /// Computes the absolute range of rows to materialize for the current
    /// scroll position: visible window plus <see cref="VirtualizationBuffer"/>
    /// rows above and below. Used by both data-source pre-fetch and templated
    /// child reconciliation.
    /// </summary>
    public (int Start, int End) GetVisibleWindow(int totalCount)
    {
        if (totalCount == 0) return (0, 0);
        var visible = Math.Max(1, VisibleItemCount);
        var start = Math.Max(0, _scrollOffset - VirtualizationBuffer);
        var end = Math.Min(totalCount, _scrollOffset + visible + VirtualizationBuffer);
        return (start, end);
    }

    /// <summary>
    /// Loads <paramref name="count"/> items starting at
    /// <paramref name="startIndex"/> from the data source, cancelling any
    /// in-flight load. No-op when no <see cref="DataSource"/> is set.
    /// </summary>
    public async ValueTask LoadDataAsync(int startIndex, int count, CancellationToken cancellationToken = default)
    {
        if (_dataSource is null) return;

        if (!_cachedItemCount.HasValue)
        {
            _cachedItemCount = await _dataSource.GetItemCountAsync(cancellationToken).ConfigureAwait(false);
        }

        // Already covered by the existing cache.
        if (_cachedRange is { } range && _cachedItems is not null &&
            range.Start <= startIndex && range.End >= startIndex + count)
        {
            return;
        }

        if (_loadCts is not null)
        {
            await _loadCts.CancelAsync().ConfigureAwait(false);
            _loadCts.Dispose();
        }
        _loadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _loadCts.Token;

        try
        {
            var fetched = await _dataSource.GetItemsAsync(startIndex, count, token).ConfigureAwait(false);
            if (token.IsCancellationRequested) return;

            _cachedItems = fetched;
            _cachedRange = (startIndex, startIndex + fetched.Count);
            MarkDirty();
            InvalidateCallback?.Invoke();
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer load; nothing to do.
        }
    }

    /// <summary>
    /// Ensures the currently selected item is loaded before a selection event
    /// fires. Called from selection/activation handlers so user code reading
    /// <see cref="SelectedItem"/> never sees <c>default(T)</c> for an un-loaded
    /// row. No-op in non-virtualized mode.
    /// </summary>
    internal async ValueTask EnsureSelectedItemLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (!IsVirtualized) return;
        if (TryGetEffectiveItem(_selectedIndex, out _)) return;

        // Load a small window around the selection so subsequent moves are quick.
        var window = Math.Max(1, VisibleItemCount + VirtualizationBuffer * 2);
        var start = Math.Max(0, _selectedIndex - VirtualizationBuffer);
        await LoadDataAsync(start, window, cancellationToken).ConfigureAwait(false);
    }

    private void SubscribeToDataSource()
    {
        _subscribedDataSource = _dataSource;
        if (_subscribedDataSource is not null)
        {
            _subscribedDataSource.CollectionChanged += OnDataSourceCollectionChanged;
        }
    }

    private void UnsubscribeFromDataSource()
    {
        if (_subscribedDataSource is not null)
        {
            _subscribedDataSource.CollectionChanged -= OnDataSourceCollectionChanged;
            _subscribedDataSource = null;
        }
    }

    private void OnDataSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _cachedItems = null;
        _cachedItemCount = null;
        _cachedRange = null;
        MarkDirty();
        InvalidateCallback?.Invoke();
    }

    #endregion

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
        var count = EffectiveItemCount;
        if (count == 0 || _itemHeight <= 0)
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
        HoveredItemIndex = idx >= 0 && idx < count ? idx : -1;
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

        if (itemIndex >= 0 && itemIndex < EffectiveItemCount)
        {
            var previousIndex = SelectedIndex;
            SetSelection(itemIndex);

            await EnsureSelectedItemLoadedAsync(ctx.CancellationToken).ConfigureAwait(false);

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
            await EnsureSelectedItemLoadedAsync(ctx.CancellationToken).ConfigureAwait(false);
            await ItemActivatedAction(ctx);
        }
    }

    private async Task MoveUpWithEvent(InputBindingActionContext ctx)
    {
        MoveUp();
        if (SelectionChangedAction != null)
        {
            await EnsureSelectedItemLoadedAsync(ctx.CancellationToken).ConfigureAwait(false);
            await SelectionChangedAction(ctx);
        }
    }

    private async Task MoveDownWithEvent(InputBindingActionContext ctx)
    {
        MoveDown();
        if (SelectionChangedAction != null)
        {
            await EnsureSelectedItemLoadedAsync(ctx.CancellationToken).ConfigureAwait(false);
            await SelectionChangedAction(ctx);
        }
    }

    internal void MoveUp()
    {
        var count = EffectiveItemCount;
        if (count == 0) return;
        SelectedIndex = SelectedIndex <= 0 ? count - 1 : SelectedIndex - 1;
    }

    internal void MoveDown()
    {
        var count = EffectiveItemCount;
        if (count == 0) return;
        SelectedIndex = (SelectedIndex + 1) % count;
    }

    internal void SetSelection(int index)
    {
        var count = EffectiveItemCount;
        if (count == 0 || index < 0 || index >= count) return;
        SelectedIndex = index;
    }

    private void EnsureSelectionVisible()
    {
        var visible = VisibleItemCount;
        var count = EffectiveItemCount;
        if (visible <= 0 || count == 0) return;

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
        if (itemIndex >= 0 && itemIndex < EffectiveItemCount)
        {
            SetSelection(itemIndex);
            return InputResult.Handled;
        }
        return InputResult.NotHandled;
    }

    protected override Size MeasureCore(Constraints constraints)
    {
        int maxWidth;
        var count = EffectiveItemCount;

        if (IsVirtualized)
        {
            // Cannot walk the whole virtual collection. Measure from cached
            // items only — the visible window is always materialised so this
            // produces a stable width for what's currently on screen.
            maxWidth = 0;
            if (_cachedItems is not null && _cachedRange is { } range)
            {
                for (int i = 0; i < _cachedItems.Count; i++)
                {
                    var len = (_cachedItems[i]?.ToString()?.Length ?? 0) + 2;
                    if (len > maxWidth) maxWidth = len;
                }
            }
            if (maxWidth == 0) maxWidth = constraints.MinWidth;
        }
        else
        {
            // Width: longest item label + indicator (default path) OR template's natural width.
            maxWidth = 0;
            for (int i = 0; i < Items.Count; i++)
            {
                var len = (Items[i]?.ToString()?.Length ?? 0) + 2; // "> " indicator
                if (len > maxWidth) maxWidth = len;
            }
        }

        var height = Math.Max(count * Math.Max(1, _itemHeight), 1);
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
                var absoluteIndex = ItemNodesWindowStart + i;
                if (absoluteIndex < _scrollOffset || absoluteIndex >= _scrollOffset + visible)
                {
                    // Park offscreen children at a degenerate rect so they don't
                    // intercept hit tests but still get their bounds updated.
                    ItemNodes[i].Arrange(new Rect(bounds.X, bounds.Y, 0, 0));
                    continue;
                }

                var rowY = bounds.Y + (absoluteIndex - _scrollOffset) * rowHeight;
                var rowRect = new Rect(bounds.X, rowY, bounds.Width, rowHeight);
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
        var visibleEnd = Math.Min(_scrollOffset + VisibleItemCount, EffectiveItemCount);

        for (int i = visibleStart; i < visibleEnd; i++)
        {
            if (TryGetItemNode(i, out var child))
            {
                context.RenderChild(child);
            }
        }
    }
}

/// <summary>
/// Shared default (no-template) renderer for both <see cref="ListNode"/> and
/// templateless <see cref="ListNode{T}"/>. Keeps the visual contract of the
/// original list — themed selection indicator, selected background, hover
/// background — in one place.
/// </summary>
internal static class ListRenderCore
{
    private const string LoadingPlaceholder = "…";

    public static void RenderDefault<T>(ListNode<T> node, Hex1bRenderContext context)
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
        var visibleEnd = Math.Min(node.ScrollOffset + node.VisibleItemCount, node.EffectiveItemCount);

        for (int i = visibleStart; i < visibleEnd; i++)
        {
            var item = node.TryGetEffectiveItem(i, out var value)
                ? value?.ToString() ?? string.Empty
                : LoadingPlaceholder;
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

/// <summary>
/// String-specialized list node. Inherits all selection/scrolling/template
/// behavior from <see cref="ListNode{T}"/> with <c>T = string</c>; exists
/// as a distinct concrete type so legacy <see cref="ListWidget"/> handlers and
/// tests that reference <c>ListNode</c> continue to compile and run unchanged.
/// </summary>
public sealed class ListNode : ListNode<string>
{
    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Redirect default bindings to the legacy ListWidget action ids so existing
        // rebind code (b.Remove(ListWidget.MoveUp), etc.) keeps working.
        ConfigureDefaultBindings(
            bindings,
            ListWidget.MoveUp,
            ListWidget.MoveDown,
            ListWidget.Activate,
            ListWidget.ScrollUp,
            ListWidget.ScrollDown);
    }
}
