using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Test-only widget that allows tests to observe reconcile and render timing.
/// </summary>
public sealed record TestWidget : Hex1bWidget
{
    internal Action<TestWidgetRenderEventArgs>? RenderHandler { get; private set; }
    internal Action<TestWidgetReconcileEventArgs>? ReconcileHandler { get; private set; }

    private int _renderCount;
    private int _reconcileCount;

    internal TestWidget OnRender(Action<TestWidgetRenderEventArgs> callback)
    {
        RenderHandler = callback;
        return this;
    }

    internal TestWidget OnReconcile(Action<TestWidgetReconcileEventArgs> callback)
    {
        ReconcileHandler = callback;
        return this;
    }

    private void RaiseRender(TestWidgetNode node, InputBindingActionContext actionContext)
    {
        _renderCount++;
        RenderHandler?.Invoke(new TestWidgetRenderEventArgs(this, node, actionContext, _renderCount));
    }

    private void RaiseReconcile(Hex1bNode? existingNode, TestWidgetNode node, InputBindingActionContext actionContext)
    {
        _reconcileCount++;
        ReconcileHandler?.Invoke(new TestWidgetReconcileEventArgs(this, node, actionContext, _reconcileCount, existingNode));
    }

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var actionContext = new InputBindingActionContext(context.FocusRing);
        var node = existingNode as TestWidgetNode ?? new TestWidgetNode();
        node.RenderCallback = () => RaiseRender(node, actionContext);
        RaiseReconcile(existingNode, node, actionContext);
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(TestWidgetNode);
}
