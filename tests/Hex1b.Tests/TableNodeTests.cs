using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class TableNodeTests
{
    #region Construction & Basics

    [Fact]
    public void Node_DefaultState_HasNoRows()
    {
        var node = new TableNode<string>();
        
        Assert.Null(node.Data);
        Assert.Null(node.HeaderBuilder);
        Assert.Null(node.RowBuilder);
    }

    [Fact]
    public void Node_WithData_HasCorrectRowCount()
    {
        var data = new[] { "A", "B", "C" };
        var node = new TableNode<string>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)]
        };
        
        Assert.Equal(3, node.Data!.Count);
    }

    #endregion

    #region Column Count Validation

    [Fact]
    public void Validation_HeaderRowMismatch_Throws()
    {
        var data = new[] { "Item1" };
        var node = new TableNode<string>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Col1"), h.Cell("Col2")], // 2 columns
            RowBuilder = (r, item, _) => [r.Cell(item)] // 1 column - mismatch!
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            node.Measure(new Constraints(0, 80, 0, 24)));

        Assert.Contains("column count mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("header has 2", ex.Message);
        Assert.Contains("row 0 has 1", ex.Message);
    }

    [Fact]
    public void Validation_FooterMismatch_Throws()
    {
        var data = new[] { "Item1" };
        var node = new TableNode<string>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Col1")],
            RowBuilder = (r, item, _) => [r.Cell(item)],
            FooterBuilder = f => [f.Cell("A"), f.Cell("B")] // 2 columns - mismatch!
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            node.Measure(new Constraints(0, 80, 0, 24)));

        Assert.Contains("column count mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("footer", ex.Message);
    }

    [Fact]
    public void Validation_ZeroColumns_Throws()
    {
        var node = new TableNode<string>
        {
            Data = ["Item"],
            HeaderBuilder = h => [], // Empty header
            RowBuilder = (r, item, _) => []
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            node.Measure(new Constraints(0, 80, 0, 24)));

        Assert.Contains("at least one column", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validation_MatchingColumns_DoesNotThrow()
    {
        var data = new[] { "A", "B" };
        var node = new TableNode<string>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Col1"), h.Cell("Col2")],
            RowBuilder = (r, item, _) => [r.Cell(item), r.Cell(item.ToLower())],
            FooterBuilder = f => [f.Cell("Total"), f.Cell("2")]
        };

        var size = node.Measure(new Constraints(0, 80, 0, 24));
        
        Assert.True(size.Width > 0);
        Assert.True(size.Height > 0);
    }

    #endregion

    #region Measurement

    [Fact]
    public void Measure_ReturnsCorrectHeight_ForBasicTable()
    {
        var data = new[] { "A", "B", "C" };
        var node = new TableNode<string>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)]
        };

        var size = node.Measure(new Constraints(0, 40, 0, 24));

        // Height = top border (1) + header (1) + separator (1) + 3 rows (3) + bottom border (1) = 7
        Assert.Equal(7, size.Height);
    }

    [Fact]
    public void Measure_ReturnsCorrectHeight_WithFooter()
    {
        var data = new[] { "A", "B" };
        var node = new TableNode<string>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)],
            FooterBuilder = f => [f.Cell("Total")]
        };

        var size = node.Measure(new Constraints(0, 40, 0, 24));

        // Height = top (1) + header (1) + sep (1) + 2 rows (2) + sep (1) + footer (1) + bottom (1) = 8
        Assert.Equal(8, size.Height);
    }

    [Fact]
    public void Measure_UsesConstraintWidth()
    {
        var node = new TableNode<string>
        {
            Data = ["A"],
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)]
        };

        var size = node.Measure(new Constraints(0, 60, 0, 24));

        Assert.Equal(60, size.Width);
    }

    [Fact]
    public void Measure_LoadingState_UsesLoadingRowCount()
    {
        var node = new TableNode<string>
        {
            Data = null, // Loading state
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)],
            LoadingRowCount = 5
        };

        var size = node.Measure(new Constraints(0, 40, 0, 24));

        // Height = top (1) + header (1) + sep (1) + 5 loading rows (5) + bottom (1) = 9
        Assert.Equal(9, size.Height);
    }

    [Fact]
    public void Measure_EmptyData_AddsEmptyStateHeight()
    {
        var node = new TableNode<string>
        {
            Data = [], // Empty
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)]
        };

        var size = node.Measure(new Constraints(0, 40, 0, 24));

        // Height = top (1) + header (1) + sep (1) + empty message (1) + bottom (1) = 5
        Assert.Equal(5, size.Height);
    }

    #endregion

    #region Widget API

    [Fact]
    public void Widget_FluentApi_ConfiguresCorrectly()
    {
        var data = new[] { "A", "B" };
        var ctx = new RootContext();
        
        var widget = ctx.Table(data)
            .WithHeader(h => [h.Cell("Name")])
            .WithRow((r, item, _) => [r.Cell(item)])
            .WithFooter(f => [f.Cell("End")])
            .WithFocus(1);  // Use key-based focus

        Assert.Same(data, widget.Data);
        Assert.NotNull(widget.HeaderBuilder);
        Assert.NotNull(widget.RowBuilder);
        Assert.NotNull(widget.FooterBuilder);
        Assert.Equal(1, widget.FocusedKey);
    }

    [Fact]
    public void Widget_WithEmpty_ConfiguresEmptyBuilder()
    {
        var ctx = new RootContext();
        
        var widget = ctx.Table(Array.Empty<string>())
            .WithHeader(h => [h.Cell("Name")])
            .WithRow((r, item, _) => [r.Cell(item)])
            .WithEmpty(e => e.Text("No items"));

        Assert.NotNull(widget.EmptyBuilder);
    }

    [Fact]
    public void Widget_WithLoading_ConfiguresLoadingBuilder()
    {
        var ctx = new RootContext();
        
        var widget = ctx.Table<string>(null)
            .WithHeader(h => [h.Cell("Name")])
            .WithRow((r, item, _) => [r.Cell(item)])
            .WithLoading((l, idx) => [l.Cell("Loading...")], rowCount: 10);

        Assert.NotNull(widget.LoadingRowBuilder);
        Assert.Equal(10, widget.LoadingRowCount);
    }

    #endregion

    #region Cell Extensions

    [Fact]
    public void TableCell_Fixed_SetsWidth()
    {
        var cell = new TableHeaderContext().Cell("Test").Fixed(15);
        
        Assert.NotNull(cell.Width);
        Assert.True(cell.Width.Value.IsFixed);
        Assert.Equal(15, cell.Width.Value.FixedValue);
    }

    [Fact]
    public void TableCell_Fill_SetsWidth()
    {
        var cell = new TableHeaderContext().Cell("Test").Fill();
        
        Assert.NotNull(cell.Width);
        Assert.True(cell.Width.Value.IsFill);
    }

    [Fact]
    public void TableCell_AlignRight_SetsAlignment()
    {
        var cell = new TableHeaderContext().Cell("Test").AlignRight();
        
        Assert.Equal(Alignment.Right, cell.Alignment);
    }

    #endregion

    #region Reconciliation

    [Fact]
    public async Task Reconcile_CreatesNewNode()
    {
        var widget = new TableWidget<string>
        {
            Data = ["A"],
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)]
        };

        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var node = await widget.ReconcileAsync(null, context);

        Assert.IsType<TableNode<string>>(node);
    }

    [Fact]
    public async Task Reconcile_ReusesExistingNode()
    {
        var existingNode = new TableNode<string>();
        var widget = new TableWidget<string>
        {
            Data = ["A"],
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)]
        };

        var context = ReconcileContext.CreateRoot();

        var node = await widget.ReconcileAsync(existingNode, context);

        Assert.Same(existingNode, node);
    }

    [Fact]
    public async Task Reconcile_UpdatesNodeData()
    {
        var existingNode = new TableNode<string> { Data = ["Old"] };
        var newData = new[] { "New1", "New2" };
        var widget = new TableWidget<string>
        {
            Data = newData,
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)]
        };

        var context = ReconcileContext.CreateRoot();
        var node = await widget.ReconcileAsync(existingNode, context);

        Assert.Same(newData, ((TableNode<string>)node).Data);
    }

    #endregion

    #region Scroll Bindings

    [Fact]
    public async Task ScrollDown_KeyBinding_ScrollsTable()
    {
        // Create a table with enough data to scroll
        var data = Enumerable.Range(1, 50).Select(i => $"Row {i}").ToArray();
        var node = new TableNode<string>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)]
        };

        // Measure with a small height to trigger scrolling
        var constraints = new Constraints(0, 40, 0, 10); // Only 10 rows visible
        node.Measure(constraints);
        node.Arrange(new Rect(0, 0, 40, 10));
        
        // Verify initial state
        Assert.Equal(0, node.ScrollOffset);
        Assert.True(node.IsScrollable, "Table should be scrollable with 50 rows in 10-row viewport");

        // Simulate key press using InputRouter
        node.IsFocused = true;
        var keyEvent = new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None);
        var result = await Input.InputRouter.RouteInputToNodeAsync(node, keyEvent);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(1, node.ScrollOffset);
    }

    [Fact]
    public async Task PageDown_KeyBinding_ScrollsByViewport()
    {
        var data = Enumerable.Range(1, 50).Select(i => $"Row {i}").ToArray();
        var node = new TableNode<string>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)]
        };

        // Measure with small viewport
        var constraints = new Constraints(0, 40, 0, 10);
        node.Measure(constraints);
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.IsFocused = true;
        var keyEvent = new Hex1bKeyEvent(Hex1bKey.PageDown, '\0', Hex1bModifiers.None);
        var result = await Input.InputRouter.RouteInputToNodeAsync(node, keyEvent);

        Assert.Equal(InputResult.Handled, result);
        Assert.True(node.ScrollOffset > 1, "PageDown should scroll more than 1 row");
    }

    #endregion
}
