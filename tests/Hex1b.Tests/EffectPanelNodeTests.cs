using Hex1b;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class EffectPanelNodeTests
{
    // --- Layout pass-through tests ---

    [TestMethod]
    public void Measure_WithChild_DelegatesToChild()
    {
        var child = new ButtonNode { Label = "OK" };
        var node = new EffectPanelNode { Child = child };

        var size = node.Measure(new Constraints(0, 50, 0, 10));

        Assert.IsTrue(size.Width > 0);
        Assert.IsTrue(size.Height > 0);
    }

    [TestMethod]
    public void Measure_WithoutChild_ReturnsConstrainedZero()
    {
        var node = new EffectPanelNode { Child = null };

        var size = node.Measure(Constraints.Unbounded);

        Assert.AreEqual(0, size.Width);
        Assert.AreEqual(0, size.Height);
    }

    [TestMethod]
    public void Arrange_PassesThroughToChild()
    {
        var child = new ButtonNode { Label = "OK" };
        var node = new EffectPanelNode { Child = child };
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
        var node = new EffectPanelNode { Child = button };

        var focusables = node.GetFocusableNodes().ToList();

        TestSeq.Single(focusables);
        Assert.AreSame(button, focusables[0]);
    }

    [TestMethod]
    public void GetFocusableNodes_WithoutChild_ReturnsEmpty()
    {
        var node = new EffectPanelNode { Child = null };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.IsEmpty(focusables);
    }

    [TestMethod]
    public void GetChildren_WithChild_ReturnsSingletonList()
    {
        var child = new TextBlockNode();
        var node = new EffectPanelNode { Child = child };

        var children = node.GetChildren();

        TestSeq.Single(children);
        Assert.AreSame(child, children[0]);
    }

    [TestMethod]
    public void GetChildren_WithoutChild_ReturnsEmpty()
    {
        var node = new EffectPanelNode { Child = null };

        var children = node.GetChildren();

        Assert.IsEmpty(children);
    }

    // --- Render tests ---

    [TestMethod]
    public void Render_WithoutEffect_PassesThroughUnchanged()
    {
        var node = SetupRenderedNode("Hello", effect: null);

        var surface = RenderToSurface(node, 40, 5);
        var text = GetRowText(surface, 0);

        Assert.Contains("Hello", text);
    }

    [TestMethod]
    public void Render_WithEffect_ModifiesSurfaceCells()
    {
        var effectCalled = false;
        var node = SetupRenderedNode("Hello", effect: surface =>
        {
            effectCalled = true;
            // Tint all cells: set background to red
            for (int y = 0; y < surface.Height; y++)
                for (int x = 0; x < surface.Width; x++)
                {
                    var cell = surface[x, y];
                    surface[x, y] = cell with { Background = Hex1bColor.FromRgb(255, 0, 0) };
                }
        });

        var surface = RenderToSurface(node, 40, 5);

        Assert.IsTrue(effectCalled);
        // The text should still be present
        var text = GetRowText(surface, 0);
        Assert.Contains("Hello", text);
        // The background should be red on rendered cells
        var cell = surface[0, 0];
        Assert.IsNotNull(cell.Background);
        Assert.AreEqual(255, cell.Background.Value.R);
        Assert.AreEqual(0, cell.Background.Value.G);
    }

    [TestMethod]
    public void Render_WithEffect_ReceivesCorrectlySizedSurface()
    {
        int? surfaceWidth = null;
        int? surfaceHeight = null;

        var node = SetupRenderedNode("X", effect: surface =>
        {
            surfaceWidth = surface.Width;
            surfaceHeight = surface.Height;
        }, width: 25, height: 3);

        RenderToSurface(node, 25, 3);

        Assert.AreEqual(25, surfaceWidth);
        Assert.AreEqual(3, surfaceHeight);
    }

    [TestMethod]
    public void Render_WithEffect_ReceivesChildRenderedContent()
    {
        string? capturedText = null;

        var node = SetupRenderedNode("TestContent", effect: surface =>
        {
            capturedText = GetRowText(surface, 0);
        });

        RenderToSurface(node, 40, 5);

        Assert.IsNotNull(capturedText);
        Assert.Contains("TestContent", capturedText);
    }

    [TestMethod]
    public void Render_WithNullChild_DoesNotThrow()
    {
        var node = new EffectPanelNode
        {
            Child = null,
            Effect = _ => { }
        };
        node.Measure(new Constraints(0, 40, 0, 5));
        node.Arrange(new Rect(0, 0, 40, 5));

        var surface = new Surface(40, 5);
        var ctx = new SurfaceRenderContext(surface);

        // Should not throw
        ctx.RenderChild(node);
    }

    [TestMethod]
    public void Render_WithZeroBounds_DoesNotThrow()
    {
        var child = new TextBlockNode();
        var effectCalled = false;
        var node = new EffectPanelNode
        {
            Child = child,
            Effect = _ => effectCalled = true
        };
        node.Measure(new Constraints(0, 0, 0, 0));
        node.Arrange(new Rect(0, 0, 0, 0));

        var surface = new Surface(40, 5);
        var ctx = new SurfaceRenderContext(surface);
        ctx.RenderChild(node);

        // Effect should not be called for zero-sized bounds
        Assert.IsFalse(effectCalled);
    }

    // --- Reconciliation tests ---

    [TestMethod]
    public async Task Reconcile_ReusesExistingNode()
    {
        var childWidget = new TextBlockWidget("Hello");
        var widget = new EffectPanelWidget(childWidget);
        var context = ReconcileContext.CreateRoot();

        var node1 = await widget.ReconcileAsync(null, context);
        var node2 = await widget.ReconcileAsync(node1, context);

        TestSeq.IsType<EffectPanelNode>(node1);
        Assert.AreSame(node1, node2);
    }

    [TestMethod]
    public async Task Reconcile_SetsEffect()
    {
        Action<Surface> effect = _ => { };
        var widget = new EffectPanelWidget(new TextBlockWidget("test"))
            .Effect(effect);
        var context = ReconcileContext.CreateRoot();

        var node = (EffectPanelNode)await widget.ReconcileAsync(null, context);

        Assert.AreSame(effect, node.Effect);
    }

    [TestMethod]
    public async Task Reconcile_UpdatesEffect()
    {
        Action<Surface> effect1 = _ => { };
        Action<Surface> effect2 = _ => { };
        var context = ReconcileContext.CreateRoot();

        var widget1 = new EffectPanelWidget(new TextBlockWidget("test")).Effect(effect1);
        var node = (EffectPanelNode)await widget1.ReconcileAsync(null, context);
        Assert.AreSame(effect1, node.Effect);

        var widget2 = new EffectPanelWidget(new TextBlockWidget("test")).Effect(effect2);
        await widget2.ReconcileAsync(node, context);
        Assert.AreSame(effect2, node.Effect);
    }

    [TestMethod]
    public async Task Reconcile_ReconcileChild()
    {
        var widget = new EffectPanelWidget(new TextBlockWidget("Hello"));
        var context = ReconcileContext.CreateRoot();

        var node = (EffectPanelNode)await widget.ReconcileAsync(null, context);

        Assert.IsNotNull(node.Child);
        TestSeq.IsType<TextBlockNode>(node.Child);
    }

    [TestMethod]
    public async Task Reconcile_GetExpectedNodeType_ReturnsEffectPanelNode()
    {
        var widget = new EffectPanelWidget(new TextBlockWidget("x"));

        Assert.AreEqual(typeof(EffectPanelNode), widget.GetExpectedNodeType());
    }

    // --- Extension method tests ---

    [TestMethod]
    public void Extension_EffectPanel_CreatesWidget()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var child = new TextBlockWidget("hello");

        var widget = ctx.EffectPanel(child);

        TestSeq.IsType<EffectPanelWidget>(widget);
        Assert.AreSame(child, widget.Child);
    }

    [TestMethod]
    public void Extension_EffectPanel_WithEffectOverload()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var child = new TextBlockWidget("hello");
        Action<Surface> effect = _ => { };

        var widget = ctx.EffectPanel(child, effect);

        TestSeq.IsType<EffectPanelWidget>(widget);
        Assert.AreSame(child, widget.Child);
        Assert.IsNotNull(widget.EffectCallback);
    }

    [TestMethod]
    public void Effect_ReturnsCopyWithEffect()
    {
        var child = new TextBlockWidget("hello");
        Action<Surface> effect = _ => { };

        var widget = new EffectPanelWidget(child).Effect(effect);

        Assert.AreSame(child, widget.Child);
        Assert.IsNotNull(widget.EffectCallback);
    }

    // --- Nested EffectPanels ---

    [TestMethod]
    public async Task NestedEffectPanels_BothEffectsApplied()
    {
        var outerCalled = false;
        var innerCalled = false;

        var innerWidget = new EffectPanelWidget(new TextBlockWidget("Hello"))
            .Effect(_ => innerCalled = true);
        var outerWidget = new EffectPanelWidget(innerWidget)
            .Effect(_ => outerCalled = true);

        var context = ReconcileContext.CreateRoot();
        var node = (EffectPanelNode)await outerWidget.ReconcileAsync(null, context);

        node.Measure(new Constraints(0, 40, 0, 5));
        node.Arrange(new Rect(0, 0, 40, 5));

        var surface = new Surface(40, 5);
        var renderContext = new SurfaceRenderContext(surface);
        renderContext.RenderChild(node);

        Assert.IsTrue(innerCalled);
        Assert.IsTrue(outerCalled);
    }

    // --- Helpers ---

    private static EffectPanelNode SetupRenderedNode(
        string text,
        Action<Surface>? effect,
        int width = 40,
        int height = 5)
    {
        var child = new TextBlockNode { Text = text };
        var node = new EffectPanelNode { Child = child, Effect = effect };
        node.Measure(new Constraints(0, width, 0, height));
        node.Arrange(new Rect(0, 0, width, height));
        return node;
    }

    private static Surface RenderToSurface(EffectPanelNode node, int width, int height)
    {
        var surface = new Surface(width, height);
        var renderContext = new SurfaceRenderContext(surface);
        renderContext.RenderChild(node);
        return surface;
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
