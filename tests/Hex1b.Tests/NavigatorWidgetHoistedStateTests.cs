using Hex1b.Nodes;
using Hex1b.Widgets;

#pragma warning disable HEX1B001 // NavigatorWidget is experimental.

namespace Hex1b.Tests;

/// <summary>
/// Symmetry coverage for <see cref="NavigatorWidget"/>'s
/// <see cref="IStatefulWidget{TSelf, TState}"/> contract. Mirrors the suite
/// already in place for <see cref="TextBoxWidget"/>.
/// </summary>
[TestClass]
public class NavigatorWidgetHoistedStateTests
{
    private static NavigatorRoute CreateRoute(string id) =>
        new(id, _ => new TextBlockWidget($"Screen: {id}"));

    [TestMethod]
    public async Task Reconcile_HoistedState_RoutesParentInstanceIntoNode()
    {
        var state = new NavigatorState(CreateRoute("home"));

        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var node = (NavigatorNode)await new NavigatorWidget(state).ReconcileAsync(null, context);

        Assert.AreSame(state, node.State);
        Assert.AreEqual("home", node.State.CurrentRoute.Id);
    }

    [TestMethod]
    public async Task Reconcile_HoistedState_PreservedAcrossReconciles()
    {
        var state = new NavigatorState(CreateRoute("home"));

        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var node = (NavigatorNode)await new NavigatorWidget(state).ReconcileAsync(null, context);
        Assert.AreSame(state, node.State);

        context.IsNew = false;
        var node2 = (NavigatorNode)await new NavigatorWidget(state).ReconcileAsync(node, context);

        Assert.AreSame(node, node2);
        Assert.AreSame(state, node2.State);
    }

    [TestMethod]
    public async Task Reconcile_HoistedState_ParentMutationVisibleAfterReconcile()
    {
        var state = new NavigatorState(CreateRoute("home"));

        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var node = (NavigatorNode)await new NavigatorWidget(state).ReconcileAsync(null, context);
        Assert.AreEqual("home", node.State.CurrentRoute.Id);

        // Parent pushes a new route from outside the widget tree, then
        // reconciles. The node should observe the new current route.
        state.Push(CreateRoute("details"));

        context.IsNew = false;
        await new NavigatorWidget(state).ReconcileAsync(node, context);

        Assert.AreEqual("details", node.State.CurrentRoute.Id);
        Assert.AreEqual(2, node.State.Depth);
    }

    [TestMethod]
    public async Task Reconcile_HoistedState_PeerInstancesAreIndependent()
    {
        var stateA = new NavigatorState(CreateRoute("home-a"));
        var stateB = new NavigatorState(CreateRoute("home-b"));

        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var nodeA = (NavigatorNode)await new NavigatorWidget(stateA).ReconcileAsync(null, context);
        var nodeB = (NavigatorNode)await new NavigatorWidget(stateB).ReconcileAsync(null, context);

        stateA.Push(CreateRoute("nested"));

        Assert.AreEqual("nested", nodeA.State.CurrentRoute.Id);
        Assert.AreEqual("home-b", nodeB.State.CurrentRoute.Id);
        Assert.AreNotSame(nodeA.State, nodeB.State);
    }

    [TestMethod]
    public async Task Reconcile_FluentState_OverridesCtorState()
    {
        // The fluent .State(...) overload must replace whatever state was
        // supplied via the ctor — symmetry with EditorWidget / TextBoxWidget.
        var ctorState = new NavigatorState(CreateRoute("ctor-home"));
        var hoistedState = new NavigatorState(CreateRoute("hoist-home"));

        var widget = new NavigatorWidget(ctorState).State(hoistedState);

        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;
        var node = (NavigatorNode)await widget.ReconcileAsync(null, context);

        Assert.AreSame(hoistedState, node.State);
        Assert.AreEqual("hoist-home", node.State.CurrentRoute.Id);
    }
}
