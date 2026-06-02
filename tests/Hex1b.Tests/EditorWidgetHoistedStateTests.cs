using Hex1b.Documents;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Symmetry coverage for <see cref="EditorWidget"/>'s
/// <see cref="IStatefulWidget{TSelf, TState}"/> contract: routing the
/// parent-supplied instance into the node, surviving reconciliation, observing
/// out-of-band parent mutations, and keeping peer instances isolated. Mirrors
/// the suite already in place for <see cref="TextBoxWidget"/>.
/// </summary>
[TestClass]
public class EditorWidgetHoistedStateTests
{
    [TestMethod]
    public async Task Reconcile_HoistedState_RoutesParentInstanceIntoNode()
    {
        var doc = new Hex1bDocument("hello");
        var state = new EditorState(doc);

        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var node = (EditorNode)await new EditorWidget(state).ReconcileAsync(null, context);

        // The parent's exact state instance must be the one the node uses —
        // not a copy — so subsequent parent mutations are observed.
        Assert.AreSame(state, node.State);
        Assert.AreSame(doc, node.State.Document);
    }

    [TestMethod]
    public async Task Reconcile_HoistedState_PreservedAcrossReconciles()
    {
        var doc = new Hex1bDocument("first");
        var state = new EditorState(doc);

        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var node = (EditorNode)await new EditorWidget(state).ReconcileAsync(null, context);
        Assert.AreSame(state, node.State);

        // A new widget instance pointing at the same state must NOT replace
        // the state instance on the node.
        context.IsNew = false;
        var node2 = (EditorNode)await new EditorWidget(state).ReconcileAsync(node, context);

        Assert.AreSame(node, node2);
        Assert.AreSame(state, node2.State);
    }

    [TestMethod]
    public async Task Reconcile_HoistedState_ParentMutationVisibleAfterReconcile()
    {
        var doc = new Hex1bDocument("hello");
        var state = new EditorState(doc);

        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var node = (EditorNode)await new EditorWidget(state).ReconcileAsync(null, context);
        Assert.AreEqual("hello", node.State.Document.GetText());

        // Parent mutates the document content from outside the widget tree,
        // then reconciles. The node should observe the new text since it
        // shares the same EditorState reference.
        state.Cursor.Position = new DocumentOffset(5);
        state.InsertText(" world");

        context.IsNew = false;
        await new EditorWidget(state).ReconcileAsync(node, context);

        Assert.AreEqual("hello world", node.State.Document.GetText());
        Assert.AreSame(doc, node.State.Document);
    }

    [TestMethod]
    public async Task Reconcile_HoistedState_PeerInstancesAreIndependent()
    {
        var stateA = new EditorState(new Hex1bDocument("alpha"));
        var stateB = new EditorState(new Hex1bDocument("beta"));

        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var nodeA = (EditorNode)await new EditorWidget(stateA).ReconcileAsync(null, context);
        var nodeB = (EditorNode)await new EditorWidget(stateB).ReconcileAsync(null, context);

        // Each node should have its own state — mutating one must not
        // affect the other. This pattern lets a composite hold two peer
        // editors via a single state class with two EditorState fields.
        stateA.Cursor.Position = new DocumentOffset(5);
        stateA.InsertText("-mutated");

        Assert.AreEqual("alpha-mutated", nodeA.State.Document.GetText());
        Assert.AreEqual("beta", nodeB.State.Document.GetText());
        Assert.AreNotSame(nodeA.State, nodeB.State);
    }

    [TestMethod]
    public async Task Reconcile_FluentState_OverridesCtorState()
    {
        // The fluent .State(...) overload is the IStatefulWidget contract
        // entry point. It must replace whatever state was supplied via the
        // ctor — so a composite that received an EditorWidget from outside
        // can rebind it to its own UseState-owned instance.
        var ctorState = new EditorState(new Hex1bDocument("from-ctor"));
        var hoistedState = new EditorState(new Hex1bDocument("from-hoist"));

        var widget = new EditorWidget(ctorState).State(hoistedState);

        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;
        var node = (EditorNode)await widget.ReconcileAsync(null, context);

        Assert.AreSame(hoistedState, node.State);
        Assert.AreEqual("from-hoist", node.State.Document.GetText());
    }
}
