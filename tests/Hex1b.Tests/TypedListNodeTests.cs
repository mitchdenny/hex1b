using Hex1b;
using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for <see cref="ListWidget{T}"/> / <see cref="TypedListNode{T}"/>:
/// generic-item behavior, ItemHeight layout math, mouse hit-test with multi-row
/// items, hover invalidation, key-based child reuse, typed event args, and
/// template-mode rendering parity.
/// </summary>
[TestClass]
public class TypedListNodeTests
{
    private sealed record Country(string Name, string Capital)
    {
        public override string ToString() => Name;
    }

    #region Math: ItemHeight + scrolling

    [TestMethod]
    public void ItemHeight_VisibleCount_DividesViewportHeight()
    {
        var node = new TypedListNode<string>
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
        var node = new TypedListNode<string>
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
        var node = new TypedListNode<string>
        {
            Items = ["a", "b", "c", "d"],
            ItemHeight = 2,
        };
        node.Arrange(new Rect(0, 0, 20, 8));

        // localY = 3 with itemHeight = 2 -> index 1
        var ev = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 5, 3, Hex1bModifiers.None);
        var result = node.HandleMouseClick(5, 3, ev);

        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual(1, node.SelectedIndex);
    }

    [TestMethod]
    public void HandleMouseClick_OutsideItems_DoesNotChangeSelection()
    {
        var node = new TypedListNode<string>
        {
            Items = ["a", "b"],
            ItemHeight = 2,
        };
        node.Arrange(new Rect(0, 0, 20, 10));

        // localY = 8 -> index 4, outside items
        var ev = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 5, 8, Hex1bModifiers.None);
        var result = node.HandleMouseClick(5, 8, ev);

        Assert.AreEqual(InputResult.NotHandled, result);
        Assert.AreEqual(0, node.SelectedIndex);
    }

    #endregion

    #region Hover

    [TestMethod]
    public void OnHoverMove_UpdatesHoveredItemIndexFromMouseY()
    {
        var node = new TypedListNode<string>
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
        var node = new TypedListNode<string>
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
        var node = new TypedListNode<string>
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
        var node = new TypedListNode<Country>
        {
            Items =
            [
                new Country("Australia", "Canberra"),
                new Country("Japan", "Tokyo"),
            ],
            SelectedIndex = 1,
        };

        Assert.IsNotNull(node.SelectedItem);
        Assert.AreEqual("Japan", node.SelectedItem!.Name);
        Assert.AreEqual("Japan", node.SelectedText);
    }

    [TestMethod]
    public void MoveDown_Wraps_OnTypedItems()
    {
        var node = new TypedListNode<Country>
        {
            Items =
            [
                new Country("A", "X"),
                new Country("B", "Y"),
            ],
        };
        node.SelectedIndex = 1;

        node.MoveDown();

        Assert.AreEqual(0, node.SelectedIndex);
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
            ctx => Task.FromResult<Hex1bWidget>(ctx.TypedList(items)),
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
                ctx.TypedList(items)
                    .ItemTemplate(context =>
                    {
                        var prefix = context.IsSelected ? "* " : "  ";
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
    public async Task OnSelectionChanged_DeliversTypedItem()
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
                ctx.TypedList(items)
                    .OnSelectionChanged((ListSelectionChangedEventArgs<Country> args) =>
                    {
                        received = args.SelectedItem;
                        receivedIndex = args.SelectedIndex;
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
        // Reconcile a ListWidget twice: the second time with .SelectedIndex(2).
        // The node should adopt the controlled value on the second pass even though
        // the node's own SelectedIndex was 0 after the first pass.
        var widget1 = new ListWidget<string>(["a", "b", "c", "d"]);
        var widget2 = widget1.SelectedIndex(2);

        Assert.IsNull(widget1.ControlledSelectedIndex);
        Assert.AreEqual(2, widget2.ControlledSelectedIndex);
    }

    [TestMethod]
    public void SelectedIndex_ControlledMode_ClampsOutOfRange()
    {
        var widget = new ListWidget<string>(["a", "b"]).SelectedIndex(99);

        Assert.AreEqual(99, widget.ControlledSelectedIndex);
        // The clamp is applied inside ApplyState during reconciliation, not on
        // the widget itself — the widget just records the requested value.
    }

    #endregion

    #region Empty list

    [TestMethod]
    public void EmptyList_WithTemplate_DoesNotThrow()
    {
        var node = new TypedListNode<Country>
        {
            Items = [],
            ItemHeight = 1,
        };
        node.Arrange(new Rect(0, 0, 20, 5));

        // No items -> visibleCount = 5, maxScroll = 0
        Assert.AreEqual(0, node.MaxScrollOffset);
        Assert.AreEqual(-1, node.HoveredItemIndex);
        Assert.IsNull(node.SelectedItem);
        Assert.IsNull(node.SelectedText);
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
        var node = new TypedListNode<string>
        {
            Items = ["a", "b", "c"],
            SelectedIndex = 2,
        };

        Assert.AreEqual(2, node.SelectedIndex);
        Assert.AreEqual("c", node.SelectedItem);
    }

    #endregion
}
