using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Pass-through behaviour tests for the minimal SelectionPanelNode.
/// At this stage SelectionPanel has no behaviour of its own — it must
/// simply forward measure, arrange, focus, and render to its child.
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
}
