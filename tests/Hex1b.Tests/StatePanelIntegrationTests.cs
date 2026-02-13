using Hex1b;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Surfaces;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class StatePanelIntegrationTests
{
    /// <summary>
    /// Full widget → reconcile → measure → arrange → render cycle.
    /// </summary>
    [Fact]
    public async Task FullCycle_StatePanelWithText_RendersCorrectly()
    {
        var stateKey = new object();
        var widget = new StatePanelWidget(stateKey, sp => new TextBlockWidget("Hello"));
        var context = ReconcileContext.CreateRoot();

        // Reconcile
        var node = (StatePanelNode)await widget.ReconcileAsync(null, context);

        // Measure
        var size = node.Measure(new Constraints(0, 40, 0, 5));
        Assert.True(size.Width > 0);

        // Arrange
        node.Arrange(new Rect(0, 0, 40, 1));

        // Render
        var surface = new Surface(40, 5);
        var renderContext = new SurfaceRenderContext(surface);
        renderContext.RenderChild(node);

        // Verify rendered text
        var text = GetRowText(surface, 0);
        Assert.Contains("Hello", text);
    }

    /// <summary>
    /// Items reorder in VStack under StatePanel scope → nodes swap correctly.
    /// </summary>
    [Fact]
    public async Task Reorder_InVStack_NodesSwapCorrectly()
    {
        var keyRoot = new object();
        var keyA = new object();
        var keyB = new object();
        var keyC = new object();
        var context = ReconcileContext.CreateRoot();

        // Frame 1: A, B, C
        var widget1 = new StatePanelWidget(keyRoot, sp =>
            new VStackWidget([
                new StatePanelWidget(keyA, _ => new TextBlockWidget("Alpha")),
                new StatePanelWidget(keyB, _ => new TextBlockWidget("Beta")),
                new StatePanelWidget(keyC, _ => new TextBlockWidget("Charlie")),
            ]));

        var root = (StatePanelNode)await widget1.ReconcileAsync(null, context);
        root.Measure(new Constraints(0, 40, 0, 10));
        root.Arrange(new Rect(0, 0, 40, 10));

        var vstack1 = (VStackNode)root.Child!;
        var nodeA = vstack1.Children[0];
        var nodeB = vstack1.Children[1];
        var nodeC = vstack1.Children[2];

        // Frame 2: C, A, B (rotated)
        var widget2 = new StatePanelWidget(keyRoot, sp =>
            new VStackWidget([
                new StatePanelWidget(keyC, _ => new TextBlockWidget("Charlie")),
                new StatePanelWidget(keyA, _ => new TextBlockWidget("Alpha")),
                new StatePanelWidget(keyB, _ => new TextBlockWidget("Beta")),
            ]));

        var root2 = (StatePanelNode)await widget2.ReconcileAsync(root, context);
        var vstack2 = (VStackNode)root2.Child!;

        // Identity preserved across reorder
        Assert.Same(nodeC, vstack2.Children[0]);
        Assert.Same(nodeA, vstack2.Children[1]);
        Assert.Same(nodeB, vstack2.Children[2]);
    }

    /// <summary>
    /// Item insertion and deletion: new nodes created, old nodes swept.
    /// </summary>
    [Fact]
    public async Task InsertAndDelete_NodesCreatedAndSwept()
    {
        var keyRoot = new object();
        var keyA = new object();
        var keyB = new object();
        var keyC = new object();
        var context = ReconcileContext.CreateRoot();

        // Frame 1: A, B
        var widget1 = new StatePanelWidget(keyRoot, sp =>
            new VStackWidget([
                new StatePanelWidget(keyA, _ => new TextBlockWidget("A")),
                new StatePanelWidget(keyB, _ => new TextBlockWidget("B")),
            ]));

        var root = (StatePanelNode)await widget1.ReconcileAsync(null, context);
        var nodeA = root.NestedStatePanels[keyA];
        Assert.Equal(2, root.NestedStatePanels.Count);

        // Frame 2: A, C (B removed, C added)
        var widget2 = new StatePanelWidget(keyRoot, sp =>
            new VStackWidget([
                new StatePanelWidget(keyA, _ => new TextBlockWidget("A")),
                new StatePanelWidget(keyC, _ => new TextBlockWidget("C")),
            ]));

        await widget2.ReconcileAsync(root, context);

        // A preserved, B removed, C new
        Assert.Equal(2, root.NestedStatePanels.Count);
        Assert.Same(nodeA, root.NestedStatePanels[keyA]);
        Assert.True(root.NestedStatePanels.ContainsKey(keyC));
        Assert.False(root.NestedStatePanels.ContainsKey(keyB));
    }

    /// <summary>
    /// Render after reorder produces correct output at new positions.
    /// </summary>
    [Fact]
    public async Task Reorder_ThenRender_ShowsCorrectTextAtNewPositions()
    {
        var keyRoot = new object();
        var keyA = new object();
        var keyB = new object();
        var context = ReconcileContext.CreateRoot();

        // Frame 1: A then B
        var widget1 = new StatePanelWidget(keyRoot, sp =>
            new VStackWidget([
                new StatePanelWidget(keyA, _ => new TextBlockWidget("Alpha")),
                new StatePanelWidget(keyB, _ => new TextBlockWidget("Beta")),
            ]));

        var root = (StatePanelNode)await widget1.ReconcileAsync(null, context);
        root.Measure(new Constraints(0, 40, 0, 10));
        root.Arrange(new Rect(0, 0, 40, 10));

        // Frame 2: B then A
        var widget2 = new StatePanelWidget(keyRoot, sp =>
            new VStackWidget([
                new StatePanelWidget(keyB, _ => new TextBlockWidget("Beta")),
                new StatePanelWidget(keyA, _ => new TextBlockWidget("Alpha")),
            ]));

        var root2 = (StatePanelNode)await widget2.ReconcileAsync(root, context);
        root2.Measure(new Constraints(0, 40, 0, 10));
        root2.Arrange(new Rect(0, 0, 40, 10));

        // Render
        var surface = new Surface(40, 10);
        var renderContext = new SurfaceRenderContext(surface);
        renderContext.RenderChild(root2);

        var row0 = GetRowText(surface, 0);
        var row1 = GetRowText(surface, 1);
        Assert.Contains("Beta", row0);
        Assert.Contains("Alpha", row1);
    }

    /// <summary>
    /// Standalone StatePanel (no ancestor) with same key reuses node.
    /// </summary>
    [Fact]
    public async Task Standalone_SameKey_ReusesNode()
    {
        var stateKey = new object();
        var context = ReconcileContext.CreateRoot();

        var widget = new StatePanelWidget(stateKey, sp => new TextBlockWidget("test"));
        var node1 = await widget.ReconcileAsync(null, context);
        var node2 = await widget.ReconcileAsync(node1, context);

        Assert.Same(node1, node2);
    }

    private static string GetRowText(Surface surface, int row)
    {
        var cells = surface.GetRow(row);
        var sb = new System.Text.StringBuilder();
        foreach (var cell in cells)
            sb.Append(cell.Character);
        return sb.ToString().TrimEnd();
    }
}
