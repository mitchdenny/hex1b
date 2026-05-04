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
    public void SnapshotText_TextBlockChild_ReturnsItsText()
    {
        var child = new TextBlockNode { Text = "Hello" };
        var node = new SelectionPanelNode { Child = child };

        Assert.Equal("Hello", node.SnapshotText());
    }

    [Fact]
    public void SnapshotText_MarkdownChild_ReturnsSourceMarkdown()
    {
        var md = new MarkdownNode { Source = "# Title\n\nBody **bold**." };
        var node = new SelectionPanelNode { Child = md };

        Assert.Equal("# Title\n\nBody **bold**.", node.SnapshotText());
    }

    [Fact]
    public void SnapshotText_BorderWithTitleAndChild_IncludesBoth()
    {
        // Mirrors AgenticPromptDemo's per-entry layout: Border(title).Markdown(text)
        var inner = new MarkdownNode { Source = "hello" };
        var border = new BorderNode { Title = "You", Child = inner };
        var panel = new SelectionPanelNode { Child = border };

        var snapshot = panel.SnapshotText();

        Assert.Contains("--- You ---", snapshot);
        Assert.Contains("hello", snapshot);
        // Title must appear before its body content.
        Assert.True(snapshot.IndexOf("You", StringComparison.Ordinal) <
                    snapshot.IndexOf("hello", StringComparison.Ordinal));
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
        var node = new SelectionPanelNode
        {
            Child = new TextBlockNode { Text = "Snapshot me" },
            SnapshotHandler = text => { captured = text; return Task.CompletedTask; },
        };
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
