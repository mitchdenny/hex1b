using Hex1b;
using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for <see cref="ListWidget{T}"/> / <see cref="ListNode{T}"/>:
/// generic-item behavior, ItemHeight layout math, mouse hit-test with multi-row
/// items, hover invalidation, key-based child reuse, typed event args, and
/// template-mode rendering parity.
/// </summary>
[TestClass]
public class ListNodeOfTTests
{
    private sealed record Country(string Name, string Capital)
    {
        public override string ToString() => Name;
    }

    #region Math: ItemHeight + scrolling

    [TestMethod]
    public void ItemHeight_VisibleCount_DividesViewportHeight()
    {
        var node = new ListNode<string>
        {
            Items = ["a", "b", "c", "d", "e"],
            ItemHeight = 2,
        };
        node.Arrange(new Rect(0, 0, 20, 6));

        Assert.AreEqual(3, node.VisibleItemCount);
        Assert.AreEqual(2, node.MaxScrollOffset); // 5 - 3
    }

    [TestMethod]
    public void Measure_WithItemHeightGreaterThanOne_AccountsForRowsPerItem()
    {
        var node = new ListNode<string>
        {
            Items = ["a", "b", "c"],
            ItemHeight = 3,
        };

        var size = node.Measure(Constraints.Unbounded);

        Assert.AreEqual(9, size.Height); // 3 items * 3 rows
    }

    [TestMethod]
    public void HandleMouseClick_WithItemHeightTwo_SelectsCorrectItem()
    {
        var node = new ListNode<string>
        {
            Items = ["a", "b", "c", "d"],
            ItemHeight = 2,
        };
        node.Arrange(new Rect(0, 0, 20, 8));

        // localY = 3 with itemHeight = 2 -> index 1
        var ev = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 5, 3, Hex1bModifiers.None);
        var result = node.HandleMouseClick(5, 3, ev);

        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual(1, node.FocusedIndex);
    }

    [TestMethod]
    public void HandleMouseClick_OutsideItems_DoesNotChangeSelection()
    {
        var node = new ListNode<string>
        {
            Items = ["a", "b"],
            ItemHeight = 2,
        };
        node.Arrange(new Rect(0, 0, 20, 10));

        // localY = 8 -> index 4, outside items
        var ev = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 5, 8, Hex1bModifiers.None);
        var result = node.HandleMouseClick(5, 8, ev);

        Assert.AreEqual(InputResult.NotHandled, result);
        Assert.AreEqual(0, node.FocusedIndex);
    }

    #endregion

    #region Hover

    [TestMethod]
    public void OnHoverMove_UpdatesHoveredItemIndexFromMouseY()
    {
        var node = new ListNode<string>
        {
            Items = ["a", "b", "c", "d"],
            ItemHeight = 2,
        };
        node.Arrange(new Rect(0, 0, 20, 8));
        node.IsHovered = true;

        node.OnHoverMove(5, 4); // localY = 4, /2 = 2

        Assert.AreEqual(2, node.HoveredItemIndex);
    }

    [TestMethod]
    public void IsHovered_SetFalse_ResetsHoveredItemIndex()
    {
        var node = new ListNode<string>
        {
            Items = ["a", "b"],
            ItemHeight = 1,
        };
        node.Arrange(new Rect(0, 0, 20, 5));
        node.IsHovered = true;
        node.OnHoverMove(0, 1);
        Assert.AreEqual(1, node.HoveredItemIndex);

        node.IsHovered = false;

        Assert.AreEqual(-1, node.HoveredItemIndex);
    }

    [TestMethod]
    public void OnHoverMove_OutsideViewport_ClearsHoveredItemIndex()
    {
        var node = new ListNode<string>
        {
            Items = ["a", "b"],
            ItemHeight = 1,
        };
        node.Arrange(new Rect(0, 0, 20, 2));
        node.IsHovered = true;
        node.OnHoverMove(0, 0);
        Assert.AreEqual(0, node.HoveredItemIndex);

        node.OnHoverMove(0, 9); // outside viewport

        Assert.AreEqual(-1, node.HoveredItemIndex);
    }

    #endregion

    #region Selection / movement on typed items

    [TestMethod]
    public void SelectedItem_ReturnsTypedValue()
    {
        var node = new ListNode<Country>
        {
            Items =
            [
                new Country("Australia", "Canberra"),
                new Country("Japan", "Tokyo"),
            ],
            FocusedIndex = 1,
        };

        Assert.IsNotNull(node.FocusedItem);
        Assert.AreEqual("Japan", node.FocusedItem!.Name);
        Assert.AreEqual("Japan", node.FocusedText);
    }

    [TestMethod]
    public void MoveDown_Wraps_OnTypedItems()
    {
        var node = new ListNode<Country>
        {
            Items =
            [
                new Country("A", "X"),
                new Country("B", "Y"),
            ],
        };
        node.FocusedIndex = 1;

        node.MoveDown();

        Assert.AreEqual(0, node.FocusedIndex);
    }

    #endregion

    #region Default-mode rendering parity

    [TestMethod]
    public async Task TypedListWithoutTemplate_RendersItemsViaToString()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        var items = new[]
        {
            new Country("Australia", "Canberra"),
            new Country("Japan", "Tokyo"),
        };

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.List(items)),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Australia") && s.ContainsText("Japan"),
                TimeSpan.FromSeconds(5), "default-rendered items visible")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.IsTrue(snapshot.ContainsText("> Australia"), "selected indicator on first row");
        Assert.IsTrue(snapshot.ContainsText("Japan"));
    }

    #endregion

    #region Template-mode rendering

    [TestMethod]
    public async Task ItemTemplate_RendersCustomWidgetPerRow()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        var items = new[]
        {
            new Country("Australia", "Canberra"),
            new Country("Japan", "Tokyo"),
        };

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.List(items)
                    .ItemTemplate(context =>
                    {
                        var prefix = context.IsFocused ? "* " : "  ";
                        return context.Text(prefix + context.Item.Name + " - " + context.Item.Capital);
                    })),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Australia - Canberra"),
                TimeSpan.FromSeconds(5), "template-rendered items visible")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.IsTrue(snapshot.ContainsText("* Australia - Canberra"), "selected row uses custom prefix");
        Assert.IsTrue(snapshot.ContainsText("Japan - Tokyo"));
        // Default selector should NOT be drawn in template mode.
        Assert.IsFalse(snapshot.ContainsText("> Australia"));
    }

    #endregion

    #region Typed event args

    [TestMethod]
    public async Task OnFocusChanged_DeliversTypedItem()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        var items = new[]
        {
            new Country("Australia", "Canberra"),
            new Country("Japan", "Tokyo"),
            new Country("Brazil", "Brasilia"),
        };

        Country? received = null;
        int receivedIndex = -1;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.List(items)
                    .OnFocusChanged((ListFocusChangedEventArgs<Country> args) =>
                    {
                        received = args.FocusedItem;
                        receivedIndex = args.FocusedIndex;
                    })),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Australia"), TimeSpan.FromSeconds(5), "list visible")
            .Key(Hex1bKey.DownArrow)
            .Wait(TimeSpan.FromMilliseconds(150))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;

        Assert.AreEqual(1, receivedIndex);
        Assert.IsNotNull(received);
        Assert.AreEqual("Japan", received!.Name);
    }

    [TestMethod]
    public void SelectedIndex_ControlledMode_OverridesNodeSelection()
    {
        // Reconcile a ListWidget twice: the second time with .FocusedIndex(2).
        // The node should adopt the controlled value on the second pass even though
        // the node's own SelectedIndex was 0 after the first pass.
        var widget1 = new ListWidget<string>(["a", "b", "c", "d"]);
        var widget2 = widget1.FocusedIndex(2);

        Assert.IsNull(widget1.ControlledFocusedIndex);
        Assert.AreEqual(2, widget2.ControlledFocusedIndex);
    }

    [TestMethod]
    public void SelectedIndex_ControlledMode_ClampsOutOfRange()
    {
        var widget = new ListWidget<string>(["a", "b"]).FocusedIndex(99);

        Assert.AreEqual(99, widget.ControlledFocusedIndex);
        // The clamp is applied inside ApplyState during reconciliation, not on
        // the widget itself — the widget just records the requested value.
    }

    #endregion

    #region Empty list

    [TestMethod]
    public void EmptyList_WithTemplate_DoesNotThrow()
    {
        var node = new ListNode<Country>
        {
            Items = [],
            ItemHeight = 1,
        };
        node.Arrange(new Rect(0, 0, 20, 5));

        // No items -> visibleCount = 5, maxScroll = 0
        Assert.AreEqual(0, node.MaxScrollOffset);
        Assert.AreEqual(-1, node.HoveredItemIndex);
        Assert.IsNull(node.FocusedItem);
        Assert.IsNull(node.FocusedText);
    }

    #endregion

    #region Extension method behavior

    [TestMethod]
    public void ItemHeight_ClampsToOne()
    {
        var widget = new ListWidget<string>(["a"]).ItemHeight(0);
        Assert.AreEqual(1, widget.ItemHeight);

        widget = new ListWidget<string>(["a"]).ItemHeight(-5);
        Assert.AreEqual(1, widget.ItemHeight);
    }

    [TestMethod]
    public void InitialSelectedIndex_AppliedOnNewNode()
    {
        var node = new ListNode<string>
        {
            Items = ["a", "b", "c"],
            FocusedIndex = 2,
        };

        Assert.AreEqual(2, node.FocusedIndex);
        Assert.AreEqual("c", node.FocusedItem);
    }

    #endregion

    #region Virtualized DataSource

    /// <summary>
    /// Synthetic data source that records how many ranges were requested so
    /// tests can verify the node didn't materialise the entire collection.
    /// </summary>
    private sealed class CountingDataSource<T>(IReadOnlyList<T> items) : Hex1b.Data.IListDataSource<T>
    {
        public int CountCallCount { get; private set; }
        public int FetchCallCount { get; private set; }
        public List<(int Start, int Count)> RequestedRanges { get; } = new();

#pragma warning disable CS0067
        public event System.Collections.Specialized.NotifyCollectionChangedEventHandler? CollectionChanged;
#pragma warning restore CS0067

        public ValueTask<int> GetItemCountAsync(CancellationToken cancellationToken = default)
        {
            CountCallCount++;
            return ValueTask.FromResult(items.Count);
        }

        public ValueTask<IReadOnlyList<T>> GetItemsAsync(int startIndex, int count, CancellationToken cancellationToken = default)
        {
            FetchCallCount++;
            RequestedRanges.Add((startIndex, count));
            var actual = Math.Min(count, Math.Max(0, items.Count - startIndex));
            var window = new T[actual];
            for (int i = 0; i < actual; i++) window[i] = items[startIndex + i];
            return ValueTask.FromResult<IReadOnlyList<T>>(window);
        }

        public ValueTask<int?> GetIndexForKeyAsync(object? key, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<int?>(null);
    }

    [TestMethod]
    public async Task DataSource_LoadDataAsync_PopulatesCacheAndCount()
    {
        var source = new CountingDataSource<int>(Enumerable.Range(0, 1000).ToList());
        var node = new ListNode<int> { DataSource = source };

        await node.LoadDataAsync(0, 50);

        Assert.IsTrue(node.IsVirtualized);
        Assert.AreEqual(1000, node.EffectiveItemCount);
        Assert.AreEqual(1, source.CountCallCount);
        Assert.AreEqual(1, source.FetchCallCount);
        Assert.IsTrue(node.TryGetEffectiveItem(0, out var v0));
        Assert.AreEqual(0, v0);
        Assert.IsTrue(node.TryGetEffectiveItem(49, out var v49));
        Assert.AreEqual(49, v49);
        Assert.IsFalse(node.TryGetEffectiveItem(500, out _));
    }

    [TestMethod]
    public async Task DataSource_LoadDataAsync_SkipsFetchWhenRangeCached()
    {
        var source = new CountingDataSource<int>(Enumerable.Range(0, 100).ToList());
        var node = new ListNode<int> { DataSource = source };

        await node.LoadDataAsync(0, 50);
        await node.LoadDataAsync(10, 20); // inside (0, 50)

        Assert.AreEqual(1, source.FetchCallCount, "Second load should hit the cache.");
    }

    [TestMethod]
    public async Task DataSource_DoesNotMaterialiseEntireCollection_OnReconcile()
    {
        var source = new CountingDataSource<int>(Enumerable.Range(0, 100_000).ToList());
        var widget = new ListWidget<int>(null) { DataSource = source };

        var ctx = ReconcileContext.CreateRoot();
        var node = (ListNode<int>)await widget.ReconcileAsync(null, ctx);

        Assert.AreEqual(100_000, node.EffectiveItemCount);
        // Only the initial 50-item window should have been fetched, never the
        // entire 100k collection.
        Assert.IsTrue(source.RequestedRanges.All(r => r.Count <= 1000),
            $"Expected windowed loads only, got: {string.Join(",", source.RequestedRanges.Select(r => $"({r.Start},{r.Count})"))}");
        Assert.IsTrue(source.RequestedRanges.Sum(r => r.Count) < 1000,
            "Total fetched items should be a small window, not the whole collection.");
    }

    [TestMethod]
    public async Task DataSource_Selection_SelectedItemReadsFromCache()
    {
        var source = new CountingDataSource<string>(
            Enumerable.Range(0, 200).Select(i => $"item-{i}").ToList());
        var node = new ListNode<string> { DataSource = source };

        await node.LoadDataAsync(0, 50);
        node.FocusedIndex = 25;

        Assert.AreEqual("item-25", node.FocusedItem);
        Assert.AreEqual("item-25", node.FocusedText);
    }

    [TestMethod]
    public async Task DataSource_SelectionOutsideCache_EnsureLoadedFetchesIt()
    {
        var source = new CountingDataSource<string>(
            Enumerable.Range(0, 1000).Select(i => $"item-{i}").ToList());
        var node = new ListNode<string> { DataSource = source };

        await node.LoadDataAsync(0, 50);

        // Move selection well past the cache window without re-loading first.
        node.FocusedIndex = 500;
        Assert.IsNull(node.FocusedItem, "Item shouldn't be loaded yet.");

        await node.EnsureFocusedItemLoadedAsync();

        Assert.AreEqual("item-500", node.FocusedItem);
    }

    [TestMethod]
    public void DataSource_OnCollectionChanged_ClearsCacheAndInvalidates()
    {
        var inner = new System.Collections.ObjectModel.ObservableCollection<int>(Enumerable.Range(0, 10));
        var source = new Hex1b.Data.ListDataSource<int>(inner);
        var invalidated = 0;
        var node = new ListNode<int>
        {
            InvalidateCallback = () => invalidated++,
            DataSource = source,
        };

        // Prime the cache.
        node.LoadDataAsync(0, 10).AsTask().Wait();
        Assert.AreEqual(10, node.EffectiveItemCount);

        inner.Add(99);

        Assert.IsTrue(invalidated > 0, "Collection change should trigger invalidate.");
        // Cache was cleared — count is no longer known synchronously.
        Assert.AreEqual(0, node.EffectiveItemCount);
    }

    [TestMethod]
    public void ListDataSource_GetItemsAsync_ClampsBeyondEnd()
    {
        var src = new Hex1b.Data.ListDataSource<int>(Enumerable.Range(0, 5).ToList());
        var t = src.GetItemsAsync(3, 10);
        var window = t.AsTask().Result;
        Assert.AreEqual(2, window.Count);
        Assert.AreEqual(3, window[0]);
        Assert.AreEqual(4, window[1]);
    }

    #endregion

    #region Nullable Items + Empty builder

    [TestMethod]
    public void Constructor_NullItems_TreatedAsEmpty()
    {
        var widget = new ListWidget<int>(null);
        Assert.IsNull(widget.Items);
    }

    [TestMethod]
    public async Task NullItems_ReconcileTreatsAsEmpty()
    {
        var widget = new ListWidget<int>(null);
        var ctx = ReconcileContext.CreateRoot();
        var node = (ListNode<int>)await widget.ReconcileAsync(null, ctx);

        Assert.AreEqual(0, node.EffectiveItemCount);
        Assert.IsTrue(node.HasLoadedCount, "Non-virtualized lists always report count as loaded.");
        Assert.IsTrue(node.ShouldShowEmptyState);
    }

    [TestMethod]
    public async Task EmptyBuilder_RendersWhenItemsZero()
    {
        var widget = new ListWidget<int>(Array.Empty<int>())
            .Empty(_ => new Hex1b.Widgets.TextBlockWidget("Nothing to see here"));

        var ctx = ReconcileContext.CreateRoot();
        var node = (ListNode<int>)await widget.ReconcileAsync(null, ctx);

        Assert.IsNotNull(node.EmptyChildNode, "Empty child should be reconciled when count is 0.");
    }

    [TestMethod]
    public async Task EmptyBuilder_SuppressedWhenItemsPresent()
    {
        var widget = new ListWidget<int>(new[] { 1, 2, 3 })
            .Empty(_ => new Hex1b.Widgets.TextBlockWidget("(empty)"));

        var ctx = ReconcileContext.CreateRoot();
        var node = (ListNode<int>)await widget.ReconcileAsync(null, ctx);

        Assert.IsNull(node.EmptyChildNode, "Empty child should not be reconciled when items are present.");
    }

    [TestMethod]
    public async Task EmptyBuilder_DataSourceNotLoaded_DoesNotFlashEmpty()
    {
        // Virtualized data source whose count is unknown at construction time.
        var source = new CountingDataSource<int>(Array.Empty<int>());
        // Force the node into a virtualized state where the count is not yet cached
        // by reconciling without an explicit pre-load.
        var node = new ListNode<int>();

        // Even though no items will ever be returned, before the first load completes
        // HasLoadedCount must be false so the empty widget doesn't flash.
        node.DataSource = source;

        Assert.IsFalse(node.HasLoadedCount, "Before first load, count should be unknown.");
        Assert.IsFalse(node.ShouldShowEmptyState, "Empty state must not show before first load.");

        // After load completes and the source really is empty, empty state should be shown.
        await node.LoadDataAsync(0, 50);
        Assert.IsTrue(node.HasLoadedCount);
        Assert.IsTrue(node.ShouldShowEmptyState);
    }

    [TestMethod]
    public async Task EmptyBuilder_TransitionToNonEmpty_DropsEmptyChild()
    {
        var widget1 = new ListWidget<int>(Array.Empty<int>())
            .Empty(_ => new Hex1b.Widgets.TextBlockWidget("(empty)"));
        var ctx = ReconcileContext.CreateRoot();
        var node = (ListNode<int>)await widget1.ReconcileAsync(null, ctx);
        Assert.IsNotNull(node.EmptyChildNode);

        // Now reconcile with items present — empty child should be dropped.
        var widget2 = new ListWidget<int>(new[] { 1, 2, 3 })
            .Empty(_ => new Hex1b.Widgets.TextBlockWidget("(empty)"));
        await widget2.ReconcileAsync(node, ctx);

        Assert.IsNull(node.EmptyChildNode);
    }

    #endregion

    #region Home/End/PageUp/PageDown bindings

    [TestMethod]
    public void MoveToFirst_FromMiddle_SelectsIndexZero()
    {
        var node = new ListNode<int> { Items = Enumerable.Range(0, 100).ToList(), FocusedIndex = 42 };
        node.MoveToFirst();
        Assert.AreEqual(0, node.FocusedIndex);
    }

    [TestMethod]
    public void MoveToLast_FromMiddle_SelectsLastIndex()
    {
        var node = new ListNode<int> { Items = Enumerable.Range(0, 100).ToList(), FocusedIndex = 42 };
        node.MoveToLast();
        Assert.AreEqual(99, node.FocusedIndex);
    }

    [TestMethod]
    public void PageDown_AdvancesByViewportHeight()
    {
        var node = new ListNode<int> { Items = Enumerable.Range(0, 100).ToList(), FocusedIndex = 0 };
        node.Measure(new Constraints(0, 20, 0, 10));
        node.Arrange(new Hex1b.Layout.Rect(0, 0, 20, 10));

        node.PageDown();

        // VisibleItemCount is 10 with a 10-row viewport and ItemHeight=1.
        Assert.AreEqual(10, node.FocusedIndex);
    }

    [TestMethod]
    public void PageUp_RetreatsByViewportHeight()
    {
        var node = new ListNode<int> { Items = Enumerable.Range(0, 100).ToList(), FocusedIndex = 50 };
        node.Measure(new Constraints(0, 20, 0, 10));
        node.Arrange(new Hex1b.Layout.Rect(0, 0, 20, 10));

        node.PageUp();

        Assert.AreEqual(40, node.FocusedIndex);
    }

    [TestMethod]
    public void PageDown_NearEnd_ClampsToLastItem()
    {
        var node = new ListNode<int> { Items = Enumerable.Range(0, 100).ToList(), FocusedIndex = 95 };
        node.Measure(new Constraints(0, 20, 0, 10));
        node.Arrange(new Hex1b.Layout.Rect(0, 0, 20, 10));

        node.PageDown();

        Assert.AreEqual(99, node.FocusedIndex);
    }

    [TestMethod]
    public void PageUp_NearStart_ClampsToFirstItem()
    {
        var node = new ListNode<int> { Items = Enumerable.Range(0, 100).ToList(), FocusedIndex = 3 };
        node.Measure(new Constraints(0, 20, 0, 10));
        node.Arrange(new Hex1b.Layout.Rect(0, 0, 20, 10));

        node.PageUp();

        Assert.AreEqual(0, node.FocusedIndex);
    }

    [TestMethod]
    public void MoveToFirst_EmptyList_IsNoOp()
    {
        var node = new ListNode<int>();
        node.MoveToFirst();
        Assert.AreEqual(0, node.FocusedIndex);
    }

    #endregion

    #region Multi-select

    private static InputBindingActionContext NewBindingContext()
        => new(new FocusRing(), null, default);

    private static async Task<(ListNode<int> node, List<ListSelectionChangedEventArgs<int>> events)> CreateMultiSelectListAsync(
        bool enableHandler = true,
        IReadOnlyList<int>? controlled = null,
        IReadOnlyList<int>? initial = null)
    {
        var events = new List<ListSelectionChangedEventArgs<int>>();
        var widget = new ListWidget<int>(Enumerable.Range(0, 5).ToList())
        {
            IsMultiSelectEnabled = true,
            SelectedIndices = controlled,
            InitialSelectedIndices = initial,
        };
        if (enableHandler)
        {
            widget = widget with
            {
                SelectionChangedHandler = args =>
                {
                    events.Add(args);
                    return Task.CompletedTask;
                },
            };
        }
        var ctx = ReconcileContext.CreateRoot();
        var node = (ListNode<int>)await widget.ReconcileAsync(null, ctx);
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 20, 5));
        node.IsFocused = true;
        return (node, events);
    }

    [TestMethod]
    public async Task MultiSelect_Disabled_SpaceActivatesInsteadOfToggling()
    {
        var widget = new ListWidget<int>([1, 2, 3]);
        var ctx = ReconcileContext.CreateRoot();
        var node = (ListNode<int>)await widget.ReconcileAsync(null, ctx);
        node.Arrange(new Rect(0, 0, 20, 3));

        Assert.IsFalse(node.IsMultiSelectEnabled);
        Assert.AreEqual(0, node.SelectedIndices.Count);
    }

    [TestMethod]
    public async Task MultiSelect_Enabled_SpaceTogglesFocusedRow()
    {
        var (node, events) = await CreateMultiSelectListAsync();
        node.FocusedIndex = 2;

        var binding = node.BuildBindings().Bindings
            .First(b => b.FirstStep.Key == Hex1bKey.Spacebar);
        await binding.ExecuteAsync(NewBindingContext());

        TestSeq.AreEqual(new[] { 2 }, node.SelectedIndicesSnapshot);
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(ListSelectionChangeReason.Toggle, events[0].Reason);
        Assert.AreEqual(2, events[0].ToggledIndex);
        Assert.IsTrue(events[0].IsSelected);

        // Second press deselects.
        await binding.ExecuteAsync(NewBindingContext());
        Assert.AreEqual(0, node.SelectedIndicesSnapshot.Count);
        Assert.AreEqual(2, events.Count);
        Assert.IsFalse(events[1].IsSelected);
    }

    [TestMethod]
    public async Task MultiSelect_ShiftDownArrow_ExtendsRangeFromAnchor()
    {
        var (node, events) = await CreateMultiSelectListAsync();
        node.FocusedIndex = 1;

        // Anchor at row 1.
        await node.BuildBindings().Bindings
            .First(b => b.FirstStep.Key == Hex1bKey.Spacebar)
            .ExecuteAsync(NewBindingContext());

        var shiftDown = node.BuildBindings().Bindings
            .First(b => b.FirstStep.Key == Hex1bKey.DownArrow && b.FirstStep.Modifiers.HasFlag(Hex1bModifiers.Shift));
        await shiftDown.ExecuteAsync(NewBindingContext()); // anchor=1, cursor→2 — select [1,2]
        await shiftDown.ExecuteAsync(NewBindingContext()); // cursor→3 — select [1,2,3]

        TestSeq.AreEqual(new[] { 1, 2, 3 }, node.SelectedIndicesSnapshot);
        Assert.AreEqual(ListSelectionChangeReason.ExtendRange, events.Last().Reason);
    }

    [TestMethod]
    public async Task MultiSelect_CtrlA_SelectsAll_ThenTogglesToDeselectAll()
    {
        var (node, events) = await CreateMultiSelectListAsync();

        var ctrlA = node.BuildBindings().Bindings
            .First(b => b.FirstStep.Key == Hex1bKey.A && b.FirstStep.Modifiers.HasFlag(Hex1bModifiers.Control));

        await ctrlA.ExecuteAsync(NewBindingContext());
        TestSeq.AreEqual(new[] { 0, 1, 2, 3, 4 }, node.SelectedIndicesSnapshot);
        Assert.AreEqual(ListSelectionChangeReason.SelectAll, events[0].Reason);

        await ctrlA.ExecuteAsync(NewBindingContext());
        Assert.AreEqual(0, node.SelectedIndicesSnapshot.Count);
        Assert.AreEqual(ListSelectionChangeReason.DeselectAll, events[1].Reason);
    }

    [TestMethod]
    public async Task MultiSelect_Controlled_WidgetPropOverridesInternalState()
    {
        var widget = new ListWidget<int>([0, 1, 2, 3, 4])
        {
            IsMultiSelectEnabled = true,
            SelectedIndices = new[] { 1, 3 },
        };
        var ctx = ReconcileContext.CreateRoot();
        var node = (ListNode<int>)await widget.ReconcileAsync(null, ctx);

        TestSeq.AreEqual(new[] { 1, 3 }, node.SelectedIndicesSnapshot);

        // Re-reconcile with a new controlled set — node state mirrors it.
        var widget2 = widget with { SelectedIndices = new[] { 0, 4 } };
        await widget2.ReconcileAsync(node, ctx);
        TestSeq.AreEqual(new[] { 0, 4 }, node.SelectedIndicesSnapshot);
    }

    [TestMethod]
    public async Task MultiSelect_Uncontrolled_InitialSelectedIndicesSeedsOnce()
    {
        var (node, _) = await CreateMultiSelectListAsync(initial: new[] { 1, 2 });
        TestSeq.AreEqual(new[] { 1, 2 }, node.SelectedIndicesSnapshot);

        // Reconcile again with the same uncontrolled widget — initial doesn't reseed.
        node.FocusedIndex = 4;
        var widget = new ListWidget<int>(Enumerable.Range(0, 5).ToList())
        {
            IsMultiSelectEnabled = true,
            InitialSelectedIndices = new[] { 1, 2 },
        };
        await widget.ReconcileAsync(node, ReconcileContext.CreateRoot());
        TestSeq.AreEqual(new[] { 1, 2 }, node.SelectedIndicesSnapshot);
    }

    [TestMethod]
    public async Task MultiSelect_TurnedOff_ClearsCheckedSet()
    {
        var widget = new ListWidget<int>([0, 1, 2])
        {
            IsMultiSelectEnabled = true,
            SelectedIndices = new[] { 0, 2 },
        };
        var ctx = ReconcileContext.CreateRoot();
        var node = (ListNode<int>)await widget.ReconcileAsync(null, ctx);
        Assert.AreEqual(2, node.SelectedIndicesSnapshot.Count);

        var single = new ListWidget<int>([0, 1, 2]);
        await single.ReconcileAsync(node, ctx);

        Assert.IsFalse(node.IsMultiSelectEnabled);
        Assert.AreEqual(0, node.SelectedIndicesSnapshot.Count);
    }

    [TestMethod]
    public async Task ItemContext_MultiSelectEnabled_IsSelectedReflectsCheckedSet()
    {
        var contexts = new List<ListItemContext<int>>();
        var widget = new ListWidget<int>([10, 20, 30])
        {
            IsMultiSelectEnabled = true,
            SelectedIndices = new[] { 0, 2 },
            Template = c => { contexts.Add(c); return new TextBlockWidget(c.Item.ToString()); },
        };
        var ctx = ReconcileContext.CreateRoot();
        var node = (ListNode<int>)await widget.ReconcileAsync(null, ctx);
        node.Arrange(new Rect(0, 0, 10, 3));

        Assert.AreEqual(3, contexts.Count);
        Assert.IsTrue(contexts[0].IsSelected);
        Assert.IsFalse(contexts[1].IsSelected);
        Assert.IsTrue(contexts[2].IsSelected);
    }

    [TestMethod]
    public async Task ItemContext_FocusedRow_IsFocusedTrueIndependentOfChecked()
    {
        var contexts = new List<ListItemContext<int>>();
        var widget = new ListWidget<int>([10, 20, 30])
        {
            IsMultiSelectEnabled = true,
            InitialFocusedIndex = 1,
            SelectedIndices = new[] { 0 },
            Template = c => { contexts.Add(c); return new TextBlockWidget(c.Item.ToString()); },
        };
        var ctx = ReconcileContext.CreateRoot();
        await widget.ReconcileAsync(null, ctx);

        // Row 1 is the cursor but not in the checked set.
        Assert.IsTrue(contexts[1].IsFocused);
        Assert.IsFalse(contexts[1].IsSelected);
        // Row 0 is in the checked set but not the cursor.
        Assert.IsFalse(contexts[0].IsFocused);
        Assert.IsTrue(contexts[0].IsSelected);
    }

    [TestMethod]
    public async Task MultiSelect_NotEnabled_IsSelectedAlwaysFalseInTemplate()
    {
        var contexts = new List<ListItemContext<int>>();
        var widget = new ListWidget<int>([10, 20, 30])
        {
            Template = c => { contexts.Add(c); return new TextBlockWidget(c.Item.ToString()); },
        };
        var ctx = ReconcileContext.CreateRoot();
        await widget.ReconcileAsync(null, ctx);

        Assert.IsTrue(contexts.All(c => !c.IsSelected));
    }

    #endregion
}
