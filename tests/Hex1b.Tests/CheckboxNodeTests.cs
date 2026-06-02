using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Unit tests for CheckboxNode behavior.
/// </summary>
[TestClass]
public class CheckboxNodeTests
{
    [TestMethod]
    public void Measure_ReturnsCorrectSize_NoLabel()
    {
        var node = new CheckboxNode { State = CheckboxState.Unchecked };
        var size = node.Measure(new Constraints(0, 100, 0, 10));

        // " ▢ " = 3 display cells, no label
        Assert.AreEqual(3, size.Width);
        Assert.AreEqual(1, size.Height);
    }

    [TestMethod]
    public void Measure_ReturnsCorrectSize_WithLabel()
    {
        var node = new CheckboxNode { State = CheckboxState.Unchecked, Label = "Option" };
        var size = node.Measure(new Constraints(0, 100, 0, 10));

        // " ▢ " = 3 cells + " " + "Option" = 3 + 1 + 6 = 10
        Assert.AreEqual(10, size.Width);
        Assert.AreEqual(1, size.Height);
    }

    [TestMethod]
    public void IsFocusable_ReturnsTrue()
    {
        var node = new CheckboxNode();
        Assert.IsTrue(node.IsFocusable);
    }

    [TestMethod]
    public void IsFocused_WhenSet_MarksDirty()
    {
        var node = new CheckboxNode();
        node.ClearDirty();

        node.IsFocused = true;

        Assert.IsTrue(node.IsDirty);
    }

    [TestMethod]
    public async Task Widget_Reconcile_CreatesNode()
    {
        var widget = new CheckboxWidget(CheckboxValue.Checked);

        var context = ReconcileContext.CreateRoot();
        var node = await widget.ReconcileAsync(null, context) as CheckboxNode;

        Assert.IsNotNull(node);
        Assert.AreEqual(CheckboxValue.Checked, node.State.Value);
    }

    [TestMethod]
    public async Task Widget_Reconcile_UpdatesState()
    {
        var widget1 = new CheckboxWidget(CheckboxValue.Unchecked);
        var widget2 = new CheckboxWidget(CheckboxValue.Checked);

        var context = ReconcileContext.CreateRoot();
        var node = await widget1.ReconcileAsync(null, context) as CheckboxNode;
        Assert.AreEqual(CheckboxValue.Unchecked, node!.State.Value);

        node.ClearDirty();
        await widget2.ReconcileAsync(node, context);

        Assert.AreEqual(CheckboxValue.Checked, node.State.Value);
        Assert.IsTrue(node.IsDirty);
    }

    [TestMethod]
    public async Task Widget_WithLabel_SetsLabel()
    {
        var widget = new CheckboxWidget().Label("Test Label");

        var context = ReconcileContext.CreateRoot();
        var node = await widget.ReconcileAsync(null, context) as CheckboxNode;

        Assert.AreEqual("Test Label", node!.Label);
    }

    [TestMethod]
    public void Widget_FluentApi_ChainsCorrectly()
    {
        var widget = new CheckboxWidget()
            .Checked()
            .Label("Option 1");

        Assert.AreEqual(CheckboxValue.Checked, widget.Value);
        Assert.AreEqual("Option 1", widget.LabelText);
    }

    [TestMethod]
    public void Widget_Indeterminate_SetsState()
    {
        var widget = new CheckboxWidget().Indeterminate();

        Assert.AreEqual(CheckboxValue.Indeterminate, widget.Value);
    }

    [TestMethod]
    public async Task Widget_HoistedState_RoutesParentInstanceIntoNode()
    {
        // When the parent supplies a CheckboxState via .State(...), the node
        // adopts that exact instance — mutations are visible to the parent.
        var parentState = new CheckboxState(CheckboxValue.Checked);
        var widget = new CheckboxWidget().State(parentState);

        var context = ReconcileContext.CreateRoot();
        var node = await widget.ReconcileAsync(null, context) as CheckboxNode;

        Assert.AreSame(parentState, node!.State);
        Assert.AreEqual(CheckboxValue.Checked, parentState.Value);
    }

    [TestMethod]
    public async Task Widget_HoistedState_TogglePropagatesToParent()
    {
        // The framework's Toggle() mutates State.Value in place — so when a
        // composite has lifted the state up via UseState, the parent's instance
        // observes the toggle without any OnToggled handler.
        var parentState = new CheckboxState(CheckboxValue.Unchecked);
        var widget = new CheckboxWidget().State(parentState);

        var context = ReconcileContext.CreateRoot();
        var node = await widget.ReconcileAsync(null, context) as CheckboxNode;

        // Simulate a toggle by mutating in-place the way Toggle() does.
        node!.State.Value = node.State.Value == CheckboxValue.Checked
            ? CheckboxValue.Unchecked
            : CheckboxValue.Checked;

        Assert.AreEqual(CheckboxValue.Checked, parentState.Value);
    }
}
