using Hex1b;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class StatePanelNodeTests
{
    // --- Layout pass-through tests ---

    [Fact]
    public void Measure_WithChild_DelegatesToChild()
    {
        var child = new ButtonNode { Label = "OK" };
        var node = new StatePanelNode { StateKey = new object(), Child = child };

        var size = node.Measure(new Constraints(0, 50, 0, 10));

        // ButtonNode measures its label; just verify we get a non-zero size
        Assert.True(size.Width > 0);
        Assert.True(size.Height > 0);
    }

    [Fact]
    public void Measure_WithoutChild_ReturnsConstrainedZero()
    {
        var node = new StatePanelNode { StateKey = new object(), Child = null };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(0, size.Width);
        Assert.Equal(0, size.Height);
    }

    [Fact]
    public void Arrange_PassesThroughToChild()
    {
        var child = new ButtonNode { Label = "OK" };
        var node = new StatePanelNode { StateKey = new object(), Child = child };
        node.Measure(Constraints.Unbounded);

        var rect = new Rect(5, 10, 30, 3);
        node.Arrange(rect);

        Assert.Equal(rect, node.Bounds);
        Assert.Equal(rect, child.Bounds);
    }

    // --- Focus pass-through tests ---

    [Fact]
    public void GetFocusableNodes_DelegatesToChild()
    {
        var button = new ButtonNode { Label = "OK" };
        var node = new StatePanelNode { StateKey = new object(), Child = button };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Single(focusables);
        Assert.Same(button, focusables[0]);
    }

    [Fact]
    public void GetFocusableNodes_WithoutChild_ReturnsEmpty()
    {
        var node = new StatePanelNode { StateKey = new object(), Child = null };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Empty(focusables);
    }

    [Fact]
    public void GetChildren_WithChild_ReturnsSingletonList()
    {
        var child = new TextBlockNode();
        var node = new StatePanelNode { StateKey = new object(), Child = child };

        var children = node.GetChildren();

        Assert.Single(children);
        Assert.Same(child, children[0]);
    }

    [Fact]
    public void GetChildren_WithoutChild_ReturnsEmpty()
    {
        var node = new StatePanelNode { StateKey = new object(), Child = null };

        var children = node.GetChildren();

        Assert.Empty(children);
    }

    // --- Mark-and-sweep tests ---

    [Fact]
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

        Assert.True(node.NestedStatePanels.ContainsKey(keyA));
        Assert.False(node.NestedStatePanels.ContainsKey(keyB));
    }

    [Fact]
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

        Assert.False(node.NestedStatePanels.ContainsKey(keyA));
    }

    // --- Identity-based reconciliation tests ---

    [Fact]
    public async Task Reconcile_SameStateKey_ReusesSameNode()
    {
        var stateKey = new object();
        var widget = new StatePanelWidget(stateKey, sp => new TextBlockWidget("Hello"));
        var context = ReconcileContext.CreateRoot();

        var node1 = await widget.ReconcileAsync(null, context);
        var node2 = await widget.ReconcileAsync(node1, context);

        Assert.IsType<StatePanelNode>(node1);
        Assert.Same(node1, node2);
    }

    [Fact]
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

        Assert.IsType<StatePanelNode>(node2);
        Assert.NotSame(node1, node2);
    }

    [Fact]
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

        Assert.IsType<StatePanelNode>(nodeA);
        Assert.IsType<StatePanelNode>(nodeB);

        // Frame 2: swap order — VStack [ SP(B), SP(A) ]
        var widget2 = new StatePanelWidget(keyRoot, sp =>
            new VStackWidget([
                new StatePanelWidget(keyB, _ => new TextBlockWidget("B")),
                new StatePanelWidget(keyA, _ => new TextBlockWidget("A")),
            ]));

        var rootNode2 = (StatePanelNode)await widget2.ReconcileAsync(rootNode, context);
        var vstack2 = (VStackNode)rootNode2.Child!;

        // Identity preserved: nodes swapped correctly
        Assert.Same(nodeB, vstack2.Children[0]);
        Assert.Same(nodeA, vstack2.Children[1]);
    }

    [Fact]
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
        Assert.Equal(2, rootNode.NestedStatePanels.Count);

        // Frame 2: only A remains
        var widget2 = new StatePanelWidget(keyRoot, sp =>
            new VStackWidget([
                new StatePanelWidget(keyA, _ => new TextBlockWidget("A")),
            ]));

        await widget2.ReconcileAsync(rootNode, context);

        // B should be swept
        Assert.Single(rootNode.NestedStatePanels);
        Assert.True(rootNode.NestedStatePanels.ContainsKey(keyA));
        Assert.False(rootNode.NestedStatePanels.ContainsKey(keyB));
    }

    [Fact]
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

        Assert.Equal(2, rootNode.NestedStatePanels.Count);
        Assert.Same(nodeA, rootNode.NestedStatePanels[keyA]);
        Assert.True(rootNode.NestedStatePanels.ContainsKey(keyC));
    }

    [Fact]
    public async Task Reconcile_NodeHasCorrectStateKey()
    {
        var stateKey = new object();
        var widget = new StatePanelWidget(stateKey, sp => new TextBlockWidget("test"));

        var context = ReconcileContext.CreateRoot();
        var node = (StatePanelNode)await widget.ReconcileAsync(null, context);

        Assert.Same(stateKey, node.StateKey);
    }

    [Fact]
    public async Task Reconcile_GetExpectedNodeType_ReturnsStatePanelNode()
    {
        var widget = new StatePanelWidget(new object(), _ => new TextBlockWidget("x"));

        Assert.Equal(typeof(StatePanelNode), widget.GetExpectedNodeType());
    }

    [Fact]
    public async Task Reconcile_ChildWidgetIsReconciled()
    {
        var stateKey = new object();
        var widget = new StatePanelWidget(stateKey, sp => new TextBlockWidget("Hello World"));
        var context = ReconcileContext.CreateRoot();

        var node = (StatePanelNode)await widget.ReconcileAsync(null, context);

        Assert.NotNull(node.Child);
        Assert.IsType<TextBlockNode>(node.Child);
    }

    // --- Extension method tests ---

    [Fact]
    public void Extension_StatePanel_SingleChild_CreatesWidget()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var stateKey = new object();

        var widget = ctx.StatePanel(stateKey, sp => sp.Text("hello"));

        Assert.IsType<StatePanelWidget>(widget);
        Assert.Same(stateKey, widget.StateKey);
    }

    // --- Registry uses reference equality ---

    [Fact]
    public async Task Reconcile_RegistryUsesReferenceEquality_NotValueEquality()
    {
        var keyRoot = new object();
        // Two strings that are value-equal but reference-different
        var keyA = new string("item-1".ToCharArray());
        var keyB = new string("item-1".ToCharArray());
        Assert.Equal(keyA, keyB);
        Assert.False(ReferenceEquals(keyA, keyB));

        var context = ReconcileContext.CreateRoot();

        var widget = new StatePanelWidget(keyRoot, sp =>
            new VStackWidget([
                new StatePanelWidget(keyA, _ => new TextBlockWidget("A")),
                new StatePanelWidget(keyB, _ => new TextBlockWidget("B")),
            ]));

        var rootNode = (StatePanelNode)await widget.ReconcileAsync(null, context);

        // Both should be separate entries despite value equality
        Assert.Equal(2, rootNode.NestedStatePanels.Count);
    }
}
