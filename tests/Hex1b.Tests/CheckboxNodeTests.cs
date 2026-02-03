using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Unit tests for CheckboxNode behavior.
/// </summary>
public class CheckboxNodeTests
{
    [Fact]
    public void Measure_ReturnsCorrectSize_NoLabel()
    {
        var node = new CheckboxNode { State = CheckboxState.Unchecked };
        var size = node.Measure(new Constraints(0, 100, 0, 10));

        // "[x]" = 3 chars, no label
        Assert.Equal(3, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_ReturnsCorrectSize_WithLabel()
    {
        var node = new CheckboxNode { State = CheckboxState.Unchecked, Label = "Option" };
        var size = node.Measure(new Constraints(0, 100, 0, 10));

        // "[x]" = 3 chars + " " + "Option" = 3 + 1 + 6 = 10
        Assert.Equal(10, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void IsFocusable_ReturnsTrue()
    {
        var node = new CheckboxNode();
        Assert.True(node.IsFocusable);
    }

    [Fact]
    public void IsFocused_WhenSet_MarksDirty()
    {
        var node = new CheckboxNode();
        node.ClearDirty();

        node.IsFocused = true;

        Assert.True(node.IsDirty);
    }

    [Fact]
    public async Task Widget_Reconcile_CreatesNode()
    {
        var widget = new CheckboxWidget(CheckboxState.Checked);

        var context = ReconcileContext.CreateRoot();
        var node = await widget.ReconcileAsync(null, context) as CheckboxNode;

        Assert.NotNull(node);
        Assert.Equal(CheckboxState.Checked, node.State);
    }

    [Fact]
    public async Task Widget_Reconcile_UpdatesState()
    {
        var widget1 = new CheckboxWidget(CheckboxState.Unchecked);
        var widget2 = new CheckboxWidget(CheckboxState.Checked);

        var context = ReconcileContext.CreateRoot();
        var node = await widget1.ReconcileAsync(null, context) as CheckboxNode;
        Assert.Equal(CheckboxState.Unchecked, node!.State);

        node.ClearDirty();
        await widget2.ReconcileAsync(node, context);

        Assert.Equal(CheckboxState.Checked, node.State);
        Assert.True(node.IsDirty);
    }

    [Fact]
    public async Task Widget_WithLabel_SetsLabel()
    {
        var widget = new CheckboxWidget().Label("Test Label");

        var context = ReconcileContext.CreateRoot();
        var node = await widget.ReconcileAsync(null, context) as CheckboxNode;

        Assert.Equal("Test Label", node!.Label);
    }

    [Fact]
    public void Widget_FluentApi_ChainsCorrectly()
    {
        var widget = new CheckboxWidget()
            .Checked()
            .Label("Option 1");

        Assert.Equal(CheckboxState.Checked, widget.State);
        Assert.Equal("Option 1", widget.LabelText);
    }

    [Fact]
    public void Widget_Indeterminate_SetsState()
    {
        var widget = new CheckboxWidget().Indeterminate();

        Assert.Equal(CheckboxState.Indeterminate, widget.State);
    }
}
