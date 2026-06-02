using Hex1b;
using Hex1b.Composition;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests.Composition;

/// <summary>
/// Tests for the default <see cref="Hex1bWidget.ReconcileAsync"/> pipeline that
/// dispatches into <see cref="Hex1bWidget.Build(CompositionContext)"/>. Covers node
/// recycling, state lifetime, and disposal across widget-type swaps.
/// </summary>
[TestClass]
public class CompositeBuildPipelineTests
{
    [TestMethod]
    public async Task Reconcile_NewComposite_BuildsChildAndStoresType()
    {
        var widget = new TextOnlyCompositeWidget("hello");
        var node = await ReconcileAsync(widget, null);

        Assert.IsNotNull(node.Child);
        TestSeq.IsType<TextBlockNode>(node.Child);
        Assert.AreEqual(typeof(TextOnlyCompositeWidget), node.CompositeWidgetType);
    }

    [TestMethod]
    public async Task Reconcile_NullBuildOutput_Throws()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await ReconcileAsync(new NullReturningCompositeWidget(), null));
    }

    [TestMethod]
    public async Task Reconcile_SameCompositeType_ReusesNodeAndPreservesState()
    {
        // The composite increments its UseState counter on every Build pass.
        // First reconciliation: state initialised to 0, then incremented to 1.
        var node1 = await ReconcileAsync(new StatefulCounterCompositeWidget(), null);
        Assert.AreEqual("count=1", LabelOf(node1));

        // Second reconciliation: state must persist across frames.
        var node2 = await ReconcileAsync(new StatefulCounterCompositeWidget(), node1);

        Assert.AreSame(node1, node2);
        Assert.AreEqual("count=2", LabelOf(node2));

        // Third reconciliation: still persistent.
        var node3 = await ReconcileAsync(new StatefulCounterCompositeWidget(), node2);
        Assert.AreEqual("count=3", LabelOf(node3));
    }

    [TestMethod]
    public async Task Reconcile_DifferentCompositeType_DisposesStateAndStartsFresh()
    {
        var node1 = await ReconcileAsync(new StatefulCounterCompositeWidget(), null);
        Assert.AreEqual("count=1", LabelOf(node1));

        // Replace with a different composite type at the same tree position. The framework's
        // GetExpectedNodeType match would normally reuse the node shell, so the composite
        // reconciler must spot the type change and wipe the prior state.
        var node2 = (Hex1bCompositeNode)await ReconcileAsync(new TextOnlyCompositeWidget("after-swap"), node1);
        Assert.AreEqual(typeof(TextOnlyCompositeWidget), node2.CompositeWidgetType);
        Assert.AreEqual("after-swap", LabelOf(node2));

        // Going back to the counter composite should start its state fresh — the previous count is gone.
        var node3 = await ReconcileAsync(new StatefulCounterCompositeWidget(), node2);
        Assert.AreEqual("count=1", LabelOf(node3));
    }

    [TestMethod]
    public async Task Reconcile_DifferentCompositeType_DisposesIDisposableState()
    {
        var disposable = new TrackingDisposable();
        DisposableSeedingCompositeWidget.NextSeed = disposable;

        var node1 = await ReconcileAsync(new DisposableSeedingCompositeWidget(), null);
        Assert.IsFalse(disposable.WasDisposed);

        // Swapping composite types must dispose the previous state objects that implement IDisposable.
        await ReconcileAsync(new TextOnlyCompositeWidget("done"), node1);
        Assert.IsTrue(disposable.WasDisposed);
    }

    // --- Composite test fixtures ---

    private sealed record TextOnlyCompositeWidget(string Message) : Hex1bWidget
    {
        protected override Hex1bWidget Build(CompositionContext ctx) => ctx.Text(Message);
    }

    private sealed record NullReturningCompositeWidget : Hex1bWidget
    {
        protected override Hex1bWidget Build(CompositionContext ctx) => null!;
    }

    private sealed record StatefulCounterCompositeWidget : Hex1bWidget
    {
        protected override Hex1bWidget Build(CompositionContext ctx)
        {
            var state = ctx.UseState(() => new CounterState());
            state.Count++;
            return ctx.Text($"count={state.Count}");
        }

        private sealed class CounterState
        {
            public int Count;
        }
    }

    private sealed record DisposableSeedingCompositeWidget : Hex1bWidget
    {
        public static TrackingDisposable? NextSeed { get; set; }

        protected override Hex1bWidget Build(CompositionContext ctx)
        {
            ctx.UseState(() => NextSeed ?? throw new InvalidOperationException("seed missing"));
            return ctx.Text("seeded");
        }
    }

    private sealed class TrackingDisposable : IDisposable
    {
        public bool WasDisposed { get; private set; }

        public void Dispose() => WasDisposed = true;
    }

    // --- Helpers ---

    private static string LabelOf(Hex1bNode node)
    {
        var composite = TestSeq.IsType<Hex1bCompositeNode>(node);
        var textNode = TestSeq.IsType<TextBlockNode>(composite.Child);
        return textNode.Text;
    }

    private static async Task<Hex1bCompositeNode> ReconcileAsync(Hex1bWidget widget, Hex1bNode? existing)
    {
        var context = ReconcileContext.CreateRoot();
        var rootShell = new RootShellNode();
        var node = await context.ReconcileChildAsync(existing, widget, rootShell);
        return TestSeq.IsType<Hex1bCompositeNode>(node);
    }

    private sealed class RootShellNode : Hex1bNode
    {
        protected override Size MeasureCore(Constraints constraints) => Size.Zero;
        public override void Render(Hex1bRenderContext context) { }
    }
}
