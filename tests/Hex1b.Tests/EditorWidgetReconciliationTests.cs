// NOTE: These tests verify EditorWidget reconciliation behavior.
// As editor capabilities evolve (e.g., lazy state, view modes), reconciliation may change.

using Hex1b.Documents;
using Hex1b.Events;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class EditorWidgetReconciliationTests
{
    [Fact]
    public async Task Reconcile_NullExisting_CreatesNewEditorNode()
    {
        // NOTE: Node creation may gain initialization hooks in future.
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        var widget = new EditorWidget(state);

        var context = ReconcileContext.CreateRoot();
        var node = await widget.ReconcileAsync(null, context);

        Assert.IsType<EditorNode>(node);
    }

    [Fact]
    public async Task Reconcile_ExistingEditorNode_ReusesSameInstance()
    {
        // NOTE: Instance reuse preserves scroll position and focus state.
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        var widget = new EditorWidget(state);

        var existingNode = new EditorNode();
        var context = ReconcileContext.CreateRoot();
        var result = await widget.ReconcileAsync(existingNode, context);

        Assert.Same(existingNode, result);
    }

    [Fact]
    public async Task Reconcile_UpdatesState()
    {
        // NOTE: State swapping may trigger scroll reset in future.
        var doc1 = new Hex1bDocument("Doc1");
        var doc2 = new Hex1bDocument("Doc2");
        var state1 = new EditorState(doc1);
        var state2 = new EditorState(doc2);

        var widget1 = new EditorWidget(state1);
        var widget2 = new EditorWidget(state2);

        var context = ReconcileContext.CreateRoot();
        var node = (EditorNode)await widget1.ReconcileAsync(null, context);
        Assert.Same(state1, node.State);

        // Reconcile with different state
        await widget2.ReconcileAsync(node, context);
        Assert.Same(state2, node.State);
    }

    [Fact]
    public async Task Reconcile_WithOnTextChanged_SetsTextChangedAction()
    {
        // NOTE: TextChangedAction wiring may change with event aggregation.
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        var widget = new EditorWidget(state)
            .OnTextChanged((EditorTextChangedEventArgs _) => { });

        var context = ReconcileContext.CreateRoot();
        var node = (EditorNode)await widget.ReconcileAsync(null, context);

        Assert.NotNull(node.TextChangedAction);
    }

    [Fact]
    public async Task Reconcile_WithoutOnTextChanged_ClearsTextChangedAction()
    {
        // NOTE: Clearing action prevents stale handler references.
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);

        // First reconcile with handler
        var widgetWithHandler = new EditorWidget(state)
            .OnTextChanged((EditorTextChangedEventArgs _) => { });
        var context = ReconcileContext.CreateRoot();
        var node = (EditorNode)await widgetWithHandler.ReconcileAsync(null, context);
        Assert.NotNull(node.TextChangedAction);

        // Second reconcile without handler
        var widgetWithout = new EditorWidget(state);
        await widgetWithout.ReconcileAsync(node, context);
        Assert.Null(node.TextChangedAction);
    }

    [Fact]
    public void GetExpectedNodeType_ReturnsEditorNodeType()
    {
        // NOTE: Node type must match for reconciliation reuse.
        var doc = new Hex1bDocument("");
        var state = new EditorState(doc);
        var widget = new EditorWidget(state);

        Assert.Equal(typeof(EditorNode), widget.GetExpectedNodeType());
    }

    [Fact]
    public async Task Reconcile_TwoWidgetsSameState_NodesShareState()
    {
        // NOTE: Shared state enables synced cursors between views.
        var doc = new Hex1bDocument("Shared");
        var state = new EditorState(doc);
        var widget1 = new EditorWidget(state);
        var widget2 = new EditorWidget(state);

        var context = ReconcileContext.CreateRoot();
        var node1 = (EditorNode)await widget1.ReconcileAsync(null, context);
        var node2 = (EditorNode)await widget2.ReconcileAsync(null, context);

        Assert.Same(node1.State, node2.State);
        Assert.Same(state, node1.State);
    }
}
