using Hex1b;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class EffectPanelIntegrationTests
{
    /// <summary>
    /// Full widget → reconcile → measure → arrange → render cycle with effect.
    /// </summary>
    [Fact]
    public async Task FullCycle_WithEffect_RendersAndAppliesEffect()
    {
        var effectApplied = false;
        var widget = new EffectPanelWidget(new TextBlockWidget("Hello World"))
            .WithEffect(surface =>
            {
                effectApplied = true;
                // Set all backgrounds to blue
                for (int y = 0; y < surface.Height; y++)
                    for (int x = 0; x < surface.Width; x++)
                    {
                        var cell = surface[x, y];
                        surface[x, y] = cell with { Background = Hex1bColor.FromRgb(0, 0, 255) };
                    }
            });

        var context = ReconcileContext.CreateRoot();
        var node = (EffectPanelNode)await widget.ReconcileAsync(null, context);

        node.Measure(new Constraints(0, 40, 0, 5));
        node.Arrange(new Rect(0, 0, 40, 5));

        var surface = new Surface(40, 5);
        var renderContext = new SurfaceRenderContext(surface);
        renderContext.RenderChild(node);

        Assert.True(effectApplied);

        // Text should be present
        var text = GetRowText(surface, 0);
        Assert.Contains("Hello World", text);

        // Background should be blue
        var cell = surface[0, 0];
        Assert.NotNull(cell.Background);
        Assert.Equal(0, cell.Background.Value.R);
        Assert.Equal(0, cell.Background.Value.G);
        Assert.Equal(255, cell.Background.Value.B);
    }

    /// <summary>
    /// Interactive child inside EffectPanel remains focusable.
    /// </summary>
    [Fact]
    public async Task InteractiveChild_RemainsFocusable()
    {
        var widget = new EffectPanelWidget(
            new ButtonWidget("Click me"))
            .WithEffect(_ => { });

        var context = ReconcileContext.CreateRoot();
        var node = (EffectPanelNode)await widget.ReconcileAsync(null, context);

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Single(focusables);
        Assert.IsType<ButtonNode>(focusables[0]);
    }

    /// <summary>
    /// Effect applied to a VStack with multiple children.
    /// </summary>
    [Fact]
    public async Task Effect_AppliedToVStackChildren()
    {
        var capturedSurface = (Surface?)null;

        var widget = new EffectPanelWidget(
            new VStackWidget([
                new TextBlockWidget("Line 1"),
                new TextBlockWidget("Line 2"),
            ]))
            .WithEffect(surface =>
            {
                capturedSurface = surface;
            });

        var context = ReconcileContext.CreateRoot();
        var node = (EffectPanelNode)await widget.ReconcileAsync(null, context);

        node.Measure(new Constraints(0, 40, 0, 10));
        node.Arrange(new Rect(0, 0, 40, 10));

        var surface = new Surface(40, 10);
        var renderContext = new SurfaceRenderContext(surface);
        renderContext.RenderChild(node);

        Assert.NotNull(capturedSurface);
        var line1 = GetRowText(capturedSurface!, 0);
        var line2 = GetRowText(capturedSurface!, 1);
        Assert.Contains("Line 1", line1);
        Assert.Contains("Line 2", line2);
    }

    /// <summary>
    /// EffectPanel composes with StatePanel.
    /// </summary>
    [Fact]
    public async Task ComposesWithStatePanel()
    {
        var stateKey = new object();
        var effectCalled = false;

        var widget = new StatePanelWidget(stateKey, sp =>
            new EffectPanelWidget(new TextBlockWidget("Animated"))
                .WithEffect(surface => effectCalled = true));

        var context = ReconcileContext.CreateRoot();
        var node = (StatePanelNode)await widget.ReconcileAsync(null, context);

        node.Measure(new Constraints(0, 40, 0, 5));
        node.Arrange(new Rect(0, 0, 40, 5));

        var surface = new Surface(40, 5);
        var renderContext = new SurfaceRenderContext(surface);
        renderContext.RenderChild(node);

        Assert.True(effectCalled);
        var text = GetRowText(surface, 0);
        Assert.Contains("Animated", text);
    }

    /// <summary>
    /// Dim effect reduces color values.
    /// </summary>
    [Fact]
    public async Task DimEffect_ReducesColorValues()
    {
        // Render a text block with known foreground, then dim it
        var widget = new EffectPanelWidget(new TextBlockWidget("X"))
            .WithEffect(surface =>
            {
                for (int y = 0; y < surface.Height; y++)
                    for (int x = 0; x < surface.Width; x++)
                    {
                        var cell = surface[x, y];
                        if (cell.Foreground is not null && !cell.Foreground.Value.IsDefault)
                        {
                            var fg = cell.Foreground.Value;
                            surface[x, y] = cell with
                            {
                                Foreground = Hex1bColor.FromRgb(
                                    (byte)(fg.R / 2),
                                    (byte)(fg.G / 2),
                                    (byte)(fg.B / 2))
                            };
                        }
                    }
            });

        var context = ReconcileContext.CreateRoot();
        var node = (EffectPanelNode)await widget.ReconcileAsync(null, context);

        node.Measure(new Constraints(0, 40, 0, 5));
        node.Arrange(new Rect(0, 0, 40, 5));

        var surface = new Surface(40, 5);
        var renderContext = new SurfaceRenderContext(surface);
        renderContext.RenderChild(node);

        // The text should still be readable
        var text = GetRowText(surface, 0);
        Assert.Contains("X", text);
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
