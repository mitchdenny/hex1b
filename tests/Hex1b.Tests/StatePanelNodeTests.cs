using Hex1b;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class StatePanelNodeTests
{
    // --- Layout pass-through tests ---

    [TestMethod]
    public void Measure_WithChild_DelegatesToChild()
    {
        var child = new ButtonNode { Label = "OK" };
        var node = new StatePanelNode { StateKey = new object(), Child = child };

        var size = node.Measure(new Constraints(0, 50, 0, 10));

        // ButtonNode measures its label; just verify we get a non-zero size
        Assert.IsTrue(size.Width > 0);
        Assert.IsTrue(size.Height > 0);
    }

    [TestMethod]
    public void Measure_WithoutChild_ReturnsConstrainedZero()
    {
        var node = new StatePanelNode { StateKey = new object(), Child = null };

        var size = node.Measure(Constraints.Unbounded);

        Assert.AreEqual(0, size.Width);
        Assert.AreEqual(0, size.Height);
    }

    [TestMethod]
    public void Arrange_PassesThroughToChild()
    {
        var child = new ButtonNode { Label = "OK" };
        var node = new StatePanelNode { StateKey = new object(), Child = child };
        node.Measure(Constraints.Unbounded);

        var rect = new Rect(5, 10, 30, 3);
        node.Arrange(rect);

        Assert.AreEqual(rect, node.Bounds);
        Assert.AreEqual(rect, child.Bounds);
    }

    // --- Focus pass-through tests ---

    [TestMethod]
    public void GetFocusableNodes_DelegatesToChild()
    {
        var button = new ButtonNode { Label = "OK" };
        var node = new StatePanelNode { StateKey = new object(), Child = button };

        var focusables = node.GetFocusableNodes().ToList();

        TestSeq.Single(focusables);
        Assert.AreSame(button, focusables[0]);
    }

    [TestMethod]
    public void GetFocusableNodes_WithoutChild_ReturnsEmpty()
    {
        var node = new StatePanelNode { StateKey = new object(), Child = null };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.IsEmpty(focusables);
    }

    [TestMethod]
    public void GetChildren_WithChild_ReturnsSingletonList()
    {
        var child = new TextBlockNode();
        var node = new StatePanelNode { StateKey = new object(), Child = child };

        var children = node.GetChildren();

        TestSeq.Single(children);
        Assert.AreSame(child, children[0]);
    }

    [TestMethod]
    public void GetChildren_WithoutChild_ReturnsEmpty()
    {
        var node = new StatePanelNode { StateKey = new object(), Child = null };

        var children = node.GetChildren();

        Assert.IsEmpty(children);
    }

    // --- Mark-and-sweep tests ---

    [TestMethod]
    public void SweepUnvisited_RemovesUnvisitedKeys()
    {
        var node = new StatePanelNode { StateKey = new object() };
        var keyA = new object();
        var keyB = new object();
        var childA = new StatePanelNode { StateKey = keyA };
        var childB = new StatePanelNode { StateKey = keyB };

        node.NestedStatePanels[keyA] = childA;
        node.NestedStatePanels[keyB] = childB;

        // Only visit keyA
        node.MarkVisited(keyA);
        node.SweepUnvisited();

        Assert.IsTrue(node.NestedStatePanels.ContainsKey(keyA));
        Assert.IsFalse(node.NestedStatePanels.ContainsKey(keyB));
    }

    [TestMethod]
    public void SweepUnvisited_ClearsVisitedSetForNextFrame()
    {
        var node = new StatePanelNode { StateKey = new object() };
        var keyA = new object();
        var childA = new StatePanelNode { StateKey = keyA };

        node.NestedStatePanels[keyA] = childA;
        node.MarkVisited(keyA);
        node.SweepUnvisited();

        // After sweep, visited set is cleared — if we sweep again without marking,
        // keyA should be removed
        node.SweepUnvisited();

        Assert.IsFalse(node.NestedStatePanels.ContainsKey(keyA));
    }

    // --- Identity-based reconciliation tests ---

    [TestMethod]
    public async Task Reconcile_SameStateKey_ReusesSameNode()
    {
        var stateKey = new object();
        var widget = new StatePanelWidget(stateKey, sp => new TextBlockWidget("Hello"));
        var context = ReconcileContext.CreateRoot();

        var node1 = await widget.ReconcileAsync(null, context);
        var node2 = await widget.ReconcileAsync(node1, context);

        TestSeq.IsType<StatePanelNode>(node1);
        Assert.AreSame(node1, node2);
    }

    [TestMethod]
    public async Task Reconcile_DifferentStateKey_Standalone_CreatesNewNode()
    {
        var keyA = new object();
        var keyB = new object();
        var context = ReconcileContext.CreateRoot();

        var widgetA = new StatePanelWidget(keyA, sp => new TextBlockWidget("A"));
        var node1 = await widgetA.ReconcileAsync(null, context);

        // Reconcile with a different key at the same position (no ancestor)
        var widgetB = new StatePanelWidget(keyB, sp => new TextBlockWidget("B"));
        var node2 = await widgetB.ReconcileAsync(node1, context);

        TestSeq.IsType<StatePanelNode>(node2);
        Assert.AreNotSame(node1, node2);
    }

    [TestMethod]
    public async Task Reconcile_NestedStatePanels_IdentitySurvivesPositionSwap()
    {
        var keyRoot = new object();
        var keyA = new object();
        var keyB = new object();
        var context = ReconcileContext.CreateRoot();

        // Frame 1: root StatePanel containing VStack [ SP(A), SP(B) ]
        var widget1 = new StatePanelWidget(keyRoot, sp =>
            new VStackWidget([
                new StatePanelWidget(keyA, _ => new TextBlockWidget("A")),
                new StatePanelWidget(keyB, _ => new TextBlockWidget("B")),
            ]));

        var rootNode = (StatePanelNode)await widget1.ReconcileAsync(null, context);
        var vstack1 = (VStackNode)rootNode.Child!;
        var nodeA = vstack1.Children[0];
        var nodeB = vstack1.Children[1];

        TestSeq.IsType<StatePanelNode>(nodeA);
        TestSeq.IsType<StatePanelNode>(nodeB);

        // Frame 2: swap order — VStack [ SP(B), SP(A) ]
        var widget2 = new StatePanelWidget(keyRoot, sp =>
            new VStackWidget([
                new StatePanelWidget(keyB, _ => new TextBlockWidget("B")),
                new StatePanelWidget(keyA, _ => new TextBlockWidget("A")),
            ]));

        var rootNode2 = (StatePanelNode)await widget2.ReconcileAsync(rootNode, context);
        var vstack2 = (VStackNode)rootNode2.Child!;

        // Identity preserved: nodes swapped correctly
        Assert.AreSame(nodeB, vstack2.Children[0]);
        Assert.AreSame(nodeA, vstack2.Children[1]);
    }

    [TestMethod]
    public async Task Reconcile_NestedStatePanels_RemovedKeyGetSwept()
    {
        var keyRoot = new object();
        var keyA = new object();
        var keyB = new object();
        var context = ReconcileContext.CreateRoot();

        // Frame 1: root with A and B
        var widget1 = new StatePanelWidget(keyRoot, sp =>
            new VStackWidget([
                new StatePanelWidget(keyA, _ => new TextBlockWidget("A")),
                new StatePanelWidget(keyB, _ => new TextBlockWidget("B")),
            ]));

        var rootNode = (StatePanelNode)await widget1.ReconcileAsync(null, context);
        Assert.AreEqual(2, rootNode.NestedStatePanels.Count);

        // Frame 2: only A remains
        var widget2 = new StatePanelWidget(keyRoot, sp =>
            new VStackWidget([
                new StatePanelWidget(keyA, _ => new TextBlockWidget("A")),
            ]));

        await widget2.ReconcileAsync(rootNode, context);

        // B should be swept
        TestSeq.Single(rootNode.NestedStatePanels);
        Assert.IsTrue(rootNode.NestedStatePanels.ContainsKey(keyA));
        Assert.IsFalse(rootNode.NestedStatePanels.ContainsKey(keyB));
    }

    [TestMethod]
    public async Task Reconcile_NestedStatePanels_NewKeyCreatesNewNode()
    {
        var keyRoot = new object();
        var keyA = new object();
        var keyC = new object();
        var context = ReconcileContext.CreateRoot();

        // Frame 1: root with A
        var widget1 = new StatePanelWidget(keyRoot, sp =>
            new VStackWidget([
                new StatePanelWidget(keyA, _ => new TextBlockWidget("A")),
            ]));

        var rootNode = (StatePanelNode)await widget1.ReconcileAsync(null, context);
        var nodeA = rootNode.NestedStatePanels[keyA];

        // Frame 2: root with A and C (new)
        var widget2 = new StatePanelWidget(keyRoot, sp =>
            new VStackWidget([
                new StatePanelWidget(keyA, _ => new TextBlockWidget("A")),
                new StatePanelWidget(keyC, _ => new TextBlockWidget("C")),
            ]));

        await widget2.ReconcileAsync(rootNode, context);

        Assert.AreEqual(2, rootNode.NestedStatePanels.Count);
        Assert.AreSame(nodeA, rootNode.NestedStatePanels[keyA]);
        Assert.IsTrue(rootNode.NestedStatePanels.ContainsKey(keyC));
    }

    [TestMethod]
    public async Task Reconcile_NodeHasCorrectStateKey()
    {
        var stateKey = new object();
        var widget = new StatePanelWidget(stateKey, sp => new TextBlockWidget("test"));

        var context = ReconcileContext.CreateRoot();
        var node = (StatePanelNode)await widget.ReconcileAsync(null, context);

        Assert.AreSame(stateKey, node.StateKey);
    }

    [TestMethod]
    public async Task Reconcile_GetExpectedNodeType_ReturnsStatePanelNode()
    {
        var widget = new StatePanelWidget(new object(), _ => new TextBlockWidget("x"));

        Assert.AreEqual(typeof(StatePanelNode), widget.GetExpectedNodeType());
    }

    [TestMethod]
    public async Task Reconcile_ChildWidgetIsReconciled()
    {
        var stateKey = new object();
        var widget = new StatePanelWidget(stateKey, sp => new TextBlockWidget("Hello World"));
        var context = ReconcileContext.CreateRoot();

        var node = (StatePanelNode)await widget.ReconcileAsync(null, context);

        Assert.IsNotNull(node.Child);
        TestSeq.IsType<TextBlockNode>(node.Child);
    }

    // --- Extension method tests ---

    [TestMethod]
    public void Extension_StatePanel_SingleChild_CreatesWidget()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var stateKey = new object();

        var widget = ctx.StatePanel(stateKey, sp => sp.Text("hello"));

        TestSeq.IsType<StatePanelWidget>(widget);
        Assert.AreSame(stateKey, widget.StateKey);
    }

    // --- Registry uses reference equality ---

    [TestMethod]
    public async Task Reconcile_RegistryUsesReferenceEquality_NotValueEquality()
    {
        var keyRoot = new object();
        // Two strings that are value-equal but reference-different
        var keyA = new string("item-1".ToCharArray());
        var keyB = new string("item-1".ToCharArray());
        Assert.AreEqual(keyA, keyB);
        Assert.IsFalse(ReferenceEquals(keyA, keyB));

        var context = ReconcileContext.CreateRoot();

        var widget = new StatePanelWidget(keyRoot, sp =>
            new VStackWidget([
                new StatePanelWidget(keyA, _ => new TextBlockWidget("A")),
                new StatePanelWidget(keyB, _ => new TextBlockWidget("B")),
            ]));

        var rootNode = (StatePanelNode)await widget.ReconcileAsync(null, context);

        // Both should be separate entries despite value equality
        Assert.AreEqual(2, rootNode.NestedStatePanels.Count);
    }
}
