using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Pass-through behaviour tests for the minimal SelectionPanelNode.
/// At this stage SelectionPanel has no behaviour of its own beyond an
/// optional snapshot callback — it must simply forward measure, arrange,
/// focus, and render to its child.
/// </summary>
public class SelectionPanelNodeTests
{
    [Fact]
    public void Measure_ReturnsChildSize()
    {
        var child = new TextBlockNode { Text = "Hello" };
        var node = new SelectionPanelNode { Child = child };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(5, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_WithNoChild_ReturnsZero()
    {
        var node = new SelectionPanelNode { Child = null };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(0, size.Width);
        Assert.Equal(0, size.Height);
    }

    [Fact]
    public void Arrange_ForwardsRectToChild()
    {
        var child = new TextBlockNode { Text = "Hi" };
        var node = new SelectionPanelNode { Child = child };
        var rect = new Rect(3, 4, 10, 2);

        node.Measure(Constraints.Unbounded);
        node.Arrange(rect);

        Assert.Equal(rect, child.Bounds);
        Assert.Equal(rect, node.Bounds);
    }

    [Fact]
    public void IsFocusable_IsFalse()
    {
        var node = new SelectionPanelNode();

        Assert.False(node.IsFocusable);
    }

    [Fact]
    public void GetFocusableNodes_ReturnsChildFocusables()
    {
        // A focusable child (TextBoxNode) should be enumerated by the panel,
        // proving the panel is fully transparent for focus traversal.
        var child = new TextBoxNode { Text = "x" };
        var node = new SelectionPanelNode { Child = child };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Single(focusables);
        Assert.Same(child, focusables[0]);
    }

    [Fact]
    public void GetChildren_ReturnsChild()
    {
        var child = new TextBlockNode { Text = "x" };
        var node = new SelectionPanelNode { Child = child };

        var children = node.GetChildren().ToList();

        Assert.Single(children);
        Assert.Same(child, children[0]);
    }

    [Fact]
    public void IsFocused_SetterForwardsToChild()
    {
        var child = new TextBoxNode { Text = "" };
        var node = new SelectionPanelNode { Child = child };

        node.IsFocused = true;

        Assert.True(child.IsFocused);
    }

    [Fact]
    public void SnapshotText_NoChild_ReturnsEmpty()
    {
        var node = new SelectionPanelNode { Child = null };

        Assert.Equal(string.Empty, node.SnapshotText());
    }

    [Fact]
    public void SnapshotText_ChildWithoutBounds_ReturnsEmpty()
    {
        // SnapshotText reads cells from a Surface sized to the child's
        // arranged bounds. A child that has not been arranged (Bounds is
        // zero-sized) cannot be snapshotted.
        var child = new TextBlockNode { Text = "Hello" };
        var node = new SelectionPanelNode { Child = child };

        Assert.Equal(string.Empty, node.SnapshotText());
    }

    [Fact]
    public void SnapshotText_ArrangedTextBlock_ReturnsRenderedText()
    {
        var child = new TextBlockNode { Text = "Hello" };
        var node = new SelectionPanelNode { Child = child };

        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 5, 1));

        Assert.Equal("Hello", node.SnapshotText());
    }

    [Fact]
    public void SnapshotText_ArrangedBorderWithText_IncludesBoxDrawing()
    {
        // Mirrors AgenticPromptDemo: every transcript entry is rendered as
        // Border(Title(...))(content). The snapshot must include the actual
        // box-drawing characters rather than a paraphrased representation,
        // because that is what the user sees on screen.
        var inner = new TextBlockNode { Text = "hi" };
        var border = new BorderNode { Title = "You", Child = inner };
        var panel = new SelectionPanelNode { Child = border };

        // Wide enough for "│ hi │" plus title room: title in a 6-wide border
        // would produce something like "┌ You ┐", "│ hi  │", "└─────┘".
        panel.Measure(Constraints.Unbounded);
        panel.Arrange(new Rect(0, 0, 8, 3));

        var snapshot = panel.SnapshotText();

        // The snapshot must contain box-drawing border chars (corners and
        // horizontals) — proving we are reading rendered cells, not just
        // walking the node tree.
        Assert.Contains("┌", snapshot);
        Assert.Contains("┐", snapshot);
        Assert.Contains("└", snapshot);
        Assert.Contains("┘", snapshot);
        Assert.Contains("─", snapshot);

        // The title and the inner text both appear.
        Assert.Contains("You", snapshot);
        Assert.Contains("hi", snapshot);
    }

    [Fact]
    public void ConfigureDefaultBindings_NoHandler_RegistersNoBinding()
    {
        var node = new SelectionPanelNode { Child = new TextBlockNode { Text = "x" } };
        var bindings = new InputBindingsBuilder();

        node.ConfigureDefaultBindings(bindings);

        Assert.Empty(bindings.Bindings);
    }

    [Fact]
    public void ConfigureDefaultBindings_WithHandler_RegistersAllFourSnapshotBindings()
    {
        var node = new SelectionPanelNode
        {
            Child = new TextBlockNode { Text = "x" },
            SnapshotHandler = _ => Task.CompletedTask,
        };
        var bindings = new InputBindingsBuilder();
        node.ConfigureDefaultBindings(bindings);

        // Expect F7 (cells), F8 (block), F9 (lines), F12 (full).
        Assert.Equal(4, bindings.Bindings.Count);

        var byKey = bindings.Bindings.ToDictionary(b =>
        {
            Assert.True(b.IsGlobal, $"Binding {b.ActionId} should be global.");
            Assert.Single(b.Steps);
            var step = b.Steps[0];
            Assert.Equal(Hex1bModifiers.None, step.Modifiers);
            return step.Key;
        });

        Assert.Equal(SelectionPanelWidget.SnapshotCells, byKey[Hex1bKey.F7].ActionId);
        Assert.Equal(SelectionPanelWidget.SnapshotBlock, byKey[Hex1bKey.F8].ActionId);
        Assert.Equal(SelectionPanelWidget.SnapshotLines, byKey[Hex1bKey.F9].ActionId);
        Assert.Equal(SelectionPanelWidget.Snapshot, byKey[Hex1bKey.F12].ActionId);
    }

    [Fact]
    public async Task ConfigureDefaultBindings_F12_FiresFullSnapshot()
    {
        string? captured = null;
        var node = new SelectionPanelNode
        {
            Child = new TextBlockNode { Text = "Snapshot me" },
            SnapshotHandler = text => { captured = text; return Task.CompletedTask; },
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 11, 1));

        var bindings = new InputBindingsBuilder();
        node.ConfigureDefaultBindings(bindings);

        var fullBinding = bindings.Bindings.Single(b => b.ActionId == SelectionPanelWidget.Snapshot);
        await fullBinding.ExecuteAsync(new InputBindingActionContext(new FocusRing()));

        Assert.Equal("Snapshot me", captured);
    }

    [Theory]
    [InlineData(nameof(SelectionPanelSnapshotMode.Full),
                "AAAAAAAAAAAA\nBBBBBBBBBBBB\nCCCCCCCCCCCC\nDDDDDDDDDDDD\nEEEEEEEEEEEE\nFFFFFFFFFFFF\nGGGGGGGGGGGG\nHHHHHHHHHHHH")]
    // Lines: rows 2..5 inclusive, every column → middle 4 lines, full width.
    [InlineData(nameof(SelectionPanelSnapshotMode.Lines),
                "CCCCCCCCCCCC\nDDDDDDDDDDDD\nEEEEEEEEEEEE\nFFFFFFFFFFFF")]
    // Block: rows 2..5 × cols 3..8 → rectangular slice, 6 chars wide.
    [InlineData(nameof(SelectionPanelSnapshotMode.Block),
                "CCCCCC\nDDDDDD\nEEEEEE\nFFFFFF")]
    // Cells (terminal-style stream): from (row=2, col=3) to (row=5, col=8).
    // Top row from col 3 → end (9 chars), middle rows full width (12 chars),
    // bottom row col 0 → col 8 (9 chars).
    [InlineData(nameof(SelectionPanelSnapshotMode.Cells),
                "CCCCCCCCC\nDDDDDDDDDDDD\nEEEEEEEEEEEE\nFFFFFFFFF")]
    public void SnapshotText_PerMode_PicksExpectedCells(string modeName, string expected)
    {
        // Surface is 12 wide × 8 tall. Each row y is painted with the
        // character ('A' + y), so the snapshot output makes it trivial to see
        // which cells each mode picked.
        var painter = new PaintingTestNode();
        var panel = new SelectionPanelNode { Child = painter };

        panel.Measure(Constraints.Unbounded);
        panel.Arrange(new Rect(0, 0, 12, 8));

        var mode = Enum.Parse<SelectionPanelSnapshotMode>(modeName);
        var snapshot = panel.SnapshotText(mode);

        Assert.Equal(expected.Replace("\r\n", "\n"), snapshot.Replace("\r\n", "\n"));
    }

    /// <summary>
    /// Test render node that paints every cell in its arranged bounds with a
    /// per-row deterministic character so cell-extraction tests can assert
    /// exactly which cells were picked.
    /// </summary>
    private sealed class PaintingTestNode : Hex1bNode
    {
        protected override Size MeasureCore(Constraints constraints)
            => constraints.Constrain(new Size(constraints.MaxWidth, constraints.MaxHeight));

        public override void Render(Hex1bRenderContext context)
        {
            var bounds = Bounds;
            for (int y = 0; y < bounds.Height; y++)
            {
                var ch = (char)('A' + (y % 26));
                var line = new string(ch, bounds.Width);
                context.SetCursorPosition(bounds.X, bounds.Y + y);
                context.Write(line);
            }
        }
    }
}
