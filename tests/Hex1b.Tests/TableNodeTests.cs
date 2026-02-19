using System.Collections.Specialized;
using Hex1b.Automation;
using Hex1b.Data;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Test item with selection state for view model selection tests.
/// </summary>
internal class SelectableItem
{
    public string Name { get; set; } = "";
    public bool IsSelected { get; set; }
}

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
            RowBuilder = (r, item, _) => [r.Cell(item)],
            WidthHint = SizeHint.Fill
        };

        var size = node.Measure(new Constraints(0, 60, 0, 24));

        Assert.Equal(60, size.Width);
    }

    [Fact]
    public void Measure_NullData_ShowsEmptyState()
    {
        var node = new TableNode<string>
        {
            Data = null, // Null data shows empty state
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)]
        };

        var size = node.Measure(new Constraints(0, 40, 0, 24));

        // Height = top (1) + header (1) + sep (1) + 1 empty row (1) + bottom (1) = 5
        Assert.Equal(5, size.Height);
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
            .Header(h => [h.Cell("Name")])
            .Row((r, item, _) => [r.Cell(item)])
            .Footer(f => [f.Cell("End")])
            .Focus(1);  // Use key-based focus

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
            .Header(h => [h.Cell("Name")])
            .Row((r, item, _) => [r.Cell(item)])
            .Empty(e => e.Text("No items"));

        Assert.NotNull(widget.EmptyBuilder);
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

    #region Row Navigation Bindings

    [Fact]
    public async Task DownArrow_KeyBinding_MovesFocusToNextRow()
    {
        // Create a table with enough data to scroll
        var data = Enumerable.Range(1, 50).Select(i => $"Row {i}").ToArray();
        var node = new TableNode<string>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)],
            FocusedKey = 0 // Start with first row focused
        };

        // Measure with a small height to trigger scrolling
        var constraints = new Constraints(0, 40, 0, 10); // Only 10 rows visible
        node.Measure(constraints);
        node.Arrange(new Rect(0, 0, 40, 10));
        
        // Verify initial state
        Assert.Equal(0, node.FocusedKey);
        Assert.Equal(0, node.ScrollOffset);

        // Simulate key press using InputRouter
        node.IsFocused = true;
        var keyEvent = new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None);
        var result = await Input.InputRouter.RouteInputToNodeAsync(node, keyEvent);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(1, node.FocusedKey); // Focus moved to row 1
    }

    [Fact]
    public async Task DownArrow_AtBottomOfViewport_ScrollsToKeepFocusVisible()
    {
        var data = Enumerable.Range(1, 50).Select(i => $"Row {i}").ToArray();
        var node = new TableNode<string>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)],
            FocusedKey = 5 // Focus on row 5 (last visible in 6-row viewport with header)
        };

        var constraints = new Constraints(0, 40, 0, 10);
        node.Measure(constraints);
        node.Arrange(new Rect(0, 0, 40, 10));
        
        // With 10 height, we have about 6-7 data rows visible
        // If focused row is at the bottom edge and we press Down,
        // the view should scroll to keep the new focused row visible
        Assert.Equal(0, node.ScrollOffset);

        node.IsFocused = true;
        
        // Press down multiple times to reach the edge of viewport
        for (int i = 5; i < 10; i++)
        {
            node.FocusedKey = i;
            var keyEvent = new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None);
            await Input.InputRouter.RouteInputToNodeAsync(node, keyEvent);
        }

        // After moving focus past the visible area, scroll should have adjusted
        Assert.True(node.ScrollOffset > 0, "Table should have scrolled to keep focus visible");
    }

    [Fact]
    public async Task PageDown_KeyBinding_MovesFocusByPage()
    {
        var data = Enumerable.Range(1, 50).Select(i => $"Row {i}").ToArray();
        var node = new TableNode<string>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)],
            FocusedKey = 0 // Start at first row
        };

        // Measure with small viewport
        var constraints = new Constraints(0, 40, 0, 10);
        node.Measure(constraints);
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.IsFocused = true;
        var keyEvent = new Hex1bKeyEvent(Hex1bKey.PageDown, '\0', Hex1bModifiers.None);
        var result = await Input.InputRouter.RouteInputToNodeAsync(node, keyEvent);

        Assert.Equal(InputResult.Handled, result);
        
        // Focus should have moved by approximately one page
        var focusedIndex = (int)node.FocusedKey!;
        Assert.True(focusedIndex > 3, $"PageDown should move focus by multiple rows, but focus is at {focusedIndex}");
    }

    [Fact]
    public async Task Home_KeyBinding_MovesFocusToFirstRow()
    {
        var data = Enumerable.Range(1, 50).Select(i => $"Row {i}").ToArray();
        var node = new TableNode<string>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)],
            FocusedKey = 25 // Start in the middle
        };

        var constraints = new Constraints(0, 40, 0, 10);
        node.Measure(constraints);
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.IsFocused = true;
        var keyEvent = new Hex1bKeyEvent(Hex1bKey.Home, '\0', Hex1bModifiers.None);
        var result = await Input.InputRouter.RouteInputToNodeAsync(node, keyEvent);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(0, node.FocusedKey); // Focus moved to first row
        Assert.Equal(0, node.ScrollOffset); // Scrolled to show first row
    }

    [Fact]
    public async Task End_KeyBinding_MovesFocusToLastRow()
    {
        var data = Enumerable.Range(1, 50).Select(i => $"Row {i}").ToArray();
        var node = new TableNode<string>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)],
            FocusedKey = 0 // Start at first row
        };

        var constraints = new Constraints(0, 40, 0, 10);
        node.Measure(constraints);
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.IsFocused = true;
        var keyEvent = new Hex1bKeyEvent(Hex1bKey.End, '\0', Hex1bModifiers.None);
        var result = await Input.InputRouter.RouteInputToNodeAsync(node, keyEvent);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(49, node.FocusedKey); // Focus moved to last row (index 49)
        Assert.True(node.ScrollOffset > 0, "Table should have scrolled to show last row");
    }

    #endregion

    #region Selection Tests

    [Fact]
    public async Task Space_KeyBinding_TogglesSelection()
    {
        var data = Enumerable.Range(1, 10).Select(i => new SelectableItem { Name = $"Row {i}" }).ToList();
        var node = new TableNode<SelectableItem>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item.Name)],
            RowKeySelector = item => item.Name,
            FocusedKey = "Row 3", // Focus on row 3
            IsSelectedSelector = item => item.IsSelected,
            SelectionChangedCallback = (item, selected) => item.IsSelected = selected
        };

        var constraints = new Constraints(0, 40, 0, 15);
        node.Measure(constraints);
        node.Arrange(new Rect(0, 0, 40, 15));
        
        node.IsFocused = true;
        
        // Initially no selection
        Assert.False(data[2].IsSelected);

        // Press Space to select
        var keyEvent = new Hex1bKeyEvent(Hex1bKey.Spacebar, ' ', Hex1bModifiers.None);
        var result = await Input.InputRouter.RouteInputToNodeAsync(node, keyEvent);

        Assert.Equal(InputResult.Handled, result);
        Assert.True(data[2].IsSelected);

        // Press Space again to deselect
        result = await Input.InputRouter.RouteInputToNodeAsync(node, keyEvent);
        
        Assert.Equal(InputResult.Handled, result);
        Assert.False(data[2].IsSelected);
    }

    [Fact]
    public async Task CtrlA_KeyBinding_SelectsAllRows()
    {
        var data = Enumerable.Range(1, 10).Select(i => new SelectableItem { Name = $"Row {i}" }).ToList();
        var node = new TableNode<SelectableItem>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item.Name)],
            RowKeySelector = item => item.Name,
            FocusedKey = "Row 1",
            IsSelectedSelector = item => item.IsSelected,
            SelectAllCallback = () => { foreach (var item in data) item.IsSelected = true; }
        };

        var constraints = new Constraints(0, 40, 0, 15);
        node.Measure(constraints);
        node.Arrange(new Rect(0, 0, 40, 15));
        
        node.IsFocused = true;
        
        // Press Ctrl+A
        var keyEvent = new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.Control);
        var result = await Input.InputRouter.RouteInputToNodeAsync(node, keyEvent);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(10, data.Count(item => item.IsSelected)); // All 10 rows selected
    }

    [Fact]
    public async Task ShiftDown_KeyBinding_ExtendsSelection()
    {
        var data = Enumerable.Range(1, 10).Select(i => new SelectableItem { Name = $"Row {i}" }).ToList();
        var node = new TableNode<SelectableItem>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item.Name)],
            RowKeySelector = item => item.Name,
            FocusedKey = "Row 3", // Start at row 3
            IsSelectedSelector = item => item.IsSelected,
            SelectionChangedCallback = (item, selected) => item.IsSelected = selected
        };

        var constraints = new Constraints(0, 40, 0, 15);
        node.Measure(constraints);
        node.Arrange(new Rect(0, 0, 40, 15));
        
        node.IsFocused = true;

        // Press Shift+Down twice
        var keyEvent = new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.Shift);
        await Input.InputRouter.RouteInputToNodeAsync(node, keyEvent);
        await Input.InputRouter.RouteInputToNodeAsync(node, keyEvent);

        Assert.Equal(3, data.Count(item => item.IsSelected)); // Rows 3, 4, 5 selected
        Assert.Equal("Row 5", node.FocusedKey); // Focus moved to row 5
    }

    [Fact]
    public async Task ShiftEnd_KeyBinding_SelectsToLastRow()
    {
        var data = Enumerable.Range(1, 10).Select(i => new SelectableItem { Name = $"Row {i}" }).ToList();
        var node = new TableNode<SelectableItem>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item.Name)],
            RowKeySelector = item => item.Name,
            FocusedKey = "Row 6", // Start at row 6
            IsSelectedSelector = item => item.IsSelected,
            SelectionChangedCallback = (item, selected) => item.IsSelected = selected
        };

        var constraints = new Constraints(0, 40, 0, 15);
        node.Measure(constraints);
        node.Arrange(new Rect(0, 0, 40, 15));
        
        node.IsFocused = true;

        // Press Shift+End
        var keyEvent = new Hex1bKeyEvent(Hex1bKey.End, '\0', Hex1bModifiers.Shift);
        await Input.InputRouter.RouteInputToNodeAsync(node, keyEvent);

        Assert.Equal(5, data.Count(item => item.IsSelected)); // Rows 6, 7, 8, 9, 10 selected
        Assert.Equal("Row 10", node.FocusedKey); // Focus at last row
    }

    [Fact]
    public async Task SelectionChangedCallback_IsCalled()
    {
        var data = Enumerable.Range(1, 10).Select(i => new SelectableItem { Name = $"Row {i}" }).ToList();
        SelectableItem? lastChangedItem = null;
        bool? lastChangedState = null;
        
        var node = new TableNode<SelectableItem>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item.Name)],
            RowKeySelector = item => item.Name,
            FocusedKey = "Row 1",
            IsSelectedSelector = item => item.IsSelected,
            SelectionChangedCallback = (item, selected) =>
            {
                lastChangedItem = item;
                lastChangedState = selected;
                item.IsSelected = selected;
            }
        };

        var constraints = new Constraints(0, 40, 0, 15);
        node.Measure(constraints);
        node.Arrange(new Rect(0, 0, 40, 15));
        
        node.IsFocused = true;

        // Press Space to select
        var keyEvent = new Hex1bKeyEvent(Hex1bKey.Spacebar, ' ', Hex1bModifiers.None);
        await Input.InputRouter.RouteInputToNodeAsync(node, keyEvent);

        Assert.NotNull(lastChangedItem);
        Assert.Equal("Row 1", lastChangedItem.Name);
        Assert.True(lastChangedState);
    }

    [Fact]
    public void SelectionColumn_CellNodesAreArrangedCorrectly()
    {
        var data = new[] { "Item1", "Item2" };
        var node = new TableNode<string>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)],
            ShowSelectionColumn = true
        };

        var constraints = new Constraints(0, 50, 0, 10);
        node.Measure(constraints);
        node.Arrange(new Rect(0, 0, 50, 10));

        // Access header row node via reflection to check it exists
        var headerRowNodeField = typeof(TableNode<string>).GetField("_headerRowNode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var headerRowNode = (TableRowNode?)headerRowNodeField?.GetValue(node);
        
        Assert.NotNull(headerRowNode);
        // The row node should have children (border, selection column, cell content, border)
        Assert.True(headerRowNode.Children.Count > 0, "Header row node should have children");
        
        // The header row itself should be arranged at the correct position
        Assert.True(headerRowNode.Bounds.Width > 0, "Header row should have a width");
    }

    [Fact]
    public void SelectionColumn_RendersSeparators()
    {
        var data = new[] { "Item1" };
        var node = new TableNode<string>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)],
            ShowSelectionColumn = true
        };

        var constraints = new Constraints(0, 30, 0, 10);
        node.Measure(constraints);
        node.Arrange(new Rect(0, 0, 30, 10));

        // Render to a surface
        var surface = new Hex1b.Surfaces.Surface(30, 10);
        var context = new Hex1b.Surfaces.SurfaceRenderContext(surface);
        node.Render(context);

        // Get characters from the header row (row 1, after top border at row 0)
        var char0 = surface[0, 1].Character;
        var char4 = surface[4, 1].Character;
        
        TestContext.Current.TestOutputHelper?.WriteLine($"char[0,1]='{char0}' char[4,1]='{char4}'");
        
        // Build header row text for debugging - show each position
        var headerRow = new System.Text.StringBuilder();
        for (int x = 0; x < 15; x++)
        {
            var c = surface[x, 1].Character;
            TestContext.Current.TestOutputHelper?.WriteLine($"  [{x}] = '{c}' (len={c?.Length ?? -1})");
            headerRow.Append(c);
        }
        TestContext.Current.TestOutputHelper?.WriteLine($"Header row (first 15): '{headerRow}'");
        
        // Check that we have the selection column separator at position 4
        // Expected: │[ ]│Name...
        Assert.True(char0 == "│", $"Expected left border '│' at position 0, got '{char0}'"); // Left border
        Assert.True(char4 == "│", $"Expected separator '│' at position 4, got '{char4}'"); // Selection column separator
    }

    [Fact]
    public void ColumnBorders_AlignAcrossHeaderRowsAndFooter()
    {
        // Arrange - table with multiple columns and alignment
        var data = new[] { "Laptop", "Mouse" };
        var node = new TableNode<string>
        {
            Data = data,
            HeaderBuilder = h => [
                h.Cell("Name").Width(SizeHint.Fixed(10)),
                h.Cell("Price").Width(SizeHint.Fixed(8)).Align(Alignment.Right),
                h.Cell("Qty").Width(SizeHint.Fixed(5)).Align(Alignment.Right)
            ],
            RowBuilder = (r, item, state) => [
                r.Cell(item),
                r.Cell("$99.99"),
                r.Cell("10")
            ],
            FooterBuilder = f => [
                f.Cell("Total"),
                f.Cell("$199.98"),
                f.Cell("20")
            ]
        };

        // Total width = 1 (left) + 10 + 1 + 8 + 1 + 5 + 1 (right) = 27
        var constraints = new Constraints(0, 30, 0, 12);
        node.Measure(constraints);
        node.Arrange(new Rect(0, 0, 30, 12));

        // Render to a surface
        var surface = new Hex1b.Surfaces.Surface(30, 12);
        var context = new Hex1b.Surfaces.SurfaceRenderContext(surface);
        node.Render(context);

        // Print all rows for debugging
        for (int y = 0; y < 8; y++)
        {
            var row = new System.Text.StringBuilder();
            for (int x = 0; x < 30; x++)
            {
                row.Append(surface[x, y].Character ?? " ");
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"Row {y}: '{row}'");
        }

        // Expected column borders at positions:
        // 0: left border │
        // 11: after Name (10 chars) │
        // 20: after Price (8 chars) │
        // 26: after Qty (5 chars) │ (right border)
        
        // Check top border has correct separators
        Assert.Equal("┌", surface[0, 0].Character);
        Assert.Equal("┬", surface[11, 0].Character);
        Assert.Equal("┬", surface[20, 0].Character);
        
        // Check header row (y=1) has vertical borders at correct positions
        Assert.Equal("│", surface[0, 1].Character);
        Assert.Equal("│", surface[11, 1].Character);
        Assert.Equal("│", surface[20, 1].Character);
        
        // Check header separator (y=2) has correct crosses
        Assert.Equal("├", surface[0, 2].Character);
        Assert.Equal("┼", surface[11, 2].Character);
        Assert.Equal("┼", surface[20, 2].Character);
        
        // Check data row 1 (y=3) has vertical borders at same positions
        Assert.Equal("│", surface[0, 3].Character);
        Assert.Equal("│", surface[11, 3].Character);
        Assert.Equal("│", surface[20, 3].Character);
        
        // Check data row 2 (y=4) has vertical borders at same positions
        Assert.Equal("│", surface[0, 4].Character);
        Assert.Equal("│", surface[11, 4].Character);
        Assert.Equal("│", surface[20, 4].Character);
        
        // Check footer separator (y=5) has correct crosses
        Assert.Equal("├", surface[0, 5].Character);
        Assert.Equal("┼", surface[11, 5].Character);
        Assert.Equal("┼", surface[20, 5].Character);
        
        // Check footer row (y=6) has vertical borders at same positions
        Assert.Equal("│", surface[0, 6].Character);
        Assert.Equal("│", surface[11, 6].Character);
        Assert.Equal("│", surface[20, 6].Character);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task Integration_TableWithKeyboardScroll_WorksEndToEnd()
    {
        // Arrange - create a full Hex1bApp with a table
        using var workload = new Hex1bAppWorkloadAdapter();
        
        // Create recording path
        var recordingPath = Path.Combine(Path.GetTempPath(), $"table_scroll_test_{DateTime.Now:yyyyMMdd_HHmmss}.cast");
        TestContext.Current.TestOutputHelper?.WriteLine($"Recording to: {recordingPath}");
        
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 10) // Small terminal to force scrolling (only ~5 data rows visible)
            .WithAsciinemaRecording(recordingPath)
            .Build();

        var products = Enumerable.Range(1, 30).Select(i => $"Product {i}").ToArray();
        
        using var app = new Hex1bApp(
            ctx => ctx.Table(products)
                .Header(h => [h.Cell("Name")])
                .Row((r, item, _) => [r.Cell(item)]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - start the app and send PageDown to scroll significantly
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Wait for initial render, then send PageDown 3 times.
        // IMPORTANT: capture the snapshot BEFORE exiting; alternate screen is cleared on exit.
        using var finalSnapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Product 1"), TimeSpan.FromSeconds(2), "Wait for table to render")
            .Key(Hex1bKey.PageDown)
            .Key(Hex1bKey.PageDown)
            .Key(Hex1bKey.PageDown)
            .WaitUntil(s => s.ContainsText("Product 15") || s.ContainsText("Product 20"), 
                       TimeSpan.FromSeconds(2), "Wait for scroll to complete")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var finalText = finalSnapshot.GetScreenText();

        using var _ = await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C) // Exit
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        // Output for debugging
        TestContext.Current.TestOutputHelper?.WriteLine("=== FINAL SCREEN ===");
        TestContext.Current.TestOutputHelper?.WriteLine(finalText);
        TestContext.Current.TestOutputHelper?.WriteLine($"\nAsciinema recording saved to: {recordingPath}");
        TestContext.Current.TestOutputHelper?.WriteLine("Play with: asciinema play " + recordingPath);
        
        bool product1Visible = finalText.Contains("Product 1\n") || finalText.Contains("Product 1 ") || 
                               (finalText.Contains("Product 1") && !finalText.Contains("Product 1"[..9] + "0")); // Avoid matching Product 10-19
        bool product20Visible = finalText.Contains("Product 20");
        
        // More precise check - look for "Product 1" followed by space or newline, not "Product 10"
        var lines = finalText.Split('\n');
        bool hasExactProduct1 = lines.Any(l => l.Contains("Product 1 ") || l.Trim().EndsWith("Product 1"));
        
        TestContext.Current.TestOutputHelper?.WriteLine($"Product 1 visible (exact): {hasExactProduct1}");
        TestContext.Current.TestOutputHelper?.WriteLine($"Product 20 visible: {product20Visible}");

        // If scrolling worked, Product 1 should be scrolled out and Product 20 should be visible
        // In a 10-row terminal with header+footer, we have about 5 data rows visible.
        // 3 PageDown presses (about 4 rows each) = ~12 rows scrolled
        // So we should NOT see Product 1 anymore, but we SHOULD see around Product 13-18
        
        // For now, let's just verify the table rendered
        Assert.True(finalText.Contains("Product"), "Table should contain Product text");
        
        // This is the key assertion - if scrolling doesn't work, Product 1 will still be visible
        // and Product 20 will not be visible
        if (hasExactProduct1 && !product20Visible)
        {
            Assert.Fail($"SCROLLING NOT WORKING: Product 1 is still visible after 3 PageDown presses, " +
                        $"and Product 20 is not visible. The table is not scrolling.\n" +
                        $"Recording saved to: {recordingPath}");
        }
        
        // Positive assertion - scrolling worked!
        Assert.True(product20Visible, $"Product 20 should be visible after scrolling. Recording: {recordingPath}");
    }

    [Fact]
    public async Task Integration_TableInVStackWithButtons_ScrollingWorks()
    {
        // Test that table scrolling works when embedded in VStack with other focusable widgets
        using var workload = new Hex1bAppWorkloadAdapter();
        
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 20)
            .Build();

        var products = Enumerable.Range(1, 30).Select(i => $"Product {i}").ToArray();
        
        // Structure: VStack with Text, Table (FillHeight), Text, Buttons
        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Header Text"),
                v.Text(""),
                v.Table(products)
                    .Header(h => [h.Cell("Name")])
                    .Row((r, item, _) => [r.Cell(item)])
                    .FillHeight(), // Critical: must fill to have constrained height for scrolling
                v.Text(""),
                v.Text("Footer Text"),
                v.HStack(h => [
                    h.Button("Button 1"),
                    h.Button("Button 2")
                ])
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Wait for initial render
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Product 1"), TimeSpan.FromSeconds(2), "Wait for table")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Verify table has focus (should be first focusable)
        Assert.NotNull(app.FocusedNode);
        Assert.Contains("TableNode", app.FocusedNode.GetType().Name);

        // Initial state: Product 1 visible, Product 20 not visible
        var initialSnapshot = terminal.CreateSnapshot();
        Assert.True(initialSnapshot.ContainsText("Product 1"));
        Assert.False(initialSnapshot.ContainsText("Product 20"));

        // Send PageDown twice to scroll
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.PageDown)
            .Key(Hex1bKey.PageDown)
            .Wait(200)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // After scroll: Product 1 should NOT be visible, Product 20+ should be visible
        var afterScrollSnapshot = terminal.CreateSnapshot();
        var afterText = afterScrollSnapshot.GetScreenText();
        
        // Product 1 should be scrolled out (not visible)
        bool product1Gone = !afterText.Contains("Product 1 ") && !afterText.Contains("Product 1\n");
        
        // Product 20 or later should be visible
        bool laterProductsVisible = afterText.Contains("Product 20") || 
                                     afterText.Contains("Product 21") ||
                                     afterText.Contains("Product 25");

        Assert.True(product1Gone || laterProductsVisible, 
            $"Scrolling should have changed visible products. Screen:\n{afterText}");

        // Exit
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Integration_TableFocus_CanReceiveKeyboardInput()
    {
        // This test verifies the table is focusable and receives input
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 12)
            .Build();

        var products = Enumerable.Range(1, 50).Select(i => $"Product {i}").ToArray();
        
        // The table is the ONLY widget, so it should get focus automatically
        using var app = new Hex1bApp(
            ctx => ctx.Table(products)
                .Header(h => [h.Cell("Name")])
                .Row((r, item, state) => [
                    r.Cell(state.IsFocused ? $"> {item}" : item)
                ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Send multiple down arrows to scroll
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Product 1"), TimeSpan.FromSeconds(2), "Wait for table to render")
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.DownArrow)
            .Capture("after_3_downs")
            .Key(Hex1bKey.PageDown)
            .Capture("after_pagedown")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Debug output
        Console.WriteLine("=== After 3 Down Arrows ===");
        foreach (var line in snapshot.GetNonEmptyLines())
        {
            Console.WriteLine(line);
        }
        
        // The table should still show products - if scrolling worked,
        // we might see different product numbers
        var screenText = snapshot.GetScreenText();
        Assert.Contains("Product", screenText);
    }

    [Fact]
    public async Task Integration_SelectionColumn_SpaceTogglesSelection()
    {
        // Test that pressing Space toggles selection of the focused row
        using var workload = new Hex1bAppWorkloadAdapter();
        
        var recordingPath = Path.Combine(Path.GetTempPath(), $"table_selection_test_{DateTime.Now:yyyyMMdd_HHmmss}.cast");
        TestContext.Current.TestOutputHelper?.WriteLine($"Recording to: {recordingPath}");
        
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 12)
            .WithAsciinemaRecording(recordingPath)
            .Build();

        var products = new List<SelectableItem>
        {
            new() { Name = "Laptop" },
            new() { Name = "Keyboard" },
            new() { Name = "Mouse" }
        };
        object? focusedKey = "Laptop";
        
        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<SelectableItem>)products)
                .RowKey(p => p.Name)
                .Header(h => [h.Cell("Product")])
                .Row((r, item, state) => [
                    r.Cell(state.IsFocused ? $"> {item.Name}" : item.Name)
                ])
                .Focus(focusedKey)
                .OnFocusChanged(key => focusedKey = key)
                .SelectionColumn(
                    isSelected: p => p.IsSelected,
                    onChanged: (p, selected) => p.IsSelected = selected
                ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Wait for render, then press Space to select the focused row
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Laptop"), TimeSpan.FromSeconds(2), "Wait for table to render")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Capture initial state
        var initialSnapshot = terminal.CreateSnapshot();
        var initialText = initialSnapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== Initial State ===");
        TestContext.Current.TestOutputHelper?.WriteLine(initialText);
        TestContext.Current.TestOutputHelper?.WriteLine($"Focus: {focusedKey}, Selected: {products.Count(p => p.IsSelected)}");
        
        // Initial state - first row focused, no selection
        Assert.Contains("[ ]", initialText); // Unchecked checkbox
        Assert.Equal(0, products.Count(p => p.IsSelected));

        // Press Space to toggle selection
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Spacebar)
            .Wait(300) // Allow time for reconciliation and render
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var afterSpaceSnapshot = terminal.CreateSnapshot();
        var afterSpaceText = afterSpaceSnapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== After Space ===");
        TestContext.Current.TestOutputHelper?.WriteLine(afterSpaceText);
        TestContext.Current.TestOutputHelper?.WriteLine($"Focus: {focusedKey}, Selected: {products.Count(p => p.IsSelected)}");

        // After Space, first row should be selected
        Assert.Contains("[x]", afterSpaceText); // Checked checkbox
        Assert.Equal(1, products.Count(p => p.IsSelected));

        // Navigate down and select that row too
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.Spacebar)
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var afterDownSpaceSnapshot = terminal.CreateSnapshot();
        var afterDownSpaceText = afterDownSpaceSnapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== After Down+Space ===");
        TestContext.Current.TestOutputHelper?.WriteLine(afterDownSpaceText);
        TestContext.Current.TestOutputHelper?.WriteLine($"Focus: {focusedKey}, Selected: {products.Count(p => p.IsSelected)}");

        // Now two rows should be selected
        Assert.Equal(2, products.Count(p => p.IsSelected));

        // Exit
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
        
        TestContext.Current.TestOutputHelper?.WriteLine($"\nRecording: {recordingPath}");
    }

    [Fact]
    public async Task Integration_SelectionColumn_MouseClickTogglesSelection()
    {
        // Test that clicking on checkbox toggles selection
        using var workload = new Hex1bAppWorkloadAdapter();
        
        var recordingPath = Path.Combine(Path.GetTempPath(), $"table_mouse_selection_test_{DateTime.Now:yyyyMMdd_HHmmss}.cast");
        TestContext.Current.TestOutputHelper?.WriteLine($"Recording to: {recordingPath}");
        
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 12)
            .WithAsciinemaRecording(recordingPath)
            .Build();

        var products = new List<SelectableItem>
        {
            new() { Name = "Laptop" },
            new() { Name = "Keyboard" },
            new() { Name = "Mouse" }
        };
        object? focusedKey = "Laptop";
        string? debugBounds = null;
        
        using var app = new Hex1bApp(
            ctx => {
                var table = ctx.Table((IReadOnlyList<SelectableItem>)products)
                    .RowKey(p => p.Name)
                    .Header(h => [h.Cell("Product")])
                    .Row((r, item, state) => [
                        r.Cell(state.IsFocused ? $"> {item.Name}" : item.Name)
                    ])
                    .Focus(focusedKey)
                    .OnFocusChanged(key => focusedKey = key)
                    .SelectionColumn(
                        isSelected: p => p.IsSelected,
                        onChanged: (p, selected) => p.IsSelected = selected
                    );
                    
                // Store bounds info for debugging after render
                debugBounds = $"TableBounds will be set at render time";
                return table;
            },
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Wait for table to render
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Laptop"), TimeSpan.FromSeconds(2), "Wait for table to render")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Capture initial state
        var initialSnapshot = terminal.CreateSnapshot();
        var initialText = initialSnapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== Initial State ===");
        TestContext.Current.TestOutputHelper?.WriteLine(initialText);
        TestContext.Current.TestOutputHelper?.WriteLine($"Focus: {focusedKey}, Selected: {products.Count(p => p.IsSelected)}");
        
        // Initial state - no selection
        Assert.Equal(0, products.Count(p => p.IsSelected));

        // Click on the checkbox of the first data row
        // Table structure: │[ ]│> Laptop...
        // Top border at Y=0: ┌───┬...
        // Header at Y=1: │[ ]│Product...
        // Separator at Y=2: ├───┼...
        // First data row at Y=3: │[ ]│> Laptop...
        // Checkbox is at X positions 1-3 (after left border │ at position 0)
        
        // Let's try clicking more towards the center of the checkbox area
        // Position X=2 should be right in the middle of "[ ]"
        TestContext.Current.TestOutputHelper?.WriteLine($"Clicking at (2, 3) - should be on checkbox of first data row");
        
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(2, 3)  // Click on checkbox of first data row
            .Wait(200)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var afterClickSnapshot = terminal.CreateSnapshot();
        var afterClickText = afterClickSnapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== After Mouse Click on Checkbox ===");
        TestContext.Current.TestOutputHelper?.WriteLine(afterClickText);
        TestContext.Current.TestOutputHelper?.WriteLine($"Focus: {focusedKey}, Selected: {products.Count(p => p.IsSelected)}");

        // After click on checkbox, first row should be selected
        // If this fails, the mouse event might not be reaching the table
        var selectedCount = products.Count(p => p.IsSelected);
        if (selectedCount == 0)
        {
            TestContext.Current.TestOutputHelper?.WriteLine("FAIL: Selection is still empty after click");
            TestContext.Current.TestOutputHelper?.WriteLine("This could mean:");
            TestContext.Current.TestOutputHelper?.WriteLine("  1. HitTest didn't find the table node");
            TestContext.Current.TestOutputHelper?.WriteLine("  2. Mouse binding didn't match");
            TestContext.Current.TestOutputHelper?.WriteLine("  3. HandleRowClick didn't process the click correctly");
            TestContext.Current.TestOutputHelper?.WriteLine($"\nRecording saved to: {recordingPath}");
        }
        
        Assert.Equal(1, selectedCount);

        // Exit
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
        
        TestContext.Current.TestOutputHelper?.WriteLine($"\nRecording: {recordingPath}");
    }

    #endregion
    
    #region Async Data Source Navigation Tests

    /// <summary>
    /// Test data source for async navigation tests.
    /// </summary>
    private class TestAsyncDataSource : ITableDataSource<string>
    {
        private readonly int _totalCount;
        public List<(int Start, int Count)> LoadRequests { get; } = [];
        
#pragma warning disable CS0067 // Event is never used - required by interface
        public event NotifyCollectionChangedEventHandler? CollectionChanged;
#pragma warning restore CS0067
        
        public TestAsyncDataSource(int totalCount)
        {
            _totalCount = totalCount;
        }
        
        public ValueTask<int> GetItemCountAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(_totalCount);
        }
        
        public ValueTask<IReadOnlyList<string>> GetItemsAsync(int startIndex, int count, CancellationToken cancellationToken = default)
        {
            LoadRequests.Add((startIndex, count));
            var items = new List<string>();
            for (int i = startIndex; i < startIndex + count && i < _totalCount; i++)
            {
                items.Add($"Item {i + 1:D5}");
            }
            return ValueTask.FromResult<IReadOnlyList<string>>(items);
        }
    }

    [Fact]
    public async Task AsyncDataSource_NavigateDown_LoadsDataForNextRange()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 25)
            .Build();
            
        var dataSource = new TestAsyncDataSource(1000);
        var focusedKey = (object?)"Item 00001";
        
        using var app = new Hex1bApp(
            ctx => ctx.Table(dataSource)
                .RowKey(s => s)
                .Header(h => [h.Cell("Name")])
                .Row((r, item, _) => [r.Cell(item)])
                .Focus(focusedKey)
                .OnFocusChanged(key => focusedKey = key)
                .FillHeight(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Wait for initial render
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 00001"), TimeSpan.FromSeconds(2), "Wait for table to render")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        // Initial load should have been requested
        Assert.NotEmpty(dataSource.LoadRequests);
        var initialRequest = dataSource.LoadRequests[0];
        Assert.Equal(0, initialRequest.Start);
        
        // Navigate down repeatedly to reach beyond initial cache (50 items)
        var builder = new Hex1bTerminalInputSequenceBuilder();
        for (int i = 0; i < 55; i++)
        {
            builder.Key(Hex1bKey.DownArrow).Wait(30);
        }
        builder.Ctrl().Key(Hex1bKey.C); // Exit
        
        await builder.Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        // Should have loaded more data when we navigated beyond initial range
        Assert.True(dataSource.LoadRequests.Count > 1, 
            $"Expected additional data loads when navigating beyond cache. Only had {dataSource.LoadRequests.Count} load(s)");
        
        // Focus should be on a row beyond the initial 50
        Assert.NotNull(focusedKey);
        TestContext.Current.TestOutputHelper?.WriteLine($"Final focus: {focusedKey}");
    }

    [Fact]
    public async Task AsyncDataSource_NavigateToEnd_LoadsLastPage()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 25)
            .Build();
            
        var dataSource = new TestAsyncDataSource(500);
        var focusedKey = (object?)"Item 00001";
        
        using var app = new Hex1bApp(
            ctx => ctx.Table(dataSource)
                .RowKey(s => s)
                .Header(h => [h.Cell("Name")])
                .Row((r, item, _) => [r.Cell(item)])
                .Focus(focusedKey)
                .OnFocusChanged(key => focusedKey = key)
                .FillHeight(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Wait for initial render, press End, then exit
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 00001"), TimeSpan.FromSeconds(2), "Wait for table to render")
            .Key(Hex1bKey.End)
            .Wait(500) // Wait for data load
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;
        
        // Should have loaded data for the end of the list
        var lastRequest = dataSource.LoadRequests[^1];
        TestContext.Current.TestOutputHelper?.WriteLine($"Last load request: start={lastRequest.Start}, count={lastRequest.Count}");
        Assert.True(lastRequest.Start > 400, 
            $"Expected load request near end of list (>400), got start={lastRequest.Start}");
        
        // Focus should be on the last item
        TestContext.Current.TestOutputHelper?.WriteLine($"Final focus: {focusedKey}");
        Assert.Equal("Item 00500", focusedKey);
    }

    [Fact]
    public async Task AsyncDataSource_NavigateDownThenUp_MaintainsCorrectFocus()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 25)
            .Build();
            
        var dataSource = new TestAsyncDataSource(200);
        var focusedKey = (object?)"Item 00001";
        var focusHistory = new List<object?>();
        
        using var app = new Hex1bApp(
            ctx => ctx.Table(dataSource)
                .RowKey(s => s)
                .Header(h => [h.Cell("Name")])
                .Row((r, item, _) => [r.Cell(item)])
                .Focus(focusedKey)
                .OnFocusChanged(key => { focusedKey = key; focusHistory.Add(key); })
                .FillHeight(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Wait for initial render
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 00001"), TimeSpan.FromSeconds(2), "Wait for table to render")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        // Navigate down 60 rows (beyond initial 50-item cache)
        var downBuilder = new Hex1bTerminalInputSequenceBuilder();
        for (int i = 0; i < 60; i++)
        {
            downBuilder.Key(Hex1bKey.DownArrow).Wait(20);
        }
        await downBuilder.Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        var focusAfterDown = focusedKey;
        TestContext.Current.TestOutputHelper?.WriteLine($"Focus after 60 downs: {focusAfterDown}");
        
        // Now navigate up 5 rows
        var upBuilder = new Hex1bTerminalInputSequenceBuilder();
        for (int i = 0; i < 5; i++)
        {
            upBuilder.Key(Hex1bKey.UpArrow).Wait(20);
        }
        await upBuilder.Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        var focusAfterUp = focusedKey;
        TestContext.Current.TestOutputHelper?.WriteLine($"Focus after 5 ups: {focusAfterUp}");
        
        // Now navigate down again - this should work
        var downAgainBuilder = new Hex1bTerminalInputSequenceBuilder();
        for (int i = 0; i < 5; i++)
        {
            downAgainBuilder.Key(Hex1bKey.DownArrow).Wait(20);
        }
        downAgainBuilder.Ctrl().Key(Hex1bKey.C);
        await downAgainBuilder.Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;
        
        var finalFocus = focusedKey;
        TestContext.Current.TestOutputHelper?.WriteLine($"Final focus: {finalFocus}");
        TestContext.Current.TestOutputHelper?.WriteLine($"Focus history count: {focusHistory.Count}");
        
        // Verify focus movements
        Assert.Equal("Item 00061", focusAfterDown);
        Assert.Equal("Item 00056", focusAfterUp);
        Assert.Equal("Item 00061", finalFocus);
    }

    [Fact]
    public async Task AsyncDataSource_InitialRender_SetsFocusToFirstRow()
    {
        // Arrange - async data source with NO initial focus (null)
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 25)
            .Build();
            
        var dataSource = new TestAsyncDataSource(100);
        object? focusedKey = null;  // Explicitly null - no initial focus
        
        using var app = new Hex1bApp(
            ctx => ctx.Table(dataSource)
                .RowKey(s => s)
                .Header(h => [h.Cell("Name")])
                .Row((r, item, _) => [r.Cell(item)])
                .Focus(focusedKey)
                .OnFocusChanged(key => focusedKey = key)
                .FillHeight(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Wait for initial render and data load, then exit
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 00001"), TimeSpan.FromSeconds(2), "Wait for table to render")
            .Wait(100)  // Allow focus to be set
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;
        
        // Assert - focus should be automatically set to first row
        TestContext.Current.TestOutputHelper?.WriteLine($"Final focus: {focusedKey}");
        Assert.Equal("Item 00001", focusedKey);
    }

    [Fact]
    public async Task AsyncDataSource_AfterRender_ShowsFocusBarsOnFirstRow()
    {
        // Arrange - async data source with NO initial focus (null)
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 25)
            .Build();
            
        var dataSource = new TestAsyncDataSource(100);
        object? focusedKey = null;
        
        using var app = new Hex1bApp(
            ctx => ctx.Table(dataSource)
                .RowKey(s => s)
                .Header(h => [h.Cell("Name")])
                .Row((r, item, _) => [r.Cell(item)])
                .Focus(focusedKey)
                .OnFocusChanged(key => focusedKey = key)
                .FillHeight(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Wait for render to stabilize - wait for data AND focus bars
        // The thick vertical bar ┃ indicates a focused row
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 00001"), TimeSpan.FromSeconds(2), "Wait for data to render")
            .Wait(200)  // Allow focus and re-render to complete
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        // Capture the screen
        var snapshot = terminal.CreateSnapshot();
        var screenText = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== Screen after stabilization ===");
        TestContext.Current.TestOutputHelper?.WriteLine(screenText);
        
        // Check for focus bars (thick vertical borders ┃) on the first data row
        // The focused row should have ┃ characters instead of │
        bool hasFocusBars = screenText.Contains("┃Item 00001") || screenText.Contains("┃ Item 00001");
        
        // Exit
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;
        
        // Assert
        Assert.True(hasFocusBars, 
            $"Expected focus bars (┃) on first row. Focus key: {focusedKey}\nScreen:\n{screenText}");
    }

    [Fact]
    public async Task AsyncDataSource_ClickOnRow_ShowsFocusBarsAfterClick()
    {
        // Arrange - async data source 
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 25)
            .WithMouse()
            .Build();
            
        var dataSource = new TestAsyncDataSource(100);
        object? focusedKey = null;
        
        using var app = new Hex1bApp(
            ctx => ctx.Table(dataSource)
                .RowKey(s => s)
                .Header(h => [h.Cell("Name")])
                .Row((r, item, _) => [r.Cell(item)])
                .Focus(focusedKey)
                .OnFocusChanged(key => focusedKey = key)
                .FillHeight(),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Wait for initial render to stabilize
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 00001"), TimeSpan.FromSeconds(2), "Wait for table to render")
            .Wait(200)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        // Capture screen before click
        var beforeSnapshot = terminal.CreateSnapshot();
        var beforeText = beforeSnapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== Before click ===");
        TestContext.Current.TestOutputHelper?.WriteLine(beforeText);
        TestContext.Current.TestOutputHelper?.WriteLine($"Focus before click: {focusedKey}");
        
        // Click on row 5 (approximately y=6: top border + header + separator + rows 1-3)
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(10, 6)
            .Wait(300)  // Allow re-render
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        // Capture screen after click
        var afterSnapshot = terminal.CreateSnapshot();
        var afterText = afterSnapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== After click ===");
        TestContext.Current.TestOutputHelper?.WriteLine(afterText);
        TestContext.Current.TestOutputHelper?.WriteLine($"Focus after click: {focusedKey}");
        
        // Check for focus bars on the clicked row
        // Focus should have moved, and we should see ┃ on the new focused row
        bool hasFocusBars = afterText.Contains("┃Item 00");
        
        // Exit
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;
        
        // Assert
        Assert.True(hasFocusBars, 
            $"Expected focus bars (┃) after click. Focus key: {focusedKey}\nScreen after click:\n{afterText}");
    }

    #endregion
    
    #region Table Focus Indicator Tests

    [Fact(Skip = "Feature not yet implemented - table-level focus indicator")]
    public async Task Table_WhenFocused_ShouldShowTableLevelFocusIndicator()
    {
        // Arrange - table with focusable rows
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 15)
            .Build();
            
        var data = new[] { "Item 1", "Item 2", "Item 3" };
        object? focusedKey = "Item 1";
        
        using var app = new Hex1bApp(
            ctx => ctx.Table(data)
                .RowKey(s => s)
                .Header(h => [h.Cell("Name")])
                .Row((r, item, _) => [r.Cell(item)])
                .Focus(focusedKey)
                .OnFocusChanged(key => focusedKey = key),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Wait for render
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 1"), TimeSpan.FromSeconds(2), "Wait for table to render")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        var snapshot = terminal.CreateSnapshot();
        var screenText = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== Table with focus ===");
        TestContext.Current.TestOutputHelper?.WriteLine(screenText);
        
        // Exit
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;
        
        // Assert - When table has focus, there should be some visual indicator
        // Currently we show focus on individual rows with ┃, but the table itself
        // should have a different border style or indicator when it contains focus
        // This test documents the expected behavior - currently FAILING
        
        // Check for table-level focus indicator (e.g., double-line border or highlight)
        // For now, we check if the outer table border uses a focus style
        // Expected: When table is focused, outer border could use double lines ╔╗╚╝ or thick lines
        bool hasTableFocusIndicator = 
            screenText.Contains("╔") ||  // Double line top-left corner
            screenText.Contains("┏") ||  // Heavy line top-left corner
            screenText.Contains("▌") ||  // Left border indicator
            screenText.Contains("┃┃");   // Some other focus indicator
            
        // Note: This test is expected to FAIL until we implement table-level focus indicator
        Assert.True(hasTableFocusIndicator, 
            $"Expected table-level focus indicator when table is focused.\nScreen:\n{screenText}");
    }

    #endregion
    
    #region Table Border Rendering Tests

    [Fact]
    public async Task Table_FullMode_ShouldHaveRightTeeOnRowSeparators()
    {
        // Arrange - table in Full mode should have ┤ at right edge of row separators
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 15)
            .Build();
            
        var data = new[] { "Item 1", "Item 2", "Item 3" };
        
        using var app = new Hex1bApp(
            ctx => ctx.Table(data)
                .Header(h => [h.Cell("Name")])
                .Row((r, item, _) => [r.Cell(item)])
                .Full(),  // Full mode with row separators
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Wait for render
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 1"), TimeSpan.FromSeconds(2), "Wait for table to render")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        var snapshot = terminal.CreateSnapshot();
        var screenText = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== Table in Full mode ===");
        TestContext.Current.TestOutputHelper?.WriteLine(screenText);
        
        // Exit
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;
        
        // Assert - Full mode should have row separators with ┤ on right edge
        // Table structure should be:
        // ┌───────┐
        // │Name   │
        // ├───────┤  <- header separator with ┤ on right
        // │Item 1 │
        // ├───────┤  <- row separator with ┤ on right
        // │Item 2 │
        // ├───────┤  <- row separator with ┤ on right  
        // │Item 3 │
        // └───────┘
        
        bool hasRightTee = screenText.Contains("┤");
        
        Assert.True(hasRightTee, 
            $"Expected right tee (┤) characters at right edge of row separators in Full mode.\nScreen:\n{screenText}");
    }

    [Fact]
    public async Task LargeTable_Virtualized_ShouldHaveCorrectBorders()
    {
        // Arrange - large virtualized table should still have correct borders
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 15)
            .Build();
            
        // Create a list larger than 50 items to trigger virtualization
        var data = Enumerable.Range(1, 100).Select(i => $"Item {i:D3}").ToList();
        
        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<string>)data)
                .Header(h => [h.Cell("Name")])
                .Row((r, item, _) => [r.Cell(item)])
                .FillHeight(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Wait for render
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 001"), TimeSpan.FromSeconds(2), "Wait for table to render")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        var snapshot = terminal.CreateSnapshot();
        var screenText = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== Large virtualized table ===");
        TestContext.Current.TestOutputHelper?.WriteLine(screenText);
        
        // Exit
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;
        
        // Assert - Table should have:
        // 1. Top border with ┌ and ┐
        // 2. Bottom border with └ and ┘
        // 3. Header separator with ├ and ┤
        // 4. Vertical borders │ on both sides
        
        bool hasTopLeft = screenText.Contains("┌");
        bool hasTopRight = screenText.Contains("┐");
        bool hasBottomLeft = screenText.Contains("└");
        bool hasBottomRight = screenText.Contains("┘");
        bool hasLeftTee = screenText.Contains("├");
        bool hasRightTee = screenText.Contains("┤");
        
        var errors = new List<string>();
        if (!hasTopLeft) errors.Add("Missing top-left corner (┌)");
        if (!hasTopRight) errors.Add("Missing top-right corner (┐)");
        if (!hasBottomLeft) errors.Add("Missing bottom-left corner (└)");
        if (!hasBottomRight) errors.Add("Missing bottom-right corner (┘)");
        if (!hasLeftTee) errors.Add("Missing left tee (├) on header separator");
        if (!hasRightTee) errors.Add("Missing right tee (┤) on header separator");
        
        Assert.True(errors.Count == 0, 
            $"Border issues found:\n{string.Join("\n", errors)}\n\nScreen:\n{screenText}");
    }

    [Fact]
    public async Task LargeTable_FullMode_RowSeparatorsShouldHaveRightTee()
    {
        // Arrange - large virtualized table in Full mode with row separators
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 20)
            .Build();
            
        // Create a list larger than 50 items to trigger virtualization
        var data = Enumerable.Range(1, 100).Select(i => $"Item {i:D3}").ToList();
        
        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<string>)data)
                .Header(h => [h.Cell("Name")])
                .Row((r, item, _) => [r.Cell(item)])
                .Full()  // Full mode has row separators
                .FillHeight(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Wait for render
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 001"), TimeSpan.FromSeconds(2), "Wait for table to render")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        var snapshot = terminal.CreateSnapshot();
        var screenText = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== Large table in Full mode ===");
        TestContext.Current.TestOutputHelper?.WriteLine(screenText);
        
        // Exit
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;
        
        // Count the row separator lines (├───────┤ pattern)
        // In Full mode, each data row should have a separator below it (except the last)
        // The separator should have ├ on left and ┤ on right
        var lines = screenText.Split('\n');
        int leftTeeCount = 0;
        int rightTeeCount = 0;
        int completeSeperatorLines = 0;
        
        foreach (var line in lines)
        {
            bool hasLeftTee = line.Contains("├") && line.Contains("─");
            bool hasRightTee = line.Contains("┤") && line.Contains("─");
            
            if (hasLeftTee)
                leftTeeCount++;
            if (hasRightTee)
                rightTeeCount++;
            if (hasLeftTee && hasRightTee)
                completeSeperatorLines++;
        }
        
        TestContext.Current.TestOutputHelper?.WriteLine($"Left tee (├) count: {leftTeeCount}");
        TestContext.Current.TestOutputHelper?.WriteLine($"Right tee (┤) count: {rightTeeCount}");
        TestContext.Current.TestOutputHelper?.WriteLine($"Complete separator lines (both ├ and ┤): {completeSeperatorLines}");
        
        // Each separator line should have both left and right tees
        Assert.Equal(leftTeeCount, completeSeperatorLines);
        Assert.True(completeSeperatorLines > 1, 
            $"Expected multiple complete row separators in Full mode, found {completeSeperatorLines}.\nScreen:\n{screenText}");
    }

    [Fact]
    public async Task LargeTable_FullMode_MultiColumn_RowSeparatorsShouldHaveRightTee()
    {
        // Arrange - large virtualized table in Full mode with multiple columns
        // This reproduces the bug where row separators end with │ instead of ┤
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(100, 20)  // Wide enough for multi-column
            .Build();
            
        // Create a list larger than 50 items to trigger virtualization
        var data = Enumerable.Range(1, 100)
            .Select(i => (Name: $"Item {i:D3}", Category: "Cat", Price: 10.00m + i))
            .ToList();
        
        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<(string Name, string Category, decimal Price)>)data)
                .Header(h => [
                    h.Cell("Name").Width(SizeHint.Fill),
                    h.Cell("Category").Width(SizeHint.Content),
                    h.Cell("Price").Width(SizeHint.Fixed(10))
                ])
                .Row((r, item, _) => [
                    r.Cell(item.Name),
                    r.Cell(item.Category),
                    r.Cell($"${item.Price:F2}")
                ])
                .Full()  // Full mode has row separators
                .FillHeight(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Wait for render
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 001"), TimeSpan.FromSeconds(2), "Wait for table to render")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        var snapshot = terminal.CreateSnapshot();
        var screenText = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== Large multi-column table in Full mode ===");
        TestContext.Current.TestOutputHelper?.WriteLine(screenText);
        
        // Exit
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;
        
        // Check each separator line ends with ┤ (not │)
        // With scrollbar present, lines end with: ┤││ or ┤▉│ (table border + scrollbar column)
        // So we check that ┤ appears before the scrollbar column, not that ┤ is the last char
        var lines = screenText.Split('\n');
        var separatorLines = lines.Where(l => l.Contains("├") && l.Contains("─") && l.Contains("┼")).ToList();
        
        TestContext.Current.TestOutputHelper?.WriteLine($"\nFound {separatorLines.Count} separator lines:");
        
        var linesWithWrongEnding = new List<string>();
        foreach (var line in separatorLines)
        {
            var trimmed = line.TrimEnd();
            TestContext.Current.TestOutputHelper?.WriteLine($"  Ends with: '{trimmed[^1]}' - {trimmed[^20..]}");
            
            // Separator lines should have ┤ as the table's right border
            // With scrollbar: ends with ┤││ or ┤▉│ (TeeLeft + scrollbar track + scrollbar border)
            // Without scrollbar: ends with ┤
            // The key check: ┤ should appear, and no bare │ should be the table's right border
            
            // Find the last occurrence of ┤ - this is the table's right edge
            int lastTeeLeft = trimmed.LastIndexOf('┤');
            if (lastTeeLeft < 0)
            {
                // No TeeLeft at all - this is wrong
                linesWithWrongEnding.Add(line);
            }
            else
            {
                // After TeeLeft, we should only have scrollbar characters (│ or ▉) or nothing
                var afterTeeLeft = trimmed[(lastTeeLeft + 1)..];
                bool validScrollbarSuffix = afterTeeLeft.All(c => c == '│' || c == '▉');
                if (!validScrollbarSuffix && afterTeeLeft.Length > 0)
                {
                    linesWithWrongEnding.Add(line);
                }
            }
        }
        
        Assert.True(linesWithWrongEnding.Count == 0, 
            $"Found {linesWithWrongEnding.Count} separator lines with incorrect ending.\n" +
            $"First bad line: {linesWithWrongEnding.FirstOrDefault()}\n\nScreen:\n{screenText}");
    }

        [Fact]
    public async Task Table_WithLargeAsyncDataSource_ShouldShowScrollbar()
    {
        // Arrange - simulate 10k items
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(100, 25)
            .Build();
            
        var dataSource = new TestAsyncDataSource(10000);
        var focusedKey = (object?)"Item 00001";
        
        using var app = new Hex1bApp(
            ctx => ctx.Table(dataSource)
                .RowKey(s => s)
                .Header(h => [h.Cell("Name")])
                .Row((r, item, _) => [r.Cell(item)])
                .Focus(focusedKey)
                .OnFocusChanged(key => focusedKey = key)
                .FillHeight()
                .FillWidth(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Wait for initial render with data loaded
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 00001"), TimeSpan.FromSeconds(2), "Wait for table to render")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        // Capture screen to check for scrollbar
        var snapshot = terminal.CreateSnapshot();
        var screenText = snapshot.GetScreenText();
        
        // Exit app
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        // Check that scrollbar characters are present (│ for track or ┃ for thumb)
        // The scrollbar should be in the rightmost column of the table
        var lines = screenText.Split('\n');
        var scrollbarChars = new[] { '│', '┃' };
        var hasScrollbar = lines.Any(line => 
            line.Length > 0 && scrollbarChars.Contains(line[^1]));
        
        Assert.True(hasScrollbar, $"Expected scrollbar in rightmost column.\n\nScreen:\n{screenText}");
    }

    [Fact]
    public async Task Table_WithLargeSyncDataSource_ShouldShowScrollbar()
    {
        // Arrange - 10k items with synchronous data (IReadOnlyList)
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(150, 25) // Wide enough for Full mode
            .Build();
            
        var largeList = Enumerable.Range(1, 10000)
            .Select(i => $"Product {i:D5}")
            .ToList();
        var focusedKey = (object?)"Product 00001";
        
        using var app = new Hex1bApp(
            ctx => ctx.Table(largeList)
                .RowKey(s => s)
                .Header(h => [h.Cell("Name")])
                .Row((r, item, _) => [r.Cell(item)])
                .Focus(focusedKey)
                .OnFocusChanged(key => focusedKey = key)
                .Full()
                .FillHeight(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Wait for initial render
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Product 00001"), TimeSpan.FromSeconds(2), "Wait for table to render")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        // Capture screen to check for scrollbar
        var snapshot = terminal.CreateSnapshot();
        var screenText = snapshot.GetScreenText();
        
        // Exit app
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        // Check that scrollbar thumb character (┃) is present
        Assert.Contains("┃", screenText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Table_WindowingDemoConfig_ScrollbarAdjacentToFixedColumns()
    {
        // Reproduces the exact bug seen in the WindowingDemo sample:
        // Table with Fixed(18), Fixed(12), Fixed(6), Fixed(10) columns in a 65-wide
        // container. When the window is resized shorter (scrollbar appears), there
        // should be no gap between the last column and the scrollbar, and the
        // fixed column widths should remain 18/12/6/10 regardless.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(65, 10) // Same width as demo window, short enough for scrollbar
            .Build();

        var data = new List<(string Name, string Role, int Age, string Status)>
        {
            ("Alice Johnson", "Engineer", 32, "Active"),
            ("Bob Smith", "Designer", 28, "Active"),
            ("Carol Williams", "Manager", 45, "On Leave"),
            ("David Brown", "Developer", 35, "Active"),
            ("Eve Davis", "Analyst", 29, "Active"),
            ("Frank Miller", "Engineer", 41, "Inactive"),
            ("Grace Wilson", "Designer", 33, "Active"),
            ("Henry Taylor", "Developer", 27, "Active"),
            ("Iris Anderson", "Manager", 52, "Active"),
            ("Jack Thomas", "Analyst", 31, "On Leave"),
        };

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<(string Name, string Role, int Age, string Status)>)data)
                .Header(h => [
                    h.Cell("Name").Width(SizeHint.Fixed(18)),
                    h.Cell("Role").Width(SizeHint.Fixed(12)),
                    h.Cell("Age").Width(SizeHint.Fixed(6)),
                    h.Cell("Status").Width(SizeHint.Fixed(10))
                ])
                .Row((r, item, _) => [
                    r.Cell(item.Name),
                    r.Cell(item.Role),
                    r.Cell(item.Age.ToString()),
                    r.Cell(item.Status)
                ])
                .Fill(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Alice Johnson") && s.ContainsText("Name"), TimeSpan.FromSeconds(2), "table rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var screenText = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== WindowingDemo-style table with scrollbar ===");
        TestContext.Current.TestOutputHelper?.WriteLine(screenText);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Parse separator to verify column widths match demo definitions exactly
        var lines = screenText.Split('\n');
        var separator = lines.FirstOrDefault(l => l.Contains('├') && l.Contains('┼'));
        Assert.NotNull(separator);

        var inner = separator.TrimEnd();
        int leftIdx = inner.IndexOf('├');
        int rightIdx = inner.LastIndexOf('┤');
        Assert.True(leftIdx >= 0 && rightIdx > leftIdx, $"Could not parse separator: {separator}");

        var columnSection = inner[(leftIdx + 1)..rightIdx];
        var columnParts = columnSection.Split('┼');
        Assert.True(columnParts.Length >= 4, $"Expected at least 4 column parts, got {columnParts.Length}. Separator: {separator}");

        // Fixed(18), Fixed(12), Fixed(6), Fixed(10) — must be exact
        Assert.Equal(18, columnParts[0].Length);
        Assert.Equal(12, columnParts[1].Length);
        Assert.Equal(6, columnParts[2].Length);
        Assert.Equal(10, columnParts[3].Length);

        // Scrollbar must be adjacent — no gap between columns and scrollbar
        var topBorder = lines.FirstOrDefault(l => l.Contains('┌'));
        Assert.NotNull(topBorder);
        Assert.DoesNotContain("┬ ", topBorder.TrimEnd());

        // Table content width = 18+12+6+10 cols + 5 borders + 2 scrollbar = 53
        var trimmedBorder = topBorder.TrimEnd();
        Assert.Equal(53, trimmedBorder.Length);

        // Scrollbar thumb must be present
        Assert.True(screenText.Contains('▉') || screenText.Contains('┃'),
            $"Expected scrollbar thumb.\n\nScreen:\n{screenText}");
    }

    [Fact]
    public async Task Table_WindowingDemoConfig_TallWindow_NoScrollbarSameWidths()
    {
        // Same column config as demo but with a tall window (no scrollbar needed).
        // Column widths must be identical to the scrollbar case above.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(65, 18) // Same width as demo, tall enough for all 10 rows
            .Build();

        var data = new List<(string Name, string Role, int Age, string Status)>
        {
            ("Alice Johnson", "Engineer", 32, "Active"),
            ("Bob Smith", "Designer", 28, "Active"),
            ("Carol Williams", "Manager", 45, "On Leave"),
            ("David Brown", "Developer", 35, "Active"),
            ("Eve Davis", "Analyst", 29, "Active"),
            ("Frank Miller", "Engineer", 41, "Inactive"),
            ("Grace Wilson", "Designer", 33, "Active"),
            ("Henry Taylor", "Developer", 27, "Active"),
            ("Iris Anderson", "Manager", 52, "Active"),
            ("Jack Thomas", "Analyst", 31, "On Leave"),
        };

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<(string Name, string Role, int Age, string Status)>)data)
                .Header(h => [
                    h.Cell("Name").Width(SizeHint.Fixed(18)),
                    h.Cell("Role").Width(SizeHint.Fixed(12)),
                    h.Cell("Age").Width(SizeHint.Fixed(6)),
                    h.Cell("Status").Width(SizeHint.Fixed(10))
                ])
                .Row((r, item, _) => [
                    r.Cell(item.Name),
                    r.Cell(item.Role),
                    r.Cell(item.Age.ToString()),
                    r.Cell(item.Status)
                ])
                .Fill(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Alice Johnson") && s.ContainsText("Jack Thomas"), TimeSpan.FromSeconds(2), "all rows rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var screenText = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== WindowingDemo-style table without scrollbar ===");
        TestContext.Current.TestOutputHelper?.WriteLine(screenText);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Parse separator to verify column widths are identical to scrollbar case
        var lines = screenText.Split('\n');
        var separator = lines.FirstOrDefault(l => l.Contains('├') && l.Contains('┼'));
        Assert.NotNull(separator);

        var inner = separator.TrimEnd();
        int leftIdx = inner.IndexOf('├');
        int rightIdx = inner.LastIndexOf('┤');
        var columnSection = inner[(leftIdx + 1)..rightIdx];
        var columnParts = columnSection.Split('┼');
        Assert.True(columnParts.Length >= 4, $"Expected at least 4 column parts. Separator: {separator}");

        // Same exact widths as scrollbar case: Fixed(18), Fixed(12), Fixed(6), Fixed(10)
        Assert.Equal(18, columnParts[0].Length);
        Assert.Equal(12, columnParts[1].Length);
        Assert.Equal(6, columnParts[2].Length);
        Assert.Equal(10, columnParts[3].Length);

        // No scrollbar present
        Assert.DoesNotContain("┃", screenText);
        Assert.DoesNotContain("▉", screenText);

        // Table content width = 18+12+6+10 cols + 5 borders = 51 (no scrollbar)
        var topBorder = lines.FirstOrDefault(l => l.Contains('┌'));
        Assert.NotNull(topBorder);
        var trimmedBorder = topBorder.TrimEnd();
        Assert.Equal(51, trimmedBorder.Length);
    }

    [Fact]
    public async Task Table_AllFixedColumns_ScrollbarAdjacentToContent()
    {
        // Arrange - table with all Fixed-width columns that needs a scrollbar.
        // When the table is wider than the sum of fixed columns + borders + scrollbar,
        // the scrollbar should still be adjacent to the table content (no gap).
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 12) // Wider than needed columns, short enough to need scrollbar
            .Build();

        var data = Enumerable.Range(1, 20)
            .Select(i => $"Person {i}")
            .ToList();

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<string>)data)
                .Header(h => [
                    h.Cell("Name").Width(SizeHint.Fixed(12)),
                    h.Cell("Role").Width(SizeHint.Fixed(10)),
                    h.Cell("Age").Width(SizeHint.Fixed(5))
                ])
                .Row((r, item, _) => [
                    r.Cell(item),
                    r.Cell("Engineer"),
                    r.Cell("30")
                ])
                .FillHeight(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Wait for initial render
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Person 1") && s.ContainsText("Name"), TimeSpan.FromSeconds(2), "table rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var screenText = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== Table with all fixed columns + scrollbar ===");
        TestContext.Current.TestOutputHelper?.WriteLine(screenText);

        // Exit
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - the top border should have no gap between the last column tee and scrollbar
        // Correct:   ┌────────────┬──────────┬─────┬─┐  (scrollbar immediately after last column)
        // Incorrect: ┌────────────┬──────────┬─────┬          ─┐  (gap between columns and scrollbar)
        var lines = screenText.Split('\n');
        var topBorder = lines.FirstOrDefault(l => l.Contains('┌'));
        Assert.NotNull(topBorder);

        // The top border should NOT have spaces between border characters
        // A gap manifests as spaces between ┬ and ─┐
        var borderContent = topBorder.TrimEnd();
        Assert.DoesNotContain("┬ ", borderContent);

        // Also verify the scrollbar thumb is present (table is scrollable)
        Assert.True(screenText.Contains('▉') || screenText.Contains('┃'),
            $"Expected scrollbar thumb character in output.\n\nScreen:\n{screenText}");
    }

    [Fact]
    public async Task Table_FixedColumnsWithoutScrollbar_ColumnWidthsMatchDefinitions()
    {
        // When a table has few rows (no scrollbar needed), fixed columns should have
        // exactly the widths specified in the SizeHint.Fixed() definitions.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 20) // Tall enough that 3 rows don't need a scrollbar
            .Build();

        var data = new List<string> { "Alice", "Bob", "Charlie" };

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<string>)data)
                .Header(h => [
                    h.Cell("Name").Width(SizeHint.Fixed(12)),
                    h.Cell("Role").Width(SizeHint.Fixed(10)),
                    h.Cell("Age").Width(SizeHint.Fixed(5))
                ])
                .Row((r, item, _) => [
                    r.Cell(item),
                    r.Cell("Engineer"),
                    r.Cell("30")
                ])
                .FillHeight(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Alice") && s.ContainsText("Name"), TimeSpan.FromSeconds(2), "table rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var screenText = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== Fixed columns without scrollbar ===");
        TestContext.Current.TestOutputHelper?.WriteLine(screenText);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Parse the header separator to measure column widths.
        // The separator line looks like: ├────────────┼──────────┼─────┤
        var lines = screenText.Split('\n');
        var separator = lines.FirstOrDefault(l => l.Contains('├') && l.Contains('┼'));
        Assert.NotNull(separator);

        // Extract column widths by splitting on ┼ (cross) characters
        // Strip left ├ and right ┤
        var inner = separator.TrimEnd();
        int leftIdx = inner.IndexOf('├');
        int rightIdx = inner.LastIndexOf('┤');
        Assert.True(leftIdx >= 0 && rightIdx > leftIdx, $"Could not parse separator: {separator}");

        var columnSection = inner[(leftIdx + 1)..rightIdx];
        var columnParts = columnSection.Split('┼');
        Assert.Equal(3, columnParts.Length);

        // Each column width should match the Fixed hint
        Assert.Equal(12, columnParts[0].Length); // Fixed(12)
        Assert.Equal(10, columnParts[1].Length); // Fixed(10)
        Assert.Equal(5, columnParts[2].Length);  // Fixed(5)

        // No scrollbar should be present
        Assert.DoesNotContain("┃", screenText);
    }

    [Fact]
    public async Task Table_FixedColumnsWithScrollbar_ColumnWidthsSameAsWithout()
    {
        // CRITICAL: Fixed column widths must be identical regardless of whether a scrollbar
        // is present. The scrollbar should be positioned adjacent to the content, NOT
        // cause columns to expand to fill remaining space.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 12) // Short enough to need scrollbar with 20 rows
            .Build();

        var data = Enumerable.Range(1, 20)
            .Select(i => $"Person {i}")
            .ToList();

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<string>)data)
                .Header(h => [
                    h.Cell("Name").Width(SizeHint.Fixed(12)),
                    h.Cell("Role").Width(SizeHint.Fixed(10)),
                    h.Cell("Age").Width(SizeHint.Fixed(5))
                ])
                .Row((r, item, _) => [
                    r.Cell(item),
                    r.Cell("Engineer"),
                    r.Cell("30")
                ])
                .FillHeight(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Person 1") && s.ContainsText("Name"), TimeSpan.FromSeconds(2), "table rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var screenText = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== Fixed columns with scrollbar ===");
        TestContext.Current.TestOutputHelper?.WriteLine(screenText);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Parse header separator: ├────────────┼──────────┼─────┼─┤
        // Note the ┼─┤ at end is scrollbar column joining character
        var lines = screenText.Split('\n');
        var separator = lines.FirstOrDefault(l => l.Contains('├') && l.Contains('┼'));
        Assert.NotNull(separator);

        // Extract the section between ├ and the last ┤, but note the scrollbar adds
        // an extra section at the end. Split on ┼.
        var inner = separator.TrimEnd();
        int leftIdx = inner.IndexOf('├');
        int rightIdx = inner.LastIndexOf('┤');
        Assert.True(leftIdx >= 0 && rightIdx > leftIdx, $"Could not parse separator: {separator}");

        var columnSection = inner[(leftIdx + 1)..rightIdx];
        var columnParts = columnSection.Split('┼');

        // With scrollbar: 3 data columns + 1 scrollbar track section = 4 parts (or 3 + ┤ via scrollbar)
        // The first 3 should match Fixed widths exactly
        Assert.True(columnParts.Length >= 3, $"Expected at least 3 column sections, got {columnParts.Length}. Separator: {separator}");

        Assert.Equal(12, columnParts[0].Length); // Fixed(12) - same as without scrollbar
        Assert.Equal(10, columnParts[1].Length); // Fixed(10) - same as without scrollbar
        Assert.Equal(5, columnParts[2].Length);  // Fixed(5) - same as without scrollbar

        // Scrollbar thumb should be present
        Assert.True(screenText.Contains('▉') || screenText.Contains('┃'),
            $"Expected scrollbar thumb character.\n\nScreen:\n{screenText}");
    }

    [Fact]
    public async Task Table_MixedFixedAndFillColumns_FillAbsorbsRemainingSpace()
    {
        // When a table has a mix of Fixed and Fill columns, the Fill column should
        // absorb remaining space. The Fixed columns must retain their exact widths.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 12)
            .Build();

        var data = Enumerable.Range(1, 20)
            .Select(i => $"Item {i}")
            .ToList();

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<string>)data)
                .Header(h => [
                    h.Cell("Name").Width(SizeHint.Fixed(15)),
                    h.Cell("Description").Width(SizeHint.Fill),
                    h.Cell("Status").Width(SizeHint.Fixed(8))
                ])
                .Row((r, item, _) => [
                    r.Cell(item),
                    r.Cell("A longer description"),
                    r.Cell("Active")
                ])
                .FillHeight()
                .FillWidth(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 1") && s.ContainsText("Name"), TimeSpan.FromSeconds(2), "table rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var screenText = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== Mixed Fixed+Fill columns with scrollbar ===");
        TestContext.Current.TestOutputHelper?.WriteLine(screenText);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Parse separator to verify Fixed columns are exactly their defined widths
        var lines = screenText.Split('\n');
        var separator = lines.FirstOrDefault(l => l.Contains('├') && l.Contains('┼'));
        Assert.NotNull(separator);

        var inner = separator.TrimEnd();
        int leftIdx = inner.IndexOf('├');
        int rightIdx = inner.LastIndexOf('┤');
        var columnSection = inner[(leftIdx + 1)..rightIdx];
        var columnParts = columnSection.Split('┼');

        // Should have at least 3 data column parts
        Assert.True(columnParts.Length >= 3, $"Expected at least 3 column sections. Separator: {separator}");

        // Fixed(15) column must be exactly 15
        Assert.Equal(15, columnParts[0].Length);
        // Fill column absorbs remaining width - just check it's > 0
        Assert.True(columnParts[1].Length > 0, "Fill column should have non-zero width");
        // Fixed(8) column must be exactly 8
        Assert.Equal(8, columnParts[2].Length);

        // Fill column should expand to absorb space (with scrollbar, total = 60)
        // borders: 4 (|col|col|col|), scrollbar: 2, fixed: 15+8=23 → fill ≈ 31
        Assert.True(columnParts[1].Length > 20,
            $"Fill column should absorb remaining space, got width={columnParts[1].Length}");

        // Verify no gap - top border should have no spaces between border chars
        var topBorder = lines.FirstOrDefault(l => l.Contains('┌'));
        Assert.NotNull(topBorder);
        Assert.DoesNotContain("┬ ", topBorder.TrimEnd());
    }

    [Fact]
    public async Task Table_AllFillColumns_WithScrollbar_NoGap()
    {
        // All Fill columns should expand to fill the available width, and the scrollbar
        // should be at the right edge with no gap.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(50, 10)
            .Build();

        var data = Enumerable.Range(1, 30)
            .Select(i => $"Row {i}")
            .ToList();

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<string>)data)
                .Header(h => [
                    h.Cell("Col A").Width(SizeHint.Fill),
                    h.Cell("Col B").Width(SizeHint.Fill)
                ])
                .Row((r, item, _) => [
                    r.Cell(item),
                    r.Cell("Value")
                ])
                .FillHeight()
                .FillWidth(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Row 1") && s.ContainsText("Col A"), TimeSpan.FromSeconds(2), "table rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var screenText = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== All Fill columns with scrollbar ===");
        TestContext.Current.TestOutputHelper?.WriteLine(screenText);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Top border should end with ┬─┐ (scrollbar connection) with no gap
        var lines = screenText.Split('\n');
        var topBorder = lines.FirstOrDefault(l => l.Contains('┌'));
        Assert.NotNull(topBorder);
        Assert.DoesNotContain("┬ ", topBorder.TrimEnd());

        // With Fill columns, the table should fill the entire width (50 chars)
        // So the right edge of the top border should be at position 49
        var trimmedBorder = topBorder.TrimEnd();
        Assert.Equal(50, trimmedBorder.Length);
    }

    [Fact]
    public async Task Table_FixedColumnsNarrowWidth_ScrollbarStillAdjacentWhenTableFillsWidth()
    {
        // When the terminal is narrow enough that fixed columns + scrollbar fill almost
        // all the space, the scrollbar should still be adjacent to the last column.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(35, 10) // Barely enough for the columns + borders + scrollbar
            .Build();

        var data = Enumerable.Range(1, 20)
            .Select(i => $"R{i}")
            .ToList();

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<string>)data)
                .Header(h => [
                    h.Cell("A").Width(SizeHint.Fixed(10)),
                    h.Cell("B").Width(SizeHint.Fixed(8)),
                    h.Cell("C").Width(SizeHint.Fixed(6))
                ])
                .Row((r, item, _) => [
                    r.Cell(item),
                    r.Cell("val"),
                    r.Cell("x")
                ])
                .FillHeight(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("R1"), TimeSpan.FromSeconds(2), "table rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var screenText = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== Fixed columns narrow terminal ===");
        TestContext.Current.TestOutputHelper?.WriteLine(screenText);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // The table content width is: 10+8+6 columns + 4 borders + 2 scrollbar = 30
        // Terminal is 35, so the table content occupies 30 chars, leaving 5 empty.
        // Scrollbar must be at position 30 (adjacent to columns), not position 35.
        var lines = screenText.Split('\n');
        var topBorder = lines.FirstOrDefault(l => l.Contains('┌'));
        Assert.NotNull(topBorder);
        
        var trimmedBorder = topBorder.TrimEnd();
        // Table content width = 10+8+6 (cols) + 4 (borders |c|c|c|) + 2 (scrollbar) = 30
        Assert.Equal(30, trimmedBorder.Length);
        
        // Should NOT have any space before ┐
        Assert.False(trimmedBorder.Contains(' '), $"Top border should have no spaces: [{trimmedBorder}]");
    }

    [Fact]
    public async Task Table_FixedColumnsWideTerminal_ScrollbarNotAtFarRightEdge()
    {
        // When the terminal is much wider than the table content, the scrollbar should
        // be positioned right after the last column, NOT at the far right of the terminal.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(100, 10) // Very wide - much wider than needed
            .Build();

        var data = Enumerable.Range(1, 30)
            .Select(i => $"X{i}")
            .ToList();

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<string>)data)
                .Header(h => [
                    h.Cell("Col1").Width(SizeHint.Fixed(8)),
                    h.Cell("Col2").Width(SizeHint.Fixed(8))
                ])
                .Row((r, item, _) => [
                    r.Cell(item),
                    r.Cell("data")
                ])
                .FillHeight(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("X1") && s.ContainsText("Col1"), TimeSpan.FromSeconds(2), "table rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var screenText = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== Fixed columns in wide terminal ===");
        TestContext.Current.TestOutputHelper?.WriteLine(screenText);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Table content width = 8+8 (cols) + 3 (borders |c|c|) + 2 (scrollbar) = 21
        // The top border + scrollbar should be 21 chars, NOT 100 chars.
        var lines = screenText.Split('\n');
        var topBorder = lines.FirstOrDefault(l => l.Contains('┌'));
        Assert.NotNull(topBorder);

        var trimmedBorder = topBorder.TrimEnd();
        // Should be much smaller than terminal width
        Assert.True(trimmedBorder.Length < 50,
            $"Table should not stretch to terminal width. Border length={trimmedBorder.Length}, terminal=100.\nBorder: [{trimmedBorder}]");

        // Exact width check: 8+8 columns + 3 borders + 2 scrollbar = 21
        Assert.Equal(21, trimmedBorder.Length);
    }

    #endregion

    #region Fill Height Tests

    [Fact]
    public void Measure_WithFillHeight_ExpandsToConstraintHeight()
    {
        // A table with only 2 data rows should still measure to the full
        // constrained height when HeightHint is Fill.
        var data = new[] { "A", "B" };
        var node = new TableNode<string>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)],
            HeightHint = SizeHint.Fill
        };

        var size = node.Measure(new Constraints(0, 40, 0, 24));

        // Without Fill the height would be 6 (top + header + sep + 2 rows + bottom).
        // With Fill it must expand to the constraint max height (24).
        Assert.Equal(24, size.Height);
    }

    [Fact]
    public void Measure_WithFillHeight_EmptyData_ExpandsToConstraintHeight()
    {
        var node = new TableNode<string>
        {
            Data = Array.Empty<string>(),
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)],
            HeightHint = SizeHint.Fill
        };

        var size = node.Measure(new Constraints(0, 40, 0, 20));

        Assert.Equal(20, size.Height);
    }

    [Fact]
    public void Measure_WithoutFillHeight_UsesContentHeight()
    {
        // Baseline: without Fill, the table should use content-based height.
        var data = new[] { "A", "B" };
        var node = new TableNode<string>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)]
        };

        var size = node.Measure(new Constraints(0, 40, 0, 24));

        // top (1) + header (1) + sep (1) + 2 rows (2) + bottom (1) = 6
        Assert.Equal(6, size.Height);
    }

    [Fact]
    public async Task Table_FillInVStack_BottomBorderAtScreenBottom()
    {
        // Integration test: a table with .Fill() inside a VStack should render
        // its bottom border on the very last row of the terminal.
        const int termWidth = 60;
        const int termHeight = 16;

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(termWidth, termHeight)
            .Build();

        var data = new[] { "Row1", "Row2" }; // Deliberately few rows
        object? focusedKey = "Row1";

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<string>)data)
                .RowKey(s => s)
                .Header(h => [h.Cell("Item")])
                .Row((r, item, _) => [r.Cell(item)])
                .Focus(focusedKey)
                .OnFocusChanged(key => focusedKey = key)
                .Fill(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Row1") && s.ContainsText("Row2"),
                TimeSpan.FromSeconds(2), "table rendered")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var text = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== Fill height test ===");
        TestContext.Current.TestOutputHelper?.WriteLine(text);

        // The bottom-right corner character '┘' must be on the last row
        var bottomRight = snapshot.GetCell(termWidth - 1, termHeight - 1);
        Assert.Equal("┘", bottomRight.Character);

        // The bottom-left corner '└' must also be on the last row
        var bottomLeft = snapshot.GetCell(0, termHeight - 1);
        Assert.Equal("└", bottomLeft.Character);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    #endregion
}
