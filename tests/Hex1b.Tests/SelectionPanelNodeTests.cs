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
    public async Task ConfigureDefaultBindings_WithHandler_RegistersGlobalSnapshotBinding()
    {
        string? captured = null;
        var child = new TextBlockNode { Text = "Snapshot me" };
        var node = new SelectionPanelNode
        {
            Child = child,
            SnapshotHandler = text => { captured = text; return Task.CompletedTask; },
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 11, 1));

        var bindings = new InputBindingsBuilder();
        node.ConfigureDefaultBindings(bindings);

        Assert.Single(bindings.Bindings);

        var binding = bindings.Bindings[0];
        Assert.True(binding.IsGlobal);
        Assert.Equal(SelectionPanelWidget.Snapshot, binding.ActionId);

        // Single-step F12, no modifiers.
        Assert.Single(binding.Steps);
        var step = binding.Steps[0];
        Assert.Equal(Hex1bKey.F12, step.Key);
        Assert.Equal(Hex1bModifiers.None, step.Modifiers);

        // Firing the handler invokes the registered callback with the snapshot text.
        await binding.ExecuteAsync(new InputBindingActionContext(new FocusRing()));
        Assert.Equal("Snapshot me", captured);
    }
}
