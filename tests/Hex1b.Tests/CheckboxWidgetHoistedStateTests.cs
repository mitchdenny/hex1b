using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Symmetry coverage for <see cref="CheckboxWidget"/>'s
/// <see cref="IStatefulWidget{TSelf, TState}"/> contract — the bits that
/// <see cref="CheckboxNodeTests"/> doesn't already cover. Mirrors the
/// <see cref="TextBoxWidgetTests"/> hoisted-state suite.
/// </summary>
[TestClass]
public class CheckboxWidgetHoistedStateTests
{
    [TestMethod]
    public async Task Reconcile_HoistedState_PreservedAcrossReconciles()
    {
        var state = new CheckboxState(CheckboxValue.Unchecked);

        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var node = (CheckboxNode)await new CheckboxWidget().State(state).ReconcileAsync(null, context);
        Assert.AreSame(state, node.State);

        // A new widget instance pointing at the same state must NOT replace
        // the state instance on the node — that would discard any user
        // toggles that happened between reconciliations.
        context.IsNew = false;
        var node2 = (CheckboxNode)await new CheckboxWidget().State(state).ReconcileAsync(node, context);

        Assert.AreSame(node, node2);
        Assert.AreSame(state, node2.State);
    }

    [TestMethod]
    public async Task Reconcile_HoistedState_ParentMutationVisibleAfterReconcile()
    {
        var state = new CheckboxState(CheckboxValue.Unchecked);

        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var node = (CheckboxNode)await new CheckboxWidget().State(state).ReconcileAsync(null, context);
        Assert.AreEqual(CheckboxValue.Unchecked, node.State.Value);

        // Parent flips the value from outside the widget tree, then
        // reconciles. The node observes the new value because it shares
        // the same instance.
        state.Value = CheckboxValue.Checked;

        context.IsNew = false;
        await new CheckboxWidget().State(state).ReconcileAsync(node, context);

        Assert.AreEqual(CheckboxValue.Checked, node.State.Value);
    }

    [TestMethod]
    public async Task Reconcile_HoistedState_PeerInstancesAreIndependent()
    {
        var stateA = new CheckboxState(CheckboxValue.Unchecked);
        var stateB = new CheckboxState(CheckboxValue.Unchecked);

        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var nodeA = (CheckboxNode)await new CheckboxWidget().State(stateA).ReconcileAsync(null, context);
        var nodeB = (CheckboxNode)await new CheckboxWidget().State(stateB).ReconcileAsync(null, context);

        stateA.Value = CheckboxValue.Checked;

        Assert.AreEqual(CheckboxValue.Checked, nodeA.State.Value);
        Assert.AreEqual(CheckboxValue.Unchecked, nodeB.State.Value);
        Assert.AreNotSame(nodeA.State, nodeB.State);
    }

    [TestMethod]
    public async Task Reconcile_HoistedState_IgnoresCtorValue()
    {
        // CheckboxWidget's ctor `Value` parameter has a default and is
        // therefore "always supplied". When the parent has hoisted state
        // via .State(...), that state is the single source of truth — the
        // ctor value must NOT clobber it.
        var state = new CheckboxState(CheckboxValue.Checked);

        // Even though the ctor says Unchecked, the hoisted state wins.
        var widget = new CheckboxWidget(CheckboxValue.Unchecked).State(state);

        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;
        var node = (CheckboxNode)await widget.ReconcileAsync(null, context);

        Assert.AreSame(state, node.State);
        Assert.AreEqual(CheckboxValue.Checked, node.State.Value);
    }

    [TestMethod]
    public async Task Reconcile_HoistedState_OutOfBandChangeMarksDirty()
    {
        // After the first reconcile the node is marked clean. If the parent
        // swaps in a different state instance (or just toggles the same one)
        // and reconciles again, the node must be re-marked dirty so the
        // framework's render-skip optimisation doesn't strand a stale frame.
        var first = new CheckboxState(CheckboxValue.Unchecked);
        var second = new CheckboxState(CheckboxValue.Checked);

        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var node = (CheckboxNode)await new CheckboxWidget().State(first).ReconcileAsync(null, context);
        node.ClearDirty();

        context.IsNew = false;
        await new CheckboxWidget().State(second).ReconcileAsync(node, context);

        Assert.IsTrue(node.IsDirty);
        Assert.AreSame(second, node.State);
    }
}
