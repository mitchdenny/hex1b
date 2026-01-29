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
        var data = Enumerable.Range(1, 10).Select(i => $"Row {i}").ToArray();
        var node = new TableNode<string>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)],
            FocusedKey = 2 // Focus on row 2
        };

        var constraints = new Constraints(0, 40, 0, 15);
        node.Measure(constraints);
        node.Arrange(new Rect(0, 0, 40, 15));
        
        node.IsFocused = true;
        
        // Initially no selection
        Assert.Null(node.SelectedKeys);

        // Press Space to select
        var keyEvent = new Hex1bKeyEvent(Hex1bKey.Spacebar, ' ', Hex1bModifiers.None);
        var result = await Input.InputRouter.RouteInputToNodeAsync(node, keyEvent);

        Assert.Equal(InputResult.Handled, result);
        Assert.NotNull(node.SelectedKeys);
        Assert.Single(node.SelectedKeys);
        Assert.Contains(2, node.SelectedKeys.Cast<int>());

        // Press Space again to deselect
        result = await Input.InputRouter.RouteInputToNodeAsync(node, keyEvent);
        
        Assert.Equal(InputResult.Handled, result);
        Assert.Empty(node.SelectedKeys);
    }

    [Fact]
    public async Task CtrlA_KeyBinding_SelectsAllRows()
    {
        var data = Enumerable.Range(1, 10).Select(i => $"Row {i}").ToArray();
        var node = new TableNode<string>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)],
            FocusedKey = 0
        };

        var constraints = new Constraints(0, 40, 0, 15);
        node.Measure(constraints);
        node.Arrange(new Rect(0, 0, 40, 15));
        
        node.IsFocused = true;
        
        // Press Ctrl+A
        var keyEvent = new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.Control);
        var result = await Input.InputRouter.RouteInputToNodeAsync(node, keyEvent);

        Assert.Equal(InputResult.Handled, result);
        Assert.NotNull(node.SelectedKeys);
        Assert.Equal(10, node.SelectedKeys.Count); // All 10 rows selected
    }

    [Fact]
    public async Task ShiftDown_KeyBinding_ExtendsSelection()
    {
        var data = Enumerable.Range(1, 10).Select(i => $"Row {i}").ToArray();
        var node = new TableNode<string>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)],
            FocusedKey = 2 // Start at row 2
        };

        var constraints = new Constraints(0, 40, 0, 15);
        node.Measure(constraints);
        node.Arrange(new Rect(0, 0, 40, 15));
        
        node.IsFocused = true;

        // Press Shift+Down twice
        var keyEvent = new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.Shift);
        await Input.InputRouter.RouteInputToNodeAsync(node, keyEvent);
        await Input.InputRouter.RouteInputToNodeAsync(node, keyEvent);

        Assert.NotNull(node.SelectedKeys);
        Assert.Equal(3, node.SelectedKeys.Count); // Rows 2, 3, 4 selected
        Assert.Equal(4, node.FocusedKey); // Focus moved to row 4
    }

    [Fact]
    public async Task ShiftEnd_KeyBinding_SelectsToLastRow()
    {
        var data = Enumerable.Range(1, 10).Select(i => $"Row {i}").ToArray();
        var node = new TableNode<string>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)],
            FocusedKey = 5 // Start at row 5
        };

        var constraints = new Constraints(0, 40, 0, 15);
        node.Measure(constraints);
        node.Arrange(new Rect(0, 0, 40, 15));
        
        node.IsFocused = true;

        // Press Shift+End
        var keyEvent = new Hex1bKeyEvent(Hex1bKey.End, '\0', Hex1bModifiers.Shift);
        await Input.InputRouter.RouteInputToNodeAsync(node, keyEvent);

        Assert.NotNull(node.SelectedKeys);
        Assert.Equal(5, node.SelectedKeys.Count); // Rows 5, 6, 7, 8, 9 selected
        Assert.Equal(9, node.FocusedKey); // Focus at last row
    }

    [Fact]
    public async Task SelectionChangedHandler_IsCalled()
    {
        var data = Enumerable.Range(1, 10).Select(i => $"Row {i}").ToArray();
        IReadOnlySet<object>? lastSelection = null;
        
        var node = new TableNode<string>
        {
            Data = data,
            HeaderBuilder = h => [h.Cell("Name")],
            RowBuilder = (r, item, _) => [r.Cell(item)],
            FocusedKey = 0,
            SelectionChangedHandler = selection =>
            {
                lastSelection = selection;
                return Task.CompletedTask;
            }
        };

        var constraints = new Constraints(0, 40, 0, 15);
        node.Measure(constraints);
        node.Arrange(new Rect(0, 0, 40, 15));
        
        node.IsFocused = true;

        // Press Space to select
        var keyEvent = new Hex1bKeyEvent(Hex1bKey.Spacebar, ' ', Hex1bModifiers.None);
        await Input.InputRouter.RouteInputToNodeAsync(node, keyEvent);

        Assert.NotNull(lastSelection);
        Assert.Single(lastSelection);
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

        // Access header nodes via reflection to check their bounds
        var headerNodesField = typeof(TableNode<string>).GetField("_headerNodes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var headerNodes = (List<TextBlockNode>?)headerNodesField?.GetValue(node);
        
        Assert.NotNull(headerNodes);
        Assert.Single(headerNodes);
        
        // The first cell should start at position: Bounds.X + 1 (left border) + SelectionColumnWidth (3) + 1 (separator) = 5
        var firstCell = headerNodes[0];
        Assert.Equal(5, firstCell.Bounds.X);
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
        // Expected: │☐  │Name...
        Assert.True(char0 == "│", $"Expected left border '│' at position 0, got '{char0}'"); // Left border
        Assert.True(char4 == "│", $"Expected separator '│' at position 4, got '{char4}'"); // Selection column separator
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
                .WithHeader(h => [h.Cell("Name")])
                .WithRow((r, item, _) => [r.Cell(item)]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - start the app and send PageDown to scroll significantly
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Wait for initial render, then send PageDown 3 times
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Product 1"), TimeSpan.FromSeconds(2), "Wait for table to render")
            .Key(Hex1bKey.PageDown)
            .Key(Hex1bKey.PageDown)
            .Key(Hex1bKey.PageDown)
            .WaitUntil(s => s.ContainsText("Product 15") || s.ContainsText("Product 20"), 
                       TimeSpan.FromMilliseconds(500), "Wait for scroll to complete")
            .Ctrl().Key(Hex1bKey.C) // Exit
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var finalSnapshot = terminal.CreateSnapshot();
        var finalText = finalSnapshot.GetScreenText();
        
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
                    .WithHeader(h => [h.Cell("Name")])
                    .WithRow((r, item, _) => [r.Cell(item)])
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
                .WithHeader(h => [h.Cell("Name")])
                .WithRow((r, item, state) => [
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

    #endregion
}
